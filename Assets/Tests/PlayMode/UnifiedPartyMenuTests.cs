#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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

        [UnityTest]public IEnumerator TownKeyboardInventoryOpensUsesItemAndCloses()
        {
            yield return harness.LoadTown(new TestScenario {Seed=12345}.CreateSave());
            var town=Object.FindFirstObjectByType<Town>();
            var manager=town.GetComponentInChildren<TownMenuManager>();
            var keyboard=InputSystem.AddDevice<Keyboard>();
            MenuUIInputModule.Active.actionsAsset.devices=new InputDevice[]{keyboard};

            yield return PressNavigation(keyboard,Key.Q);
            var menu=manager.PartyMenu;
            Assert.That(manager.CurrentDialog,Is.SameAs(menu));
            Assert.That(menu,Is.Not.Null);
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Inventory));
            Assert.That(menu.EntryButtons,Is.Empty);
            Assert.That(menu.InventoryTab.interactable,Is.True);
            yield return PressNavigation(keyboard,Key.Q);
            Assert.That(manager.Opened,Is.False,"Q closes an empty inventory.");

            var hero=town.TownPlayer.ControllingTownAlly;
            hero.Hp=1;
            var effect=Asset<ModifyStatsItemEffectDefinition>();effect.VitalModification=new VitalModification{Hp=5};
            var definition=Asset<UsableItemDefinition>();definition.ItemName="Keyboard recovery test";
            definition.ItemEffectDefinition=effect;definition.Targeting=SkillTargeting.SelectedTarget;
            definition.TargetSelector=new TargetSelector{Team=TargetTeam.Self,Area=TargetArea.Self};
            var item=definition.AsInventoryItem(null);town.TownPlayer.Inventory.Add(item);

            yield return PressNavigation(keyboard,Key.Q);
            Assert.That(menu.EntryButtons.Count,Is.EqualTo(1));
            yield return PressNavigation(keyboard,Key.Enter);
            Assert.That(manager.CurrentDialog,Is.TypeOf<PartyMenuPicker>(),"Enter opens the item actions.");
            yield return PressNavigation(keyboard,Key.Enter);
            Assert.That(manager.DialogStack.Count,Is.EqualTo(3),"Target selection uses a second authored picker.");
            yield return PressNavigation(keyboard,Key.Escape);
            Assert.That(manager.DialogStack.Count,Is.EqualTo(2));
            Assert.That(town.TownPlayer.Inventory.Contains(item),Is.True,"Cancel does not consume the item.");
            yield return PressNavigation(keyboard,Key.Enter);
            yield return PressNavigation(keyboard,Key.Enter);
            Assert.That(manager.CurrentDialog,Is.SameAs(menu));
            Assert.That(hero.Hp,Is.EqualTo(6));
            Assert.That(town.TownPlayer.Inventory.Contains(item),Is.False);

            yield return PressNavigation(keyboard,Key.R);
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Skills));
            yield return PressNavigation(keyboard,Key.Q);
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Inventory));
            yield return PressNavigation(keyboard,Key.Escape);
            Assert.That(manager.Opened,Is.False);
            Assert.That(Common.Instance.MenuInputHandler.PlayerInput.currentActionMap.name,Is.EqualTo("Player"));
            menu.InventoryTab.onClick.Invoke();yield return null;
            Assert.That(manager.CurrentDialog,Is.SameAs(menu),"The HUD shortcut is wired on ordinary town startup.");
        }

        [UnityTest]public IEnumerator TownTabsNestedBackAndRecoveryPreserveCheckpoint()
        {
            yield return harness.LoadTown(new TestScenario {Seed=12345}.CreateSave());
            var town=Object.FindFirstObjectByType<Town>();var manager=Object.FindFirstObjectByType<TownMenuManager>();
            manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            var menu=manager.PartyMenu;
            yield return VerifyHudTabs(menu);
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
            var menu=manager.PartyMenu;
            yield return VerifyHudTabs(menu);
            menu.Shortcut(PartyMenuTab.Skills);yield return null;
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
            yield return VerifyHudTabs(manager.PartyMenu,includeCapabilities:true);
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
            original.Level=LevelSystem.MaxLevel;original.Experience=LevelSystem.ExperienceAtLevel(LevelSystem.MaxLevel);
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
            var canvases=new[]{canvas,menu.HudTabs.GetComponent<Canvas>()};
            foreach(var surface in canvases)
            {
                surface.renderMode=RenderMode.ScreenSpaceCamera;surface.worldCamera=camera;surface.planeDistance=1;
                foreach(var t in surface.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            }
            Directory.CreateDirectory("Temp/UnifiedPresentation");
            foreach(var size in new[]{new Vector2Int(960,600),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)})
            {
                var target=new RenderTexture(size.x,size.y,24);owned.Add(target);camera.targetTexture=target;yield return null;Canvas.ForceUpdateCanvases();
                var root=(RectTransform)canvas.transform;
                foreach(var control in new Component[]{menu.Panel,menu.InventoryTab,menu.SkillsTab,menu.Details})
                {
                    var surface=(RectTransform)control.GetComponentInParent<Canvas>().transform;
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(surface,control.transform);
                    // Scrolling content is deliberately taller than its clipped viewport.
                    if(control==menu.Details)continue;
                    Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(surface.rect.xMin-1));Assert.That(bounds.max.x,Is.LessThanOrEqualTo(surface.rect.xMax+1));
                }
                foreach(var tab in new[]{PartyMenuTab.Inventory,PartyMenuTab.Stats})
                {
                    menu.SwitchTab(tab);yield return null;Canvas.ForceUpdateCanvases();
                    if(tab==PartyMenuTab.Stats)VerifyStatsFit(menu);
                    else
                    {
                        Assert.That(menu.scrollView.vertical,Is.True,"Inventory scrolling returns after leaving Stats.");
                        Assert.That(menu.scrollView.content.rect.height,Is.GreaterThan(menu.scrollView.viewport.rect.height));
                    }
                    camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
                    string name=tab==PartyMenuTab.Stats?"stats":"menu";
                    File.WriteAllBytes($"Temp/UnifiedPresentation/{name}-{size.x}x{size.y}.png",image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);
                }
                camera.targetTexture=null;
            }
            menu.SwitchTab(PartyMenuTab.Inventory);yield return null;
            Assert.That(menu.HeroesRoot.gameObject.activeSelf,Is.False);
            Assert.That(menu.HeroText.gameObject.activeSelf,Is.False);
            Assert.That(menu.DetailsControl.Scroll.gameObject.activeSelf,Is.False);
            Assert.That(menu.Hints.gameObject.activeSelf,Is.False);
            var list=(RectTransform)menu.scrollView.transform;
            Assert.That(list.anchorMin,Is.EqualTo(new Vector2(.035f,.025f)));
            Assert.That(list.anchorMax,Is.EqualTo(new Vector2(.965f,.975f)));
            var controlled=town.TownPlayer.ControllingTownAlly;menu.BrowseHero(1);Assert.That(town.TownPlayer.ControllingTownAlly,Is.SameAs(controlled));
        }

        private static void VerifyStatsFit(PartyMenu menu)
        {
            Assert.That(menu.scrollView.vertical,Is.False,"The entire Stats submenu fits without scrolling.");
            Assert.That(menu.EntryButtons.Count,Is.GreaterThanOrEqualTo(19),"All stats remain present.");
            Assert.That(menu.EntryButtons.Last().GetComponentInChildren<TMPro.TMP_Text>().text,Does.StartWith("Lightning "));
            var viewport=menu.scrollView.viewport;
            Assert.That(menu.scrollView.content.rect.height,Is.LessThanOrEqualTo(viewport.rect.height+1));
            foreach(RectTransform row in menu.RowsRoot)
            {
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,row);
                Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(viewport.rect.yMin-1),row.name+" extends below the Stats panel.");
                Assert.That(bounds.max.y,Is.LessThanOrEqualTo(viewport.rect.yMax+1),row.name+" extends above the Stats panel.");
                foreach(var label in row.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing,Is.False,label.text+" must be fully visible.");
                }
            }
            var position=menu.scrollView.content.anchoredPosition;
            menu.scrollView.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-10)});
            Assert.That(menu.scrollView.content.anchoredPosition,Is.EqualTo(position),"Mouse-wheel input must not scroll Stats.");
        }

        private IEnumerator VerifyHudTabs(PartyMenu menu,bool includeCapabilities=false)
        {
            var mouse=InputSystem.AddDevice<Mouse>();
            var pad=InputSystem.AddDevice<Gamepad>();
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var module=MenuUIInputModule.Active;
            module.actionsAsset.devices=new InputDevice[]{mouse,pad,keyboard};
            var launcher=menu.HudTabs;
            var hud=launcher.GetComponent<Canvas>();
            var owner=menu.Owner;
            var hero=menu.Hero.Id;
            Assert.That(menu.Panel.anchorMin,Is.EqualTo(new Vector2(.73f,.03f)));
            Assert.That(menu.Panel.anchorMax,Is.EqualTo(new Vector2(.98f,.88f)));
            var reference=GameUITheme.Current.DungeonMenuPrefab.GetComponent<PartyMenu>();
            var panelGraphic=GameUISkin.PanelGraphic(menu.Panel);
            Assert.That(panelGraphic.sprite,Is.EqualTo(GameUITheme.Current.DungeonWood),"Every scene uses the dungeon inventory panel.");
            Assert.That(panelGraphic.pixelsPerUnitMultiplier,Is.EqualTo(1));
            Assert.That(menu.EntryTemplate,Is.SameAs(reference.EntryTemplate));
            Assert.That(menu.PlainEntryTemplate,Is.SameAs(reference.PlainEntryTemplate));
            Assert.That(menu.HeadingTemplate,Is.SameAs(reference.HeadingTemplate));
            Assert.That(menu.EmptyTemplate,Is.SameAs(reference.EmptyTemplate));
            Assert.That(menu.BackButton.gameObject.activeSelf,Is.False);
            Assert.That(menu.Panel.GetComponentsInChildren<DungeonDialogClose>(true).All(close=>!close.gameObject.activeSelf),Is.True);
            Assert.That(hud.enabled,Is.True,"The original HUD tabs stay visible when the menu opens.");
            Assert.That(hud.sortingOrder,Is.GreaterThan(menu.GetComponent<Canvas>().sortingOrder));
            foreach(var shield in menu.GetComponentsInChildren<Image>().Where(i=>i.name=="Input shield"))
                Assert.That(shield.color.a,Is.Zero,"The world remains visible behind the menu.");
            Assert.That(menu.Panel.GetComponentsInChildren<Button>().Any(b=>
                new[]{"Inventory","Equipment","Skills","Stats","Capabilities"}.Contains(b.GetComponentInChildren<TMPro.TMP_Text>()?.text)),Is.False,
                "The open panel must not contain a second row of navigation tabs.");

            yield return VerifyHorizontalTabNavigation(menu,keyboard,pad,includeCapabilities);

            foreach(var tab in includeCapabilities
                ? new[]{PartyMenuTab.Inventory,PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats,PartyMenuTab.Capabilities}
                : new[]{PartyMenuTab.Inventory,PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats})
            {
                var button=launcher.ButtonFor(tab);
                Assert.That(button.transform.IsChildOf(menu.transform),Is.False);
                Assert.That(module.Allows(button.gameObject),Is.True,"HUD headers share the menu's input focus.");
                yield return ClickHudTab(mouse,button);
                Assert.That(owner.Current,Is.SameAs(menu));
                Assert.That(owner.Stack.Count,Is.EqualTo(1));
                Assert.That(menu.Tab,Is.EqualTo(tab));
                Assert.That(menu.Hero.Id,Is.EqualTo(hero));
                if(tab==PartyMenuTab.Stats)VerifyStatsFit(menu);
                Assert.That(button.GetComponentInChildren<TMPro.TMP_Text>().text,Does.StartWith("> "));
                var label=button.GetComponentInChildren<TMPro.TMP_Text>();label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing,Is.False,"HUD tab labels must fit, including the active marker and shortcut.");
                Assert.That(((Image)button.targetGraphic).sprite,Is.EqualTo(GameUITheme.Current.DungeonWoodButton),"All scenes use the same wooden tab buttons.");
                Assert.That(((Image)button.targetGraphic).pixelsPerUnitMultiplier,Is.EqualTo(1));
                Assert.That(button.targetGraphic.GetComponent<DungeonUIRole>().IsValid(),Is.True);
                Assert.That(label.font,Is.EqualTo(TMPro.TMP_Settings.defaultFontAsset));
                Assert.That(label.color,Is.EqualTo(GameUITheme.LightInk));
                Assert.That(button.colors.normalColor,Is.EqualTo(GameUITheme.Selected),"The current tab stays highlighted while browsing its list.");
                var rect=(RectTransform)button.transform;
                Assert.That(rect.anchorMax.x-rect.anchorMin.x,Is.EqualTo(.105f).Within(.0001f),"Overworld's extra tab must not shrink the shared tabs.");
                Assert.That(rect.anchorMin.y,Is.EqualTo(.935f));Assert.That(rect.anchorMax.y,Is.EqualTo(.985f));
                yield return ClickHudTab(mouse,button);
                Assert.That(owner.Current,Is.SameAs(menu),"Clicking the active header keeps the menu open.");
            }

            var previousTab=menu.Tab;
            menu.Pick("Nested actions",new(){("Cancel this action",()=>{})});yield return null;
            Assert.That(menu.GetComponent<Canvas>().enabled,Is.False);
            Assert.That(hud.enabled,Is.True);
            Assert.That(menu.InventoryTab.interactable,Is.False);
            Assert.That(module.Allows(menu.InventoryTab.gameObject),Is.False);
            menu.InventoryTab.onClick.Invoke();
            yield return PressNavigation(keyboard,Key.RightArrow);
            Assert.That(menu.Tab,Is.EqualTo(previousTab),"A nested choice must retain its tab and actor.");
            var picker=(PartyMenuPicker)owner.Current;
            Assert.That(picker.BackButton.gameObject.activeSelf,Is.False);
            Assert.That(((RectTransform)picker.scrollView.transform).anchorMin.y,Is.EqualTo(.035f));
            var panel=(RectTransform)picker.Title.transform.parent;
            Assert.That(GameUISkin.PanelGraphic(panel).sprite,Is.EqualTo(GameUITheme.Current.DungeonWood));
            Assert.That(picker.RowTemplate,Is.SameAs(GameUITheme.Current.DungeonPickerPrefab.GetComponent<PartyMenuPicker>().RowTemplate));
            Assert.That(panel.anchorMin,Is.EqualTo(menu.Panel.anchorMin));
            Assert.That(panel.anchorMax,Is.EqualTo(menu.Panel.anchorMax));
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            Assert.That(owner.Current,Is.SameAs(menu),"Controller Cancel closes the submenu without an on-screen Back button.");
            Assert.That(menu.GetComponent<Canvas>().enabled,Is.True);
            Assert.That(menu.InventoryTab.interactable,Is.True);
            menu.Pick("No available items",new());yield return null;yield return null;
            Assert.That(((PartyMenuPicker)owner.Current).BackButton.gameObject.activeSelf,Is.False);
            yield return PressNavigation(keyboard,Key.Escape);
            Assert.That(owner.Current,Is.SameAs(menu),"An empty inventory submenu still accepts Escape.");

            GameMessages.Post("The party is ready to explore.");yield return null;
            GameMessages.ShowHistory();yield return null;yield return null;
            var history=owner.Current as EventHistoryDialog;
            Assert.That(history,Is.Not.Null,"History shares the scene's dialog stack.");
            Assert.That(history.Panel.anchorMin,Is.EqualTo(menu.Panel.anchorMin));
            Assert.That(history.Panel.anchorMax,Is.EqualTo(menu.Panel.anchorMax));
            Assert.That(history.Panel.GetComponent<Image>().sprite,Is.EqualTo(GameUITheme.Current.DungeonWood));
            Assert.That(menu.GetComponent<Canvas>().enabled,Is.False);
            Assert.That(history.Entries.text,Does.Contain("The party is ready to explore."));
            GameMessages.ShowHistory();Assert.That(owner.Current,Is.SameAs(history));
            Assert.That(owner.Stack.Count,Is.EqualTo(2),"Repeated history clicks must not create floating copies.");
            history.Setup(Enumerable.Range(0,60).Select(i=>"History entry "+i).ToArray());
            Assert.That(history.scrollView.verticalNormalizedPosition,Is.EqualTo(0).Within(.001f));
            ExecuteEvents.Execute(history.Reader.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Up},ExecuteEvents.moveHandler);
            Assert.That(history.scrollView.verticalNormalizedPosition,Is.GreaterThan(0));
            Directory.CreateDirectory("Temp/UnifiedPresentation");
            ScreenCapture.CaptureScreenshot("Temp/UnifiedPresentation/history-"+menu.gameObject.scene.name+".png");
            yield return new WaitForSecondsRealtime(.15f);
            history.Back.onClick.Invoke();yield return null;yield return null;
            Assert.That(owner.Current,Is.SameAs(menu));
            Assert.That(menu.GetComponent<Canvas>().enabled,Is.True);

            yield return PressNavigation(keyboard,Key.Escape);
            Assert.That(owner.Opened,Is.False);
            Assert.That(hud.enabled,Is.True,"Closing the menu leaves the same HUD buttons available.");
            var feed=Object.FindFirstObjectByType<GameMessages>();
            var feedPanel=(RectTransform)feed.GetComponentInChildren<CanvasGroup>().transform;
            Assert.That(feedPanel.anchorMin,Is.EqualTo(new Vector2(.24f,.016f)));
            Assert.That(feedPanel.anchorMax,Is.EqualTo(new Vector2(.79f,.20f)),"Events use the dungeon's bottom feed, not a floating panel.");
            var feedReference=Resources.Load<GameMessages>("UI/Authored/DungeonEvents").GetComponentInChildren<CanvasGroup>(true);
            var referenceImages=feedReference.GetComponentsInChildren<Image>(true);
            var feedImages=feedPanel.GetComponentsInChildren<Image>(true);
            Assert.That(feedImages.Length,Is.EqualTo(referenceImages.Length),"All scenes share the dungeon event feed hierarchy.");
            for(int i=0;i<feedImages.Length;i++)
            {
                Assert.That(feedImages[i].sprite,Is.EqualTo(referenceImages[i].sprite));
                Assert.That(feedImages[i].type,Is.EqualTo(referenceImages[i].type));
                Assert.That(feedImages[i].pixelsPerUnitMultiplier,Is.EqualTo(referenceImages[i].pixelsPerUnitMultiplier));
            }
            var historyButton=feedPanel.GetComponentInChildren<Button>();
            var referenceButton=feedReference.GetComponentInChildren<Button>(true);
            Assert.That(historyButton.colors,Is.EqualTo(referenceButton.colors));
            var historyLabel=historyButton.GetComponentInChildren<TMPro.TMP_Text>();
            Assert.That(historyLabel.color,Is.EqualTo(GameUITheme.LightInk));
            historyLabel.ForceMeshUpdate();Assert.That(historyLabel.isTextOverflowing,Is.False);
            ScreenCapture.CaptureScreenshot("Temp/UnifiedPresentation/events-"+menu.gameObject.scene.name+".png");
            yield return new WaitForSecondsRealtime(.15f);
            yield return ClickHudTab(mouse,historyButton);
            Assert.That(owner.Current,Is.TypeOf<EventHistoryDialog>(),"The shared History button opens the scene's docked reader.");
            yield return PressNavigation(keyboard,Key.Escape);
            Assert.That(owner.Opened,Is.False);
            yield return ClickHudTab(mouse,launcher.ButtonFor(PartyMenuTab.Inventory));
            Assert.That(owner.Current,Is.SameAs(menu));
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Inventory));
            module.actionsAsset.devices=null;
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(keyboard);
        }

        private IEnumerator VerifyHorizontalTabNavigation(PartyMenu menu,Keyboard keyboard,Gamepad pad,bool includeCapabilities)
        {
            var module=MenuUIInputModule.Active;
            var owner=menu.Owner;
            var hero=menu.Hero.Id;
            string selectedItem=null;
            if(menu.EntryButtons.Count>0)
            {
                var selected=menu.EntryButtons.Last();selected.Select();
                selectedItem=selected.GetComponentInChildren<TMPro.TMP_Text>().text;
            }
            yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Equipment),"Right switches tabs directly from the menu contents, without Submit.");
            yield return PressNavigation(keyboard,Key.LeftArrow);
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Inventory));
            if(selectedItem!=null)
                Assert.That(EventSystem.current.currentSelectedGameObject.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo(selectedItem),
                    "Returning to a tab restores the selected inventory entry.");

            // Tab changes work from HUD headers and empty lists.
            menu.InventoryTab.Select();
            yield return PressNavigation(keyboard,Key.LeftArrow);
            Assert.That(menu.Tab,Is.EqualTo(includeCapabilities?PartyMenuTab.Capabilities:PartyMenuTab.Stats));
            yield return PressNavigation(keyboard,Key.RightArrow);
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Inventory),"Right wraps from the last available tab to Inventory.");
            menu.InventoryTab.Select();
            foreach(var expected in includeCapabilities
                ? new[]{PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats,PartyMenuTab.Capabilities,PartyMenuTab.Inventory}
                : new[]{PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats,PartyMenuTab.Inventory})
            {
                yield return PressNavigation(keyboard,Key.RightArrow);
                Assert.That(menu.Tab,Is.EqualTo(expected));
                Assert.That(owner.Current,Is.SameAs(menu));
                Assert.That(owner.Stack.Count,Is.EqualTo(1));
                Assert.That(menu.Hero.Id,Is.EqualTo(hero));
            }
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.left});yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            Assert.That(menu.Tab,Is.EqualTo(includeCapabilities?PartyMenuTab.Capabilities:PartyMenuTab.Stats),"The controller stick also cycles tabs.");

            menu.SwitchTab(PartyMenuTab.Inventory);yield return null;
            float delay=module.moveRepeatDelay,rate=module.moveRepeatRate;
            try
            {
                module.moveRepeatDelay=.5f;module.moveRepeatRate=10;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;yield return null;
                Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Equipment));
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Equipment),"A held direction waits for the navigation repeat delay.");
                yield return harness.WaitUntil(()=>menu.Tab==PartyMenuTab.Skills,"held horizontal tab repeat");
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Skills),"Releasing horizontal input stops cycling.");
            }
            finally
            {
                module.moveRepeatDelay=delay;module.moveRepeatRate=rate;
            }

            menu.SwitchTab(PartyMenuTab.Equipment);yield return null;
            menu.EntryButtons[0].Select();
            yield return PressNavigation(keyboard,Key.DownArrow);
            Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Equipment));
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(menu.EntryButtons[1].gameObject));
            var last=menu.EntryButtons.Last();last.Select();
            yield return PressNavigation(keyboard,Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(last.gameObject),"Down at the last entry never targets a removed close button.");
            yield return PressNavigation(keyboard,Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(menu.EntryButtons[menu.EntryButtons.Count-2].gameObject));
            menu.EntryButtons[0].Select();
            yield return PressNavigation(keyboard,Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(menu.EquipmentTab.gameObject));
            yield return PressNavigation(keyboard,Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(menu.EntryButtons[0].gameObject));
            Assert.That(menu.Hero.Id,Is.EqualTo(hero));
        }

        private static IEnumerator PressNavigation(Keyboard keyboard,Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }

        private static IEnumerator ClickHudTab(Mouse mouse,Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
            Assert.That(hits.Count,Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button),"The menu shield must not intercept its HUD header.");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;
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
