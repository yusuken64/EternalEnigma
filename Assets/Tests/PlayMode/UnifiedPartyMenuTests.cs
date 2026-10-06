#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class UnifiedPartyMenuTests
    {
        private GameTestHarness harness;
        private readonly List<Object> owned=new();
        private bool? fullControl;
        private DungeonAnimationMode? animation;
        private TestInputScope inputScope;
        [UnitySetUp]public IEnumerator Setup(){inputScope=new TestInputScope();fullControl=DungeonPreferences.FullControlOverride;animation=DungeonPreferences.AnimationOverride;DungeonPreferences.FullControlOverride=false;DungeonPreferences.AnimationOverride=DungeonAnimationMode.NoAnimations;harness=new GameTestHarness();yield return null;}
        [UnityTearDown]public IEnumerator Cleanup(){yield return harness.Cleanup();DungeonPreferences.FullControlOverride=fullControl;DungeonPreferences.AnimationOverride=animation;inputScope.Dispose();foreach(var asset in owned)if(asset!=null)Object.DestroyImmediate(asset);owned.Clear();}
        private T Asset<T>() where T:ScriptableObject {var asset=ScriptableObject.CreateInstance<T>();owned.Add(asset);return asset;}

        [UnityTest]public IEnumerator MainMenuCompositionKeepsDeveloperControlsClosed()
        {
            yield return harness.LoadMainMenu(null);
            var menu=Object.FindFirstObjectByType<MainMenu>();yield return harness.WaitUntil(()=>menu.IsReady,"main menu ready");
            Assert.That(menu.TestDungeonButton.gameObject.activeSelf,Is.False);
            Assert.That(((RectTransform)menu.StartButton.transform.parent).anchorMin.x,Is.EqualTo(.77f));
            Directory.CreateDirectory("Temp/UnifiedPresentation");ScreenCapture.CaptureScreenshot("Temp/UnifiedPresentation/main-menu.png");
            yield return new WaitForSecondsRealtime(.3f);
        }

        [UnityTest]public IEnumerator TownTabsNestedBackAndRecoveryPreserveCheckpoint()
        {
            yield return harness.LoadTown(new TestScenario {Seed=12345}.CreateSave());
            var town=Object.FindFirstObjectByType<Town>();var manager=Object.FindFirstObjectByType<TownMenuManager>();
            manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            var menu=manager.PartyMenu;
            Assert.That(menu.Panel.anchorMin,Is.EqualTo(new Vector2(.08f,.12f)));
            Assert.That(Object.FindObjectsByType<ResourceHUD>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            menu.Shortcut(PartyMenuTab.Skills);Assert.That(manager.CurrentDialog,Is.SameAs(menu));Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Skills));
            menu.Shortcut(PartyMenuTab.Equipment);Assert.That(menu.EntryButtons.Count,Is.GreaterThanOrEqualTo(3));
            menu.Shortcut(PartyMenuTab.Stats);Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Stats));
            menu.Shortcut(PartyMenuTab.Skills);
            menu.Pick("Cancel without changes",new(){("Do nothing",()=>{})});yield return null;
            menu.Shortcut(PartyMenuTab.Inventory);Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Skills));
            manager.CurrentDialog.CloseDialog();yield return null;Assert.That(manager.CurrentDialog,Is.SameAs(menu));
            menu.Shortcut(PartyMenuTab.Skills);Assert.That(manager.Opened,Is.False);

            var caster=town.TownPlayer.RecruitedAllies[0];var stats=TownUtilityService.StatsFor(caster);
            Assert.That(stats.HPMax,Is.GreaterThan(5));caster.Hp=1;caster.Sp=-1;
            var effect=Asset<ModifyStatsItemEffectDefinition>();effect.VitalModification=new VitalModification{Hp=5};
            var definition=Asset<UsableItemDefinition>();definition.ItemName="Recovery test";definition.ItemEffectDefinition=effect;
            var first=definition.AsInventoryItem(null);var duplicate=definition.AsInventoryItem(null);
            town.TownPlayer.Inventory.Add(first);town.TownPlayer.Inventory.Add(duplicate);
            var checkpoint = JsonUtility.ToJson(SaveSystem.LoadData());
            var service=new TownUtilityService(town.TownPlayer.RecruitedAllies,town.TownPlayer.Inventory,town.SaveProgress);
            Assert.That(service.Execute(caster,null,duplicate,caster,null,out _),Is.True);
            Assert.That(caster.Hp,Is.EqualTo(6));Assert.That(town.TownPlayer.Inventory.Contains(first),Is.True);Assert.That(town.TownPlayer.Inventory.Contains(duplicate),Is.False);
            Assert.That(service.Execute(caster,null,duplicate,caster,null,out _),Is.False);
            Assert.That(JsonUtility.ToJson(SaveSystem.LoadData()),Is.EqualTo(checkpoint));
            effect.VitalModification.Hunger=1;Assert.That(service.Describe(caster,null,first,out _,out _),Is.False);
            effect.VitalModification.Hunger=0;caster.Hp=-1;Assert.That(service.Describe(caster,null,first,out _,out _),Is.False);

            manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            Directory.CreateDirectory("Temp/UnifiedPresentation");
            ScreenCapture.CaptureScreenshot("Temp/UnifiedPresentation/town-menu.png");yield return new WaitForSecondsRealtime(.3f);
            caster.Level=2;
            menu.Setup(new TownPartyMenuContext(town),PartyMenuTab.Stats,caster.Id);
            menu.EntryButtons[0].onClick.Invoke();yield return null;
            Assert.That(manager.CurrentDialog,Is.TypeOf<LevelUpChoiceDialog>());
            ((LevelUpChoiceDialog)manager.CurrentDialog).StrButton.onClick.Invoke();yield return null;
            Assert.That(caster.Attributes.Str,Is.EqualTo(1));
            Assert.That(Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData.First(a=>a.AllyId==caster.Id).Attributes.Str,Is.EqualTo(1));
        }

        [UnityTest]public IEnumerator RecoverySkillRejectsMixedEffectsAndCostsNothingOnInvalidTarget()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town=Object.FindFirstObjectByType<Town>();var caster=town.TownPlayer.RecruitedAllies[0];
            var skill=Asset<Skill>();skill.SkillName="Town test heal";skill.SPCost=2;skill.Targeting=SkillTargeting.Self;
            skill.TargetSelector=new TargetSelector{Team=TargetTeam.Self,Area=TargetArea.Self};skill.ActionEffects=new(){new ScaledHealAction{BaseHeal=3,PerLevel=1}};
            caster.SetRank(skill.SkillName,3);caster.Hp=1;caster.Sp=0;caster.Level=5;
            int saves=0;var service=new TownUtilityService(town.TownPlayer.RecruitedAllies,town.TownPlayer.Inventory,()=>saves++);
            Assert.That(service.Execute(caster,skill,null,caster,null,out _),Is.False);Assert.That(caster.Hp,Is.EqualTo(1));Assert.That(saves,Is.Zero);
            caster.Sp=-1;skill.ActionEffects.Add(new RestoreSPAction{Amount=-1});
            Assert.That(service.Execute(caster,skill,null,caster,null,out _),Is.False);Assert.That(caster.Sp,Is.EqualTo(-1));
            skill.ActionEffects.RemoveAt(1);Assert.That(service.Execute(caster,skill,null,caster,null,out _),Is.True);
            Assert.That(caster.Hp,Is.GreaterThan(4));Assert.That(saves,Is.EqualTo(1));
        }

        [UnityTest]public IEnumerator TownPartyRecoveryAndInventoryFiltersValidateBeforeCharging()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town=Object.FindFirstObjectByType<Town>();var caster=town.TownPlayer.RecruitedAllies[0];
            var ally=Object.Instantiate(caster,town.transform);ally.Id="utility-target";town.TownPlayer.RecruitedAllies.Add(ally);
            var skill=Asset<Skill>();skill.SkillName="Party SP recovery";skill.SPCost=2;skill.Targeting=SkillTargeting.AllTargets;
            skill.TargetSelector=new TargetSelector{Team=TargetTeam.Allies,Area=TargetArea.All};
            skill.ActionEffects=new(){new RestoreSPAction{Amount=5,ExcludeCaster=true}};
            caster.SetRank(skill.SkillName,1);caster.Hp=-1;caster.Sp=-1;ally.Hp=0;ally.Sp=0;
            int saves=0;var service=new TownUtilityService(town.TownPlayer.RecruitedAllies,town.TownPlayer.Inventory,()=>saves++);
            Assert.That(service.Execute(caster,skill,null,null,null,out _),Is.False);Assert.That(caster.Sp,Is.EqualTo(-1));
            ally.Hp=-1;Assert.That(service.Execute(caster,skill,null,null,null,out _),Is.True);
            Assert.That(ally.Sp,Is.EqualTo(Mathf.Min(5,TownUtilityService.StatsFor(ally).SPMax)));
            Assert.That(caster.Sp,Is.EqualTo(TownUtilityService.StatsFor(caster).SPMax-2));Assert.That(saves,Is.EqualTo(1));

            var equipment=Asset<EquipmentItemDefinition>();equipment.ItemName="Inspected weapon";equipment.EquipmentSlot=EquipmentSlot.MainHand;
            var equipped=new EquipableInventoryItem(equipment);caster.Equipment.Equip(equipped);
            var inspect=Asset<Skill>();inspect.SkillName="Town inspection";inspect.SPCost=1;inspect.Targeting=SkillTargeting.InventoryItem;
            inspect.InventoryTargetSelector=new InventoryTargetSelector{ItemType=InventoryTargetType.Weapon,IncludeEquipped=false};
            inspect.ActionEffects=new(){new InspectInventoryItemEffect()};caster.SetRank(inspect.SkillName,1);
            int before=caster.Sp;
            Assert.That(service.Execute(caster,inspect,null,null,equipped,out _),Is.False);Assert.That(caster.Sp,Is.EqualTo(before));
            inspect.InventoryTargetSelector.IncludeEquipped=true;
            Assert.That(service.Describe(caster,inspect,null,out var utility,out _),Is.True);
            Assert.That(service.InventoryTargets(caster,utility),Does.Contain(equipped));
            Assert.That(service.Execute(caster,inspect,null,null,equipped,out var description),Is.True);
            Assert.That(description,Does.Contain(equipment.ItemName));Assert.That(caster.Sp,Is.EqualTo(before-1));
            Assert.That(caster.Equipment.IsEquipped(equipped),Is.True);Assert.That(saves,Is.EqualTo(2));
        }

        [UnityTest]public IEnumerator DungeonRootSwitchingDoesNotConsumeATurn()
        {
            yield return harness.LoadDungeon(new TestScenario {Seed=12345,IncludeStartingItems=true});
            var manager=MenuManager.Instance;var actor=harness.Ally;int actions=actor.Vitals.ActionsPerTurnLeft;
            manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            var menu=manager.PartyMenu;menu.Shortcut(PartyMenuTab.Skills);yield return null;
            menu.Shortcut(PartyMenuTab.Equipment);Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Equipment));
            menu.Shortcut(PartyMenuTab.Stats);Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Stats));
            menu.Shortcut(PartyMenuTab.Skills);
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Skills));Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));
            Assert.That(harness.Game.PlayerController.ControlledAlly,Is.SameAs(actor));
            menu.Shortcut(PartyMenuTab.Inventory);yield return null;
            Directory.CreateDirectory("Temp/UnifiedPresentation");ScreenCapture.CaptureScreenshot("Temp/UnifiedPresentation/dungeon-menu.png");yield return new WaitForSecondsRealtime(.3f);
            menu.CloseDialog();yield return null;Assert.That(manager.Opened,Is.False);
        }

        [UnityTest]public IEnumerator EquipmentConflictsKeepCopiesAndOverworldCommitsSurviveReload()
        {
            harness.TimeoutSeconds=120;
            yield return harness.LoadMainMenu(null);
            var common=Common.Instance;common.GameSaveData=Object.FindFirstObjectByType<MainMenu>().CreateNewSave(12345);
            common.Travel.NewCampaign(12345);
            yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<Town>()?.IsReady==true,"campaign town");
            var town=Object.FindFirstObjectByType<Town>();var hero=town.TownPlayer.RecruitedAllies[0];
            hero.Level=3;
            Assert.That(AttributeSpending.TrySpend(hero,HeroAttribute.Str),Is.True);
            // Use a registered compatible item for a real serialization/reload transaction.
            var authored=common.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
                .First(d=>HeroClass.AllowsItem(hero.PrimaryClass,hero.SecondaryClass,new EquipableInventoryItem(d)));
            var first=authored.AsInventoryItem(null);var second=authored.AsInventoryItem(null);
            town.TownPlayer.Inventory.Add(first);town.TownPlayer.Inventory.Add(second);town.SaveProgress();
            var checkpoint = JsonUtility.ToJson(SaveSystem.LoadData());
            var context=common.CampaignContext;Assert.That(context.BeginTownDungeon("story-0"),Is.True);Assert.That(context.CompleteDungeon(true),Is.True);
            Assert.That(common.Travel.ExitTown(town),Is.True);
            yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<OverworldScene>()?.IsReady==true,"overworld");
            var world=Object.FindFirstObjectByType<OverworldScene>();
            var adapter=new OverworldPartyMenuContext(world);var inspected=adapter.Heroes[0];
            Assert.That(inspected.TownActor.Attributes.Str,Is.EqualTo(1));
            var copies=adapter.Entries(inspected,PartyMenuTab.Inventory).Where(e=>e.Item.ItemName==authored.ItemName).ToArray();
            Assert.That(copies.Length,Is.EqualTo(2));Assert.That(copies[0].Item,Is.Not.SameAs(copies[1].Item));
            var manager=world.GetComponent<OverworldMenuManager>();manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            manager.PartyMenu.Shortcut(PartyMenuTab.Equipment);Assert.That(manager.PartyMenu.Tab,Is.EqualTo(PartyMenuTab.Equipment));
            manager.PartyMenu.Shortcut(PartyMenuTab.Stats);Assert.That(manager.PartyMenu.Tab,Is.EqualTo(PartyMenuTab.Stats));
            manager.PartyMenu.Shortcut(PartyMenuTab.Capabilities);Assert.That(manager.PartyMenu.Tab,Is.EqualTo(PartyMenuTab.Capabilities));
            manager.PartyMenu.Setup(adapter,PartyMenuTab.Inventory,inspected.Id);
            adapter.Actions(inspected,copies[1]).Single().Execute(manager.PartyMenu);
            Assert.That(inspected.Equipment.IsEquipped(copies[1].Item),Is.True);Assert.That(inspected.Equipment.IsEquipped(copies[0].Item),Is.False);
            var saved=common.GameSaveData;Assert.That(saved.TownSaveData.InventoryItems.Count(i=>i.ItemName==authored.ItemName),Is.EqualTo(1));
            Assert.That(saved.TownSaveData.RecruitedAlliesData[0].Equipment.Count(i=>i.ItemName==authored.ItemName),Is.EqualTo(1));
            Assert.That(saved.Roster.Single(r=>r.AllyId==inspected.Id).Equipment.Count(i=>i.ItemName==authored.ItemName),Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(SaveSystem.LoadData()),Is.EqualTo(checkpoint));
            Assert.That(adapter.Spend(inspected,HeroAttribute.Int),Is.True);
            Assert.That(saved.TownSaveData.RecruitedAlliesData[0].Attributes.Int,Is.EqualTo(1));
            manager.PartyMenu.CloseDialog();adapter.Dispose();
            common.GameSaveData=saved;
            var reloaded=new OverworldPartyMenuContext(world);Assert.That(reloaded.Heroes[0].Equipment.GetEquippedItems().Single().ItemName,Is.EqualTo(authored.ItemName));
            Assert.That(reloaded.Heroes[0].TownActor.Attributes.Str,Is.EqualTo(1));
            reloaded.Dispose();
            manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            ScreenCapture.CaptureScreenshot("Temp/UnifiedPresentation/overworld-menu.png");yield return new WaitForSecondsRealtime(.3f);
        }

        [UnityTest]public IEnumerator FourHeroesAndLongDescriptionsFitSupportedLayouts()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town=Object.FindFirstObjectByType<Town>();var original=town.TownPlayer.RecruitedAllies[0];
            for(int i=1;i<4;i++)
            {
                var clone=Object.Instantiate(original,town.transform);clone.Id="layout-"+i;clone.Name="Companion "+i;
                town.TownPlayer.RecruitedAllies.Add(clone);
            }
            for(int i=0;i<15;i++)
            {
                var definition=Asset<UsableItemDefinition>();definition.ItemName="Recovery item "+i;definition.Description=string.Join(" ",Enumerable.Repeat("A long description that can be read by scrolling the details panel.",20));
                town.TownPlayer.Inventory.Add(definition.AsInventoryItem(null));
            }
            var manager=Object.FindFirstObjectByType<TownMenuManager>();manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            var menu=manager.PartyMenu;var canvas=menu.GetComponent<Canvas>();
            var camera=new GameObject("Layout capture",typeof(Camera)).GetComponent<Camera>();owned.Add(camera.gameObject);
            camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.15f,.14f);
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            foreach(var size in new[]{new Vector2Int(960,600),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)})
            {
                var target=new RenderTexture(size.x,size.y,24);owned.Add(target);camera.targetTexture=target;yield return null;Canvas.ForceUpdateCanvases();
                var root=(RectTransform)canvas.transform;
                foreach(var control in new Component[]{menu.Panel,menu.InventoryTab,menu.SkillsTab,menu.BackButton,menu.Details})
                {
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(root,control.transform);
                    // Scrolling content is deliberately taller than its clipped viewport.
                    if(control==menu.Details)continue;
                    Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(root.rect.xMin-1));Assert.That(bounds.max.x,Is.LessThanOrEqualTo(root.rect.xMax+1));
                }
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
                File.WriteAllBytes($"Temp/UnifiedPresentation/menu-{size.x}x{size.y}.png",image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);
                camera.targetTexture=null;
            }
            Assert.That(menu.HeroesRoot.GetComponentsInChildren<Button>().Length,Is.EqualTo(4));
            var controlled=town.TownPlayer.ControllingTownAlly;menu.BrowseHero(1);Assert.That(town.TownPlayer.ControllingTownAlly,Is.SameAs(controlled));
        }

        [UnityTest]public IEnumerator SilhouettesProjectGeometryWithoutDarkeningOverlapsOrHiddenCasters()
        {
            yield return harness.LoadCommon();
            var camera=new GameObject("Shadow validation camera",typeof(Camera)).GetComponent<Camera>();owned.Add(camera.gameObject);
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3;camera.aspect=1;
            camera.transform.position=new Vector3(0,0,-10);camera.transform.rotation=Quaternion.identity;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<29;
            var target=new RenderTexture(256,256,24);owned.Add(target);camera.targetTexture=target;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Quad);owned.Add(ground);ground.layer=29;ground.transform.localScale=Vector3.one*6;
            var white=new Material(Shader.Find("Unlit/Color")){color=Color.white};owned.Add(white);ground.GetComponent<Renderer>().sharedMaterial=white;
            SilhouetteParticipant.Register(ground.transform,SilhouetteRole.Receiver);
            var caster=GameObject.CreatePrimitive(PrimitiveType.Cube);owned.Add(caster);caster.layer=29;caster.transform.position=new Vector3(0,0,-1);
            caster.GetComponent<Renderer>().sharedMaterial=white;SilhouetteParticipant.Register(caster.transform,SilhouetteRole.Caster,true);
            var shadows=camera.gameObject.AddComponent<SilhouetteRenderer>();shadows.Direction=new Vector3(.6f,0,.8f);
            var mask=new RenderTexture(256,256,0);owned.Add(mask);shadows.InspectionMask=mask;
            var readback=new Texture2D(256,256,TextureFormat.RGB24,false);owned.Add(readback);
            Color Sample()
            {
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                readback.ReadPixels(new Rect(0,0,256,256),0,0);readback.Apply();RenderTexture.active=previous;
                var point=camera.WorldToViewportPoint(new Vector3(1.1f,0,0));return readback.GetPixel((int)(point.x*256),(int)(point.y*256));
            }
            shadows.enabled=false;float before=Sample().grayscale;
            Directory.CreateDirectory("Temp/UnifiedPresentation");File.WriteAllBytes("Temp/UnifiedPresentation/mask-before.png",readback.EncodeToPNG());
            shadows.enabled=true;float one=Sample().grayscale;
            File.WriteAllBytes("Temp/UnifiedPresentation/mask-after.png",readback.EncodeToPNG());
            RenderTexture.active=mask;readback.ReadPixels(new Rect(0,0,256,256),0,0);readback.Apply();RenderTexture.active=null;
            File.WriteAllBytes("Temp/UnifiedPresentation/mask-only.png",readback.EncodeToPNG());
            Assert.That(one,Is.LessThan(before-.08f),"Projected geometry must visibly darken eligible ground.");
            var duplicate=Object.Instantiate(caster);owned.Add(duplicate);
            Assert.That(Sample().grayscale,Is.EqualTo(one).Within(.015f),"Overlapping casters must share one opacity mask.");
            duplicate.SetActive(false);caster.GetComponent<Renderer>().forceRenderingOff=true;
            Assert.That(Sample().grayscale,Is.EqualTo(before).Within(.015f),"Hidden dynamic casters must not leak shadows.");
            caster.GetComponent<Renderer>().forceRenderingOff=false;
            var cover=GameObject.CreatePrimitive(PrimitiveType.Quad);owned.Add(cover);cover.layer=29;
            cover.transform.position=new Vector3(1.1f,0,-2);cover.transform.localScale=Vector3.one*.6f;cover.GetComponent<Renderer>().sharedMaterial=white;
            Assert.That(Sample().grayscale,Is.EqualTo(before).Within(.015f),"Scene depth must prevent painting over rooftops/characters.");
            cover.SetActive(false);ground.GetComponent<SilhouetteParticipant>().Role=SilhouetteRole.None;
            Assert.That(Sample().grayscale,Is.EqualTo(before).Within(.015f),"Non-receivers, including water, must remain untouched.");
            camera.targetTexture=null;
        }

        [UnityTest]public IEnumerator SilhouetteMaskCapturesAndFrameSamples()
        {
            yield return harness.LoadTown(new TestScenario {Seed=12345}.CreateSave());
            var renderer=Camera.main.GetComponent<SilhouetteRenderer>();Assert.That(renderer,Is.Not.Null);
            var build=(System.Action)System.Delegate.CreateDelegate(typeof(System.Action),renderer,
                typeof(SilhouetteRenderer).GetMethod("OnPreCull",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance));
            for(int i=0;i<10;i++)build();
            long allocated=System.GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<30;i++)build();
            allocated=System.GC.GetAllocatedBytesForCurrentThread()-allocated;
            Assert.That(allocated,Is.Zero,"Warm silhouette command construction must not allocate managed memory.");
            var samples=new List<float>();var report=new System.Text.StringBuilder("seed=12345; same town/camera; Editor frame samples (not player GPU timings)\n");
            foreach(bool enabled in new[]{false,true})
            {
                renderer.enabled=enabled;
                for(int i=0;i<30;i++)yield return null;
                samples.Clear();
                for(int i=0;i<120;i++){yield return null;samples.Add(Time.unscaledDeltaTime*1000);}
                samples.Sort();report.AppendLine($"silhouettes={enabled}; median_ms={samples[samples.Count/2]:F3}");
                ScreenCapture.CaptureScreenshot($"Temp/UnifiedPresentation/town-silhouettes-{enabled}.png");yield return new WaitForSecondsRealtime(.3f);
            }
            File.WriteAllText("Temp/UnifiedPresentation/FrameSamples.txt",report.ToString());
            Assert.That(Resources.Load<Shader>("Presentation/Silhouette").isSupported,Is.True);
        }
    }
}
#endif
