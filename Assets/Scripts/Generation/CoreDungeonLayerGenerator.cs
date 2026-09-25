using System;
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
[ActionName(Name = "Core Dungeon Layer")]
public sealed class CoreDungeonLayerGenerator : TWCBlueprintAction, ITWCAction
{
    public string LayerName = DungeonLayers.Floor;
    public bool Throne;
    public int EnemyCount = 10, GoldCount = 5, ItemCount = 5, TrapCount = 5;
    [NonSerialized] private TWCGUILayout guiLayout;

    public CoreDungeonLayerGenerator() { }
    public ITWCAction Clone() => new CoreDungeonLayerGenerator { LayerName = LayerName, Throne = Throne, EnemyCount = EnemyCount, GoldCount = GoldCount, ItemCount = ItemCount, TrapCount = TrapCount };
    public float GetGUIHeight() => guiLayout != null ? guiLayout.height : 18 * 6;

    public DungeonFloorOptions Options(TileWorldCreator twc) =>
        Throne ? DungeonFloorOptions.Throne(twc.currentSeed)
               : new DungeonFloorOptions(twc.currentSeed, twc.twcAsset.mapWidth, twc.twcAsset.mapHeight, false, EnemyCount, GoldCount, ItemCount, TrapCount);

    public bool[,] Execute(bool[,] map, TileWorldCreator twc)
    {
        var floor = CoreLayoutCache.GetDungeon(twc, Options(twc));
        if (!floor.Layers.TryGetValue(LayerName, out var layer)) { Debug.LogWarning($"Core dungeon has no layer '{LayerName}'."); return map; }
        var cells = layer.ToArray();
        if (cells.GetLength(0) != map.GetLength(0) || cells.GetLength(1) != map.GetLength(1))
            throw new InvalidOperationException($"Core layer is {cells.GetLength(0)}x{cells.GetLength(1)} but the TWC asset is {map.GetLength(0)}x{map.GetLength(1)}. Throne floors must use a 12x12 asset.");
        return TileWorldCreatorUtilities.MergeMap(map, cells);
    }

    /// Sets the same options on every CoreDungeonLayerGenerator in the asset (all layers of one asset must agree).
    public static void Configure(TileWorldCreatorAsset asset, bool throne, int enemies = 10, int gold = 5, int items = 5, int traps = 5) { /* iterate asset.mapBlueprintLayers[*].stack[*].action as CoreDungeonLayerGenerator and assign */ }

#if UNITY_EDITOR
    public override void DrawGUI(Rect rect, int layerIndex, TileWorldCreatorAsset asset, TileWorldCreator twc)
    {
        using (guiLayout = new TWCGUILayout(rect))
        {
            guiLayout.Add();
            int index = Mathf.Max(0, Array.IndexOf(CoreLayerNames.Dungeon, LayerName));
            index = EditorGUI.Popup(guiLayout.rect, "core layer", index, CoreLayerNames.Dungeon);
            LayerName = CoreLayerNames.Dungeon[index];
            guiLayout.Add(); Throne = EditorGUI.Toggle(guiLayout.rect, "throne floor", Throne);
            guiLayout.Add(); EnemyCount = EditorGUI.IntField(guiLayout.rect, "enemies", EnemyCount);
            guiLayout.Add(); GoldCount = EditorGUI.IntField(guiLayout.rect, "gold", GoldCount);
            guiLayout.Add(); ItemCount = EditorGUI.IntField(guiLayout.rect, "items", ItemCount);
            guiLayout.Add(); TrapCount = EditorGUI.IntField(guiLayout.rect, "traps", TrapCount);
        }
    }
#endif
}
