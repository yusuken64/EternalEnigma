using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public class WeaponCatalogTests
    {
        private static EquipmentItemDefinition[] Weapons => AssetDatabase.FindAssets("t:EquipmentItemDefinition", new[] { "Assets/Prefabs/Dungeon/Items/Weapons" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null && i.name != "RightHand_Arrows").ToArray();

        [Test]
        public void EveryWeaponCanBeBoughtOrFoundAndHasDescription()
        {
            var shop = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
            var offered = shop.ShopCatalog.Select(o => o.Item).ToHashSet();
            var scene = File.ReadAllText("Assets/Scenes/Common.unity");
            var loot = Regex.Match(scene, @"  ItemDefinitions:\s*(.*?)  StartingItems:", RegexOptions.Singleline).Groups[1].Value;
            var starting = scene.Substring(scene.IndexOf("  StartingItems:", StringComparison.Ordinal));
            foreach (var weapon in Weapons)
            {
                var path = AssetDatabase.GetAssetPath(weapon);
                var guid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(offered.Contains(weapon) || loot.Contains(guid) || starting.Contains(guid), Is.True, path);
                Assert.That(weapon.Description, Is.Not.Empty, path);
                Assert.That(weapon.WeaponModelName, Is.Not.Empty, path);
            }
            Assert.That(Weapons.Length, Is.EqualTo(76));
        }

        [Test]
        public void GreatswordsBowsAndAmmunitionProgressThroughShopTiers()
        {
            var shop = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
            for (int n = 1; n <= 7; n++)
            {
                var sword = Weapons.Single(w => w.name == $"RightHand_THS{n:00}_Sword");
                Assert.That(sword.StatModification.Strength, Is.EqualTo(6 + n * 2));
                Assert.That(shop.ShopCatalog.Single(o => o.Item == sword).MinimumTier,
                    Is.EqualTo(n == 1 ? 2 : n <= 3 ? 3 : 4));
            }
            var offhand = Weapons.Single(w => w.name == "LeftHand_OHS07_Sword");
            Assert.That((offhand.StatModification.Strength, offhand.StatModification.Defense), Is.EqualTo((7, 0)));
            var bows = Weapons.Where(w => w.WeaponModelName == "Bows").OrderBy(w => w.StatModification.Strength).ToArray();
            Assert.That(bows.Select(w => w.StatModification.Strength), Is.EqualTo(new[] { 3, 5, 7, 9, 11 }));
            Assert.That(bows.Select(w => w.WeaponModelVariant), Is.EqualTo(new[] { "Bow01", "Bow02", "Bow03", "Bow04", "Bow05" }));
            var arrows = shop.ShopCatalog.Where(o => o.Item is EquipmentItemDefinition { IsAmmunition: true })
                .OrderBy(o => o.MinimumTier).ToArray();
            Assert.That(arrows.Select(o => o.MinimumTier), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(arrows.Select(o => ((EquipmentItemDefinition)o.Item).WeaponModelVariant),
                Is.EqualTo(new[] { "Arrow01", "Arrow02", "Arrow03", "Arrow04", "Arrow05" }));
            foreach (var offer in shop.ShopCatalog.Where(o => o.Item is EquipmentItemDefinition { IsAmmunition: false }))
            {
                var item = (EquipmentItemDefinition)offer.Item;
                int stat = Math.Max(item.StatModification.Strength, item.StatModification.Defense);
                Assert.That(stat, Is.LessThanOrEqualTo(new[] { 0, 6, 9, 12, 20 }[offer.MinimumTier]), item.name);
            }
        }

        [Test]
        public void FloorDropsAreEligibleRepeatableAndConsumablesKeepTheirShare()
        {
            var go = new GameObject("Weapon loot audit");
            try
            {
                var manager = go.AddComponent<ItemManager>();
                var scene = File.ReadAllText("Assets/Scenes/Common.unity");
                var section = Regex.Match(scene, @"  ItemDefinitions:\s*(.*?)  StartingItems:", RegexOptions.Singleline).Groups[1].Value;
                manager.ItemDefinitions = Regex.Matches(section, @"guid: ([0-9a-f]+)").Cast<Match>()
                    .Select(m => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value)))
                    .Where(i => i != null).ToList();
                manager.StartingItems = new();
                for (int floor = 1; floor <= 40; floor++)
                {
                    var eligible = manager.DungeonLoot.Where(i => i.MinFloor <= floor && floor <= i.MaxFloor).ToArray();
                    Assert.That(eligible, Is.Not.Empty, $"floor {floor}");
                    int consumables = 0;
                    for (int roll = 0; roll < 1000; roll++)
                    {
                        var item = manager.GetRandomDrop(roll, floor);
                        Assert.That(eligible, Does.Contain(item), $"floor {floor}, roll {roll}");
                        Assert.That(manager.GetRandomDrop(roll, floor), Is.SameAs(item));
                        if (item is not EquipmentItemDefinition) consumables++;
                    }
                    Assert.That(consumables, Is.InRange(270, 290), $"floor {floor}");
                }
                Assert.That(manager.GetRandomDrop(123), Is.SameAs(manager.GetRandomDrop(123, 1)));
                Assert.That(manager.GetRandomDrop((Character)null), Is.Not.Null);
                manager.ItemDefinitions.Clear();
                Assert.That(Assert.Throws<InvalidOperationException>(() => manager.GetRandomDrop(7, 40)).Message,
                    Does.Contain("floor 40"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
