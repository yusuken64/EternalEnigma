using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

/// <summary>A bounded, depth-first environmental consequence chain, recorded once for playback.</summary>
internal sealed class TrapResolutionAction : GameAction
{
    readonly FantasyTrap trap;
    readonly Character victim;
    readonly Vector3Int direction;
    readonly HashSet<FantasyTrap> visited = new();
    readonly List<GameAction> replay = new();
    bool resolved;
    internal TrapResolutionAction(FantasyTrap trap, Character victim, Vector3Int direction)
    { this.trap = trap; this.victim = victim; this.direction = new Vector3Int(System.Math.Sign(direction.x), System.Math.Sign(direction.y)); TrackAnimationTarget(victim); }
    internal override bool IsValid(Character c) => victim != null && victim.Vitals.HP > 0;
    internal override List<GameAction> ExecuteImmediate(Character c)
    {
        if (resolved || !IsValid(c)) return new();
        resolved = true; Resolve(trap, direction);
        return new();
    }
    void Run(GameAction action)
    {
        replay.Add(action);
        var children = action.ExecuteImmediate(victim);
        action.RecordOutcome(victim);
        foreach (var child in children) Run(child);
    }
    void Text(string text, string explanation = null) => Run(new TrapFeedbackAction(victim, text, explanation));
    void Damage(Character target, int percent)
    {
        TrackAnimationTarget(target);
        Run(new TakeDamageAction(target, target, Mathf.Max(1, Mathf.CeilToInt(target.FinalStats.HPMax * percent / 100f))) { Environmental = true });
    }
    void Resolve(FantasyTrap current, Vector3Int entry)
    {
        if (current == null || !current.CanTrigger(victim) || victim.Vitals.HP <= 0 || visited.Count >= 8 || !visited.Add(current)) return;
        current.VisualObject?.SetActive(true);
        if (Random.value >= current.ActivationChance)
        { Text("Evaded", $"{GameMessages.Name(victim)} evaded the {current.GetInteractionText()}; no trap effects occurred."); return; }
        Text(current.GetInteractionText(), $"{GameMessages.Name(victim)} triggered the {current.GetInteractionText()}.");
        var dungeon = Game.Instance.CurrentDungeon;
        switch (current.Kind)
        {
            case FantasyTrapKind.Arrow: case FantasyTrapKind.IronArrow:
                Run(new TrapProjectileAction(victim, current.Kind == FantasyTrapKind.IronArrow ? "IronArrowProjectile" : "ArrowProjectile"));
                Damage(victim, current.DamagePercent); break;
            case FantasyTrapKind.FallingRock:
                Damage(victim, current.DamagePercent); break;
            case FantasyTrapKind.RollingLog:
                Run(new TrapProjectileAction(victim, "LogProjectile"));
                Damage(victim, current.DamagePercent); Move(current.Distance, entry, false); break;
            case FantasyTrapKind.Bomb:
                foreach (var target in Game.Instance.AllCharacters.Where(a => a.Vitals.HP > 0 && TileWorldDungeon.ChevDistance(a.TilemapPosition, current.Position) <= 1).ToArray())
                    Damage(target, current.DamagePercent);
                break;
            case FantasyTrapKind.Pitfall:
                Damage(victim, current.DamagePercent); Teleport(); break;
            case FantasyTrapKind.Warp: Teleport(); break;
            case FantasyTrapKind.Updraft: Move(current.Distance, entry, true); break;
            case FantasyTrapKind.IceSlick: Move(current.Distance, entry, false); break;
            case FantasyTrapKind.ShadowBind: case FantasyTrapKind.Hallucination: case FantasyTrapKind.WitheringHex:
                bool applied = false;
                foreach (var effect in current.Effects ?? new StatusEffect[0])
                {
                    if (effect == null || ClassPassives.IsImmune(victim, effect)) continue;
                    Run(new ApplyStatusEffectAction(victim, effect, victim)); applied = true;
                    string detail = effect switch
                    {
                        WeakenStatusEffect weaken => $"strength reduced by {weaken.Amount}",
                        SilenceStatusEffect => "skills and spells blocked",
                        StuckStatusEffect => "movement prevented",
                        ConfusionStatusEffect => "actions become uncontrolled",
                        _ => effect.GetEffectName()
                    };
                    Text(effect.GetEffectName(), $"{GameMessages.Name(victim)} received {effect.GetEffectName()} for {effect.TurnsLeft} turns: {detail}.");
                }
                if (!applied) Text("No effect", $"{GameMessages.Name(victim)} resisted the {current.GetInteractionText()}; no status was applied.");
                break;
            case FantasyTrapKind.Disarm: DropItems(true); break;
            case FantasyTrapKind.ClumsyJinx: DropItems(false); break;
            case FantasyTrapKind.Blight: case FantasyTrapKind.Scorch: ChangeFood(current.Kind == FantasyTrapKind.Blight); break;
            case FantasyTrapKind.Summoning: Spawn(false); break;
            case FantasyTrapKind.Transformation: Spawn(true); break;
        }
    }
    void Arrive(Vector3Int cell, Vector3Int entry)
    {
        var origin = victim.TilemapPosition;
        Run(new TrapMoveAction(victim, cell));
        foreach (var hazard in Game.Instance.CurrentDungeon.EntryEffects(victim, origin, cell)) Run(hazard);
        if (victim.Vitals.HP <= 0) return;
        if (Game.Instance.CurrentDungeon.GetInteractable(cell) is FantasyTrap next) Resolve(next, entry);
        else if (victim is Enemy && Game.Instance.CurrentDungeon.GetInteractable(cell) is CaltropTrap caltrops)
            foreach (var effect in caltrops.GetTrapSideEffects(victim)) Run(effect);
    }
    void Move(int distance, Vector3Int entry, bool launch)
    {
        if (victim.Vitals.HP <= 0) return;
        if (victim.IsMovementBlocked || entry == Vector3Int.zero) { Text("No effect", $"{GameMessages.Name(victim)} could not be displaced: movement is blocked."); return; }
        var dungeon = Game.Instance.CurrentDungeon;
        var origin = victim.TilemapPosition;
        if (launch)
        {
            // Travel cannot pass through a wall or occupied footprint; only landing triggers a trap.
            var landing = origin;
            for (int i = 0; i < distance; i++)
            {
                var next = landing + entry;
                if (!DungeonPlacement.CanStep(dungeon, victim, landing, next)) break;
                landing = next;
            }
            if (landing != origin)
            {
                Text("Launched", $"{GameMessages.Name(victim)} was launched {TileWorldDungeon.ChevDistance(origin, landing)} tiles across this floor.");
                Arrive(landing, entry);
            }
            else Text("No effect", $"{GameMessages.Name(victim)} could not be launched: the path is blocked.");
            return;
        }
        int travelled = 0;
        for (int i = 0; i < distance && victim.Vitals.HP > 0 && !victim.IsMovementBlocked; i++)
        {
            var next = victim.TilemapPosition + entry;
            if (!DungeonPlacement.CanStep(dungeon, victim, victim.TilemapPosition, next)) break;
            travelled++; Arrive(next, entry);
            if (victim.TilemapPosition != next) break; // A chained displacement takes over.
        }
        if (travelled == 0) Text("No effect", $"{GameMessages.Name(victim)} could not be pushed or slid: the path is blocked.");
        else Text("Displaced", $"{GameMessages.Name(victim)} was pushed or slid {travelled} tiles before stopping or triggering another trap.");
    }
    void Teleport()
    {
        if (victim.Vitals.HP <= 0) return;
        if (victim.IsMovementBlocked) { Text("No effect", $"{GameMessages.Name(victim)} could not warp: movement is blocked."); return; }
        var dungeon = Game.Instance.CurrentDungeon;
        var reachable = DungeonPlacement.Reachable(dungeon, victim.TilemapPosition);
        var candidates = DungeonPlacement.OpenCells(dungeon, victim).Where(p => p != victim.TilemapPosition && reachable.Contains(p)).ToList();
        if (candidates.Count == 0) { Text("No effect", $"{GameMessages.Name(victim)} could not warp: no free reachable destination exists."); return; }
        var destination = candidates[Random.Range(0, candidates.Count)];
        Text("Warped", $"{GameMessages.Name(victim)} warped to another open spot on the same floor.");
        Arrive(destination, direction);
    }
    Inventory Bag => victim is Ally ? Game.Instance.PlayerController.Inventory : victim.GetComponent<Inventory>();
    void DropItems(bool equipment)
    {
        var bag = Bag;
        var equippedItems = victim.Equipment != null ? victim.Equipment.GetEquippedItems().Cast<InventoryItem>() : Enumerable.Empty<InventoryItem>();
        var items = equipment ? equippedItems.Where(i => i?.ItemDefinition != null && !i.ItemDefinition.ProtectedFromTraps).Take(1).ToArray()
            : (bag?.InventoryItems ?? new List<InventoryItem>()).Where(i => i?.ItemDefinition != null && !i.ItemDefinition.ProtectedFromTraps && !Game.Instance.AllCharacters.Concat(Game.Instance.DownedAllies).Any(a => a != null && a.Equipment != null && a.Equipment.IsEquipped(i))).Take(3).ToArray();
        bool changed = false;
        foreach (var item in items)
        {
            if (!DungeonPlacement.TryDrop(Game.Instance.CurrentDungeon, victim.TilemapPosition, item, out _)) continue;
            if (item is EquipableInventoryItem equipped) victim.Equipment?.UnEquip(equipped);
            if (bag != null && bag.InventoryItems.Contains(item)) bag.Remove(item);
            Text("Dropped " + item.ItemName, $"{GameMessages.Name(victim)} dropped {(equipment ? "equipped " : "")}{item.ItemName} nearby; it can be recovered.");
            changed = true;
        }
        if (!equipment && victim is Enemy)
        {
            var carrier = victim.GetComponent<EnemyBehavior>();
            if (carrier?.TrapCarriedItem != null && !carrier.TrapCarriedItem.ItemDefinition.ProtectedFromTraps && DungeonPlacement.TryDrop(Game.Instance.CurrentDungeon, victim.TilemapPosition, carrier.TrapCarriedItem, out _))
            { Text("Dropped loot", $"{GameMessages.Name(victim)} dropped carried {carrier.TrapCarriedItem.ItemName} nearby; it can be recovered."); carrier.TrapCarriedItem = null; changed = true; }
            var transformed = victim.GetComponent<TrapCarriedItem>();
            if (transformed != null && transformed.TryDrop()) { Text("Dropped loot", $"{GameMessages.Name(victim)} dropped its animated item nearby; it can be recovered."); changed = true; }
        }
        if (!changed) Text("No effect", $"{GameMessages.Name(victim)} dropped nothing: no eligible {(equipment ? "equipment" : "items")} or free drop location.");
    }
    void ChangeFood(bool blight)
    {
        var bag = Bag;
        var carrier = victim is Enemy ? victim.GetComponent<EnemyBehavior>() : null;
        var animated = victim is Enemy ? victim.GetComponent<TrapCarriedItem>() : null;
        var item = bag?.InventoryItems.FirstOrDefault(i => i.ItemDefinition.IsFood && (blight ? i.ItemDefinition.SpoiledFood : i.ItemDefinition.CharredFood) != null)
            ?? (carrier?.TrapCarriedItem?.ItemDefinition.IsFood == true ? carrier.TrapCarriedItem : null)
            ?? (animated?.Item?.ItemDefinition.IsFood == true ? animated.Item : null);
        var definition = item == null ? null : blight ? item.ItemDefinition.SpoiledFood : item.ItemDefinition.CharredFood;
        if (definition == null) { Text("No effect", $"{GameMessages.Name(victim)} had no eligible food to {(blight ? "spoil" : "scorch")}."); return; }
        var replacement = definition.AsInventoryItem(definition.StackMax > 0 ? 1 : (int?)null);
        if (!item.HasStacks || item.StackStock.GetValueOrDefault() <= 1)
        {
            if (bag != null && bag.InventoryItems.Contains(item)) { bag.Remove(item); bag.Add(replacement); }
            else if (carrier != null && carrier.TrapCarriedItem == item) carrier.TrapCarriedItem = replacement;
            else animated.Item = replacement;
        }
        else
        {
            if (bag != null && bag.CanAdd()) bag.Add(replacement);
            else if (!DungeonPlacement.TryDrop(Game.Instance.CurrentDungeon, victim.TilemapPosition, replacement, out _)) { Text("No effect", "The food was unchanged: no room in the bag or on the floor for the changed unit."); return; }
            item.Decrement();
        }
        Text(blight ? "Food spoiled" : "Food scorched", $"{GameMessages.Name(victim)} had one {item.ItemName} changed into {replacement.ItemName}; it restores {(blight ? 25 : 50)}% of normal fullness.");
    }
    void Spawn(bool transform)
    {
        var game = Game.Instance; var dungeon = game.CurrentDungeon;
        var item = transform ? dungeon.Interactables.OfType<DroppedItem>().Where(i => !i.Opened && i.InventoryItem?.ItemDefinition != null && !i.InventoryItem.ItemDefinition.ProtectedFromTraps && TileWorldDungeon.ChevDistance(i.Position, victim.TilemapPosition) <= 2).OrderBy(i => i.Position.y).ThenBy(i => i.Position.x).FirstOrDefault() : null;
        if (transform && item == null) { Text("No effect", "Transformation failed: no ordinary dropped item was within 2 tiles."); return; }
        var manager = game.EnemyManager;
        var prefabs = manager.SpawnDefinitions.Where(s => s.FloorMin <= game.PlayerController.Floor && s.FloorMax >= game.PlayerController.Floor && s.EnemyCharacterPrefab != null && !s.EnemyCharacterPrefab.IsBoss)
            .OrderBy(s => s.SpawnName, System.StringComparer.Ordinal).Select(s => s.EnemyCharacterPrefab).ToArray();
        if (prefabs.Length == 0) { Text("No effect", "No eligible monster is available for this floor."); return; }
        bool spawned = false;
        for (int i = 0; i < (transform ? 1 : 2); i++)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            var cells = DungeonPlacement.OpenCells(dungeon, prefab).Where(p => TileWorldDungeon.ChevDistance(p, victim.TilemapPosition) <= 2 && (dungeon.GetInteractable(p) == null || dungeon.GetInteractable(p) == item)).ToList();
            if (cells.Count == 0) continue;
            var action = new SpawnEnemyAction(prefab, cells[Random.Range(0, cells.Count)], dungeon.CellToWorld(victim.TilemapPosition));
            Run(action); spawned = true;
            if (transform)
            {
                action.SpawnedEnemy.gameObject.AddComponent<TrapCarriedItem>().Item = item.InventoryItem;
                Text("Item animated", $"{item.InventoryItem.ItemName} became {GameMessages.Name(action.SpawnedEnemy)}; defeat it to recover the item.");
                dungeon.RemoveInteractable(item);
            }
            else Text("Monster summoned", $"The trap summoned {GameMessages.Name(action.SpawnedEnemy)} nearby.");
        }
        if (!spawned) Text("No effect", "No monster appeared: nearby spawn locations are blocked.");
    }
    internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false)
    {
        foreach (var action in replay)
        { action.UpdateDisplayedStats(); yield return action.ExecuteRoutine(victim, skipAnimation); }
    }
    internal override void AddDestinationSight(HashSet<Vector3Int> tiles)
    { foreach (var action in replay) action.AddDestinationSight(tiles); }
    internal override IEnumerable<Vector3Int> AnimationCells(Character c)
    { foreach (var action in replay) foreach (var cell in action.AnimationCells(victim)) yield return cell; }
}

