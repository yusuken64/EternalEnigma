#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Progression;
using UnityEngine;

public static class ChaosFixtures
{
    public static List<ItemDefinition> Items(ItemManager manager) => manager.ItemDefinitions.Concat(manager.StartingItems)
        .Concat(Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings").SelectMany(b => b.ShopCatalog).Select(o => o.Item))
        .Where(i => i != null && !i.IsProgressionItem).Distinct().OrderBy(i => i.ItemName, StringComparer.Ordinal).ToList();

    public static ChaosFixture Create(int seed, TownConfiguration configuration, ItemManager manager, string entry = "Town",
        string previousComposition = null, ChaosPrerequisites required = null, ChaosCoverage coverage = null)
    {
        configuration.Validate();
        if (required != null && (required.Level < 1 || required.Level > LevelSystem.MaxLevel || required.HP < -1 || required.Mana < -1))
            throw new InvalidDataException("Prerequisite level/resources are outside supported limits.");
        var fixture = new ChaosFixture { Seed = seed, HeroSeed = ChaosRandom.Derive(seed, "heroes"),
            InventorySeed = ChaosRandom.Derive(seed, "inventory"), InputSeed = ChaosRandom.Derive(seed, "input"), Entry = entry };
        var heroes = new ChaosRandom(fixture.HeroSeed); var inventory = new ChaosRandom(fixture.InventorySeed);
        var pool = configuration.AllyCatalog.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        heroes.Shuffle(pool);
        if (coverage != null) pool = pool.OrderBy(h => Participation(coverage, "Hero", h.Id))
            .ThenBy(h => Participation(coverage, "Class", h.PrimaryClass?.Id)).ToList();
        TownAlly guaranteed = null;
        if (required != null)
        {
            guaranteed = pool.FirstOrDefault(h => (string.IsNullOrEmpty(required.HeroId) || h.Id == required.HeroId) &&
                (string.IsNullOrEmpty(required.ClassId) || h.PrimaryClass?.Id == required.ClassId || h.SecondaryClass?.Id == required.ClassId));
            if (guaranteed == null) throw new InvalidDataException("No eligible hero satisfies the scenario prerequisites.");
            pool.Remove(guaranteed); pool.Insert(0, guaranteed);
        }
        int count = 1 + heroes.Range(Math.Min(configuration.MaxPartySize, pool.Count));
        var selected = pool.Take(count).ToList();
        string Composition(IEnumerable<TownAlly> party) => string.Join("|", party.Select(h => h.Id).OrderBy(id => id, StringComparer.Ordinal));
        if (Composition(selected) == previousComposition && pool.Count > 1)
        {
            if (count < Math.Min(configuration.MaxPartySize, pool.Count)) selected.Add(pool[count]);
            else if (count > 1) selected.RemoveAt(selected.Count - 1);
            else if (guaranteed == null) selected[0] = pool[1];
        }
        heroes.Shuffle(selected);
        int controlling = heroes.Range(selected.Count);
        if (required?.ControlRequiredHero == true) controlling = selected.IndexOf(guaranteed);
        selected = selected.Skip(controlling).Concat(selected.Take(controlling)).ToList();
        fixture.ControlledHeroId = selected[0].Id;
        fixture.Composition = Composition(selected);
        var items = Items(manager);
        if (items.Select(i => i.ItemName).Distinct().Count() != items.Count)
            throw new InvalidDataException("Player-obtainable item names are not unique.");
        foreach (var hero in selected)
        {
            var data = new TownAllyData { AllyId = hero.Id, AllyName = hero.Name, Skills = hero.Skills?.ToList() ?? new() };
            HeroClassBinding.Capture(hero, data);
            foreach (var name in TownAlly.StartingSkillNames(hero.PrimaryClass, hero.SecondaryClass))
                if (!data.Skills.Contains(name)) data.Skills.Add(name);
            var allowed = items.OfType<EquipmentItemDefinition>().Where(i => HeroClass.AllowsItem(hero.PrimaryClass, hero.SecondaryClass,
                new EquipableInventoryItem(i, i.StackMax > 0 ? 1 : null))).ToList();
            inventory.Shuffle(allowed);
            var weapon = allowed.FirstOrDefault(i => !i.IsAmmunition && (i.EquipmentSlot == EquipmentSlot.MainHand || i.EquipmentSlot == EquipmentSlot.TwoHand));
            if (weapon != null) data.Equipment.Add(Item(weapon, inventory));
            bool bow = weapon?.WeaponType == WeaponType.BowAndArrow;
            if (bow || weapon?.EquipmentSlot != EquipmentSlot.TwoHand)
            {
                var offhand = allowed.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.OffHand && i.IsAmmunition == bow);
                if (offhand != null) data.Equipment.Add(Item(offhand, inventory));
            }
            var accessory = allowed.FirstOrDefault(i => i.EquipmentSlot == EquipmentSlot.Accessory);
            if (accessory != null && inventory.Range(2) == 0) data.Equipment.Add(Item(accessory, inventory));
            if (hero == guaranteed)
            {
                data.Level = data.HighestLevel = Math.Max(1, required.Level);
                data.Experience = LevelSystem.ExperienceAtLevel(data.Level);
                data.Hp = required.HP; data.Mana = required.Mana;
                if (!string.IsNullOrEmpty(required.SkillName))
                {
                    var offers = new[] { hero.PrimaryClass, hero.SecondaryClass }.Where(c => c != null)
                        .SelectMany(c => c.Skills).Where(e => e.Skill != null && e.Skill.SkillName == required.SkillName).ToList();
                    if (offers.Count == 0 || required.Rank < 1 || required.Rank > offers.Max(e => e.MaxRank))
                        throw new InvalidDataException("The required skill/rank is not legal for this hero's classes.");
                    if (!data.Skills.Contains(required.SkillName)) data.Skills.Add(required.SkillName);
                    data.SkillRanks.Add(new SkillRankSaveData { SkillName = required.SkillName, Rank = required.Rank });
                }
                if (required.Equipment.Count > 0) data.Equipment = ChaosJson.Clone(required).Equipment;
                fixture.Guaranteed.Add(JsonUtility.ToJson(required));
            }
            ValidateEquipment(data, hero, items);
            fixture.Party.Add(data);
        }
        fixture.Inventory = required == null ? new() : ChaosJson.Clone(required).Inventory;
        foreach (var item in fixture.Inventory) ValidateItem(item, items);
        if (fixture.Inventory.Count > Inventory.DefaultCapacity) throw new InvalidDataException("Prerequisites exceed inventory capacity.");
        var randomItems = items.ToList(); inventory.Shuffle(randomItems);
        var groups = randomItems.GroupBy(i => i is EquipmentItemDefinition e ? e.IsAmmunition ? "ammunition" : "equipment" :
            i is UsableItemDefinition ? "usable" : "material").Select(g => g.First()).ToList();
        groups.AddRange(randomItems.Except(groups));
        int desired = Math.Min(Inventory.DefaultCapacity, Math.Max(fixture.Inventory.Count, 4 + inventory.Range(7)));
        foreach (var item in groups)
        {
            if (fixture.Inventory.Count >= desired) break;
            if (fixture.Inventory.All(i => i.ItemName != item.ItemName)) fixture.Inventory.Add(Item(item, inventory));
        }
        var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed));
        var snapshot = context.Capture();
        snapshot.Identity = ChaosJson.Hash("chaos-fixture-v1/" + seed + "/" + entry).Substring(0, 32);
        snapshot.Roster = snapshot.Active = fixture.Party.Skip(1).Select(h => h.AllyId).ToArray();
        var save = new GameSaveData { ProtagonistId = fixture.ControlledHeroId, Roster = fixture.Party, Campaign = snapshot,
            TownSaveData = new TownSaveData { ConfigurationId = configuration.Id, TownSeed = seed,
                RecruitedAlliesData = fixture.Party, InventoryItems = fixture.Inventory },
            DungeonSaveData = new DungeonSaveData { StartFloor = 1, EndFloor = 2 } };
        if (entry == "DungeonScene")
        {
            var dungeon = context.Campaign.Locations.Where(l => l.ParentTownId == snapshot.LocationId &&
                (l.Kind == LocationKind.StoryDungeon || l.Kind == LocationKind.RepeatableDungeon)).OrderBy(l => l.Id).First();
            snapshot.Scene = "DungeonScene"; snapshot.PendingDungeon = snapshot.LocationId = dungeon.Id;
        }
        else if (entry == "Overworld")
        {
            var position = context.Grid.Locations[snapshot.LocationId];
            snapshot.X = position.X; snapshot.Y = position.Y; snapshot.Scene = "Overworld";
        }
        else if (entry != "Town" && entry != "MainMenu") throw new ArgumentException("Unsupported fixture entry: " + entry);
        save.SavedUtc = "2000-01-01T00:00:00Z";
        save.SavePointId = "chaos-fixture/home";
        save.Summary = new CampaignSaveSummary { Town = "Chaos fixture", Region = context.Location?.RegionId ?? "" };
        save.Summary.Party = fixture.Party.Select(h => new SavedHeroSummary { Id = h.AllyId, PortraitId = h.AllyId,
            Name = h.AllyName, PrimaryClass = h.PrimaryClassId, SecondaryClass = h.SecondaryClassId, Level = h.Level }).ToList();
        fixture.SaveJson = JsonUtility.ToJson(save);
        fixture.Seal(); return fixture;
    }
    private static int Participation(ChaosCoverage coverage, string kind, string id) => coverage.Participation.FirstOrDefault(p => p.Kind == kind && p.Id == id)?.Exercised ?? 0;
    private static ItemSaveData Item(ItemDefinition item, ChaosRandom random) => new() { ItemName = item.ItemName, HasStock = item.StackMax > 0,
        Stock = item.StackMax > 0 ? 1 + random.Range(item.StackMax) : 0 };
    public static void ValidateItem(ItemSaveData data, List<ItemDefinition> items)
    {
        var item = items.SingleOrDefault(i => i.ItemName == data.ItemName) ?? throw new InvalidDataException("Unknown/unobtainable item " + data.ItemName);
        if (data.HasStock != (item.StackMax > 0) || data.HasStock && (data.Stock < 1 || data.Stock > item.StackMax))
            throw new InvalidDataException("Invalid stack: " + data.ItemName);
    }
    private static void ValidateEquipment(TownAllyData data, TownAlly hero, List<ItemDefinition> items)
    {
        var equipment = data.Equipment.Select(i => { ValidateItem(i, items); var definition = items.Single(d => d.ItemName == i.ItemName) as EquipmentItemDefinition;
            return definition == null ? null : new EquipableInventoryItem(definition, i.HasStock ? i.Stock : null); }).ToList();
        if (equipment.Any(i => i == null || !HeroClass.AllowsItem(hero.PrimaryClass, hero.SecondaryClass, i)))
            throw new InvalidDataException("Illegal prerequisite equipment.");
        var loadout = equipment.Aggregate(new Loadout(null, null, null), (current, item) => current.With(item));
        if (loadout.Items.Count() != equipment.Count || equipment.Any(i => !loadout.Items.Contains(i)))
            throw new InvalidDataException("Conflicting prerequisite equipment would be displaced during restore.");
    }
}
#endif
