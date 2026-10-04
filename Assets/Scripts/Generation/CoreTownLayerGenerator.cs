using System;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using TWC.Utilities;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using TWC.editor;
#endif

[Serializable]
[ActionCategory(Category = ActionCategoryAttribute.CategoryTypes.Generators)]
[ActionName(Name = "Core Town Layer")]
public sealed class CoreTownLayerGenerator : TWCBlueprintAction, ITWCAction
{
    public string LayerName = TownLayers.Roads;
    public int BuildingCount = 4;
    public string ShopFlags = "0100";      // one char per building, '1' = has shop, in configuration order
    public int AllyCount = 3;              // authored knob; not derived from configuration
    public int PartySpawnX = 10, PartySpawnY = 2;
    public int ExitX = 10, ExitY = 0;
    public int SpineX = TownPlan.DefaultSpineX;
    public bool Detailed;
    public string InteriorKinds = "", ShopThemes = "";
#if UNITY_EDITOR
    [NonSerialized] private TWCGUILayout guiLayout;
#endif

    public CoreTownLayerGenerator() { }

    public ITWCAction Clone() => new CoreTownLayerGenerator
    {
        LayerName = LayerName,
        BuildingCount = BuildingCount,
        ShopFlags = ShopFlags,
        AllyCount = AllyCount,
        PartySpawnX = PartySpawnX,
        PartySpawnY = PartySpawnY,
        ExitX = ExitX,
        ExitY = ExitY,
        SpineX = SpineX,
        Detailed = Detailed, InteriorKinds = InteriorKinds, ShopThemes = ShopThemes
    };

#if UNITY_EDITOR
    public float GetGUIHeight() => guiLayout != null ? guiLayout.height : 18 * 8;
#else
    public float GetGUIHeight() => 18 * 8;
#endif

    public TownPlanOptions Options(TileWorldCreator twc)
    {
        var flags = new bool[Mathf.Max(1, BuildingCount)];
        for (int i = 0; i < flags.Length; i++) flags[i] = i < ShopFlags.Length && ShopFlags[i] == '1';
        return new TownPlanOptions(twc.currentSeed, twc.twcAsset.mapWidth, twc.twcAsset.mapHeight, flags, AllyCount,
            new GridPoint(PartySpawnX, PartySpawnY), new GridPoint(ExitX, ExitY), SpineX, Detailed,
            InteriorKinds.Length == flags.Length ? Enumerable.Range(0,flags.Length).Select(i=>new TownInteriorSpec((TownInteriorKind)(InteriorKinds[i]-'0'),i<ShopThemes.Length?(TownShopTheme)(ShopThemes[i]-'0'):TownShopTheme.General)).ToArray() : null);
    }

    public bool[,] Execute(bool[,] map, TileWorldCreator twc)
    {
        var town = CoreLayoutCache.GetTown(twc, Options(twc));
        if (LayerName == "HouseFloor")
        {
            var floor = town.Layers[TownLayers.Roofs].ToArray();
            foreach (var footprint in town.Footprints)
                foreach (var cell in footprint.Cells) floor[cell.X, cell.Y] = true;
            foreach (var door in town.BuildingSlots)
            {
                var room = town.ShopRoomAt(door);
                if (room == null) continue;
                foreach (var cell in room.Floor.Concat(room.Wall))
                    if (cell.Y > door.Y) floor[cell.X, cell.Y] = true;
            }
            return TileWorldCreatorUtilities.MergeMap(map, floor);
        }
        if (!town.Layers.TryGetValue(LayerName, out var layer)) { Debug.LogWarning($"Core town has no layer '{LayerName}'."); return map; }
        var cells = layer.ToArray();
        if (cells.GetLength(0) != map.GetLength(0) || cells.GetLength(1) != map.GetLength(1))
            throw new InvalidOperationException($"Core layer is {cells.GetLength(0)}x{cells.GetLength(1)} but the TWC asset is {map.GetLength(0)}x{map.GetLength(1)}.");
        return TileWorldCreatorUtilities.MergeMap(map, cells);
    }

