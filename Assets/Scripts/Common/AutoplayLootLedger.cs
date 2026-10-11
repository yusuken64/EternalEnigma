using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

[Serializable]
public sealed class AutoplayLootRecord
{
    public string Id, Source, Kind, Item, ActorId, Failure;
    public Vector3Int InitialCell, Cell;
    public int ExpectedAmount, Before, After, Action;
    public bool Spawned, Collected, Removed;
    public Vector3 VisualCenter;
}

/// <summary>Receipts surround individual production pickups, including companion actions.
/// A missing object or a walkable coordinate is never accepted as a receipt.</summary>
internal sealed class AutoplayLootLedger
{
    readonly AutoplayReport report;
    readonly Action<AutoplayEvent> log;
    readonly Dictionary<Interactable, AutoplayLootRecord> pickups = new();
    readonly Dictionary<DungeonProp, List<AutoplayLootRecord>> rewards = new();
    readonly List<Vector3Int> declaredGathering = new();
    internal void DeclareGathering(Vector3Int cell) => declaredGathering.Add(cell);
    DungeonProp releasing;
    internal IEnumerable<Interactable> Pending => pickups.Where(p => !p.Value.Collected && p.Key != null).Select(p => p.Key)
        .Concat(rewards.Where(p => p.Key != null && !p.Key.Opened).Select(p => (Interactable)p.Key));
    internal bool Failed => report.PlacementIssues.Count > 0 || report.RecoveryEvents > 0 || report.Loot.Any(r => r.Failure != null);
    internal AutoplayLootLedger(AutoplayReport report, Action<AutoplayEvent> log) { this.report = report; this.log = log; }
    static string Kind(Interactable value) => value is DroppedItem ? "Item" : value is Gold ? "Gold" : value is SmallKey ? "Key" : value is GatheringPoint ? "Gathering" : null;
    AutoplayLootRecord New(string kind, Vector3Int cell, string source)
    {
        var record = new AutoplayLootRecord { Id = "loot-" + (report.Loot.Count + 1).ToString("D4"), Kind = kind, Source = source,
            InitialCell = cell, Cell = cell };
        report.Loot.Add(record); report.ExpectedLoot = report.Loot.Count;
        return record;
    }
    internal void Observe(Interactable value)
    {
        if (value == null) return;
        if (value is DungeonProp prop)
        {
            if (rewards.ContainsKey(prop) || prop.Opened || prop.Definition.Reward == SceneryReward.None && !prop.Definition.HoldsKey) return;
            var rows = new List<AutoplayLootRecord>(); rewards.Add(prop, rows);
            string source = prop.Definition.Kind + "@" + prop.Position;
            if (prop.Definition.Reward != SceneryReward.None) rows.Add(New(prop.Definition.Reward.ToString(), prop.Position, source));
            if (prop.Definition.HoldsKey) rows.Add(New("Key", prop.Position, source));
            return;
        }
        var kind = Kind(value);
        if (kind == null || pickups.ContainsKey(value)) return;
        AutoplayLootRecord row = null;
        if (releasing != null && rewards.TryGetValue(releasing, out var expected)) row = expected.FirstOrDefault(r => r.Kind == kind && !r.Spawned);
        row ??= New(kind, value.Position, "Loose");
        row.Spawned = true; row.Cell = value.Position;
        if (value is DroppedItem item) { row.Item = item.InventoryItem.ItemName; row.ExpectedAmount = item.InventoryItem.StackStock ?? 1; }
        if (value is Gold gold) row.ExpectedAmount = gold.SeededAmount ?? 0;
        if (value is SmallKey) row.ExpectedAmount = 1;
        pickups.Add(value, row);
        if (Game.Instance?.IsReady == true) InspectPlacement(Game.Instance.CurrentDungeon, value);
        log(new AutoplayEvent { Kind = "LootExpected", LootId = row.Id, Message = row.Source, Item = row.Item, Position = row.Cell.ToString(), ExpectedAmount = row.ExpectedAmount });
    }
    internal IDisposable Release(DungeonProp prop)
    {
        Observe(prop);
        var previous = releasing; releasing = prop;
        return new OnDispose(() => { releasing = previous; Audit(); });
    }
    internal IDisposable Pickup(Interactable value, Character actor, int amount, ItemDefinition material = null)
    {
        Observe(value);
        if (!pickups.TryGetValue(value, out var row)) return null;
        var game = Game.Instance;
        InspectPlacement(game.CurrentDungeon, value);
        var inventory = game.PlayerController.Inventory;
        // Expand before CanAdd, including pickups occurring inside a companion's turn.
        inventory.MaxItems = Math.Max(inventory.MaxItems, inventory.Count() + 32);
        var item = (value as DroppedItem)?.InventoryItem;
        int Stock() => value is Gold ? game.PlayerController.Gold : value is SmallKey ? SmallKeys.Count :
            material != null ? inventory.InventoryItems.Where(i => i.ItemDefinition == material).Sum(i => i.StackStock ?? 1) :
            inventory.InventoryItems.Where(i => ReferenceEquals(i, item)).Sum(i => i.StackStock ?? 1);
        int before = Stock();
        bool wasPresent = game.CurrentDungeon.Interactables.Contains(value);
        row.ExpectedAmount = amount;
        return new OnDispose(() =>
        {
            int after = Stock(); bool removed = !game.CurrentDungeon.Interactables.Contains(value);
            bool success = wasPresent && !row.Collected && removed && after - before == amount && amount > 0 && actor is Ally;
            if (success)
            {
                row.Collected = true; row.Removed = true; row.Before = before; row.After = after;
                row.ActorId = actor is Ally ally ? ally.TownAllyId : actor.name; row.Action = report.Actions;
                report.CollectedLoot++;
            }
            else if (removed || after != before) row.Failure = "Pickup removal/resource delta mismatch or duplicate collection";
            log(new AutoplayEvent { Kind = "LootCollection", LootId = row.Id, ActorId = row.ActorId, Item = row.Item,
                Position = row.Cell.ToString(), ExpectedAmount = amount, StockBefore = before, StockAfter = after,
                Success = success, Removed = removed, Message = row.Failure });
        });
    }
    internal void Audit()
    {
        foreach (var pair in pickups)
            if (!pair.Value.Collected && (pair.Key == null || Game.Instance != null && !Game.Instance.CurrentDungeon.Interactables.Contains(pair.Key)))
                pair.Value.Failure ??= "Pickup disappeared without a verified collection";
        foreach (var pair in rewards)
            if (pair.Key == null || pair.Key.Opened)
                foreach (var row in pair.Value.Where(r => !r.Spawned)) row.Failure ??= "Reward source opened without producing its expected drop";
        report.UnresolvedLoot = report.Loot.Where(r => !r.Collected || r.Failure != null)
            .Select(r => $"{r.Id} {r.Kind} {r.Cell}: {r.Failure ?? (r.Spawned ? "Not collected" : "Reward not released")}").ToList();
    }
    internal void InspectPlacement(TileWorldDungeon dungeon)
    {
        foreach (var value in dungeon.Interactables.ToArray()) Observe(value);
        foreach (var value in Pending.ToArray())
            InspectPlacement(dungeon, value);
    }
    internal void VerifyGeneration(TileWorldDungeon dungeon)
    {
        var matched = new HashSet<AutoplayLootRecord>();
        void Expected(string kind, Vector3Int cell)
        {
            var actual = report.Loot.FirstOrDefault(r => r.Kind == kind && r.InitialCell == cell && r.Source == "Loose" && !matched.Contains(r));
            if (actual != null) { matched.Add(actual); return; }
            var row = New(kind, cell, "Generation");
            row.Failure = "Generated loot was never instantiated";
            log(new AutoplayEvent { Kind = "PlacementFailure", LootId = row.Id, Position = cell.ToString(), Message = row.Failure });
        }
        foreach (var p in dungeon.Floor.Items) Expected("Item", p.Cell.ToCell());
        foreach (var p in dungeon.Floor.Gold) Expected("Gold", p.Cell.ToCell());
        foreach (var cell in declaredGathering) Expected("Gathering", cell);
        foreach (var p in dungeon.Floor.Scenery.Where(p => p.Reward != SceneryReward.None || p.HoldsKey))
        {
            if (rewards.Keys.Any(r => r != null && r.Position == p.Cell.ToCell())) continue;
            if (p.Reward != SceneryReward.None) New(p.Reward.ToString(), p.Cell.ToCell(), "Generation").Failure = "Generated reward prop was never instantiated";
            if (p.HoldsKey) New("Key", p.Cell.ToCell(), "Generation").Failure = "Generated key prop was never instantiated";
        }
        Audit();
    }
    private void InspectPlacement(TileWorldDungeon dungeon, Interactable value)
    {
            bool prop = value is DungeonProp;
            if (!(prop ? dungeon.IsFloorCell(value.Position) : dungeon.IsWalkable(value.Position)))
                PlacementFailure(value, "Loot is outside valid floor or overlaps completed blockers");
            var renderers = value.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) { PlacementFailure(value, "Loot has no visible mesh"); return; }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (pickups.TryGetValue(value, out var row)) row.VisualCenter = bounds.center;
            float size = dungeon.CellToWorld(Vector3Int.right).x - dungeon.CellToWorld(Vector3Int.zero).x;
            var center = dungeon.CellToWorld(value.Position) + new Vector3(size * .5f, size * .5f);
            if (Mathf.Abs(center.x - bounds.center.x) > size * .05f || Mathf.Abs(center.y - bounds.center.y) > size * .05f)
                PlacementFailure(value, "Visible mesh does not align with logical pickup cell");
    }
    internal void PlacementFailure(Interactable value, string reason)
    {
        string message = $"{value.name}@{value.Position}: {reason}";
        if (report.PlacementIssues.Contains(message)) return;
        report.PlacementIssues.Add(message);
        log(new AutoplayEvent { Kind = "PlacementFailure", Position = value.Position.ToString(), Message = message });
    }
    internal sealed class OnDispose : IDisposable
    {
        Action action;
        internal OnDispose(Action action) { this.action = action; }
        public void Dispose() { var callback = action; action = null; callback?.Invoke(); }
    }
}
