#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DioramaGameplayTests
    {
        GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup(){harness=new GameTestHarness();yield return null;}
        [UnityTearDown] public IEnumerator Cleanup(){yield return harness.Cleanup();}
        static Vector3[] Points(GameObject obj)=>(Vector3[])System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(a=>a.GetType("DungeonPickupAuthoring")).First(t=>t!=null).GetMethod("Points").Invoke(null,new object[]{obj});

        [UnityTest]
        public IEnumerator CurrencyUsesProductionPlacementAndAwardsItsSeededAmount()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var dungeon=harness.Game.CurrentDungeon;int before=harness.Game.PlayerController.Gold;
            dungeon.SetGoldAmount(harness.Ally.TilemapPosition,37);
            var gold=dungeon.Interactables.OfType<Gold>().Last();
            gold.GetInteractionSideEffects(harness.Ally);
            Assert.That(harness.Game.PlayerController.Gold,Is.EqualTo(before+37));
            Assert.That(dungeon.Interactables.Contains(gold),Is.False);
        }

        [UnityTest]
        public IEnumerator IndividualAndFallbackItemsDropAndThrowThroughProductionPaths()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var dungeon=harness.Game.CurrentDungeon;
            var source=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset");
            foreach(bool useOverride in new[]{true,false})
            {
                var definition=Object.Instantiate(source);definition.ItemName="Diorama drop "+useOverride;
                if(!useOverride)definition.DroppedItemPrefab=null;
                try
                {
                    var expected=definition.ResolveDroppedPrefab(dungeon.DroppedItemPrefabs);
                    var item=definition.AsInventoryItem(1);
                    Assert.That(DungeonPlacement.TryDrop(dungeon,harness.Ally.TilemapPosition,item,out var dropped),Is.True);
                    Assert.That(dropped.InventoryItem,Is.SameAs(item));
                    var bounds=dropped.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                    Assert.That(bounds.center.x,Is.EqualTo(dungeon.CellToWorld(dropped.Position).x+1).Within(.003f));
                    Assert.That(bounds.center.y,Is.EqualTo(dungeon.CellToWorld(dropped.Position).y+1).Within(.003f));
                    CollectionAssert.AreEqual(expected.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh),dropped.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh));
                    dungeon.RemoveInteractable(dropped);Object.Destroy(dropped.gameObject);
                    var bag=harness.Game.PlayerController.Inventory;bag.Add(item);
                    var dialog=MenuManager.Instance.ActionDialog;
                    dialog.Setup(null,item,harness.Ally);dialog.Throw_Clicked();
                    yield return null;yield return harness.WaitForIdle();
                    Assert.That(bag.InventoryItems.Contains(item),Is.False,"Throw must consume the bag item through the real action.");
                    var landed=dungeon.Interactables.OfType<DroppedItem>().FirstOrDefault(d=>d.InventoryItem.ItemDefinition==definition);
                    Assert.That(landed,Is.Not.Null,"The unobstructed test throw must land with its item definition.");
                    CollectionAssert.AreEqual(expected.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh),landed.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh));
                    dungeon.RemoveInteractable(landed);Object.Destroy(landed.gameObject);
                }
                finally{Object.Destroy(definition);}
            }
        }

        [UnityTest]
        public IEnumerator FloorItemRatiosUseTheLiveDungeonHero()
        {
            yield return harness.LoadDungeon(new TestScenario());
            yield return null;
            var table=Resources.Load<DungeonPickupPresentation>("DungeonThemes/PickupPresentation");
            var authoring=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("DungeonPickupAuthoring")).First(t=>t!=null);
            Vector2[] Project(Vector3[] points)=>(Vector2[])authoring.GetMethod("Project").Invoke(null,new object[]{Camera.main,points});
            var hero=Points(harness.Ally.gameObject);var heroScreen=Project(hero);
            float screenHeight=heroScreen.Max(p=>p.y)-heroScreen.Min(p=>p.y);
            float height=hero.Max(p=>p.z)-hero.Min(p=>p.z);
            Assert.That(height,Is.EqualTo(table.HeroHeight).Within(table.HeroHeight*.06f),"Authoring must retain the production rig scale.");
            var report=new System.Text.StringBuilder("name,live_hero_ratio,world_height_ratio\n");
            foreach(var entry in table.Items)
            {
                var obj=Object.Instantiate(entry.Prefab);
                try
                {
                    obj.transform.localScale=Vector3.Scale(obj.transform.localScale,entry.ParentScale);
                    var points=Points(obj);var projected=Project(points);
                    float diameter=(float)authoring.GetMethod("Diameter").Invoke(null,new object[]{projected});
                    float ratio=diameter/screenHeight,vertical=(points.Max(p=>p.z)-points.Min(p=>p.z))/height;
                    if(entry.Size==DungeonPickupSize.Chest)Assert.That(vertical,Is.EqualTo(.5f).Within(.03f),entry.Name);
                    else Assert.That(ratio,Is.InRange(entry.Size==DungeonPickupSize.Elongated?.5f:1f/3,entry.Size==DungeonPickupSize.Elongated?2f/3:.5f),entry.Name);
                    report.AppendLine($"\"{entry.Name}\",{ratio:F4},{vertical:F4}");
                }
                finally {Object.Destroy(obj);}
            }
            System.IO.Directory.CreateDirectory("Docs/Art/Verification/DungeonSmartLayers");
            System.IO.File.WriteAllText("Docs/Art/Verification/DungeonSmartLayers/LivePickupRatios.csv",report.ToString());
        }

        [UnityTest]
        public IEnumerator KeyAndMimicUseAuthoredFloorPoses()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var dungeon=harness.Game.CurrentDungeon;
            var free=Enumerable.Range(0,dungeon.dungeonWidth*dungeon.dungeonHeight).Select(i=>new Vector3Int(i%dungeon.dungeonWidth,i/dungeon.dungeonWidth))
                .Where(p=>dungeon.IsWalkable(p)&&!harness.Game.AllCharacters.Any(c=>c.ToBounds().Contains(p)))
                .OrderBy(p=>(p-harness.Ally.TilemapPosition).sqrMagnitude).Take(2).ToArray();
            var key=SmallKey.Create(dungeon,free[0]);
            var keyModel=key.transform.GetChild(0);Assert.That(keyModel.localRotation,Is.EqualTo(dungeon.SmallKeyPrefab.transform.localRotation));
            Assert.That(key.GetComponentsInChildren<Collider>(),Is.Empty);
            yield return harness.SpawnEnemy("Enemy_ChestMonster",free[1]);
            var mimic=harness.Game.Enemies.Last();Assert.That(mimic.GetComponent<EnemyBehavior>().Disguised,Is.True);
            var chest=mimic.GetComponentsInChildren<Transform>().Single(t=>t.name=="Treasure chest disguise");
            Assert.That(chest.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m.shader.name=="Standard")),Is.True);
            Assert.That(chest.GetComponentInChildren<DungeonPickupFootprint>().GroundZ,Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.001f));
            var chestBounds=chest.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            var chestPoints=Points(chest.gameObject);
            Assert.That(chestPoints.Max(p=>p.z)-chestPoints.Min(p=>p.z),Is.EqualTo(Resources.Load<DungeonPickupPresentation>("DungeonThemes/PickupPresentation").HeroHeight*.5f).Within(.001f));
            Assert.That(Mathf.Max(chestBounds.size.x,chestBounds.size.y),Is.LessThanOrEqualTo(1.641f));
            var camera=Camera.main;var originalPosition=camera.transform.position;float originalSize=camera.orthographicSize;
            try
            {
                var middle=(key.transform.position+mimic.transform.position+harness.Ally.transform.position)/3+Vector3.back*.4f;
                camera.transform.position=middle-camera.transform.forward*20;camera.orthographicSize=3;
                var target=RenderTexture.GetTemporary(1280,800,24);var oldTarget=camera.targetTexture;var previous=RenderTexture.active;
                var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                    System.IO.Directory.CreateDirectory("Docs/Art/Previews/Diorama/Items");
                    System.IO.File.WriteAllBytes("Docs/Art/Previews/Diorama/Items/Key_Mimic_Gameplay.png",image.EncodeToPNG());
                }
                finally {camera.targetTexture=oldTarget;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);}
            }
            finally {camera.transform.position=originalPosition;camera.orthographicSize=originalSize;}
        }
        [UnityTest]
        public IEnumerator AuthoredPortraitFrameReservesTextSpaceAndClearsWhenClosed()
        {
            yield return harness.LoadCommon();
            var dialog=Common.Instance.MessageDialog;
            var portrait=AssetDatabase.FindAssets("t:TownNpcDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TownNpcDefinition>).First(n=>n.Portrait!=null).Portrait;
            dialog.gameObject.SetActive(true);dialog.SetPortrait(portrait);
            Assert.That(dialog.PortraitFrame.activeSelf,Is.True);Assert.That(dialog.Portrait.sprite,Is.SameAs(portrait));
            Assert.That(dialog.PromptText.rectTransform.anchorMin.x,Is.GreaterThan(.26f));
            dialog.PromptText.text="Welcome, traveler. The town's shops are open, and a warm bed is waiting at the inn.";
            yield return DioramaUICapture.Save(dialog.GetComponentInParent<Canvas>(),"npc-portrait");
            dialog.gameObject.SetActive(false);yield return null;
            Assert.That(dialog.PortraitFrame.activeSelf,Is.False);Assert.That(dialog.Portrait.sprite,Is.Null);
            Assert.That(dialog.PromptText.rectTransform.anchorMin.x,Is.LessThan(.1f));
        }
        [UnityTest]
        public IEnumerator ResultIconsReadInVictoryAndDefeatDialogs()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var result=Object.FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
            result.gameObject.SetActive(true);
            foreach(bool victory in new[]{true,false})
            {
                result.Setup(harness.Game.PlayerController,victory);
                Assert.That(result.ResultIcon.sprite,Is.SameAs(victory?result.VictoryIcon:result.DefeatIcon));
                Assert.That(result.ResultIcon.enabled,Is.True);
                yield return DioramaUICapture.Save(result.GetComponent<Canvas>(),victory?"victory":"defeat");
            }
            result.gameObject.SetActive(false);
        }
    }
}
#endif
