using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

[Serializable]
public sealed class CampaignLayerBinding
{
    public string CoreLayer;
    public string BlueprintLayer;
    public CampaignLayerBinding(string coreLayer, string blueprintLayer) { CoreLayer = coreLayer; BlueprintLayer = blueprintLayer; }
}

/// <summary>Optional overworld presentation adapter. Use on a dedicated TWC object, not the town generator.</summary>
[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(TileWorldCreator))]
public sealed class CampaignOverworld : MonoBehaviour
{
    public TileWorldCreatorAsset Template;
    public int Seed = 42;
    [Range(16, 1024)] public int Width = 256;
    [Range(16, 1024)] public int Height = 256;
    [Tooltip("Empty imports every core layer using its own name. Otherwise only these mappings are imported.")]
    public List<CampaignLayerBinding> LayerBindings = new();
    public OverworldGrid CurrentGrid { get; private set; }

    [SerializeField, HideInInspector] private TileWorldCreatorAsset generatedAsset;
    [SerializeField, HideInInspector] private TileWorldCreatorAsset previousAsset;
    private bool building;
    private Dictionary<string, WorldMap> previousMaps;
    private TileWorldCreator Creator => GetComponent<TileWorldCreator>();

    private void OnEnable()
    {
        // Generated assets are deliberately not saved into scenes. Recover the authored reference on reopen.
        if (generatedAsset == null && previousAsset != null && Creator.twcAsset == null)
            Creator.twcAsset = previousAsset;
    }

    [ContextMenu("Generate Campaign Layers")]
    public void GenerateLayers() => Generate(CampaignGenerator.Generate(Seed));

    [ContextMenu("Generate And Build Overworld")]
    public void GenerateAndBuild()
    {
        GenerateLayers();
        BuildMeshes();
    }

    public OverworldGrid Generate(Campaign campaign)
    {
        var grid = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(Width, Height));
        Apply(grid);
        return grid;
    }

    public void Apply(OverworldGrid grid, CapabilitySet held = default, ISet<string> resolvedLocks = null)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (building) throw new InvalidOperationException("Wait for the current TWC build to finish before replacing the map.");
        var creator = Creator;
        var bindings = LayerBindings.Count == 0 ? grid.Layers.Keys.OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => new CampaignLayerBinding(n, n)).ToList() : LayerBindings;
        if (bindings.Any(b => b == null || !grid.Layers.ContainsKey(b.CoreLayer ?? "") || string.IsNullOrWhiteSpace(b.BlueprintLayer)) ||
            bindings.Select(b => b.BlueprintLayer).Distinct(StringComparer.Ordinal).Count() != bindings.Count)
            throw new ArgumentException("Bindings require known core layers and unique, nonempty TWC layer names.");

        var template = Template != null ? Template : generatedAsset != null ? previousAsset : creator.twcAsset;
        if (template != null && template.mapBlueprintLayers.Select(l => l.layerName).Distinct(StringComparer.Ordinal).Count() != template.mapBlueprintLayers.Count)
            throw new ArgumentException("The TWC template contains duplicate blueprint layer names.");
        ReleaseAsset();
        previousAsset = creator.twcAsset;
        previousMaps = new Dictionary<string, WorldMap>(creator.generatedBlueprintMaps);
        generatedAsset = template != null ? Instantiate(template) : ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
        generatedAsset.name = "Generated Campaign Layers";
        generatedAsset.hideFlags = HideFlags.DontSave;
        generatedAsset.worldName = "CampaignOverworld_" + GetInstanceID();
        generatedAsset.mapWidth = grid.Width; generatedAsset.mapHeight = grid.Height;
        generatedAsset.useRandomSeed = true; generatedAsset.randomSeed = grid.CampaignSeed;
        generatedAsset.useNewRandomSeedForEveryLayer = false;
        generatedAsset.mergePreviewTextures = false;
        // Preserve template GUIDs so authored build layers still point to the same blueprint slots.
        foreach (var binding in bindings)
            if (!generatedAsset.mapBlueprintLayers.Any(l => l.layerName == binding.BlueprintLayer))
                generatedAsset.mapBlueprintLayers.Add(new TileWorldCreatorAsset.BlueprintLayerData(binding.BlueprintLayer, true));
        foreach (var layer in generatedAsset.mapBlueprintLayers)
        {
            var binding = bindings.FirstOrDefault(b => b.BlueprintLayer == layer.layerName);
            var mask = binding == null ? new bool[grid.Width, grid.Height] : binding.CoreLayer == OverworldLayers.Walkable
                ? grid.CreateWalkableLayer(held, resolvedLocks) : grid.Layers[binding.CoreLayer].ToArray();
            layer.stack = new List<TileWorldCreatorAsset.BlueprintLayerData.ActionStack>
            { new("Campaign mask", new CampaignLayerAction(mask)) };
            layer.active = true;
            layer.randomSeedOverride = false;
            layer.previewTextureMap = null;
        }
        creator.twcAsset = generatedAsset;
        creator.generatedBlueprintMaps.Clear();
        CurrentGrid = grid;
        var randomState = UnityEngine.Random.state;
        try
        {
            // This produces both GUID and GUID_UNSUBD WorldMaps, as needed by tile and object build layers.
            creator.ExecuteAllBlueprintLayers();
            foreach (var layer in generatedAsset.mapBlueprintLayers)
            {
                // Empty placement masks are valid (e.g. a seed without obstacle-form locks).
                layer.mapResultFailed = false;
                foreach (var action in layer.stack) ((CampaignLayerAction)action.action).resultFailed = false;
            }
        }
        finally { UnityEngine.Random.state = randomState; }
    }

    public void BuildMeshes()
    {
        if (CurrentGrid == null || generatedAsset == null) throw new InvalidOperationException("Generate campaign layers first.");
        if (building) throw new InvalidOperationException("A TWC build is already running.");
        if (generatedAsset.mapBuildLayers.Count == 0)
        {
            Debug.LogWarning("Campaign layers are ready. Assign a TWC template with tile/object build layers to render them.", this);
            return;
        }
        Creator.OnBuildLayersComplete -= BuildFinished;
        Creator.OnBuildLayersComplete += BuildFinished;
        building = true;
        try { Creator.ExecuteAllBuildLayers(true); }
        catch { BuildFinished(Creator); throw; }
    }

    private void BuildFinished(TileWorldCreator creator)
    { building = false; creator.OnBuildLayersComplete -= BuildFinished; }

    private void OnDestroy()
    {
        var creator = GetComponent<TileWorldCreator>();
        if (creator != null) creator.OnBuildLayersComplete -= BuildFinished;
        ReleaseAsset();
    }

    private void ReleaseAsset()
    {
        if (generatedAsset == null) return;
        var creator = GetComponent<TileWorldCreator>();
        if (creator != null && creator.twcAsset == generatedAsset)
        {
            creator.twcAsset = previousAsset;
            creator.generatedBlueprintMaps = previousMaps ?? new Dictionary<string, WorldMap>();
        }
        foreach (var layer in generatedAsset.mapBlueprintLayers)
            if (layer.previewTextureMap != null)
            {
                if (Application.isPlaying) Destroy(layer.previewTextureMap); else DestroyImmediate(layer.previewTextureMap);
            }
        if (Application.isPlaying) Destroy(generatedAsset); else DestroyImmediate(generatedAsset);
        generatedAsset = null;
        previousMaps = null;
        CurrentGrid = null;
    }
}
