#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class CampaignSleepTests
    {
        GameTestHarness harness;
        DungeonAnimationMode? animation;
        [UnitySetUp] public IEnumerator Setup() { harness=new GameTestHarness{TimeoutSeconds=120};animation=DungeonPreferences.AnimationOverride;yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() { DungeonPreferences.AnimationOverride=animation;yield return harness.Cleanup(); }
        [UnityTest] public IEnumerator HomeSleepSavesOnceRestoresAwakePositionAndRollsBackProgression()
        {
            yield return harness.LoadMainMenu(null);
            var common=Common.Instance;
            common.GameSaveData=Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);common.Travel.NewCampaign(42);
            yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<Town>()?.IsReady==true,"home");
            var town=Object.FindFirstObjectByType<Town>();var bed=Object.FindFirstObjectByType<HomeBed>();var hero=town.TownPlayer.ControllingTownAlly;
            System.IO.Directory.CreateDirectory("Temp/CampaignSaves");
            yield return harness.WaitUntil(()=>!common.ScreenTransition.BlockScreen.activeSelf,"home reveal");
            ScreenCapture.CaptureScreenshot("Temp/CampaignSaves/home.png"); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(bed,Is.Not.Null);Assert.That(town.TownBuildings.Count(b=>b.Definition.Id=="inn"),Is.EqualTo(1));
            Assert.That(hero.TilemapPosition,Is.EqualTo(bed.Tile-Vector3Int.up));Assert.That(common.CampaignContext.IsGridGenerated,Is.False);
            Assert.That(SaveSystem.LoadData().SavePointId,Is.EqualTo("town-0/home"));
            hero.Hp=1;hero.Sp=0;hero.Level=4;hero.Experience=77;town.TownPlayer.Gold=321;hero.SetFacing(Facing.Left);
            var position=hero.TilemapPosition;var world=hero.transform.position;int writes=harness.Store.Writes;
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.Normal;
            bed.SleepAndSave();bed.SleepAndSave();Assert.That(town.TownPlayer.CutsceneLocked,Is.True);
            yield return harness.WaitUntil(()=>!bed.IsSleeping,"sleep and wake");
            Assert.That(harness.Store.Writes,Is.EqualTo(writes+1));Assert.That(hero.transform.position,Is.EqualTo(world));Assert.That(hero.CurrentFacing,Is.EqualTo(Facing.Left));
            Assert.That(town.TownPlayer.CutsceneLocked,Is.False);Assert.That(hero.Hp,Is.EqualTo(-1));
            var saved=SaveSystem.LoadData();Assert.That(saved.ArrivalX,Is.EqualTo(position.x));Assert.That(saved.ArrivalY,Is.EqualTo(position.y));
            Assert.That(saved.Summary.Party[0].Level,Is.EqualTo(4));Assert.That(saved.Summary.Party[0].Hp,Is.EqualTo(saved.Summary.Party[0].MaxHp));
            common.MessageDialog.Ok_Clicked();hero.Level=9;hero.Experience=600;town.TownPlayer.Gold=1;town.SaveProgress();
            Assert.That(SaveSystem.LoadData().TownSaveData.Gold,Is.EqualTo(321));
            Assert.That(InnCheckpoint.TryRestore(common),Is.True);
            yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<Town>()!=town && Object.FindFirstObjectByType<Town>()?.IsReady==true,"awake load");
            town=Object.FindFirstObjectByType<Town>();hero=town.TownPlayer.ControllingTownAlly;
            Assert.That(hero.Level,Is.EqualTo(4));Assert.That(hero.Experience,Is.EqualTo(77));Assert.That(hero.TilemapPosition,Is.EqualTo(position));
            Assert.That(Object.FindFirstObjectByType<HomeBed>().IsSleeping,Is.False);
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.NoAnimations;writes=harness.Store.Writes;
            Object.FindFirstObjectByType<HomeBed>().SleepAndSave();
            Object.FindFirstObjectByType<HomeBed>().SleepAndSave();
            Assert.That(harness.Store.Writes,Is.EqualTo(writes+1));Assert.That(town.TownPlayer.CutsceneLocked,Is.False);
            common.MessageDialog.Ok_Clicked();yield return null;
            string previous=harness.Store.Json;harness.Store.FailWrites=true;
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.Normal;
            bed=Object.FindFirstObjectByType<HomeBed>();bed.SleepAndSave();
            yield return harness.WaitUntil(()=>!bed.IsSleeping,"failed save wake");
            Assert.That(harness.Store.Json,Is.EqualTo(previous));Assert.That(town.TownPlayer.CutsceneLocked,Is.False);
            Assert.That(common.MessageDialog.PromptText.text,Does.Contain("Saving failed"));harness.Store.FailWrites=false;
            common.MessageDialog.Ok_Clicked();
            Assert.That(common.Travel.EnterTownDungeon(town,"story-0"),Is.True);yield return harness.WaitForIdle();
            var dungeonHero=Game.Instance.Allies[0];Assert.That(dungeonHero.Vitals.Level,Is.EqualTo(4));Assert.That(dungeonHero.Vitals.Exp,Is.EqualTo(77));
            int strength=dungeonHero.BaseStats.Strength;
            GameOverScreen.Retreat(Game.Instance.PlayerController);
            yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<Town>()?.IsReady==true,"retreat");
            town=Object.FindFirstObjectByType<Town>();Assert.That(common.Travel.EnterTownDungeon(town,"story-0"),Is.True);yield return harness.WaitForIdle();
            Assert.That(Game.Instance.Allies[0].BaseStats.Strength,Is.EqualTo(strength),"Repeated entry must not duplicate growth.");

        }

        [UnityTest] public IEnumerator ThreeSeedsHaveAccessibleHomesAndSlotBrowserPreservesCanceledReplacement()
        {
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.NoAnimations;
            yield return harness.LoadMainMenu(null);
            var common=Common.Instance;
            for(int slot=0;slot<3;slot++)
            {
                SaveSystem.ActiveSlot=slot;
                var menu=Object.FindFirstObjectByType<MainMenu>();
                var catalog=TownSceneLoader.Default.AllyCatalog;
                common.GameSaveData=menu.CreateNewSave(new[]{42,12345,73421}[slot],catalog[0]);
                foreach(var prefab in catalog.Skip(1).Take(3)) common.GameSaveData.TownSaveData.RecruitedAlliesData.Add(HeroClassBinding.FromPrefab(prefab,new TownAllyData { AllyId=prefab.Id,AllyName=prefab.Name,Skills=prefab.Skills.ToList() }));
                common.Travel.NewCampaign(common.GameSaveData.TownSaveData.TownSeed);
                yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<Town>()?.IsReady==true,"four-member home");
                var town=Object.FindFirstObjectByType<Town>();var bed=Object.FindFirstObjectByType<HomeBed>();
                var party=town.TownPlayer.RecruitedAllies;
                Assert.That(party.Count,Is.EqualTo(4),"active party");Assert.That(party.Select(h=>h.TilemapPosition).Distinct().Count(),Is.EqualTo(4),"unique arrival tiles");
                Assert.That(party.Any(h=>h.TilemapPosition==bed.Tile),Is.False);
                var home=town.TownBuildings.Single(b=>b.Definition.Id=="home");
                // The door and bed approach belong to one traversable component.
                var reached=new System.Collections.Generic.HashSet<Vector3Int>{home.TilemapPosition};
                var queue=new System.Collections.Generic.Queue<Vector3Int>();queue.Enqueue(home.TilemapPosition);
                while(queue.Count>0)
                {
                    var cell=queue.Dequeue();
                    foreach(var d in new[]{Vector3Int.up,Vector3Int.down,Vector3Int.left,Vector3Int.right})
                    { var next=cell+d;if(next!=bed.Tile && !reached.Contains(next) && town.WalkableMap.CanWalkTo(cell,next)){reached.Add(next);queue.Enqueue(next);} }
                }
                Assert.That(reached,Contains.Item(bed.Tile-Vector3Int.up));
                Assert.That(SaveSystem.LoadData(slot).Summary.Party.Count,Is.EqualTo(4));
                Assert.That(common.CampaignContext.IsGridGenerated,Is.False);
                Assert.That(common.Travel.ReturnToMenu(),Is.True);
                yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<MainMenu>()?.IsReady==true,"menu");
            }
            SaveSystem.ActiveSlot=0;
            var stress=SaveSystem.LoadData(0);stress.TownSaveData.Gold=int.MaxValue;stress.PlaytimeSeconds=1234567;
            foreach(var h in stress.Summary.Party) h.Name="Alexandria of the Northern Highlands";
            SaveSystem.SaveData(0,stress);
            string original=harness.Store.Read(0);
            var main=Object.FindFirstObjectByType<MainMenu>();main.StartGame_Clicked();yield return null;
            var browser=Object.FindFirstObjectByType<CampaignSlots>();
            System.IO.Directory.CreateDirectory("Temp/CampaignSaves");
            ScreenCapture.CaptureScreenshot("Temp/CampaignSaves/slots.png");yield return new WaitForSecondsRealtime(.3f);
            var canvas=browser.GetComponent<Canvas>();var camera=new GameObject("Slot layout capture").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)})
            {
                var target=new RenderTexture(size.x,size.y,24);camera.targetTexture=target;yield return null;Canvas.ForceUpdateCanvases();
                foreach(var text in browser.GetComponentsInChildren<TMPro.TMP_Text>()) {text.ForceMeshUpdate();Assert.That(text.isTextOverflowing,Is.False,text.text);}
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
                System.IO.File.WriteAllBytes($"Temp/CampaignSaves/slots-{size.x}x{size.y}.png",image.EncodeToPNG());RenderTexture.active=previous;
                camera.targetTexture=null;Object.Destroy(image);Object.Destroy(target);
            }
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;Object.Destroy(camera.gameObject);

            var slotButton=browser.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Slot 1");
            Assert.That(browser.Slots.All(slot=>slot.Button is SelectToActivateButton),Is.True);
            Assert.That(browser.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="Continue" || b.name=="Choose hero"),Is.False);
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=-1};
            // The browser initially focuses the active slot. Exercise the two-click
            // contract from an unselected slot; an already focused slot activates.
            EventSystem.current.SetSelectedGameObject(null);
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(browser.gameObject.activeSelf,Is.True,"First click selects the save slot.");
            Assert.That(Object.FindFirstObjectByType<CampaignChoice>(),Is.Null);
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            yield return null;
            Object.FindFirstObjectByType<CampaignChoice>().Confirm.onClick.Invoke();yield return null;
            Object.FindFirstObjectByType<ProtagonistHeroPicker>().GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Back").onClick.Invoke();yield return null;
            Assert.That(harness.Store.Read(0),Is.EqualTo(original));
            var completed=SaveSystem.LoadData(1);completed.Campaign.Finished=true;SaveSystem.SaveData(1,completed);
            SaveSystem.ActiveSlot=1;main.Continue_Clicked();yield return null;
            browser=Object.FindFirstObjectByType<CampaignSlots>();
            var completedSlot=browser.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Slot 2");
            Assert.That(completedSlot.interactable,Is.True);
            completedSlot.onClick.Invoke();yield return null;
            Assert.That(browser.gameObject.activeSelf,Is.True,"Completed campaigns remain inspectable but cannot continue.");
            browser.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Back").onClick.Invoke();yield return null;
            SaveSystem.ActiveSlot=0;main.Continue_Clicked();yield return null;
            browser=Object.FindFirstObjectByType<CampaignSlots>();
            slotButton=browser.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Slot 1");
            EventSystem.current.SetSelectedGameObject(null);
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(browser.gameObject.activeSelf,Is.True,"First Continue click only selects the slot.");
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slotButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<Town>()?.IsReady==true,"continue selected campaign");
        }
    }
}
#endif
