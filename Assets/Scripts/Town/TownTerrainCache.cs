using System.Collections.Generic;
using System.Text;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

/// <summary>
/// The most recently built town's static geometry (ground, walls, houses, roofs, decorations and
/// interior props), owned by Common across scene loads. Buildings, vendors, allies and other
/// stateful objects live under the Town itself and are always created fresh.
/// </summary>
public sealed class TownTerrainCache
{
    private readonly Transform parent;
    private string identity;
    private TileWorldCreatorAsset asset;
    private Dictionary<string, WorldMap> maps;
    private GameObject world;
    private TownPlanOptions planKey;
    private TownPlan plan;
    private Town activeView;
    public GameObject Root { get; private set; }
    public bool IsBuilt => Root != null;
    public int BuildCount { get; private set; }

    /// <summary>
    /// Player builds reuse town geometry; the editor always rebuilds so tools and tests that expect a
    /// freshly generated town are unaffected. Set true in the editor to try the cache there.
    /// </summary>
    public static bool Enabled = !Application.isEditor;

    public TownTerrainCache(Transform parent) { this.parent = parent; }

    /// <summary>
    /// Everything the town's geometry is generated from: the template, its configured layers (which
    /// carry the layout options and building services), the seed and the biome. Valid for one session.
    /// </summary>
    public static string Identity(TileWorldCreatorAsset template, TileWorldCreatorAsset configured, int seed,
        OverworldBiome biome, TownConfiguration configuration)
    {
        var value = new StringBuilder("town-terrain-v1|");
        value.Append(configuration.Id).Append('|').Append(seed).Append('|').Append(biome).Append('|')
            .Append(template.GetInstanceID()).Append('|').Append(configuration.FurnishInteriors).Append('|');
        foreach (var building in configuration.SlotBuildings ?? configuration.Buildings)
            value.Append(building != null ? building.Id : "-").Append(',');
        foreach (var layer in configured.mapBlueprintLayers)
        {
            value.Append('|').Append(layer.layerName).Append(layer.active);
            foreach (var step in layer.stack)
                if (step.action != null) value.Append(step.action.GetType().FullName).Append(JsonUtility.ToJson(step.action));
        }
        foreach (var layer in configured.mapBuildLayers)
            value.Append('|').Append(layer.GetType().FullName).Append(layer.active).Append(JsonUtility.ToJson(layer));
        return value.ToString();
    }

    /// <summary>
    /// Hands the cached geometry to this scene's creator when it was built from the same inputs.
    /// The scene's own authored preview is removed; the cached plan is registered for the creator.
    /// </summary>
    public bool TryRestore(string current, TileWorldCreator creator, Town view, out TownPlan cachedPlan)
    {
        cachedPlan = null;
        if (!IsBuilt || world == null || asset == null || identity != current) return false;
        // Root is still hidden, so this finds the scene's own world object, not the cached one.
        var own = creator.worldObject;
        if (own != null && own != world) { own.SetActive(false); Object.Destroy(own); }
        creator.twcAsset = asset;
        creator.generatedBlueprintMaps = maps;
        creator.worldObject = world;
        CoreLayoutCache.SupplyTown(creator, planKey, plan);
        cachedPlan = plan;
        activeView = view;
        Root.SetActive(true);
        return true;
    }

    /// <summary>Takes ownership of the town just built, replacing whichever town was cached before.</summary>
    public bool Store(string current, TileWorldCreator creator, TileWorldCreatorAsset configured, Town view)
    {
        if (!CoreLayoutCache.TryGetTown(creator, out var builtPlan, out var builtKey)) return false;
        Clear();
        identity = current; asset = configured; maps = creator.generatedBlueprintMaps; world = creator.worldObject;
        plan = builtPlan; planKey = builtKey;
        Root = new GameObject("Cached town terrain");
        Root.transform.SetParent(parent, false);
        world.transform.SetParent(Root.transform, true);
        activeView = view;
        BuildCount++;
        // Every build listener has run, so nothing combines or reads these meshes again.
        int released = RuntimeMeshMemory.ReleaseCpuCopies(Root);
        if (RuntimeMeshMemory.Logging) RuntimeMeshMemory.Log($"Town terrain cached, {released} mesh CPU copies released");
        return true;
    }

    public void Hide(Town view)
    {
        if (activeView != view) return;
        if (Root != null) Root.SetActive(false);
        activeView = null;
    }

    public void Clear()
    {
        if (Root != null)
        {
            // TWC's runtime cluster meshes have no owner; release them with the geometry instead of
            // waiting for the next unused-asset sweep. Owner meshes go with their EnvironmentMeshOwner.
            var clusters = new HashSet<Mesh>();
            foreach (var filter in Root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.name.Contains("_Cluster_")) clusters.Add(filter.sharedMesh);
            foreach (var collider in Root.GetComponentsInChildren<MeshCollider>(true))
                if (collider.sharedMesh != null && collider.sharedMesh.name.Contains("_Cluster_")) clusters.Add(collider.sharedMesh);
            Root.SetActive(false); Object.Destroy(Root);
            foreach (var mesh in clusters) Object.Destroy(mesh);
        }
        if (asset != null)
        {
            foreach (var layer in asset.mapBlueprintLayers)
                if (layer.previewTextureMap != null) Object.Destroy(layer.previewTextureMap);
            Object.Destroy(asset);
        }
        Root = null; identity = null; asset = null; maps = null; world = null; plan = null; planKey = null; activeView = null;
    }
}
