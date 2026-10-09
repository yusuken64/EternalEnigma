using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class DioramaItemTests
    {
        [Test] public void ProductionDungeonKeepsCurrencyAndAllCategoryBindings()
        {
            var dungeon=AssetDatabase.LoadAssetAtPath<TileWorldDungeon>("Assets/Prefabs/Dungeon/TileWorldDungeon.prefab");
            Assert.That(dungeon.GoldPrefab,Is.Not.Null,"Currency must retain its Gold interaction behaviour.");
            Assert.That(dungeon.GoldPrefab.GetComponent<DroppedItem>(),Is.Null);
            Assert.That(dungeon.SmallKeyPrefab,Is.Not.Null);
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(DroppedItemVisual)),dungeon.DroppedItemPrefabs.Select(p=>p.DroppedItemVisual));
        }
        [Test] public void AllItemsResolveLitColliderFreeVisualsAndIcons()
        {
            var categories=Enum.GetValues(typeof(DroppedItemVisual)).Cast<DroppedItemVisual>().Select(v=>AssetDatabase.LoadAssetAtPath<DroppedItem>("Assets/Prefabs/Dungeon/DroppedItems/"+v+".prefab")).ToArray();
            var items=AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<ItemDefinition>().Concat(MaterialCatalog.All);
            foreach(var item in items)
            {
                var prefab=item.ResolveDroppedPrefab(categories);Assert.That(prefab,Is.Not.Null,item.ItemName);Assert.That(item.ResolveIcon(),Is.Not.Null,item.ItemName);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty,item.ItemName);
                foreach(var material in prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials))
                {Assert.That(material.shader.name,Is.EqualTo("Standard"),item.ItemName);Assert.That(material.mainTexture,Is.Not.Null,item.ItemName);}
                var instance=Object.Instantiate(prefab.gameObject);
                try
                {
                    var rs=instance.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                    Assert.That(b.size.z,Is.InRange(.02f,Resources.Load<DungeonPickupPresentation>("DungeonThemes/PickupPresentation").HeroHeight),item.ItemName);
                    Assert.That(Mathf.Max(b.size.x,b.size.y,b.size.z),Is.GreaterThan(.35f),item.ItemName);
                    Assert.That(Mathf.Max(b.size.x,b.size.y),Is.LessThanOrEqualTo(1.641f),item.ItemName);
                    var footprint=instance.GetComponentInChildren<DungeonPickupFootprint>();
                    Assert.That(footprint,Is.Not.Null,item.ItemName);
                    Assert.That(footprint.GroundZ,Is.EqualTo(-.025f).Within(.003f),item.ItemName);
                    Assert.That(b.center.x,Is.EqualTo(1).Within(.003f),item.ItemName+" cell center");
                    Assert.That(b.center.y,Is.EqualTo(1).Within(.003f),item.ItemName+" cell center");
                }
                finally {Object.DestroyImmediate(instance);}
            }
        }
        [Test] public void ShopItemsHaveDistinctModelAndTintCombinations()
        {
            var catalog=DioramaItemCatalog.Load();
            foreach(var shop in Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings"))
            {
                var items=shop.ShopCatalog.Where(o=>o.Item!=null).Select(o=>o.Item).Distinct().ToArray();
                var keys=items.Select(i=>catalog.Items.Single(e=>e.Name==i.ItemName)).Select(e=>e.Source+"/"+ColorUtility.ToHtmlStringRGB(e.Tint)).ToArray();
                Assert.That(keys.Distinct().Count(),Is.EqualTo(keys.Length),shop.Id);
            }
        }
        [Test] public void AnUnsetOverrideKeepsCategoryFallbackAndAnOverrideWorksWithoutCategories()
        {
            var item=ScriptableObject.CreateInstance<UsableItemDefinition>();var profile=GamePresentationProfile.Current;
            var fallback=AssetDatabase.LoadAssetAtPath<DroppedItem>("Assets/Prefabs/Dungeon/DroppedItems/Bread.prefab");
            try
            {
                item.DroppedItemVisual=DroppedItemVisual.Bread;
                Assert.That(item.ResolveDroppedPrefab(new[]{fallback}),Is.SameAs(fallback));
                Assert.That(item.ResolveIcon(profile),Is.SameAs(profile.ItemIcons[(int)DroppedItemVisual.Bread]));
                item.DroppedItemPrefab=fallback;item.Icon=profile.BagIcon;
                Assert.That(item.ResolveDroppedPrefab(Array.Empty<DroppedItem>()),Is.SameAs(fallback));Assert.That(item.ResolveIcon(profile),Is.SameAs(profile.BagIcon));
            }
            finally {Object.DestroyImmediate(item);}
        }
    }
}