internal sealed class TrapFeedbackAction : GameAction
{
    readonly Character target;
    internal readonly string Message;
    internal readonly string HistoryMessage;
    internal TrapFeedbackAction(Character target, string message, string explanation = null)
    { this.target = target; Message = message; HistoryMessage = explanation ?? $"{GameMessages.Name(target)}: {message}"; }
    internal override bool IsValid(Character c) => true;
    internal override List<GameAction> ExecuteImmediate(Character c)
    { GameMessages.ForCharacter(target, HistoryMessage); return new(); }
    internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false)
    {
        if (!skipAnimation) DungeonFloatingText.Show(Game.Instance, Message, Message == "Evaded" ? Color.white : Color.yellow, target);
        yield break;
    }
}

internal sealed class TrapMoveAction : GameAction
{
    readonly Character target;
    readonly Vector3Int destination;
    internal TrapMoveAction(Character target, Vector3Int destination) { this.target = target; this.destination = destination; }
    internal override bool IsValid(Character c) => true;
    internal override List<GameAction> ExecuteImmediate(Character c)
    { target.TilemapPosition = destination; if (target is Ally ally) ally.currentInteractable = null; return new(); }
    internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false)
    {
        var world = Game.Instance.CurrentDungeon.CellToWorld(destination);
        if (skipAnimation) target.transform.position = world;
        else yield return target.transform.DOMove(world, .12f).WaitForCompletion();
    }
    internal override void AddDestinationSight(HashSet<Vector3Int> tiles) => AddAllySight(tiles, target, destination);
    internal override IEnumerable<Vector3Int> AnimationCells(Character c) => AnimationPath(target, destination);
}
