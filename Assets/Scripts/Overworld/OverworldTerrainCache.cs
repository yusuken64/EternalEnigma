using System.Collections.Generic;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

/// <summary>One session's immutable rendered terrain, owned by Common across scene loads.</summary>
public sealed class OverworldTerrainCache
{
    private readonly Transform parent;
    private CampaignContext context;
    private TileWorldCreatorAsset template;
    private TileWorldCreatorAsset asset;
    private Dictionary<string, WorldMap> maps;
    private GameObject world;
    private GameObject surfaces;
    private readonly HashSet<Mesh> ownedMeshes = new();
    private OverworldScene activeView;
    public GameObject Root { get; private set; }
    public bool IsBuilt => Root != null;
    public int BuildCount { get; private set; }

    public OverworldTerrainCache(Transform parent) { this.parent = parent; }

    public bool TryRestore(CampaignContext current, CampaignOverworld map, OverworldScene view)
    {
        if (!IsBuilt || !ReferenceEquals(context, current) || template != map.Template) return false;
        map.UseCachedTerrain(current.Grid, asset, maps, world);
        map.GetComponent<OverworldBiomeRenderer>()?.UseCachedSurfaces(surfaces);
        activeView = view;
        Root.SetActive(true);
        return true;
    }

    public void Store(CampaignContext current, CampaignOverworld map, OverworldScene view)
    {
        Clear();
        var creator = map.GetComponent<TileWorldCreator>();
        context = current; template = map.Template;
        asset = map.ReleaseGeneratedAssetOwnership();
        maps = creator.generatedBlueprintMaps;
        world = creator.worldObject;
        // TWC creates these cluster meshes at runtime; child prefab meshes remain asset-owned.
        foreach (var cluster in world.GetComponentsInChildren<ClusterIdentifier>(true))
        {
            var filter = cluster.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) ownedMeshes.Add(filter.sharedMesh);
            var collider = cluster.GetComponent<MeshCollider>();
            if (collider != null && collider.sharedMesh != null) ownedMeshes.Add(collider.sharedMesh);
        }
        Root = new GameObject("Cached overworld terrain");
        Root.transform.SetParent(parent, false);
        world.transform.SetParent(Root.transform, true);
        var biomes = map.GetComponent<OverworldBiomeRenderer>();
        if (biomes != null)
        {
            surfaces = biomes.ReleaseSurfacesOwnership(out var meshes);
            ownedMeshes.UnionWith(meshes);
            if (surfaces != null) surfaces.transform.SetParent(Root.transform, true);
        }
        activeView = view;
        BuildCount++;
    }

    public void Hide(OverworldScene view)
    {
        if (activeView != view) return;
        if (Root != null) Root.SetActive(false);
        activeView = null;
    }

    public void Clear()
    {
        if (Root != null) { Root.SetActive(false); Object.Destroy(Root); }
        foreach (var mesh in ownedMeshes) if (mesh != null) Object.Destroy(mesh);
        ownedMeshes.Clear();
        if (asset != null)
        {
            foreach (var layer in asset.mapBlueprintLayers)
                if (layer.previewTextureMap != null) Object.Destroy(layer.previewTextureMap);
            Object.Destroy(asset);
        }
        Root = null; context = null; template = null; asset = null;
        maps = null; world = null; surfaces = null; activeView = null;
    }
}
