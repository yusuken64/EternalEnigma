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
        private const string WeaponDirectory = "Assets/Prefabs/Dungeon/Items/Weapons";
        private static readonly (int Min, int Max)[] TierFloors =
            { (1, 10), (1, 10), (6, 20), (15, 30), (25, 40) };
        private static EquipmentItemDefinition[] AllWeaponAssets => AssetDatabase.FindAssets("t:EquipmentItemDefinition", new[] { WeaponDirectory })
            .Select(g => AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null).ToArray();
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
            Assert.That(AllWeaponAssets.Where(w => !Weapons.Contains(w)).Select(w => w.name),
                Is.EquivalentTo(new[] { "RightHand_Arrows" }), "Only the legacy arrow definition is excluded.");
            foreach (var weapon in Weapons)
            {
                var path = AssetDatabase.GetAssetPath(weapon);
                var guid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(offered.Contains(weapon) || loot.Contains(guid) || starting.Contains(guid), Is.True, path);
                Assert.That(weapon.Description, Is.Not.Empty, path);
                Assert.That(weapon.WeaponModelName, Is.Not.Empty, path);
                Assert.That(weapon.ItemName, Is.Not.Empty, path);
            }
            Assert.That(Weapons.Length, Is.EqualTo(76));
            var names = Weapons.Concat(shop.ShopCatalog.Select(o => o.Item).OfType<EquipmentItemDefinition>())
                .Distinct().GroupBy(w => w.ItemName).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.That(names, Is.Empty, "Saved equipment resolves by ItemName.");
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
            Assert.That(arrows.Select(o => o.Item.ItemName).Distinct().Count(), Is.EqualTo(5));
            Assert.That(shop.ShopCatalog.Where(o => o.Item is EquipmentItemDefinition { IsAmmunition: false })
                .Select(o => o.Item), Is.EquivalentTo(Weapons));
            foreach (var offer in shop.ShopCatalog.Where(o => o.Item is EquipmentItemDefinition { IsAmmunition: false }))
            {
                var item = offer.Item;
                Assert.That(offer.MinimumTier, Is.InRange(0, 4), item.name);
                Assert.That((item.MinFloor, item.MaxFloor), Is.EqualTo(TierFloors[offer.MinimumTier]), item.name);
            }
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
                var shop = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
                foreach (var arrows in shop.ShopCatalog.Select(o => o.Item).OfType<EquipmentItemDefinition>()
                    .Where(i => i.IsAmmunition))
                    Assert.That(manager.GetAsInventoryItemByName(arrows.ItemName).ItemDefinition,
                        Is.SameAs(arrows), arrows.ItemName);
                foreach (var weapon in Weapons)
                    Assert.That(manager.DungeonLoot, Does.Contain(weapon), weapon.name);
                for (int floor = 1; floor <= 40; floor++)
                {
                    var eligible = manager.DungeonLoot.Where(i => i.MinFloor <= floor && floor <= i.MaxFloor).ToArray();
                    Assert.That(eligible, Is.Not.Empty, $"floor {floor}");
                    var equipment = eligible.OfType<EquipmentItemDefinition>().ToArray();
                    var food = eligible.Where(i => i is not EquipmentItemDefinition).ToArray();
                    Assert.That(equipment, Is.Not.Empty, $"floor {floor} equipment");
                    Assert.That(food, Is.Not.Empty, $"floor {floor} consumables");
                    foreach (var item in equipment.Select((value, index) => (value, index)))
                        Assert.That(manager.GetRandomDrop(item.index * 100 + 28, floor), Is.SameAs(item.value));
                    foreach (var item in food.Select((value, index) => (value, index)))
                        Assert.That(manager.GetRandomDrop(item.index * 100, floor), Is.SameAs(item.value));
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
