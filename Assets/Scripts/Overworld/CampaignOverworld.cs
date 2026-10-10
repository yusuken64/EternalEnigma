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
    public EnvironmentKit CosmeticKit;
    public OverworldStyleProfile StyleProfile;
    public TileWorldCreatorAsset EffectiveTemplate => StyleProfile?.ResolveTemplate() ?? Template;
    public EnvironmentKit EffectiveKit => StyleProfile?.ResolveKit() ?? CosmeticKit;
    public string LastGenerationError { get; private set; }
    public int Seed = 42;
    [Range(16, 1024)] public int Width = 256;
    [Range(16, 1024)] public int Height = 256;
    [Tooltip("Empty imports every core layer using its own name. Otherwise only these mappings are imported.")]
    public List<CampaignLayerBinding> LayerBindings = new();
    public OverworldGrid CurrentGrid { get; private set; }
    public event Action<TileWorldCreator> TerrainBuilt;

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
        var template = EffectiveTemplate != null ? EffectiveTemplate : generatedAsset != null ? previousAsset : creator.twcAsset;
        var styleLayers = StyleProfile?.ResolveLayers().ToDictionary(b=>Guid.Parse(b.BlueprintGuid));
        var bindings = StyleProfile != null ? StyleProfile.Bindings(grid,template) : LayerBindings.Count == 0 ? grid.Layers.Keys.OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => new CampaignLayerBinding(n, n)).ToList() : LayerBindings.ToList();
        var groundTemplate = template;
        if (groundTemplate != null)
        {
            foreach (var input in groundTemplate.mapBuildLayers.OfType<OverworldGroundLayer>().Where(l=>l.active).SelectMany(l=>l.Inputs))
                if (!bindings.Any(b=>b.BlueprintLayer==input.BlueprintLayer || StyleProfile!=null && b.CoreLayer==input.CoreLayer))
                    bindings.Add(new CampaignLayerBinding(input.CoreLayer,input.BlueprintLayer));
            if(StyleProfile==null && groundTemplate.mapBuildLayers.OfType<OverworldTreeWallLayer>().Any(l=>l.active)&&!bindings.Any(b=>b.BlueprintLayer==OverworldLayers.Trees))
                bindings.Add(new CampaignLayerBinding(OverworldLayers.Trees,OverworldLayers.Trees));
        }
        if (bindings.Any(b => b == null || !grid.Layers.ContainsKey(b.CoreLayer ?? "") || string.IsNullOrWhiteSpace(b.BlueprintLayer)) ||
            bindings.Select(b => b.BlueprintLayer).Distinct(StringComparer.Ordinal).Count() != bindings.Count)
            throw new ArgumentException("Bindings require known core layers and unique, nonempty TWC layer names.");

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
        if(StyleProfile?.ResolveGround() is { } groundStyle)
            foreach(var output in generatedAsset.mapBuildLayers.OfType<OverworldGroundLayer>())output.Style=groundStyle;
        if(StyleProfile!=null)
        {
            foreach(var output in generatedAsset.mapBuildLayers.OfType<OverworldGroundLayer>())
                output.Inputs=output.Inputs.Select(input=>new CampaignLayerBinding(input.CoreLayer,
                    (bindings.FirstOrDefault(b=>b.CoreLayer==input.CoreLayer && b.BlueprintLayer==input.BlueprintLayer)??
                    bindings.FirstOrDefault(b=>b.CoreLayer==input.CoreLayer))?.BlueprintLayer??input.BlueprintLayer)).ToArray();
            foreach(var output in generatedAsset.mapBuildLayers.OfType<OverworldCosmeticLayer>())output.Kit=EffectiveKit??output.Kit;
            foreach(var output in generatedAsset.mapBuildLayers.OfType<OverworldTreeWallLayer>())output.Kit=EffectiveKit??output.Kit;
        }
        // Preserve template GUIDs so authored build layers still point to the same blueprint slots.
        foreach (var binding in bindings)
            if (!generatedAsset.mapBlueprintLayers.Any(l => l.layerName == binding.BlueprintLayer))
                generatedAsset.mapBlueprintLayers.Add(new TileWorldCreatorAsset.BlueprintLayerData(binding.BlueprintLayer, true));
        foreach (var layer in generatedAsset.mapBlueprintLayers)
        {
            if(StyleProfile!=null && !layer.active)continue;
            var styleBinding=styleLayers!=null && styleLayers.TryGetValue(layer.guid,out var specification)?specification:null;
            // Keep authored Add/Shrink stacks for the nested cliff tiers, as in TWC's layered samples.
            if (styleBinding?.Role==OverworldLayerRole.DerivedTerrain ||
                styleBinding==null && SmartEnvironmentMasks.IsDerived(layer.layerName)) continue;
            if(styleBinding?.Role==OverworldLayerRole.Cosmetic && layer.stack.Count>0)
            {
                var protect=OverworldCosmetics.Protected(grid);
                layer.stack=layer.stack.SelectMany(step=>new[] {step,new TileWorldCreatorAsset.BlueprintLayerData.ActionStack(
                    "Reserve approaches",new ProtectOverworldApproaches(protect))}).ToList();
                continue;
            }
            var binding = bindings.FirstOrDefault(b => b.BlueprintLayer == layer.layerName);
            var mask = binding == null ? new bool[grid.Width, grid.Height] : binding.CoreLayer == OverworldLayers.Walkable
                ? grid.CreateWalkableLayer(held, resolvedLocks) : grid.Layers[binding.CoreLayer].ToArray();
            if ((styleBinding?.Role==OverworldLayerRole.PlannedCosmetic || StyleProfile==null && layer.layerName == OverworldCosmetics.Layer) && EffectiveKit != null)
                foreach (var prop in OverworldCosmetics.Plan(grid, EffectiveKit,generatedAsset.mapBuildLayers.OfType<OverworldCosmeticLayer>().FirstOrDefault()?.TreeModels)) mask[prop.X, prop.Y] = true;
            mask = SmartEnvironmentMasks.World(grid, StyleProfile!=null?styleBinding?.Projection:layer.layerName) ?? mask;
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
        LastGenerationError=null;
        try
        {
            // This produces both GUID and GUID_UNSUBD WorldMaps, as needed by tile and object build layers.
            creator.ExecuteAllBlueprintLayers();
            foreach (var layer in generatedAsset.mapBlueprintLayers)
            {
                if(!layer.active)continue;
                var output=creator.GetMapOutputFromBlueprintLayer(layer.guid);
                if(output==null || output.GetLength(0)!=grid.Width || output.GetLength(1)!=grid.Height)
                    throw new InvalidOperationException("Blueprint produced a missing or incorrectly sized mask: "+layer.layerName);
                bool empty=!output.Cast<bool>().Any(v=>v);
                if(empty && styleLayers!=null && styleLayers.TryGetValue(layer.guid,out var spec) && !spec.AllowEmpty)
                    throw new InvalidOperationException("Required style layer produced an empty mask: "+layer.layerName);
                // TWC uses these flags solely to flag empty intermediate masks. Exceptions and
                // invalid outputs above are generation errors; intentional empty masks are valid.
                layer.mapResultFailed = false;
                foreach (var action in layer.stack) if (action.action is TWC.Actions.TWCBlueprintAction blueprint) blueprint.resultFailed = false;
            }
        }
        catch(Exception error)
        {
            LastGenerationError=error.Message;
            ReleaseAsset();
            throw;
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
        catch { building = false; Creator.OnBuildLayersComplete -= BuildFinished; throw; }
    }

    public TileWorldCreatorAsset ReleaseGeneratedAssetOwnership()
    {
        if (building || generatedAsset == null) throw new InvalidOperationException("Terrain must finish building before it can be cached.");
        var asset = generatedAsset;
        generatedAsset = null; previousMaps = null;
        return asset;
    }

    public void UseCachedTerrain(OverworldGrid grid, TileWorldCreatorAsset asset, Dictionary<string, WorldMap> maps, GameObject world)
    {
        if (building) throw new InvalidOperationException("Cannot restore terrain during a build.");
        ReleaseAsset();
        CurrentGrid = grid;
        Creator.twcAsset = asset;
        Creator.generatedBlueprintMaps = maps;
        Creator.worldObject = world;
    }

    private void BuildFinished(TileWorldCreator creator)
    {
        building = false; creator.OnBuildLayersComplete -= BuildFinished;
        BiomeDecorations.World(creator,CurrentGrid);
        TerrainBuilt?.Invoke(creator);
    }

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
