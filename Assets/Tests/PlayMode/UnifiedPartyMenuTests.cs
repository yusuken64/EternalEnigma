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

        [UnityTest]public IEnumerator EquipmentStrategyAppliesOnlyToTheControlledAllyWithoutSpendingActions()
        {
            yield return harness.LoadDungeon(new TestScenario {AdditionalAllies=new[]{"Avery"}});
            var first=harness.Ally;
            var second=harness.Game.Allies.Single(actor=>actor!=first);
            first.AllyStrategy=AllyStrategy.Aggresive;second.AllyStrategy=AllyStrategy.Follow;
            int actions=first.Vitals.ActionsPerTurnLeft, secondActions=second.Vitals.ActionsPerTurnLeft;
            var manager=MenuManager.Instance;
            manager.OpenPartyMenu(PartyMenuTab.Equipment);yield return null;
            var menu=manager.PartyMenu;
            Button StrategyRow()=>menu.EntryButtons.Single(button=>button.GetComponent<AuthoredButton>().Label.text==AllyStrategyInfo.Name(menu.Hero.DungeonActor.AllyStrategy));
            var row=StrategyRow();
            Assert.That(row.GetComponent<Image>().sprite,Is.EqualTo(menu.EntryButtons[0].GetComponent<Image>().sprite),"Strategy uses the equipment slot styling.");
            Assert.That(row.GetComponent<AuthoredButton>().Label.text,Does.Contain("Aggressive"));
            row.Select();yield return new WaitForSecondsRealtime(.2f);
            Directory.CreateDirectory("Temp/StrategyMenu");
            ScreenCapture.CaptureScreenshot("Temp/StrategyMenu/equipment-slot.png");yield return new WaitForEndOfFrame();
            row.onClick.Invoke();yield return null;
            var picker=(PartyMenuPicker)manager.CurrentDialog;
            var choices=picker.Rows.GetComponentsInChildren<AuthoredButton>();
            Assert.That(choices.Select(choice=>choice.Label.text),Is.EqualTo(new[]{"Follow","Aggressive","Hold Position"}));
            yield return harness.WaitUntil(()=>EventSystem.current.currentSelectedGameObject==choices[1].gameObject,"current strategy receives deferred dialog focus");
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(choices[1].gameObject),"Focus starts on this ally's current strategy.");
            Assert.That(picker.OptionDescription.text,Does.Contain("Pursue visible enemies"));
            ScreenCapture.CaptureScreenshot("Temp/StrategyMenu/strategy-options.png");yield return new WaitForEndOfFrame();
            choices[0].Button.onClick.Invoke();yield return null;
            Assert.That(manager.CurrentDialog,Is.SameAs(menu));
            Assert.That(first.AllyStrategy,Is.EqualTo(AllyStrategy.Follow));
            Assert.That(first.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));
            Assert.That(StrategyRow().GetComponent<AuthoredButton>().Label.text,Does.Contain("Follow"));
            StrategyRow().onClick.Invoke();yield return null;
            ((PartyMenuPicker)manager.CurrentDialog).CloseDialog();yield return null;
            Assert.That(first.AllyStrategy,Is.EqualTo(AllyStrategy.Follow),"Cancelling does not change strategy.");
            menu.BrowseHero(1);yield return null;
            Assert.That(harness.Game.PlayerController.ControlledAlly,Is.SameAs(second));
            Assert.That(menu.Hero.DungeonActor,Is.SameAs(second));
            StrategyRow().onClick.Invoke();yield return null;
            picker=(PartyMenuPicker)manager.CurrentDialog;
            picker.Rows.GetComponentsInChildren<AuthoredButton>().Single(choice=>choice.Label.text=="Hold Position").Button.onClick.Invoke();
            yield return null;
            Assert.That(second.AllyStrategy,Is.EqualTo(AllyStrategy.HoldPosition));
            Assert.That(first.AllyStrategy,Is.EqualTo(AllyStrategy.Follow));
            Assert.That(second.Vitals.ActionsPerTurnLeft,Is.EqualTo(secondActions));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn,Is.False);
            manager.CloseAllMenus();yield return null;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;yield return null;
            Assert.That(manager.Opened,Is.False,"E no longer opens an ally menu.");
        }

        [UnityTest]public IEnumerator TownPassivesAppearOnlyInStatsWithRanksAndDescriptions()
        {
            yield return harness.LoadTown(new TestScenario {Skills=new[]{"Damage","Strength Up"}}.CreateSave());
            var town=Object.FindFirstObjectByType<Town>();
            var hero=town.TownPlayer.ControllingTownAlly;
            hero.SetRank("Strength Up",3);
            var context=new TownPartyMenuContext(town);
            var inspected=context.Heroes.Single(entry=>entry.Id==hero.Id);
            AssertPassiveSeparation(context,inspected);
            context.Dispose();
            var manager=Object.FindFirstObjectByType<TownMenuManager>();
            manager.OpenPartyMenu(PartyMenuTab.Skills);yield return null;
            var menu=manager.PartyMenu;
            Assert.That(menu.EntryButtons.Any(button=>button.GetComponent<AuthoredButton>().Label.text.Contains("Strength Up")),Is.False);
            Directory.CreateDirectory("Temp/MenuFeedback");
            ScreenCapture.CaptureScreenshot("Temp/MenuFeedback/skills-active-only.png");
            yield return new WaitForSecondsRealtime(.3f);
            menu.SwitchTab(PartyMenuTab.Stats);yield return null;
            var passive=menu.EntryButtons.Single(button=>button.GetComponent<AuthoredButton>().Label.text.Contains("Strength Up"));
            Assert.That(passive.GetComponent<AuthoredButton>().Label.text,Does.Contain("Rank 3"));
            passive.Select();yield return new WaitForSecondsRealtime(.3f);
            AssertDetachedSelectionLayout(menu);
            Assert.That(menu.Details.isActiveAndEnabled,Is.True);
            Assert.That(menu.Details.text,Does.Contain("Strength Up").And.Contain("always active"));
            menu.scrollView.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
            ScreenCapture.CaptureScreenshot("Temp/MenuFeedback/stats-passives.png");
            yield return new WaitForSecondsRealtime(.3f);
            passive.onClick.Invoke();yield return null;
            Assert.That(manager.CurrentDialog,Is.SameAs(menu),"Confirming a passive does not open another details screen.");
        }

        [UnityTest]public IEnumerator DungeonPassivesAppearOnlyInStatsWithoutSpendingActions()
        {
            yield return harness.LoadDungeon(new TestScenario {Skills=new[]{"Damage","Strength Up"}});
            var actor=harness.Ally;
            var context=new DungeonPartyMenuContext(harness.Game);
            var hero=context.Heroes.Single(entry=>entry.DungeonActor==actor);
            AssertPassiveSeparation(context,hero);context.Dispose();
            int actions=actor.Vitals.ActionsPerTurnLeft;
            MenuManager.Instance.OpenPartyMenu(PartyMenuTab.Stats);yield return null;
            var menu=MenuManager.Instance.PartyMenu;
            var passive=menu.EntryButtons.Single(button=>button.GetComponent<AuthoredButton>().Label.text.Contains("Strength Up"));
            passive.Select();yield return null;
            AssertDetachedSelectionLayout(menu);
            Assert.That(menu.Details.isActiveAndEnabled,Is.True);
            Assert.That(menu.Details.text,Does.Contain("Strength Up").And.Contain("always active"));
            passive.onClick.Invoke();yield return null;
            Assert.That(MenuManager.Instance.CurrentDialog,Is.SameAs(menu));
            Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));
        }

        private static void AssertPassiveSeparation(PartyMenuContext context,PartyMenuHero hero)
        {
            var skills=context.Entries(hero,PartyMenuTab.Skills);
            Assert.That(skills,Is.Not.Empty);
            Assert.That(skills.All(entry=>entry.Skill.ActivationType==ActivationType.Active),Is.True);
            var passive=context.Entries(hero,PartyMenuTab.Stats).Single(entry=>entry.Skill?.SkillName=="Strength Up");
            Assert.That(passive.Section,Is.EqualTo("Passive skills"));
            Assert.That(passive.Description,Does.Contain(passive.Skill.Description));
            Assert.That(context.Actions(hero,passive),Is.Empty);
        }

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
            definition.Description="Restores 5 HP.";
            definition.ItemEffectDefinition=effect;definition.Targeting=SkillTargeting.SelectedTarget;
            definition.TargetSelector=new TargetSelector{Team=TargetTeam.Self,Area=TargetArea.Self};
            var item=definition.AsInventoryItem(null);town.TownPlayer.Inventory.Add(item);

            yield return PressNavigation(keyboard,Key.Q);
            Assert.That(menu.EntryButtons.Count,Is.EqualTo(1));
            Assert.That(menu.Details.isActiveAndEnabled,Is.True,"Selecting an inventory item exposes its description before confirmation.");
            Assert.That(menu.Details.GetParsedText(),Does.Contain(definition.Description));
            Assert.That(hero.Hp,Is.EqualTo(1),"Reading an item must not use it.");
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
            Assert.That(Object.FindFirstObjectByType<GameMessages>().History,Is.Not.Empty,"Party actions report their result in the visible event feed.");

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

        [UnityTest]public IEnumerator InventorySelectionExplainsBreadBeforeOpeningActions()
        {
            yield return harness.LoadDungeon(new TestScenario {AllyName="Avery",Items=new[]{"Bread","Oblivion Draught","Arcane Wand","Frailty Potion","Explosion Spell","Bow","Wooden Buckler","Wooden Arrows","Antidote","Mana Potion"}});
            var keyboard=InputSystem.AddDevice<Keyboard>();
            MenuUIInputModule.Active.actionsAsset.devices=new InputDevice[]{keyboard};
            var manager=MenuManager.Instance;
            var actor=harness.Ally;
            actor.Vitals.Hunger=20;
            actor.SyncDisplayedStats();
            int actions=actor.Vitals.ActionsPerTurnLeft;
            var bag=harness.Game.PlayerController.Inventory.InventoryItems;
            var bread=bag.Single(item=>item.ItemName=="Bread");
            var other=bag.Single(item=>item.ItemName=="Oblivion Draught");
            int food=((ModifyStatsItemEffectDefinition)bread.ItemDefinition.ItemEffectDefinition).VitalModification.Hunger;
            manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
            var menu=manager.PartyMenu;
            Assert.That(menu.Details.isActiveAndEnabled,Is.True);
            Assert.That(menu.Details.GetParsedText(),Does.Contain($"Restores {food} Food"));
            Assert.That(manager.CurrentDialog,Is.SameAs(menu),"The effect is readable before the action chooser opens.");

            var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var sizeProperty=view.GetType().GetProperty("selectedSizeIndex",flags);
            var oldSize=sizeProperty.GetValue(view);
            try
            {
                Directory.CreateDirectory("Temp/InventoryDescriptions");
                foreach(var size in new[]{new Vector2Int(960,600),new Vector2Int(1280,720)})
                {
                    view.GetType().GetMethod("SetCustomResolution",flags).Invoke(view,new object[]{new Vector2(size.x,size.y),"Inventory descriptions"});
                    yield return null;Canvas.ForceUpdateCanvases();yield return null;
                    AssertDetachedSelectionLayout(menu);
                    AssertTenRowsFit(menu);
                    ScreenCapture.CaptureScreenshot($"Temp/InventoryDescriptions/bread-{size.x}x{size.y}.png");
                    yield return new WaitForSecondsRealtime(.25f);
                }
            }
            finally {sizeProperty.SetValue(view,oldSize);}

            yield return PressNavigation(keyboard,Key.DownArrow);
            Assert.That(menu.Details.GetParsedText(),Does.Contain(other.ItemDefinition.Description));
            Assert.That(menu.Details.GetParsedText(),Does.Not.Contain(bread.ItemDefinition.Description));
            Assert.That(manager.CurrentDialog,Is.SameAs(menu),"Browsing changes the description without opening actions.");
            for(int i=1;i<menu.EntryButtons.Count;i++)yield return PressNavigation(keyboard,Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(menu.EntryButtons[^1].gameObject),"Down stops at the last item instead of entering the description popup.");
            Assert.That(MenuUIInputModule.IsUsable(menu.DetailsControl.gameObject),Is.False,"The inventory description is display-only.");
            for(int i=1;i<menu.EntryButtons.Count;i++)yield return PressNavigation(keyboard,Key.UpArrow);
            Assert.That(menu.Details.GetParsedText(),Does.Contain(bread.ItemDefinition.Description));
            Assert.That(bag.Count,Is.EqualTo(10));
            Assert.That(actor.Vitals.Hunger,Is.EqualTo(20));
            Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));
            yield return PressNavigation(keyboard,Key.Enter);
            Assert.That(manager.CurrentDialog,Is.SameAs(manager.ActionDialog),"Confirm still opens the existing action chooser.");
            Assert.That(bag.Contains(bread),Is.True,"Opening actions does not consume Bread.");
        }

        [UnityTest]public IEnumerator ArcaneWandAddsSixAttackOnceAndLeavesStrengthAttributeUnchanged()
        {
            yield return harness.LoadDungeon(new TestScenario {AllyName="Avery",Items=new[]{"Arcane Wand"}});
            foreach(var enemy in harness.Game.Enemies.ToArray())Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            yield return null;
            var actor=harness.Ally;
            var wand=(EquipableInventoryItem)harness.Game.PlayerController.Inventory.InventoryItems.Single(item=>item.ItemName=="Arcane Wand");
            var before=new Stats(actor.FinalStats);
            var attributes=actor.Attributes;
            int baseStrength=actor.BaseStats.Strength;
            int expectedAttack=before.Strength+6;
            string expectedComparison=$"Your Attack {before.Strength} → {expectedAttack}, change +6";
            Assert.That(wand.GetEquipmentStatModification().DescribeEffect(),Is.EqualTo(new[]{"Attack +6"}));
            var predicted=StatPreview.Final(actor,actor.Equipment.Current().With(wand));
            Assert.That(StatPreview.Diff(before,predicted),Is.EqualTo(expectedComparison));
            Assert.That(actor.FinalStats.Strength,Is.EqualTo(before.Strength),"Inspecting the preview must not apply the item.");

            var manager=MenuManager.Instance;
            manager.OpenInventoryAs(actor);
            yield return null;
            manager.PartyMenu.EntryButtons.Single().Select();
            yield return null;
            var details=manager.PartyMenu.Details.GetParsedText();
            Assert.That(details,Does.Contain("Grants +6 Attack while equipped.").And.Not.Contain("Your Attack").And.Not.Contain("strength"));
            AssertEquipmentComparison(manager.PartyMenu.Details,before.Strength,expectedAttack);
            var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var sizeProperty=view.GetType().GetProperty("selectedSizeIndex",flags);
            var oldSize=sizeProperty.GetValue(view);
            try
            {
                Directory.CreateDirectory("Temp/EquipmentComparison");
                foreach(var size in new[]{new Vector2(960,600),new Vector2(1280,720),new Vector2(2560,1080)})
                {
                    view.GetType().GetMethod("SetCustomResolution",flags).Invoke(view,new object[]{size,"Equipment comparison"});
                    yield return null;Canvas.ForceUpdateCanvases();yield return null;
                    AssertEquipmentComparison(manager.PartyMenu.Details,before.Strength,expectedAttack);
                    AssertComparisonFits(manager.PartyMenu.Details);
                    yield return AssertComparisonStaysAnchored(manager.PartyMenu.Details,manager.PartyMenu.DetailsControl.Scroll);
                    ScreenCapture.CaptureScreenshot($"Temp/EquipmentComparison/equip-{size.x}x{size.y}.png");
                    yield return new WaitForSecondsRealtime(.2f);
                }
            }
            finally{sizeProperty.SetValue(view,oldSize);}
            manager.OpenPartyMenu(PartyMenuTab.Equipment);yield return null;
            manager.PartyMenu.EntryButtons.First().onClick.Invoke();yield return null;
            var picker=(PartyMenuPicker)manager.CurrentDialog;
            picker.Rows.GetComponentsInChildren<AuthoredButton>().Single(row=>row.Label.text=="Arcane Wand").Button.Select();
            yield return null;Canvas.ForceUpdateCanvases();yield return null;
            AssertEquipmentComparison(picker.OptionDescription,before.Strength,expectedAttack);
            AssertComparisonFits(picker.OptionDescription);
            yield return AssertComparisonStaysAnchored(picker.OptionDescription,picker.OptionDescription.GetComponentInParent<ScrollRect>());
            ScreenCapture.CaptureScreenshot("Temp/EquipmentComparison/picker.png");yield return null;
            Assert.That(actor.FinalStats.Strength,Is.EqualTo(before.Strength),"Browsing the choice must not equip it.");
            picker.CloseDialog();yield return null;
            TestContext.Out.WriteLine("Before equipping: "+details);
            for(int cycle=0;cycle<2;cycle++)
            {
                yield return harness.UseItemThroughMenu(wand);
                Assert.That(actor.Equipment.EquippedWeapon,Is.SameAs(wand));
                Assert.That(actor.FinalStats.Strength,Is.EqualTo(expectedAttack));
                Assert.That(actor.DisplayedStats.Strength,Is.EqualTo(expectedAttack));
                Assert.That(StatPreview.Diff(before,actor.FinalStats),Is.EqualTo(expectedComparison));
                Assert.That(actor.BaseStats.Strength,Is.EqualTo(baseStrength));
                Assert.That(actor.Attributes,Is.EqualTo(attributes),"The item grants Attack, not six STR attribute points.");
                if(cycle==0)
                {
                    manager.OpenPartyMenu(PartyMenuTab.Equipment);
                    yield return null;
                    var menu=manager.PartyMenu;
                    menu.EntryButtons.Single(button=>button.GetComponent<AuthoredButton>().Label.text.Contains("Arcane Wand")).Select();
                    yield return null;
                    string equipmentDetails=menu.Details.GetParsedText();
                    Assert.That(equipmentDetails,Does.Contain("Grants +6 Attack while equipped.").And.Not.Contain("Item bonus").And.Not.Contain("Your Attack").And.Not.Contain("Strength"));
                    AssertEquipmentComparison(menu.Details,expectedAttack,before.Strength);
                    Assert.That(menu.Details.GetComponent<EquipmentComparisonView>().Preview.Caption,Is.EqualTo("Unequip preview"));
                    ScreenCapture.CaptureScreenshot("Temp/EquipmentComparison/unequip.png");
                    yield return null;
                    menu.SwitchTab(PartyMenuTab.Stats);yield return null;
                    Assert.That(menu.Details.GetComponent<EquipmentComparisonView>().Viewport.gameObject.activeSelf,Is.False,"Switching away clears the previous item's comparison.");
                    TestContext.Out.WriteLine("Equipped: "+equipmentDetails);
                }
                actor.Equipment.Equip(wand);
                actor.InvalidateCachedStats();
                Assert.That(actor.FinalStats.Strength,Is.EqualTo(expectedAttack),"Re-equipping the same instance must not stack its bonus.");
                yield return harness.UseItemThroughMenu(wand);
                Assert.That(actor.Equipment.IsEquipped(wand),Is.False);
                Assert.That(actor.FinalStats.Strength,Is.EqualTo(before.Strength));
                Assert.That(actor.DisplayedStats.Strength,Is.EqualTo(before.Strength));
                Assert.That(actor.Attributes,Is.EqualTo(attributes));
                TestContext.Out.WriteLine($"Cycle {cycle+1}: Attack {before.Strength} -> {expectedAttack} -> {before.Strength}; base {baseStrength} unchanged; STR {actor.Attributes.Str} unchanged.");
            }
        }

        [UnityTest]public IEnumerator EquipmentDescriptionAndProficiencyFitAboveFixedPreview()
        {
            yield return harness.LoadDungeon(new TestScenario {AllyName="Avery",Items=new[]{"Arcane Wand","Wooden Arrows"}});
            var manager=MenuManager.Instance;
            manager.OpenInventoryAs(harness.Ally);yield return null;
            var menu=manager.PartyMenu;
            float normalFontSize=menu.Details.fontSize;
            var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var sizeProperty=view.GetType().GetProperty("selectedSizeIndex",flags);
            var oldSize=sizeProperty.GetValue(view);
            try
            {
                Directory.CreateDirectory("Temp/EquipmentComparison");
                foreach(var size in new[]{new Vector2(960,600),new Vector2(1280,720),new Vector2(2560,1080)})
                {
                    view.GetType().GetMethod("SetCustomResolution",flags).Invoke(view,new object[]{size,"Equipment description fit"});
                    menu.EntryButtons.Single(button=>button.GetComponent<AuthoredButton>().Label.text.StartsWith("Arcane Wand")).Select();
                    yield return null;Canvas.ForceUpdateCanvases();yield return null;
                    var comparison=menu.Details.GetComponent<EquipmentComparisonView>();
                    var canvas=(RectTransform)menu.GetComponent<Canvas>().transform;
                    var before=VisibleComparisonRect(canvas,comparison.Viewport);
                    menu.EntryButtons.Single(button=>button.GetComponent<AuthoredButton>().Label.text.StartsWith("Wooden Arrows")).Select();
                    yield return null;Canvas.ForceUpdateCanvases();yield return null;
                    ScreenCapture.CaptureScreenshot($"Temp/EquipmentComparison/arrows-{size.x}x{size.y}.png");
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(menu.Details.GetParsedText(),Does.Contain("Wooden arrows for bows.").And.Contain("proficiency").And.Contain("cannot equip this item."));
                    AssertEquipmentDescriptionFits(menu.Details,menu.DetailsControl.Scroll);
                    AssertComparisonFits(menu.Details);
                    var after=VisibleComparisonRect(canvas,comparison.Viewport);
                    Assert.That(Vector3.Distance(before.center,after.center),Is.LessThan(.1f),"The longer restriction text must not move the stat preview.");
                }
                menu.SwitchTab(PartyMenuTab.Equipment);yield return null;
                menu.EntryButtons[1].onClick.Invoke();yield return null;
                var picker=(PartyMenuPicker)manager.CurrentDialog;
                picker.Rows.GetComponentsInChildren<AuthoredButton>().Single(row=>row.Label.text.StartsWith("Wooden Arrows")).Button.Select();
                yield return null;Canvas.ForceUpdateCanvases();yield return null;
                AssertEquipmentDescriptionFits(picker.OptionDescription,picker.OptionDescription.GetComponentInParent<ScrollRect>());
                picker.CloseDialog();yield return null;
                menu.SwitchTab(PartyMenuTab.Stats);yield return null;
                Assert.That(menu.Details.fontSize,Is.EqualTo(normalFontSize),"Description fitting must not shrink other tabs.");
            }
            finally{sizeProperty.SetValue(view,oldSize);}
        }

        private static void AssertEquipmentDescriptionFits(TMPro.TMP_Text description,ScrollRect scroll)
        {
            description.ForceMeshUpdate();
            Assert.That(description.preferredHeight,Is.LessThanOrEqualTo(scroll.viewport.rect.height+.5f),
                $"The full item description and class restriction must fit above the stats. Font {description.fontSize}, text rect {description.rectTransform.rect.size}, viewport {scroll.viewport.rect.size}. Text: {description.text}");
            Assert.That(description.fontSize,Is.GreaterThanOrEqualTo(20),"Fitting must retain readable text.");
            var canvas=(RectTransform)description.GetComponentInParent<Canvas>().transform;
            var textBounds=VisibleComparisonRect(canvas,description.rectTransform);
            var visibleBounds=VisibleComparisonRect(canvas,scroll.viewport);
            Assert.That(textBounds.min.y,Is.GreaterThanOrEqualTo(visibleBounds.min.y-.5f));
            Assert.That(textBounds.max.y,Is.LessThanOrEqualTo(visibleBounds.max.y+.5f));
        }

        private static void AssertEquipmentComparison(TMPro.TMP_Text description,int before,int after)
        {
            var comparison=description.GetComponent<EquipmentComparisonView>();
            Assert.That(comparison,Is.Not.Null);
            Assert.That(comparison.Preview,Is.Not.Null);
            var attack=comparison.Preview.Values.Single(value=>value.Label=="Attack");
            Assert.That(attack.Before,Is.EqualTo(before));Assert.That(attack.After,Is.EqualTo(after));
            Assert.That(attack.DeltaText,Is.EqualTo(after>before?"+6":"−6"));
            var stat=comparison.Strip.GetChild(1);
            Assert.That(stat.Find("Before").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo(before.ToString()));
            var final=stat.Find("After").GetComponent<TMPro.TMP_Text>();
            var delta=stat.Find("Change").GetComponent<TMPro.TMP_Text>();
            Assert.That(final.text,Is.EqualTo(after.ToString()));
            Assert.That(delta.text,Is.EqualTo(attack.DeltaText));
            Assert.That(delta.color,Is.EqualTo(after>before?EquipmentComparisonView.Increase:EquipmentComparisonView.Decrease));
            Assert.That(final.color,Is.EqualTo(delta.color));
            foreach(var label in comparison.Strip.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False,label.text);
            }
        }
        private static void AssertComparisonFits(TMPro.TMP_Text description)
        {
            var comparison=description.GetComponent<EquipmentComparisonView>();
            var strip=comparison.Strip;
            var viewport=comparison.Viewport;
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,strip);
            Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(viewport.rect.yMin-1),"The whole stat strip should be visible without scrolling.");
            Assert.That(bounds.max.y,Is.LessThanOrEqualTo(viewport.rect.yMax+1));
            Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(viewport.rect.xMin-1));
            Assert.That(bounds.max.x,Is.LessThanOrEqualTo(viewport.rect.xMax+1));
        }

        private static IEnumerator AssertComparisonStaysAnchored(TMPro.TMP_Text description,ScrollRect descriptionScroll)
        {
            var comparison=description.GetComponent<EquipmentComparisonView>();
            Assert.That(comparison.Strip.IsChildOf(description.transform),Is.False,"Stats have their own text objects outside the item description.");
            Assert.That(comparison.Viewport.anchorMin.y,Is.Zero);
            Assert.That(comparison.Viewport.anchorMax.y,Is.Zero);
            var canvas=(RectTransform)description.GetComponentInParent<Canvas>().transform;
            var before=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,comparison.Strip);
            var original=description.text;
            try
            {
                description.text=original+"\n\n"+string.Join("\n",Enumerable.Repeat("A long item description stays in its own scrolling area.",12));
                Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();yield return null;
                var after=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,comparison.Strip);
                Assert.That(Vector3.Distance(before.center,after.center),Is.LessThan(.1f),"Changing item text must not move the comparison.");
                Assert.That(descriptionScroll.viewport.GetComponent<RectMask2D>(),Is.Not.Null,"The text area clips its scrolling content.");
                var textBounds=VisibleComparisonRect(canvas,descriptionScroll.viewport);
                var previewBounds=VisibleComparisonRect(canvas,comparison.Viewport);
                Assert.That(textBounds.min.y,Is.GreaterThan(previewBounds.max.y),"Long text is clipped above the preview, with a visible gap.");
                descriptionScroll.verticalNormalizedPosition=0;
                yield return null;
                var scrolled=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,comparison.Strip);
                Assert.That(Vector3.Distance(before.center,scrolled.center),Is.LessThan(.1f),"Scrolling the item text must not scroll the stats.");
            }
            finally
            {
                description.text=original;
                Canvas.ForceUpdateCanvases();descriptionScroll.verticalNormalizedPosition=1;
            }
            yield return null;
        }

        private static Bounds VisibleComparisonRect(RectTransform root,RectTransform rect)
        {
            // Measure the viewport itself, excluding the intentionally taller scrolling children.
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var bounds=new Bounds(root.InverseTransformPoint(corners[0]),Vector3.zero);
            for(int i=1;i<corners.Length;i++)bounds.Encapsulate(root.InverseTransformPoint(corners[i]));
            return bounds;
        }

        [UnityTest]public IEnumerator SkillSelectionExplainsBeforeCastingWithoutTakingDescriptionFocus()
        {
            yield return harness.LoadDungeon(new TestScenario {AllyName="Avery",Mana=30,
                Skills=new[]{"Fire Bolt","Ice Bolt","Lightning Bolt","Fireball","Blizzard","Thunderclap","Inferno","Glacier","Tempest","Amplify"}});
            var keyboard=InputSystem.AddDevice<Keyboard>();
            MenuUIInputModule.Active.actionsAsset.devices=new InputDevice[]{keyboard};
            var manager=MenuManager.Instance;
            var actor=harness.Ally;
            int actions=actor.Vitals.ActionsPerTurnLeft, mana=actor.Vitals.Mana;
            var first=actor.Skills.Single(skill=>skill.SkillName=="Fire Bolt");
            var second=actor.Skills.Single(skill=>skill.SkillName=="Ice Bolt");
            manager.OpenPartyMenu(PartyMenuTab.Skills);yield return null;
            var menu=manager.PartyMenu;
            Assert.That(menu.Details.isActiveAndEnabled,Is.True);
            Assert.That(menu.Details.GetParsedText(),Does.Contain(first.Description));
            Assert.That(menu.Details.GetParsedText(),Does.Contain(first.CostText(actor)));
            Assert.That(manager.CurrentDialog,Is.SameAs(menu));

            var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var sizeProperty=view.GetType().GetProperty("selectedSizeIndex",flags);
            var oldSize=sizeProperty.GetValue(view);
            try
            {
                Directory.CreateDirectory("Temp/SkillDescriptions");
                foreach(var size in new[]{new Vector2Int(960,600),new Vector2Int(1280,720)})
                {
                    view.GetType().GetMethod("SetCustomResolution",flags).Invoke(view,new object[]{new Vector2(size.x,size.y),"Skill descriptions"});
                    yield return null;Canvas.ForceUpdateCanvases();yield return null;
                    AssertDetachedSelectionLayout(menu);
                    AssertTenRowsFit(menu);
                    ScreenCapture.CaptureScreenshot($"Temp/SkillDescriptions/skills-{size.x}x{size.y}.png");
                    yield return new WaitForSecondsRealtime(.25f);
                }
            }
            finally {sizeProperty.SetValue(view,oldSize);}

            yield return PressNavigation(keyboard,Key.DownArrow);
            Assert.That(menu.Details.GetParsedText(),Does.Contain(second.Description));
            Assert.That(menu.Details.GetParsedText(),Does.Not.Contain(first.Description));
            for(int i=1;i<menu.EntryButtons.Count;i++)yield return PressNavigation(keyboard,Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(menu.EntryButtons[^1].gameObject),"Down stops at the last skill.");
            Assert.That(MenuUIInputModule.IsUsable(menu.DetailsControl.gameObject),Is.False,"Skill descriptions cannot take focus.");
            Assert.That(manager.CurrentDialog,Is.SameAs(menu),"Browsing skills does not cast or open targeting.");
            Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));
            Assert.That(actor.Vitals.Mana,Is.EqualTo(mana));
            for(int i=1;i<menu.EntryButtons.Count;i++)yield return PressNavigation(keyboard,Key.UpArrow);
            yield return PressNavigation(keyboard,Key.Enter);
            Assert.That(manager.CurrentDialog,Is.SameAs(manager.TargetDialog),"Confirm still opens skill targeting.");
            Assert.That(actor.Vitals.Mana,Is.EqualTo(mana));
        }

        [UnityTest]public IEnumerator HistoryShortcutTogglesAcrossPresentationsAndRespectsRebinding()
        {
            bool originalTerminal=TerminalMode.Requested;
            var preferences=GamePreferences.Isolate();
            try
            {
                ControlBindings.Reload();
                TerminalMode.SetRequested(false);
                var keyboard=InputSystem.AddDevice<Keyboard>();
                var mouse=InputSystem.AddDevice<Mouse>();
                yield return harness.LoadDungeon(new TestScenario {Items=new[]{"Bread"}});
                var playerInput=Common.Instance.MenuInputHandler.PlayerInput;
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse);
                Assert.That(playerInput.notificationBehavior,Is.EqualTo(PlayerNotifications.SendMessages));
                Assert.That(playerInput.devices.Contains(keyboard),Is.True,"Exercise the same PlayerInput callbacks as a physical keyboard.");
                var module=MenuUIInputModule.Active;
                module.actionsAsset.devices=new InputDevice[]{keyboard,mouse};
                var manager=MenuManager.Instance;
                int actions=harness.Ally.Vitals.ActionsPerTurnLeft;
                var position=harness.Ally.TilemapPosition;
                GameMessages.Post("The party is ready to explore.");
                var feed=Object.FindFirstObjectByType<GameMessages>().GetComponentInChildren<CanvasGroup>();
                foreach(bool terminal in new[]{false,true})
                {
                    TerminalMode.SetRequested(terminal);
                    yield return new WaitForSecondsRealtime(.2f);
                    yield return PressNavigation(keyboard,Key.H);
                    var history=manager.CurrentDialog as EventHistoryDialog;
                    Assert.That(history,Is.Not.Null,"H opens full History in both presentations.");
                    Assert.That(history.Entries.text,Does.Contain("The party is ready to explore."));
                    AssertExpandedHistoryLayout(history);
                    if(!terminal)
                    {
                        var safeArea=history.GetComponentInChildren<SafeAreaPanel>();
                        var safeRect=(RectTransform)safeArea.transform;
                        var safeMin=safeRect.anchorMin;var safeMax=safeRect.anchorMax;
                        safeArea.enabled=false;
                        try
                        {
                            safeRect.anchorMin=new Vector2(.08f,.04f);safeRect.anchorMax=new Vector2(.94f,.96f);
                            AssertExpandedHistoryLayout(history);
                        }
                        finally {safeRect.anchorMin=safeMin;safeRect.anchorMax=safeMax;safeArea.enabled=true;}
                        var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                        var sizeProperty=view.GetType().GetProperty("selectedSizeIndex",flags);
                        var oldSize=sizeProperty.GetValue(view);
                        try
                        {
                            history.Setup(Enumerable.Range(1,40).Select(i=>$"Event {i}: the party explores the dungeon.").ToArray());
                            Directory.CreateDirectory("Temp/HistoryShortcut");
                            foreach(var size in new[]{new Vector2Int(960,600),new Vector2Int(1280,720)})
                            {
                                view.GetType().GetMethod("SetCustomResolution",flags).Invoke(view,new object[]{new Vector2(size.x,size.y),"Expanded history"});
                                yield return null;Canvas.ForceUpdateCanvases();yield return null;
                                AssertExpandedHistoryLayout(history);
                                Assert.That(history.scrollView.verticalNormalizedPosition,Is.EqualTo(0).Within(.001f),"The newest events remain visible after resizing.");
                                ScreenCapture.CaptureScreenshot($"Temp/HistoryShortcut/expanded-{size.x}x{size.y}.png");
                                yield return new WaitForSecondsRealtime(.25f);
                            }
                            float before=history.scrollView.verticalNormalizedPosition;
                            yield return PressNavigation(keyboard,Key.UpArrow);
                            Assert.That(history.scrollView.verticalNormalizedPosition,Is.GreaterThan(before));
                        }
                        finally {sizeProperty.SetValue(view,oldSize);}
                    }
                    yield return PressNavigation(keyboard,Key.H);
                    Assert.That(manager.Opened,Is.False,"H closes History without reopening it in terminal mode.");
                    Assert.That(feed.alpha,Is.EqualTo(1).Within(.001f),"Collapsing History restores the small feed.");
                    Assert.That(feed.blocksRaycasts,Is.True);

                    yield return PressNavigation(keyboard,Key.H);
                    yield return ClickHudTab(mouse,history.Back);
                    Assert.That(manager.Opened,Is.False,"The Events / History button collapses the taller panel.");
                    Assert.That(feed.alpha,Is.EqualTo(1).Within(.001f),"Mouse collapse restores the small feed too.");

                    manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
                    var menu=manager.PartyMenu;
                    yield return PressNavigation(keyboard,Key.I);
                    Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Equipment));AssertEventFeedWidth();
                    yield return PressNavigation(keyboard,Key.T);
                    Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Stats));AssertEventFeedWidth();
                    yield return PressNavigation(keyboard,Key.Q);
                    Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Inventory));AssertEventFeedWidth();
                    yield return PressNavigation(keyboard,Key.F11);
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(TerminalMode.Effective,Is.EqualTo(!terminal));AssertEventFeedWidth();
                    yield return PressNavigation(keyboard,Key.F11);
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(TerminalMode.Effective,Is.EqualTo(terminal));AssertEventFeedWidth();
                    var selected=EventSystem.current.currentSelectedGameObject;
                    yield return PressNavigation(keyboard,Key.H);
                    Assert.That(manager.CurrentDialog,Is.SameAs(history));
                    yield return PressNavigation(keyboard,Key.H);
                    Assert.That(manager.CurrentDialog,Is.SameAs(menu));
                    Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(selected));
                    Assert.That(feed.alpha,Is.EqualTo(1).Within(.001f));
                    Assert.That(((RectTransform)feed.transform).anchorMax,Is.EqualTo(new Vector2(.775f,.20f)));
                    yield return PressNavigation(keyboard,Key.Escape);

                    var settings=Common.Instance.GlobalSettings;
                    settings.ShowDialog();yield return null;
                    yield return PressNavigation(keyboard,Key.H);
                    Assert.That(module.OwnsFocus(settings),Is.True,"The shortcut must not interrupt settings or binding capture.");
                    settings.Exit_Clicked();yield return null;
                }
                ControlBindings.ApplyBinding("Player/History",0,"<Keyboard>/j");
                yield return null;yield return null;
                yield return PressNavigation(keyboard,Key.H);
                Assert.That(manager.Opened,Is.False,"The old terminal-only H handler must not bypass rebinding.");
                yield return PressNavigation(keyboard,Key.J);
                Assert.That(manager.CurrentDialog,Is.TypeOf<EventHistoryDialog>());
                Assert.That(InputPrompts.History,Does.Contain("J"));
                yield return PressNavigation(keyboard,Key.Escape);
                Assert.That(manager.Opened,Is.False);
                Assert.That(harness.Ally.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));
                Assert.That(harness.Ally.TilemapPosition,Is.EqualTo(position));
            }
            finally
            {
                TerminalMode.SetRequested(originalTerminal);
                preferences.Dispose();
                ControlBindings.Reload();
            }
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
            Assert.That(stats.HPMax,Is.GreaterThan(5));caster.Hp=1;caster.Mana=-1;
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
            var skill=Asset<Skill>();skill.SkillName="Town test heal";skill.ManaCost=2;skill.Targeting=SkillTargeting.Self;
            skill.TargetSelector=new TargetSelector{Team=TargetTeam.Self,Area=TargetArea.Self};skill.ActionEffects=new(){new ScaledHealAction{BaseHeal=3,PerLevel=1}};
            caster.SetRank(skill.SkillName,3);caster.Hp=1;caster.Mana=0;caster.Level=5;
            int saves=0;var service=new TownUtilityService(town.TownPlayer.RecruitedAllies,town.TownPlayer.Inventory,()=>saves++);
            Assert.That(service.Execute(caster,skill,null,caster,null,out _),Is.False);Assert.That(caster.Hp,Is.EqualTo(1));Assert.That(saves,Is.Zero);
            caster.Mana=-1;skill.ActionEffects.Add(new RestoreManaAction{Amount=-1});
            Assert.That(service.Execute(caster,skill,null,caster,null,out _),Is.False);Assert.That(caster.Mana,Is.EqualTo(-1));
            skill.ActionEffects.RemoveAt(1);Assert.That(service.Execute(caster,skill,null,caster,null,out _),Is.True);
            Assert.That(caster.Hp,Is.GreaterThan(4));Assert.That(saves,Is.EqualTo(1));
        }

        [UnityTest]public IEnumerator TownPartyRecoveryAndInventoryFiltersValidateBeforeCharging()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town=Object.FindFirstObjectByType<Town>();var caster=town.TownPlayer.RecruitedAllies[0];
            var ally=Object.Instantiate(caster,town.transform);ally.Id="utility-target";town.TownPlayer.RecruitedAllies.Add(ally);
            var skill=Asset<Skill>();skill.SkillName="Party Mana recovery";skill.ManaCost=2;skill.Targeting=SkillTargeting.AllTargets;
            skill.TargetSelector=new TargetSelector{Team=TargetTeam.Allies,Area=TargetArea.All};
            skill.ActionEffects=new(){new RestoreManaAction{Amount=5,ExcludeCaster=true}};
            caster.SetRank(skill.SkillName,1);caster.Hp=-1;caster.Mana=-1;ally.Hp=0;ally.Mana=0;
            int saves=0;var service=new TownUtilityService(town.TownPlayer.RecruitedAllies,town.TownPlayer.Inventory,()=>saves++);
            Assert.That(service.Execute(caster,skill,null,null,null,out _),Is.False);Assert.That(caster.Mana,Is.EqualTo(-1));
            ally.Hp=-1;Assert.That(service.Execute(caster,skill,null,null,null,out _),Is.True);
            Assert.That(ally.Mana,Is.EqualTo(Mathf.Min(5,TownUtilityService.StatsFor(ally).ManaMax)));
            Assert.That(caster.Mana,Is.EqualTo(TownUtilityService.StatsFor(caster).ManaMax-2));Assert.That(saves,Is.EqualTo(1));

            var equipment=Asset<EquipmentItemDefinition>();equipment.ItemName="Inspected weapon";equipment.EquipmentSlot=EquipmentSlot.MainHand;
            var equipped=new EquipableInventoryItem(equipment);caster.Equipment.Equip(equipped);
            var inspect=Asset<Skill>();inspect.SkillName="Town inspection";inspect.ManaCost=1;inspect.Targeting=SkillTargeting.InventoryItem;
            inspect.InventoryTargetSelector=new InventoryTargetSelector{ItemType=InventoryTargetType.Weapon,IncludeEquipped=false};
            inspect.ActionEffects=new(){new InspectInventoryItemEffect()};caster.SetRank(inspect.SkillName,1);
            int before=caster.Mana;
            Assert.That(service.Execute(caster,inspect,null,null,equipped,out _),Is.False);Assert.That(caster.Mana,Is.EqualTo(before));
            inspect.InventoryTargetSelector.IncludeEquipped=true;
            Assert.That(service.Describe(caster,inspect,null,out var utility,out _),Is.True);
            Assert.That(service.InventoryTargets(caster,utility),Does.Contain(equipped));
            Assert.That(service.Execute(caster,inspect,null,null,equipped,out var description),Is.True);
            Assert.That(description,Does.Contain(equipment.ItemName));Assert.That(caster.Mana,Is.EqualTo(before-1));
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
            harness.PrepareOpening();
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
            var audio = AudioManager.Instance;
            var equipmentCues = new List<(SoundCue cue, UnityEngine.Audio.AudioMixerGroup group)>();
            void Heard(SoundCue cue, AudioClip _, UnityEngine.Audio.AudioMixerGroup group) => equipmentCues.Add((cue, group));
            audio.CuePlayed += Heard;
            try
            {
                adapter.Actions(inspected,copies[1]).Single().Execute(manager.PartyMenu);
                adapter.Actions(inspected,copies[1]).Single().Execute(manager.PartyMenu);
                adapter.Actions(inspected,copies[1]).Single().Execute(manager.PartyMenu);
                Assert.That(equipmentCues.Where(e => e.cue == SoundCue.Equip || e.cue == SoundCue.Unequip).Select(e => e.cue),
                    Is.EqualTo(new[] { SoundCue.Equip, SoundCue.Unequip, SoundCue.Equip }));
                Assert.That(equipmentCues.Where(e => e.cue == SoundCue.Equip || e.cue == SoundCue.Unequip)
                    .All(e => e.group == audio.EffectAudioMixerGroup), Is.True);
                var missingItem = new PartyMenuEntry { Item = authored.AsInventoryItem(null) };
                adapter.Actions(inspected,missingItem).Single().Execute(manager.PartyMenu);
                Assert.That(equipmentCues.Count(e => e.cue == SoundCue.Denied), Is.EqualTo(1));
                Assert.That(equipmentCues.Single(e => e.cue == SoundCue.Denied).group, Is.SameAs(audio.UIAudioMixerGroup));
            }
            finally { audio.CuePlayed -= Heard; }
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
            original.SetRank("Strength Up",1);
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
            var equipment=Asset<EquipmentItemDefinition>();equipment.ItemName="An exceptionally long equipment name that needs several lines";
            equipment.EquipmentSlot=EquipmentSlot.MainHand;
            original.Equipment.Equip(new EquipableInventoryItem(equipment));
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
                foreach(var tab in new[]{PartyMenuTab.Inventory,PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats})
                {
                    menu.SwitchTab(tab);yield return null;Canvas.ForceUpdateCanvases();
                    if(tab==PartyMenuTab.Stats)VerifyStatsFit(menu);
                    else if(tab==PartyMenuTab.Inventory || tab==PartyMenuTab.Skills)
                    {
                        AssertDetachedSelectionLayout(menu);
                        Assert.That(menu.scrollView.vertical,Is.True,"List scrolling returns after leaving Stats.");
                        if(tab==PartyMenuTab.Inventory)Assert.That(menu.scrollView.content.rect.height,Is.GreaterThan(menu.scrollView.viewport.rect.height));
                    }
                    foreach(var label in menu.RowsRoot.GetComponentsInChildren<TMPro.TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing,Is.False,label.text+" must fit its row without truncation.");
                    }
                    if(menu.HeroText.isActiveAndEnabled){menu.HeroText.ForceMeshUpdate();Assert.That(menu.HeroText.isTextOverflowing,Is.False);}
                    camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
                    string name=tab==PartyMenuTab.Stats?"stats":tab==PartyMenuTab.Equipment?"equipment":tab==PartyMenuTab.Skills?"skills":"menu";
                    File.WriteAllBytes($"Temp/UnifiedPresentation/{name}-{size.x}x{size.y}.png",image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);
                }
                camera.targetTexture=null;
            }
            menu.SwitchTab(PartyMenuTab.Inventory);yield return null;
            Assert.That(menu.HeroesRoot.gameObject.activeSelf,Is.False);
            Assert.That(menu.HeroText.gameObject.activeSelf,Is.False);
            Assert.That(menu.DetailsControl.Scroll.gameObject.activeSelf,Is.True);
            Assert.That(menu.Hints.gameObject.activeSelf,Is.False);
            var list=(RectTransform)menu.scrollView.transform;
            Assert.That(list.anchorMin,Is.EqualTo(new Vector2(.035f,.16f)));
            Assert.That(list.anchorMax,Is.EqualTo(new Vector2(.965f,.89f)));
            var controlled=town.TownPlayer.ControllingTownAlly;menu.BrowseHero(1);Assert.That(town.TownPlayer.ControllingTownAlly,Is.SameAs(controlled));
            Assert.That(menu.HeroText.gameObject.activeSelf,Is.False,"Inventory does not repeat the selected portrait's name.");
        }

        private static void AssertDetachedSelectionLayout(PartyMenu menu)
        {
            Assert.That(menu.HeroText.gameObject.activeSelf,Is.False);
            Assert.That(menu.RowsRoot.GetComponentsInChildren<TMPro.TMP_Text>().Any(label=>label.text=="Shared bag" || label.text=="Active skills"),Is.False);
            Assert.That(menu.DetailsControl.Scroll.transform.IsChildOf(menu.Panel),Is.False,"Descriptions must not occupy list space.");
            Assert.That(MenuUIInputModule.IsUsable(menu.DetailsControl.gameObject),Is.False);
            Assert.That(((RectTransform)menu.scrollView.transform).anchorMin.y,Is.EqualTo(menu.Tab==PartyMenuTab.Stats ? .025f : .16f),
                "Stats keeps its full list; Inventory and Skills reserve their paging footer.");
            var paper=menu.GetComponentsInChildren<Transform>().Single(t=>t.name=="Description parchment");
            var root=(RectTransform)menu.GetComponent<Canvas>().transform;
            var detailBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(root,paper);
            var listBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(root,menu.Panel);
            Assert.That(detailBounds.max.x,Is.LessThan(listBounds.min.x),"A visible gap separates the description from the list.");
            Assert.That(detailBounds.min.x,Is.GreaterThanOrEqualTo(root.rect.xMin));
            Assert.That(detailBounds.max.y,Is.LessThanOrEqualTo(root.rect.yMax));
        }

        private static void AssertTenRowsFit(PartyMenu menu)
        {
            Assert.That(menu.EntryButtons.Count,Is.EqualTo(10));
            foreach(var button in menu.EntryButtons)
            {
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(menu.scrollView.viewport,button.transform);
                Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(menu.scrollView.viewport.rect.yMin-1),"All ten rows fit without scrolling.");
                Assert.That(bounds.max.y,Is.LessThanOrEqualTo(menu.scrollView.viewport.rect.yMax+1));
                var label=button.GetComponent<AuthoredButton>().Label;
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing,Is.False,label.text+" must fit its row.");
            }
        }

        private static void VerifyStatsFit(PartyMenu menu)
        {
            AssertDetachedSelectionLayout(menu);
            Assert.That(menu.Details.isActiveAndEnabled,Is.True);
            bool hasPassives=menu.RowsRoot.GetComponentsInChildren<TMPro.TMP_Text>()
                .Any(label=>label.text=="Passive skills");
            Assert.That(menu.scrollView.vertical,Is.EqualTo(hasPassives),"Learned passives extend Stats into a scrolling list.");
            Assert.That(menu.EntryButtons.Count,Is.GreaterThanOrEqualTo(19),"All stats remain present.");
            Assert.That(menu.EntryButtons.Any(button=>button.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("Lightning ")),Is.True);
            var viewport=menu.scrollView.viewport;
            if(hasPassives)Assert.That(menu.scrollView.content.rect.height,Is.GreaterThan(viewport.rect.height));
            else Assert.That(menu.scrollView.content.rect.height,Is.LessThanOrEqualTo(viewport.rect.height+1));
            foreach(RectTransform row in menu.RowsRoot)
            {
                if(!hasPassives)
                {
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,row);
                    Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(viewport.rect.yMin-1),row.name+" extends below the Stats panel.");
                    Assert.That(bounds.max.y,Is.LessThanOrEqualTo(viewport.rect.yMax+1),row.name+" extends above the Stats panel.");
                }
                foreach(var label in row.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing,Is.False,label.text+" must be fully visible.");
                }
            }
            var position=menu.scrollView.content.anchoredPosition;
            menu.scrollView.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-10)});
            if(hasPassives)Assert.That(menu.scrollView.content.anchoredPosition,Is.Not.EqualTo(position),"Mouse-wheel input can reach learned passives below the stats.");
            else Assert.That(menu.scrollView.content.anchoredPosition,Is.EqualTo(position),"Basic Stats fits without scrolling.");
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
            Assert.That(menu.Panel.GetComponentsInChildren<DungeonDialogClose>().Length,Is.EqualTo(1),"Each party panel has one visible exit.");
            Assert.That(hud.enabled,Is.True,"The original HUD tabs stay visible when the menu opens.");
            Assert.That(hud.sortingOrder,Is.GreaterThan(menu.GetComponent<Canvas>().sortingOrder));
            Assert.That(launcher.OptionsButton,Is.Not.Null,"Every gameplay scene exposes Options beside its party tabs.");
            if(Game.Instance!=null)Assert.That(launcher.OptionsButton,Is.SameAs(DungeonHud.Ensure(Game.Instance).OptionsButton),
                "Dungeon tabs reuse the existing HUD Options control.");
            if(launcher.OptionsButton!=null)
            {
                var settings=Common.Instance.GlobalSettings;
                var optionsTab=menu.Tab;
                yield return ClickHudTab(mouse,launcher.OptionsButton);
                Assert.That(settings.IsOpen,Is.True,"Pointer users can open Options from the party menu.");
                AssertEventFeedWidth();
                Assert.That(module.OwnsFocus(settings),Is.True);
                Assert.That(settings.SettingsCanvas.GetComponent<Canvas>().sortingOrder,Is.GreaterThan(hud.sortingOrder));
                Directory.CreateDirectory("Temp/UIScreenAudit");
                ScreenCapture.CaptureScreenshot("Temp/UIScreenAudit/settings-over-party-"+menu.gameObject.scene.name+".png");
                yield return new WaitForSecondsRealtime(.15f);
                if(Common.Instance.CampaignContext!=null)
                {
                    settings.MainMenu_Clicked();yield return null;yield return null;
                    var quit=Object.FindFirstObjectByType<CampaignChoice>();
                    Assert.That(quit,Is.Not.Null);
                    AssertEventFeedWidth();
                    Assert.That(quit.GetComponent<Canvas>().sortingOrder,Is.GreaterThan(settings.SettingsCanvas.GetComponent<Canvas>().sortingOrder));
                    ScreenCapture.CaptureScreenshot("Temp/UIScreenAudit/settings-quit-confirmation-"+menu.gameObject.scene.name+".png");
                    yield return new WaitForSecondsRealtime(.15f);
                    yield return ClickHudTab(mouse,quit.Back);
                    Assert.That(settings.IsOpen,Is.True,"Cancelling quit restores Settings.");
                    Assert.That(module.OwnsFocus(settings),Is.True);
                }
                yield return ClickHudTab(mouse,settings.ResumeButton);
                Assert.That(settings.IsOpen,Is.False);
                Assert.That(owner.Current,Is.SameAs(menu));Assert.That(menu.Tab,Is.EqualTo(optionsTab));
                Assert.That(module.Allows(EventSystem.current.currentSelectedGameObject),Is.True);
                Assert.That(MenuUIInputModule.IsUsable(EventSystem.current.currentSelectedGameObject),Is.True);
            }
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
                AssertEventFeedWidth();
                if(tab==PartyMenuTab.Stats)VerifyStatsFit(menu);
                Assert.That((button.GetComponentInChildren<TMPro.TMP_Text>().fontStyle & TMPro.FontStyles.Underline)!=0,Is.True);
                var label=button.GetComponentInChildren<TMPro.TMP_Text>();label.ForceMeshUpdate();
                Assert.That(TextLayoutAudit.Capture($"Temp/HudTabAudit/{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}-{tab}-{Screen.width}x{Screen.height}"),Is.Empty,
                    "Rendered tab glyphs and backgrounds must fit; TMP's line-box flag also includes unused font padding.");
                Assert.That(((Image)button.targetGraphic).sprite,Is.EqualTo(GameUITheme.Current.DungeonWoodButton),"All scenes use the same wooden tab buttons.");
                Assert.That(((Image)button.targetGraphic).pixelsPerUnitMultiplier,Is.EqualTo(1));
                Assert.That(button.targetGraphic.GetComponent<DungeonUIRole>().IsValid(),Is.True);
                Assert.That(label.font,Is.EqualTo(TMPro.TMP_Settings.defaultFontAsset));
                Assert.That(label.color,Is.EqualTo(GameUITheme.LightInk));
                Assert.That(button.colors.normalColor,Is.EqualTo(GameUITheme.Selected),"The current tab stays highlighted while browsing its list.");
                var rect=(RectTransform)button.transform;
                var inventoryRect=(RectTransform)launcher.ButtonFor(PartyMenuTab.Inventory).transform;
                Assert.That(rect.rect.width,Is.EqualTo(inventoryRect.rect.width).Within(.1f),"Every header, including Capabilities, has the same usable width.");
                Assert.That(rect.anchorMin.y,Is.EqualTo(inventoryRect.anchorMin.y));
                Assert.That(rect.anchorMax.y,Is.EqualTo(inventoryRect.anchorMax.y));
                yield return ClickHudTab(mouse,button);
                Assert.That(owner.Current,Is.SameAs(menu),"Clicking the active header keeps the menu open.");
            }

            var previousTab=menu.Tab;
            menu.Pick("Nested actions",new(){("Cancel this action",()=>{})});yield return null;
            yield return null;
            AssertEventFeedWidth();
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
            var emptyExit=owner.Current.GetComponentsInChildren<DungeonDialogClose>().Single().GetComponent<Button>();
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(emptyExit.gameObject));
            yield return ClickHudTab(mouse,emptyExit);
            Assert.That(owner.Current,Is.SameAs(menu),"An empty inventory submenu has a working pointer exit.");

            GameMessages.Post("The party is ready to explore.");yield return null;
            yield return PressNavigation(keyboard,Key.H);
            var history=owner.Current as EventHistoryDialog;
            Assert.That(history,Is.Not.Null,"History shares the scene's dialog stack.");
            AssertExpandedHistoryLayout(history);
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
            yield return PressNavigation(keyboard,Key.H);
            Assert.That(owner.Current,Is.SameAs(menu));
            Assert.That(menu.GetComponent<Canvas>().enabled,Is.True);

            yield return PressNavigation(keyboard,Key.Escape);
            Assert.That(owner.Opened,Is.False);
            Assert.That(hud.enabled,Is.True,"Closing the menu leaves the same HUD buttons available.");
            var feed=Object.FindFirstObjectByType<GameMessages>();
            var feedPanel=(RectTransform)feed.GetComponentInChildren<CanvasGroup>().transform;
            Assert.That(feedPanel.anchorMin,Is.EqualTo(new Vector2(.225f,.016f)));
            Assert.That(feedPanel.anchorMax,Is.EqualTo(new Vector2(.775f,.20f)),"Events use the dungeon's bottom feed, not a floating panel.");
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
            yield return VerifyHistoryWidthAcrossPresentations(menu,keyboard,pad,includeCapabilities);
            module.actionsAsset.devices=null;
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(keyboard);
        }

        private IEnumerator VerifyHistoryWidthAcrossPresentations(PartyMenu menu,Keyboard keyboard,Gamepad pad,bool includeCapabilities)
        {
            bool originalTerminal=TerminalMode.Requested;
            try
            {
                foreach(bool terminal in new[]{false,true})
                {
                    TerminalMode.SetRequested(terminal);yield return new WaitForSecondsRealtime(.2f);
                    yield return PressNavigation(keyboard,Key.I);
                    Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Equipment));AssertEventFeedWidth();
                    yield return PressNavigation(keyboard,Key.T);
                    Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Stats));AssertEventFeedWidth();
                    foreach(var tab in includeCapabilities
                        ? new[]{PartyMenuTab.Inventory,PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats,PartyMenuTab.Capabilities}
                        : new[]{PartyMenuTab.Inventory,PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats})
                    {
                        menu.SwitchTab(tab);yield return null;yield return null;
                        AssertEventFeedWidth();
                        yield return PressNavigation(keyboard,Key.H);
                        Assert.That(menu.Owner.Current,Is.TypeOf<EventHistoryDialog>());
                        AssertExpandedHistoryLayout((EventHistoryDialog)menu.Owner.Current);
                        yield return PressNavigation(keyboard,Key.H);
                        Assert.That(menu.Owner.Current,Is.SameAs(menu));
                        AssertEventFeedWidth();
                    }
                    menu.Pick("History width audit",new(){("Cancel",()=>{})});yield return null;yield return null;
                    AssertEventFeedWidth();
                    yield return PressNavigation(keyboard,Key.Escape);
                    var settings=Common.Instance.GlobalSettings;
                    settings.ShowDialog();yield return null;yield return null;
                    AssertEventFeedWidth();
                    settings.ShowEventHistory();yield return null;yield return null;
                    Assert.That(menu.Owner.Current,Is.TypeOf<EventHistoryDialog>(),"The Options command uses the same History panel.");
                    AssertExpandedHistoryLayout((EventHistoryDialog)menu.Owner.Current);
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;
                    InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
                    Assert.That(menu.Owner.Current,Is.SameAs(menu),"Controller Cancel restores the party tab.");
                    AssertEventFeedWidth();
                }
            }
            finally {TerminalMode.SetRequested(originalTerminal);}
            menu.SwitchTab(PartyMenuTab.Inventory);yield return new WaitForSecondsRealtime(.2f);
        }

        internal static RectTransform AssertEventFeedWidth()
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Canvas.ForceUpdateCanvases();
            var feed=Object.FindFirstObjectByType<GameMessages>();
            Assert.That(feed,Is.Not.Null);
            var panel=(RectTransform)feed.GetComponentsInChildren<CanvasGroup>(true)
                .Single(group=>group.GetComponentInParent<EventHistoryDialog>(true)==null).transform;
            Assert.That(panel.anchorMin,Is.EqualTo(new Vector2(.225f,.016f)));
            Assert.That(panel.anchorMax,Is.EqualTo(new Vector2(.775f,.20f)),"Commands and menus must not narrow the event feed.");
            var sourceScroll=panel.GetComponent<ScrollRect>();
            Assert.That(typeof(GameMessages).GetField("scroll",flags).GetValue(feed),Is.SameAs(sourceScroll),
                "The feed controller must reflow the visible scroll viewport.");
            Assert.That(sourceScroll.content.GetComponent<TMPro.TMP_Text>().fontSize,Is.EqualTo(33.11f).Within(.001f),"Every scene uses the saved event font size.");
            // Terminal mode disables this canvas; Unity keeps its old pixel geometry
            // through window resizes until the canvas is shown again.
            if(!panel.GetComponentInParent<Canvas>().isActiveAndEnabled)return panel;
            var bounds=ScreenBounds(panel);
            Assert.That(bounds.xMin,Is.EqualTo(Screen.width*.225f).Within(1));
            Assert.That(bounds.xMax,Is.EqualTo(Screen.width*.775f).Within(1));
            Assert.That(bounds.yMin,Is.EqualTo(Screen.height*.016f).Within(1));
            Assert.That(bounds.yMax,Is.GreaterThanOrEqualTo(Screen.height*.20f-1),"Wide windows can add vertical room for four complete lines.");
            var viewport=ScreenBounds(sourceScroll.viewport);
            var history=ScreenBounds(panel.GetComponentInChildren<Button>().targetGraphic.rectTransform);
            Assert.That(viewport.yMax,Is.LessThanOrEqualTo(history.yMin-1),"Event text must stay below the History button.");
            var focus=MenuUIInputModule.Active?.FocusRoot;
            var dock=focus != null ? focus.GetComponentInParent<JuicyChickenGames.Menu.Dialog>()?.GameplayDockPanel : null;
            if(dock != null)
            {
                var dockBounds=ScreenBounds(dock);
                Assert.That(history.xMax,Is.LessThanOrEqualTo(dockBounds.xMin-1),"The whole History button must remain clear of the dock.");
                Assert.That(viewport.xMax,Is.LessThanOrEqualTo(dockBounds.xMin-1),
                    $"Event text must wrap before the dock covers it; offset {sourceScroll.viewport.offsetMax}, inset {typeof(GameMessages).GetField("dockInset",flags).GetValue(feed)}.");
            }
            return panel;
        }

        private static Rect ScreenBounds(RectTransform panel)
        {
            var corners=new Vector3[4];panel.GetWorldCorners(corners);
            var minimum=RectTransformUtility.WorldToScreenPoint(null,corners[0]);
            var maximum=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            return Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
        }

        internal static void AssertExpandedHistoryLayout(EventHistoryDialog history)
        {
            var compact=AssertEventFeedWidth();
            Assert.That(history.Panel.anchorMin,Is.EqualTo(new Vector2(.225f,.016f)));
            Assert.That(history.Panel.anchorMax,Is.EqualTo(new Vector2(.775f,.88f)),"History keeps the feed width and grows upward.");
            var normalBounds=new Rect(Screen.width*.225f,Screen.height*.016f,Screen.width*.55f,Screen.height*.184f);
            var expandedBounds=ScreenBounds(history.Panel);
            Assert.That(expandedBounds.xMin,Is.EqualTo(normalBounds.xMin).Within(1));
            Assert.That(expandedBounds.xMax,Is.EqualTo(normalBounds.xMax).Within(1),"The actual screen edges must match, not just the anchors.");
            Assert.That(expandedBounds.yMin,Is.EqualTo(normalBounds.yMin).Within(1));
            Assert.That(expandedBounds.yMax,Is.EqualTo(Screen.height*.88f).Within(1));
            if(!TerminalMode.Effective)
                Assert.That(GameUISkin.PanelGraphic(history.Panel).sprite,Is.EqualTo(GameUISkin.PanelGraphic(compact).sprite));
            var sourceScroll=compact.GetComponent<ScrollRect>();
            Assert.That(history.Entries.fontSize,Is.EqualTo(sourceScroll.content.GetComponent<TMPro.TMP_Text>().fontSize));
            var buttonBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(history.Panel,history.Back.transform);
            Assert.That(buttonBounds.max.y,Is.LessThanOrEqualTo(history.Panel.rect.yMax));
            Assert.That(buttonBounds.size.y,Is.LessThan(history.Panel.rect.height*.1f),"The expand/collapse button keeps the feed's compact height.");
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
            AssertEventFeedWidth();
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
                AssertEventFeedWidth();
                Assert.That(owner.Current,Is.SameAs(menu));
                Assert.That(owner.Stack.Count,Is.EqualTo(1));
                Assert.That(menu.Hero.Id,Is.EqualTo(hero));
            }
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.left});yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            Assert.That(menu.Tab,Is.EqualTo(includeCapabilities?PartyMenuTab.Capabilities:PartyMenuTab.Stats),"The controller stick also cycles tabs.");
            AssertEventFeedWidth();

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
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button),
                $"Pointer control {button.name} must receive its click; first hit was {hits[0].gameObject.name}.");
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