    /// Writes configuration-derived options onto every CoreTownLayerGenerator in the asset. AllyCount is left as authored.
    public static void Configure(TileWorldCreatorAsset asset, TownConfiguration configuration)
    {
        if (configuration.Layout != null)
        {
            var options = configuration.Layout.Options;
            var interiorFlags = configuration.SlotBuildings.Select(b => b != null && b.HasInterior).ToArray();
            var interiors = options.Interiors.ToArray();
            for(int i=0;i<interiors.Length;i++) if(configuration.SlotBuildings[i]?.Id=="home") interiors[i]=new TownInteriorSpec(TownInteriorKind.Residential);
            Configure(asset, new TownPlanOptions(options.Seed, options.Width, options.Height, interiorFlags, options.AllyCount,
                options.PartySpawn, options.Exit, options.SpineX, options.Detailed, interiors));
            return;
        }
        int count = configuration.Buildings.Count;
        if(configuration.FurnishInteriors)
        {
            int side=Math.Max(52,TownPlanOptions.SizeFor(count));
            var specs=configuration.Buildings.Select(b=>new TownInteriorSpec(b.HasInterior?(b.InteriorKind==TownInteriorKind.None?TownInteriorKind.Shop:b.InteriorKind):TownInteriorKind.None,b.ShopTheme)).ToArray();
            configuration.PartySpawn=new Vector3Int(side/2,2,0);
            Configure(asset,new TownPlanOptions(0,side,side,specs.Select(s=>s.Kind!=TownInteriorKind.None).ToArray(),spineX:side/2,detailed:true,interiors:specs));
            return;
        }
        string flags = string.Concat(configuration.Buildings.Select(b => b != null && b.HasInterior ? '1' : '0'));
        var spawn = configuration.PartySpawn;
        foreach (var layer in asset.mapBlueprintLayers)
            foreach (var stack in layer.stack)
                if (stack.action is CoreTownLayerGenerator g)
                {
                    g.BuildingCount = count;
                    g.ShopFlags = flags;
                    g.PartySpawnX = spawn.x;
                    g.PartySpawnY = spawn.y;
                    g.ExitX = spawn.x;
                    g.ExitY = 0;
                    g.SpineX = TownPlan.DefaultSpineX;
                    g.Detailed = false;g.InteriorKinds="";g.ShopThemes="";
                }
        ConfigureRoofTiles(asset);
        ConfigureHouseLayers(asset);
    }

    /// <summary>Copies the complete Core contract to every layer on a runtime template.</summary>
    public static void Configure(TileWorldCreatorAsset asset, TownPlanOptions options)
    {
        asset.mapWidth = options.Width;
        asset.mapHeight = options.Height;
        foreach (string name in TownLayers.All.Concat(TownLayers.Detail).Concat(TownLayers.InteriorLayers))
        {
            var layer = asset.mapBlueprintLayers.FirstOrDefault(l => l.layerName == name);
            if (layer == null)
            {
                layer = new TileWorldCreatorAsset.BlueprintLayerData(name, true);
                asset.mapBlueprintLayers.Add(layer);
            }
            layer.stack = new System.Collections.Generic.List<TileWorldCreatorAsset.BlueprintLayerData.ActionStack> {
                new TileWorldCreatorAsset.BlueprintLayerData.ActionStack("Core " + name, new CoreTownLayerGenerator {
                    LayerName = name, BuildingCount = options.BuildingCount,
                    ShopFlags = string.Concat(options.ShopFlags.Select(f => f ? '1' : '0')),
                    AllyCount = options.AllyCount, PartySpawnX = options.PartySpawn.X, PartySpawnY = options.PartySpawn.Y,
                    ExitX = options.Exit.X, ExitY = options.Exit.Y, SpineX = options.SpineX, Detailed = options.Detailed,
                    InteriorKinds=string.Concat(options.Interiors.Select(i=>(char)('0'+(int)i.Kind))),ShopThemes=string.Concat(options.Interiors.Select(i=>(char)('0'+(int)i.Theme)))
                })
            };
            layer.randomSeedOverride = false;
            layer.previewTextureMap = null;
        }
        // Smart rendering layers can also contain Core actions (roads and shop boundaries).
        // Keep their derived stacks, but give every source action the same town contract.
        foreach(var generator in asset.mapBlueprintLayers.SelectMany(l=>l.stack).Select(s=>s.action).OfType<CoreTownLayerGenerator>())
        {
            generator.BuildingCount=options.BuildingCount;generator.ShopFlags=string.Concat(options.ShopFlags.Select(f=>f?'1':'0'));
            generator.AllyCount=options.AllyCount;generator.PartySpawnX=options.PartySpawn.X;generator.PartySpawnY=options.PartySpawn.Y;
            generator.ExitX=options.Exit.X;generator.ExitY=options.Exit.Y;generator.SpineX=options.SpineX;generator.Detailed=options.Detailed;
            generator.InteriorKinds=string.Concat(options.Interiors.Select(i=>(char)('0'+(int)i.Kind)));generator.ShopThemes=string.Concat(options.Interiors.Select(i=>(char)('0'+(int)i.Theme)));
        }
        TownInteriorRendering.Configure(asset);
        ConfigureRoofTiles(asset);
        ConfigureHouseLayers(asset);
    }

