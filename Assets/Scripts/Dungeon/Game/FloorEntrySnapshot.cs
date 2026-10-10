using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>The floor is rebuilt from its fixed seed; mutable party objects retain their identities.</summary>
internal sealed class FloorEntrySnapshot
{
    sealed class Member
    {
        public Ally Ally;
        public Vitals Vitals;
        public Stats Stats;
        public Vector3Int Position;
        public AttributePoints Attributes;
        public List<EquipableInventoryItem> Equipment;
        public List<Skill> Skills;
        public List<StatusEffect> Statuses;
        public bool PromptPending;
        public bool SkillsForgotten;
    }
    readonly List<Member> members;
    readonly List<InventoryItem> bag;
    readonly Dictionary<InventoryItem, (ItemDefinition definition, int? stock)> items;
    readonly int gold;
    readonly Ally controlled, leader;
    readonly Random.State random;
    readonly FloorRevealState reveal;

    internal FloorEntrySnapshot(Game game)
    {
        gold = game.PlayerController.Gold;
        controlled = game.PlayerController.ControlledAlly;
        leader = game.PlayerController.PartyLeader;
        bag = game.PlayerController.Inventory.InventoryItems.ToList();
        members = PartyRules.PartyMembers(game).Select(a => new Member {
            Ally = a, Vitals = new Vitals(a.Vitals), Stats = new Stats(a.BaseStats), Position = a.TilemapPosition,
            Attributes = a.Attributes, Equipment = a.Equipment.GetEquippedItems().ToList(),
            Skills = a.Skills.Select(Object.Instantiate).ToList(),
            Statuses = a.StatusEffects.Where(s => s != null).Select(s => { var copy = Object.Instantiate(s); copy.DamageSource = s.DamageSource; copy.gameObject.SetActive(false); return copy; }).ToList(),
            PromptPending = a.AttributePromptPending, SkillsForgotten = a.SkillsForgotten
        }).ToList();
        items = bag.Concat(members.SelectMany(m => m.Equipment)).Distinct()
            .ToDictionary(i => i, i => (i.ItemDefinition, i.StackStock));
        random = Random.state;
        reveal = JsonUtility.FromJson<FloorRevealState>(JsonUtility.ToJson(game.FloorReveal));
    }

    internal void Restore(Game game)
    {
        SummonRules.DespawnClones(game);
        game.DownedAllies.Clear();
        game.Allies.Clear();
        game.DeadUnits.RemoveAll(c => c is Ally);
        game.PlayerController.Inventory.Clear();
        foreach (var item in items) { item.Key.ItemDefinition = item.Value.definition; item.Key.StackStock = item.Value.stock; }
        foreach (var item in bag) game.PlayerController.Inventory.Add(item);
        game.PlayerController.Gold = gold;
        foreach (var member in members)
        {
            var ally = member.Ally;
            game.Allies.Add(ally);
            ally.gameObject.SetActive(true); ally.VisualParent.SetActive(true);
            foreach (var item in ally.Equipment.GetEquippedItems().ToArray()) ally.Equipment.UnEquip(item);
            foreach (var item in member.Equipment) ally.Equipment.Equip(item);
            foreach (var status in ally.StatusEffects.ToArray()) { ally.RemoveStatusEffect(status); Object.Destroy(status.gameObject); }
            foreach (var status in member.Statuses) { var copy = Object.Instantiate(status, ally.transform); copy.DamageSource = status.DamageSource; copy.gameObject.SetActive(true); ally.StatusEffects.Add(copy); }
            ally.Skills = member.Skills.Select(Object.Instantiate).ToList();
            ally.Attributes = member.Attributes;
            ally.AttributePromptPending = member.PromptPending;
            ally.SkillsForgotten = member.SkillsForgotten;
            ally.BaseStats = new Stats(member.Stats); ally.InvalidateCachedStats();
            ally.Vitals.Sync(member.Vitals); ally.SyncDisplayedStats();
            ally.SetPosition(member.Position); ally.currentInteractable = null;
            global::PendingCast.Cancel(ally, "floor reset");
        }
        game.PlayerController.TakeControl(leader);
        game.PlayerController.FocusCommand(controlled);
        game.FloorReveal = JsonUtility.FromJson<FloorRevealState>(JsonUtility.ToJson(reveal));
        Random.state = random;
    }

    internal void Dispose()
    {
        foreach (var member in members)
        {
            foreach (var skill in member.Skills) if (skill != null) Object.Destroy(skill);
            foreach (var status in member.Statuses) if (status != null) Object.Destroy(status.gameObject);
        }
    }
}
