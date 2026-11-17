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

    private static DungeonFloorOptions prewarmKey;
    private static DungeonFloor prewarmFloor;
    private static System.Threading.Tasks.Task<DungeonFloor> prewarmTask;

    /// <summary>
    /// Generates a floor ahead of need. Core generation is a pure function of its options, so the
    /// result is the same floor that <see cref="GetDungeon"/> would otherwise build on demand.
    /// </summary>
    public static void Prewarm(DungeonFloorOptions options)
    {
        if (options == null || options.Equals(prewarmKey)) return;
        prewarmKey = options; prewarmFloor = null; prewarmTask = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        // The web player has no worker threads; callers choose an idle moment.
        prewarmFloor = DungeonFloorGenerator.Generate(options);
#else
        prewarmTask = System.Threading.Tasks.Task.Run(() => DungeonFloorGenerator.Generate(options));
#endif
    }

    private static DungeonFloor TakePrewarmed(DungeonFloorOptions options)
    {
        if (prewarmKey == null || !options.Equals(prewarmKey)) return null;
        var floor = prewarmFloor;
        var task = prewarmTask;
        prewarmKey = null; prewarmFloor = null; prewarmTask = null;
        if (floor != null || task == null) return floor;
        // A failed background run falls through to the ordinary path, which reports the error.
        try { return task.Result; } catch (AggregateException) { return null; }
    }

    public static DungeonFloor GetDungeon(TileWorldCreator twc, DungeonFloorOptions options)
    {
        if (twc == null) throw new ArgumentNullException(nameof(twc));
        var slot = slots.GetOrCreateValue(twc);
        if (slot.Dungeon == null || !options.Equals(slot.DungeonKey))
        {
            if (RuntimeMeshMemory.Enabled) RuntimeMeshMemory.Log("Core dungeon generation start");
            slot.Dungeon = TakePrewarmed(options) ?? DungeonFloorGenerator.Generate(options);
            if (RuntimeMeshMemory.Enabled) RuntimeMeshMemory.Log("Core dungeon generated");
            slot.DungeonKey = options;
            slot.Town = slot.Dungeon.TownPlan;
            slot.TownKey = options.Variant == DungeonVariant.Town ? TownDungeonGenerator.TownOptions(options) : null;
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
            if (RuntimeMeshMemory.Enabled) RuntimeMeshMemory.Log("Core town generation start");
            slot.Town = TownPlanGenerator.Generate(options);
            if (RuntimeMeshMemory.Enabled) RuntimeMeshMemory.Log("Core town generated");
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

    /// <summary>The town plan together with the options it was generated from, for handing to another creator.</summary>
    public static bool TryGetTown(TileWorldCreator twc, out TownPlan plan, out TownPlanOptions options)
    {
        if (twc == null) throw new ArgumentNullException(nameof(twc));
        plan = null; options = null;
        if (!slots.TryGetValue(twc, out var slot) || slot.Town == null) return false;
        plan = slot.Town; options = slot.TownKey;
        return true;
    }

    /// <summary>Registers an already generated town plan for a creator that is reusing its geometry.</summary>
    public static void SupplyTown(TileWorldCreator twc, TownPlanOptions options, TownPlan plan)
    {
        var slot = slots.GetOrCreateValue(twc);
        slot.TownKey = options; slot.Town = plan;
        slot.Dungeon = null; slot.DungeonKey = null;
    }

    public static void Clear(TileWorldCreator twc) => slots.Remove(twc);

    public static void SupplyDungeon(TileWorldCreator twc, DungeonFloorOptions options, DungeonFloor floor)
    {
        var slot = slots.GetOrCreateValue(twc);
        slot.DungeonKey = options; slot.Dungeon = floor; slot.Town = floor.TownPlan;
        slot.TownKey = options.Variant == DungeonVariant.Town ? TownDungeonGenerator.TownOptions(options) : null;
    }

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