    private static void ConfigureHouseLayers(TileWorldCreatorAsset asset)
    {
        // The Roofs mask contains the entire building outline, including carved
        // shop interiors. The old shop wall mesh would cover the new facade.
        foreach (var layer in asset.mapBuildLayers)
            if (layer.layerName == "Smart/Walls") layer.active = false;

        var roofs = asset.mapBlueprintLayers.FirstOrDefault(l => l.layerName == TownLayers.Roofs);
        var source = roofs?.stack.Select(s => s.action).OfType<CoreTownLayerGenerator>().FirstOrDefault();
        if (source == null) return;
        // Streets include the approach cell, while indoor paving has its own mask.
        var streets = asset.mapBlueprintLayers.FirstOrDefault(l => l.layerName == "Smart/Roads");
        if (streets != null)
        {
            streets.stack.RemoveAll(s => s.action is CoreTownLayerGenerator g &&
                (g.LayerName == TownLayers.ShopFloor || g.LayerName == TownLayers.Buildings));
            var doors = (CoreTownLayerGenerator)source.Clone();
            doors.LayerName = TownLayers.Buildings;
            streets.stack.Add(new TileWorldCreatorAsset.BlueprintLayerData.ActionStack("Door approaches", doors));
        }
        var floor = asset.mapBlueprintLayers.FirstOrDefault(l => l.layerName == "HouseFloor");
        if (floor == null)
        {
            floor = new TileWorldCreatorAsset.BlueprintLayerData("HouseFloor", true);
            asset.mapBlueprintLayers.Add(floor);
        }
        var floorSource = (CoreTownLayerGenerator)source.Clone();
        floorSource.LayerName = "HouseFloor";
        floor.stack = new System.Collections.Generic.List<TileWorldCreatorAsset.BlueprintLayerData.ActionStack> {
            new TileWorldCreatorAsset.BlueprintLayerData.ActionStack("Complete house footprint", floorSource)
        };
        floor.randomSeedOverride = false;
        asset.mapBuildLayers.RemoveAll(l => l.layerName == "House floors");
        asset.mapBuildLayers.Add(new TownFloorTileLayer {
            layerName = "House floors", guid = new Guid("d71dd1c5-038a-4b6b-bdd6-6023daef1003"),
            assignedGenerationLayerGuid = floor.guid, active = true,
            Kit = EnvironmentKit.Load(), Material = Resources.Load<Material>("EnvironmentKit/HouseFloor")
        });
    }

    private static void ConfigureRoofTiles(TileWorldCreatorAsset asset)
    {
        var blueprint = asset.mapBlueprintLayers.FirstOrDefault(l => l.layerName == TownLayers.Roofs);
        if (blueprint == null) return;
        var index = asset.mapBuildLayers.FindIndex(l => l.layerName == "Roofs");
        var old = index >= 0 ? asset.mapBuildLayers[index] : null;
        var preset = (old as TWC.Actions.InstantiateTiles)?.tiles?.FirstOrDefault()?.preset ??
            (old as TownRoofTileLayer)?.Preset;
        var layer = new TownRoofTileLayer {
            layerName = "Roofs", assignedGenerationLayerGuid = blueprint.guid,
            guid = old?.guid ?? new Guid("fe20e151-a26d-4a49-b2b5-9825304a57e0"),
            active = true, Kit = EnvironmentKit.Load(), Preset = preset
        };
        if (index >= 0) asset.mapBuildLayers[index] = layer;
        else asset.mapBuildLayers.Add(layer);
    }

#if UNITY_EDITOR
    public override void DrawGUI(Rect rect, int layerIndex, TileWorldCreatorAsset asset, TileWorldCreator twc)
    {
        using (guiLayout = new TWCGUILayout(rect))
        {
            guiLayout.Add();
            int index = Mathf.Max(0, Array.IndexOf(CoreLayerNames.Town, LayerName));
            index = EditorGUI.Popup(guiLayout.rect, "core layer", index, CoreLayerNames.Town);
            LayerName = CoreLayerNames.Town[index];

            guiLayout.Add(); BuildingCount = EditorGUI.IntField(guiLayout.rect, "building count", BuildingCount);
            guiLayout.Add(); ShopFlags = EditorGUI.TextField(guiLayout.rect, "shop flags", ShopFlags);
            guiLayout.Add(); AllyCount = EditorGUI.IntField(guiLayout.rect, "ally count", AllyCount);
            guiLayout.Add(); PartySpawnX = EditorGUI.IntField(guiLayout.rect, "party spawn x", PartySpawnX);
            guiLayout.Add(); PartySpawnY = EditorGUI.IntField(guiLayout.rect, "party spawn y", PartySpawnY);
            guiLayout.Add(); ExitX = EditorGUI.IntField(guiLayout.rect, "exit x", ExitX);
            guiLayout.Add(); ExitY = EditorGUI.IntField(guiLayout.rect, "exit y", ExitY);
            guiLayout.Add(); SpineX = EditorGUI.IntField(guiLayout.rect, "road spine x", SpineX);
            guiLayout.Add(); Detailed = EditorGUI.Toggle(guiLayout.rect, "detailed layout", Detailed);
            guiLayout.Add(); InteriorKinds = EditorGUI.TextField(guiLayout.rect, "interior kinds (0–4)", InteriorKinds);
            guiLayout.Add(); ShopThemes = EditorGUI.TextField(guiLayout.rect, "shop themes (0–3)", ShopThemes);
        }
    }
#endif
}
