using System;
using System.Runtime.CompilerServices;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using TWC;

public static class CoreLayoutCache
{
    private sealed class Slot { public DungeonFloorOptions DungeonKey; public DungeonFloor Dungeon; public TownPlanOptions TownKey; public TownPlan Town; }
    private static readonly ConditionalWeakTable<TileWorldCreator, Slot> slots = new();
    /// Number of core generations performed (test hook).
    public static int Generations { get; private set; }

    public static DungeonFloor GetDungeon(TileWorldCreator twc, DungeonFloorOptions options)
    {
        if (twc == null) throw new ArgumentNullException(nameof(twc));
        var slot = slots.GetOrCreateValue(twc);
        if (slot.Dungeon == null || !options.Equals(slot.DungeonKey))
        {
            slot.Dungeon = DungeonFloorGenerator.Generate(options);
            slot.DungeonKey = options;
            Generations++;
        }
        return slot.Dungeon;
    }

    public static TownPlan GetTown(TileWorldCreator twc, TownPlanOptions options)
    {
        if (twc == null) throw new ArgumentNullException(nameof(twc));
        var slot = slots.GetOrCreateValue(twc);
        if (slot.Town == null || !options.Equals(slot.TownKey))
        {
            slot.Town = TownPlanGenerator.Generate(options);
            slot.TownKey = options;
            Generations++;
        }
        return slot.Town;
    }

    public static bool TryGetDungeon(TileWorldCreator twc, out DungeonFloor floor)
    {
        if (twc == null) throw new ArgumentNullException(nameof(twc));
        floor = null;
        if (slots.TryGetValue(twc, out var slot) && slot.Dungeon != null)
        {
            floor = slot.Dungeon;
            return true;
        }
        return false;
    }

    public static bool TryGetTown(TileWorldCreator twc, out TownPlan plan)
    {
        if (twc == null) throw new ArgumentNullException(nameof(twc));
        plan = null;
        if (slots.TryGetValue(twc, out var slot) && slot.Town != null)
        {
            plan = slot.Town;
            return true;
        }
        return false;
    }

    public static void Clear(TileWorldCreator twc) => slots.Remove(twc);

    /// TWC flags an all-false layer as failed; empty Carpet/ShopFloor layers are valid, so clear the flags after ExecuteAllBlueprintLayers (same as CampaignOverworld.Apply).
    public static void ClearResultFlags(TileWorldCreatorAsset asset)
    {
        if (asset == null) return;
        foreach (var layer in asset.mapBlueprintLayers)
        {
            layer.mapResultFailed = false;
            foreach (var stack in layer.stack) if (stack.action is TWC.Actions.TWCBlueprintAction action) action.resultFailed = false;
        }
    }
}
