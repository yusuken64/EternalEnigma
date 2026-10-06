using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public class EconomyCatalogTests
    {
        private TownBuildingDefinition Bakery => Resources.Load<TownBuildingDefinition>("Towns/Buildings/Bakery");
        [Test]
        public void AmmunitionPacksHaveExactTiersQuantitiesAndSaveStocks()
        {
            var shop=Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
            var offers=shop.ShopCatalog.Where(o=>o.Item is EquipmentItemDefinition { IsAmmunition:true }).OrderBy(o=>o.MinimumTier).ToArray();
            Assert.That(offers.Length,Is.EqualTo(5));
            var go=new GameObject("Ammo save test");
            try
            {
                var manager=go.AddComponent<ItemManager>(); manager.ItemDefinitions=offers.Select(o=>o.Item).ToList(); manager.StartingItems=new();
                for(int i=0;i<3;i++)
                {
                    var offer=offers[i]; var def=(EquipmentItemDefinition)offer.Item;
                    Assert.That(offer.MinimumTier,Is.EqualTo(i)); Assert.That(offer.Price,Is.EqualTo(new[]{100,200,400}[i]));
                    Assert.That(offer.Quantity,Is.EqualTo(4)); Assert.That(offer.StackCount,Is.EqualTo(20));
                    Assert.That(def.StackMax,Is.EqualTo(20)); Assert.That(def.ArrowTargets,Is.EqualTo(i+1));
                    Assert.That(def.ArrowDamageMultiplier,Is.EqualTo(1+.25f*i)); Assert.That(def.ShopOnly,Is.EqualTo(i>0));
                    Assert.That(ItemSaveData.From(def.AsInventoryItem(7)).Restore(manager).StackStock,Is.EqualTo(7));
                }
                var legacy=new ItemSaveData {ItemName="Arrows"};
                Assert.That(legacy.Restore(manager).ItemName,Is.EqualTo("Wooden Arrows"));
                Assert.That(legacy.Restore(manager).StackStock,Is.EqualTo(20));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void BakeryTiersHaveCumulativeProductsAndExactRecovery(int tier)
        {
            var offers = TownShopCatalog.Resolve(Bakery, tier);
            Assert.That(offers.Count, Is.EqualTo(3 * (tier + 1)));
            Assert.That(offers.Select(o => o.Item.ItemName).Distinct().Count(), Is.EqualTo(offers.Count));
            foreach (var offer in offers)
            {
                var effect = (ModifyStatsItemEffectDefinition)offer.Item.ItemEffectDefinition;
                int t = offer.MinimumTier;
                bool food = offer.Item.IsFood;
                Assert.That(offer.Quantity, Is.EqualTo(food ? 6 : 3));
                Assert.That(effect.VitalModification.Hunger, Is.EqualTo(food ? 50 + 25*t : 0));
                Assert.That(effect.VitalModification.Sp, Is.EqualTo(food ? 0 : 5*(t+1)));
                Assert.That(effect.VitalModification.Hp, Is.EqualTo(offer.Item.ItemName.Contains("Latte") ? 20*(t+1) : 0));
                Assert.That(offer.Price, Is.EqualTo(food ? 100+50*t : (offer.Item.ItemName.Contains("Latte") ? 250 : 150)*(t+1)));
                Assert.That((int)offer.Item.DroppedItemVisual, Is.EqualTo(food ? 1 : 4));
                Assert.That(offer.Item.ShopOnly, Is.EqualTo(offer.Item.ItemName != "Bread"));
                Assert.That(AssetDatabase.Contains(offer.Item), Is.True);
            }
        }

        [Test]
        public void RegistrationRestoresSpecialtiesWithoutMakingThemLoot()
        {
            var obj = new GameObject();
            try
            {
                var manager = obj.AddComponent<ItemManager>();
                manager.ItemDefinitions = Bakery.ShopCatalog.Select(o => o.Item).ToList();
                manager.StartingItems = new();
                Assert.That(manager.DungeonLoot.Select(i => i.ItemName), Is.EqualTo(new[] { "Bread" }));
                for (int i = -100; i < 100; i++) Assert.That(manager.GetRandomDrop(i).ItemName, Is.EqualTo("Bread"));
                Assert.That(manager.GetRandomDrop((Character)null).ItemName, Is.EqualTo("Bread"));
                manager.ItemDefinitions.Clear();
                foreach (var offer in Bakery.ShopCatalog)
                {
                    var saved = JsonUtility.FromJson<ItemSaveData>(JsonUtility.ToJson(ItemSaveData.From(offer.Item.AsInventoryItem(null))));
                    Assert.That(saved.Restore(manager).ItemDefinition, Is.SameAs(offer.Item));
                }
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void EquipmentAndPotionProgressionRetainsOriginalOffers()
        {
            var shop = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
            Assert.That(TownShopCatalog.Resolve(shop, 0).Single(o => o.Item.ItemName == "Potion").Price, Is.EqualTo(125));
            foreach (var offer in shop.ShopCatalog.Where(o => o.MinimumTier > 0))
            {
                if (offer.Item is EquipmentItemDefinition { IsAmmunition: true }) { Assert.That(offer.Quantity, Is.EqualTo(4)); Assert.That(offer.StackCount, Is.EqualTo(20)); }
                else if (offer.Item is EquipmentItemDefinition equipment)
                {
                    Assert.That(Mathf.Max(equipment.StatModification.Strength, equipment.StatModification.Defense), Is.LessThanOrEqualTo(new[] { 0,6,9,12,20 }[offer.MinimumTier]));
                    Assert.That(offer.Quantity, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(((ModifyStatsItemEffectDefinition)offer.Item.ItemEffectDefinition).VitalModification.Hp, Is.EqualTo(20*(offer.MinimumTier+1)));
                    Assert.That(offer.Price, Is.EqualTo(125*(offer.MinimumTier+1)));
                    Assert.That(offer.Quantity, Is.EqualTo(4));
                }
            }
            Assert.That(shop.ShopCatalog.Select(o => o.Item.ItemName).Distinct().Count(), Is.EqualTo(shop.ShopCatalog.Count));
        }

        [Test]
        public void SalePricingUsesUnitPricesAndPreservesMaterials()
        {
            var bread = Bakery.ShopCatalog[0].Item.AsInventoryItem(null);
            Assert.That(TownServices.SellPrice(bread), Is.EqualTo(25));
            var definition = ScriptableObject.CreateInstance<UsableItemDefinition>();
            var material = ScriptableObject.CreateInstance<MaterialItemDefinition>();
            var equipment = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            try
            {
                definition.StackMax = 10;
                var item = definition.AsInventoryItem(3);
                Assert.That(TownServices.SellPrice(item), Is.EqualTo(75));
                item.StackStock = 0; Assert.That(TownServices.SellPrice(item), Is.Zero);
                item.StackStock = 3; definition.IsProgressionItem = true;
                Assert.That(TownServices.SellPrice(item), Is.Zero);
                material.StackMax = 10; material.SellValue = 7;
                Assert.That(TownServices.SellPrice(material.AsInventoryItem(3)), Is.EqualTo(21));
                equipment.StatModification = new StatModification { Strength = 3, Defense = 2 };
                Assert.That(TownServices.SellPrice(equipment.AsInventoryItem(null)), Is.EqualTo(62));
            }
            finally { Object.DestroyImmediate(definition); Object.DestroyImmediate(material); Object.DestroyImmediate(equipment); }
        }

        [Test]
        public void OldSavesAndSavedZeroHungerAreDistinct()
        {
            Assert.That(JsonUtility.FromJson<TownAllyData>("{\"Hp\":1}").HasHunger, Is.False);
            var ally = JsonUtility.FromJson<TownAllyData>(JsonUtility.ToJson(new TownAllyData { HasHunger = true, Hunger = 0, HungerAccumulate = 7 }));
            Assert.That(ally.HasHunger, Is.True); Assert.That(ally.Hunger, Is.Zero); Assert.That(ally.HungerAccumulate, Is.EqualTo(7));
        }

        [Test]
        public void PartialStackUsesLowestCatalogUnitPriceAndRoundsDown()
        {
            var definition = ScriptableObject.CreateInstance<UsableItemDefinition>();
            definition.StackMax = 10;
            var cheap = new TownShopOffer { Item = definition, Price = 205, StackCount = 10 };
            var expensive = new TownShopOffer { Item = definition, Price = 100, StackCount = 2 };
            var shop = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
            try
            {
                Bakery.ShopCatalog.Add(cheap); shop.ShopCatalog.Add(expensive);
                Assert.That(TownServices.SellPrice(definition.AsInventoryItem(3)), Is.EqualTo(15));
            }
            finally { Bakery.ShopCatalog.Remove(cheap); shop.ShopCatalog.Remove(expensive); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void OnlyClearsAndRetreatsAdvanceTheReturnCycle()
        {
            var configuration = ScriptableObject.CreateInstance<TownConfiguration>();
            try
            {
                var save = new GameSaveData();
                DungeonReturnService.Commit(save, configuration, false, 100, new InventoryItem[0], new Ally[0]);
                Assert.That(save.TownSaveData.RestockCycle, Is.Zero);
                Assert.That(save.DungeonSaveData.ReturnCommitted, Is.True);
                save.DungeonSaveData.ReturnCommitted = false;
                DungeonReturnService.Commit(save, configuration, true, 100, new InventoryItem[0], new Ally[0]);
                DungeonReturnService.Commit(save, configuration, true, 100, new InventoryItem[0], new Ally[0]);
                Assert.That(save.TownSaveData.RestockCycle, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(configuration); }
        }
    }
}
