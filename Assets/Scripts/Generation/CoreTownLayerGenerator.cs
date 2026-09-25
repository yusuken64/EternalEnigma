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
    [NonSerialized] private TWCGUILayout guiLayout;

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
        ExitY = ExitY
    };

    public float GetGUIHeight() => guiLayout != null ? guiLayout.height : 18 * 8;

    public TownPlanOptions Options(TileWorldCreator twc)
    {
        var flags = new bool[Mathf.Max(1, BuildingCount)];
        for (int i = 0; i < flags.Length; i++) flags[i] = i < ShopFlags.Length && ShopFlags[i] == '1';
        return new TownPlanOptions(twc.currentSeed, twc.twcAsset.mapWidth, twc.twcAsset.mapHeight, flags, AllyCount,
            new GridPoint(PartySpawnX, PartySpawnY), new GridPoint(ExitX, ExitY));
    }

    public bool[,] Execute(bool[,] map, TileWorldCreator twc)
    {
        var town = CoreLayoutCache.GetTown(twc, Options(twc));
        if (!town.Layers.TryGetValue(LayerName, out var layer)) { Debug.LogWarning($"Core town has no layer '{LayerName}'."); return map; }
        var cells = layer.ToArray();
        if (cells.GetLength(0) != map.GetLength(0) || cells.GetLength(1) != map.GetLength(1))
            throw new InvalidOperationException($"Core layer is {cells.GetLength(0)}x{cells.GetLength(1)} but the TWC asset is {map.GetLength(0)}x{map.GetLength(1)}.");
        return TileWorldCreatorUtilities.MergeMap(map, cells);
    }

    /// Writes configuration-derived options onto every CoreTownLayerGenerator in the asset. AllyCount is left as authored.
    public static void Configure(TileWorldCreatorAsset asset, TownConfiguration configuration)
    {
        int count = configuration.Buildings.Count;
        string flags = string.Concat(configuration.Buildings.Select(b => b != null && b.ShopCatalog != null && b.ShopCatalog.Count > 0 ? '1' : '0'));
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
                }
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
        }
    }
#endif
}
