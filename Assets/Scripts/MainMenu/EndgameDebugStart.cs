using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using UnityEngine;

public enum EndgameDebugDestination { Town, Dungeon, Overworld }

/// <summary>One reproducible, non-persistent endgame party for all developer starts.</summary>
public static class EndgameDebugStart
{
    public const int Seed = 42;

    public static GameSaveData Create(TownConfiguration configuration, ItemManager items, EndgameDebugDestination destination)
    {
        configuration.Validate();
        var party = new[] { "warrior", "healer", "elementalist", "archer" }
            .Select(id => configuration.AllyCatalog.First(hero => hero.PrimaryClass?.Id == id)).ToArray();
        var equipment = items.ItemDefinitions.Concat(items.StartingItems)
            .Concat(Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings")
                .SelectMany(building => building.ShopCatalog).Select(offer => offer.Item))
            .OfType<EquipmentItemDefinition>().Where(item => !item.IsProgressionItem).Distinct().ToArray();
        var roster = party.Select(hero => CreateHero(hero, equipment)).ToList();
        var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, Seed));
        var campaign = context.Campaign;
        var town = campaign.Locations.Where(location => location.Kind == LocationKind.Town)
            .OrderByDescending(location => location.Tier).ThenBy(location => location.Id, StringComparer.Ordinal).First();
        var final = campaign.Locations.Single(location => location.Id == campaign.FinalLocationId);
        var snapshot = context.Capture();
        snapshot.LocationId = snapshot.LastTownId = town.Id;
        snapshot.Roster = snapshot.Active = roster.Skip(1).Select(hero => hero.AllyId).ToArray();
        snapshot.Completed = campaign.Locations.Where(location => location.Id != final.Id &&
            (location.Kind == LocationKind.StoryDungeon || location.Kind == LocationKind.RepeatableDungeon))
            .Select(location => location.Id).ToArray();
        snapshot.Keys = campaign.Routes.Where(route => !string.IsNullOrEmpty(route.KeyId)).Select(route => route.KeyId).Distinct().ToArray();
        snapshot.Opened = snapshot.Resolved = campaign.Routes.Select(route => route.Id).ToArray();
        snapshot.Claimed = campaign.Sources.Select(source => source.Id).ToArray();
        snapshot.Permanent = campaign.Sources.Select(source => source.Capability).Distinct().ToArray();
        snapshot.Towns = campaign.Locations.Where(location => location.Kind == LocationKind.Town).Select(location => location.Id).ToArray();
        var position = context.Grid.Locations[town.Id];
        snapshot.X = position.X; snapshot.Y = position.Y;
        if (destination == EndgameDebugDestination.Overworld) snapshot.Scene = "Overworld";
        if (destination == EndgameDebugDestination.Dungeon)
        {
            snapshot.Scene = "DungeonScene";
            snapshot.LocationId = snapshot.PendingDungeon = final.Id;
            position = context.Grid.Locations[final.ParentTownId ?? final.Id];
            snapshot.X = position.X; snapshot.Y = position.Y;
        }
        var floors = CampaignContext.Floors(final.Tier);
        return new GameSaveData {
            IsSandbox = true,
            ProtagonistId = roster[0].AllyId,
            Roster = roster,
            Campaign = snapshot,
            VisitedRegions = campaign.Regions.Select(region => region.Id).ToList(),
            TownSaveData = new TownSaveData {
                ConfigurationId = configuration.Id, TownSeed = context.LocationSeed(town.Id), Gold = 99999,
                RecruitedAlliesData = roster.ToList(),
                InventoryItems = ItemSaveData.Capture(items.StartingItems.Where(item => item != null)
                    .Distinct().Select(item => item.AsInventoryItem(item.StackMax > 0 ? item.StackMax : null)))
            },
            DungeonSaveData = new DungeonSaveData {
                StartFloor = floors.Start, EndFloor = floors.End, UseBiomeLayout = true,
                LayoutTier = final.Tier, LayoutBiome = OverworldGridGenerator.BiomeForRegion(campaign, final.RegionId)
            }
        };
    }

    private static TownAllyData CreateHero(TownAlly hero, EquipmentItemDefinition[] equipment)
    {
        var data = HeroClassBinding.FromPrefab(hero, new TownAllyData {
            AllyId = hero.Id, AllyName = hero.Name, Level = LevelSystem.MaxLevel,
            HighestLevel = LevelSystem.MaxLevel, Experience = LevelSystem.ExperienceAtLevel(LevelSystem.MaxLevel),
            Skills = hero.Skills?.ToList() ?? new List<string>()
        });
        foreach (var offer in SkillLearningRules.Offers(HeroClass.ToKit(hero.PrimaryClass, hero.SecondaryClass)))
        {
            if (!data.Skills.Contains(offer.SkillId)) data.Skills.Add(offer.SkillId);
            data.SkillRanks.Add(new SkillRankSaveData { SkillName = offer.SkillId, Rank = offer.MaxRank });
        }
        while (HeroAttributes.Pending(data.Level, data.Attributes) > 0)
            data.Attributes = data.Attributes.Added(hero.PrimaryClass.PreferredAttribute);

        var allowed = equipment.Where(item => HeroClass.AllowsItem(hero.PrimaryClass, hero.SecondaryClass,
            new EquipableInventoryItem(item))).ToArray();
        var preferred = hero.PrimaryClass.AllowedWeapons.First(type => type != WeaponType.OffhandSword && type != WeaponType.OffhandShield);
        var weapons = allowed.Where(item => !item.IsAmmunition &&
            (item.EquipmentSlot == EquipmentSlot.MainHand || item.EquipmentSlot == EquipmentSlot.TwoHand || item.WeaponType == WeaponType.BowAndArrow));
        var weapon = weapons.OrderByDescending(item => item.WeaponType == preferred)
            .ThenByDescending(item => Score(item, hero)).ThenBy(item => item.ItemName, StringComparer.Ordinal).First();
        var loadout = new List<EquipmentItemDefinition> { weapon };
        bool bow = weapon.WeaponType == WeaponType.BowAndArrow;
        if (bow || weapon.EquipmentSlot != EquipmentSlot.TwoHand)
        {
            var offhand = allowed.Where(item => item.EquipmentSlot == EquipmentSlot.OffHand &&
                    (bow ? item.IsAmmunition : !item.IsAmmunition && item.WeaponType != WeaponType.BowAndArrow))
                .OrderByDescending(item => bow ? item.ArrowDamageMultiplier * item.ArrowTargets : Score(item, hero))
                .ThenBy(item => item.ItemName, StringComparer.Ordinal).FirstOrDefault();
            if (offhand != null) loadout.Add(offhand);
        }
        var accessory = allowed.Where(item => item.EquipmentSlot == EquipmentSlot.Accessory)
            .OrderByDescending(item => Score(item, hero)).ThenBy(item => item.ItemName, StringComparer.Ordinal).FirstOrDefault();
        if (accessory != null) loadout.Add(accessory);
        data.Equipment = ItemSaveData.Capture(loadout.Select(item => item.AsInventoryItem(item.StackMax > 0 ? item.StackMax : null)));
        return data;
    }

    private static float Score(EquipmentItemDefinition item, TownAlly hero)
    {
        var stats = item.StatModification ?? new StatModification();
        bool magic = hero.PrimaryClass.PreferredAttribute == HeroAttribute.Int;
        return stats.Strength * (magic ? 1 : 3) + stats.MagicPower * (magic ? 3 : 1) +
            stats.Defense * 2 + (stats.HPMax + stats.SPMax) * .1f;
    }
}
