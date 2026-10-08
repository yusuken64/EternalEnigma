#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonDockTests
    {
        GameTestHarness harness;
        TestInputScope inputs;
        UnityEditor.EditorWindow view;
        int size;
        DungeonAnimationMode? animation;
        bool? fullControl;
        Keyboard keyboard;Gamepad pad;
        Skill longSkill;
        const System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        [UnitySetUp] public IEnumerator Setup()
        {
            inputs=new TestInputScope();animation=DungeonPreferences.AnimationOverride;DungeonPreferences.AnimationOverride=DungeonAnimationMode.NoAnimations;
            fullControl=DungeonPreferences.FullControlOverride;DungeonPreferences.FullControlOverride=false;
            view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            size=(int)view.GetType().GetProperty("selectedSizeIndex",Flags).GetValue(view);
            harness=new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario{Seed=12345,AdditionalAllies=new[]{"Avery","Reese","Sage"},IncludeStartingItems=true,Skills=new[]{"Damage","Fire Bolt"}});
            longSkill=Object.Instantiate(harness.Ally.Skills[0]);
            longSkill.Description=string.Concat(Enumerable.Repeat("A long description remains independently scrollable without moving the entry list.\n",20));
            harness.Ally.Skills[0]=longSkill;
            Directory.CreateDirectory("Temp/DungeonDock");
            keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();
            MenuUIInputModule.Active.actionsAsset.devices=new InputDevice[]{keyboard,pad};
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(pad!=null)InputSystem.RemoveDevice(pad);
            yield return harness.Cleanup();inputs.Dispose();DungeonPreferences.AnimationOverride=animation;
            if(longSkill!=null)Object.DestroyImmediate(longSkill);
            DungeonPreferences.FullControlOverride=fullControl;
            view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,size);
        }
        [UnityTest] public IEnumerator BrowsingActionsHistoryAndTargetingAtAllResolutions()
        {
            var manager=MenuManager.Instance;var actor=harness.Ally;
            Assert.That(harness.Game.InventoryMenu,Is.SameAs(manager.InventoryMenu),"Authoring preserves scene references to the inventory picker.");
            int turns=actor.Vitals.ActionsPerTurnLeft,sp=actor.Vitals.SP;
            var camera=Camera.main.transform.position;
            foreach(var resolution in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(1280,800),new Vector2(2560,1080)})
            {
                view.GetType().GetMethod("SetCustomResolution",Flags).Invoke(view,new object[]{resolution,"Dungeon dock "+resolution});view.Repaint();
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("hud",resolution);
                manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
                var menu=manager.PartyMenu;
                Assert.That(menu.Panel.anchorMin,Is.EqualTo(new Vector2(.73f,.03f)));
                Assert.That(menu.Panel.anchorMax,Is.EqualTo(new Vector2(.98f,.88f)));
                Assert.That(Object.FindFirstObjectByType<Minimap>().minimapImage.gameObject.activeSelf,Is.False);
                Assert.That(menu.HeroesRoot.gameObject.activeSelf,Is.False);
                var other=Game.Instance.Allies.First(a=>a!=actor && !PartyRules.IsSummon(a));
                yield return ClickPortrait(other);
                Assert.That(Game.Instance.PlayerController.ControlledAlly,Is.SameAs(other));
                Assert.That(menu.Hero.DungeonActor,Is.SameAs(other));
                yield return ClickPortrait(actor);
                Assert.That(menu.Hero.DungeonActor,Is.SameAs(actor));
                Assert.That(menu.Details.color,Is.EqualTo(GameUITheme.Ink));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Tab));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
                Assert.That(menu.Hero.DungeonActor,Is.Not.SameAs(actor));
                Assert.That(menu.Hero.DungeonActor,Is.SameAs(Game.Instance.PlayerController.ControlledAlly));
                yield return ClickPortrait(actor);
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightShoulder));yield return null;yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
                Assert.That(menu.Hero.DungeonActor,Is.Not.SameAs(actor));
                yield return ClickPortrait(actor);
                yield return Capture("inventory",resolution);
                var first=menu.EntryButtons[0];first.Select();yield return null;
                float scroll=menu.scrollView.verticalNormalizedPosition;
                first.onClick.Invoke();yield return null;
                Assert.That(manager.CurrentDialog,Is.SameAs(manager.ActionDialog),"Items open actions directly.");
                Assert.That(((RectTransform)manager.ActionDialog.Panel.transform).anchorMin,Is.EqualTo(menu.Panel.anchorMin));
                Assert.That(manager.ActionDialog.Buttons.Last().gameObject.activeSelf,Is.False);
                Assert.That(manager.ActionDialog.Panel.GetComponentsInChildren<DungeonDialogClose>(true).All(close=>!close.gameObject.activeSelf),Is.True);
                Assert.That(menu.GetComponent<Canvas>().enabled,Is.False);
                Assert.That(Game.Instance.PlayerController.TryControlFromUI(other),Is.False,"Nested item actions retain their actor.");
                yield return Capture("item-actions",resolution);
                manager.CurrentDialog.CloseDialog();yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(first.gameObject));
                Assert.That(menu.scrollView.verticalNormalizedPosition,Is.EqualTo(scroll).Within(.01f));
                menu.SwitchTab(PartyMenuTab.Skills);yield return null;
                yield return ClickPortrait(other);
                Assert.That(menu.Hero.DungeonActor,Is.SameAs(other));
                Assert.That(menu.Tab,Is.EqualTo(PartyMenuTab.Skills));
                yield return ClickPortrait(actor);
                menu.EntryButtons[0].Select();yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(menu.HeroText.gameObject.activeSelf,Is.False);
                Assert.That(menu.DetailsControl.Scroll.gameObject.activeSelf,Is.False);
                Assert.That(menu.Hints.gameObject.activeSelf,Is.False);
                Assert.That(menu.BackButton.gameObject.activeSelf,Is.False);
                Assert.That(menu.Panel.GetComponentsInChildren<DungeonDialogClose>(true).All(close=>!close.gameObject.activeSelf),Is.True);
                var list=(RectTransform)menu.scrollView.transform;
                Assert.That(list.anchorMin,Is.EqualTo(new Vector2(.035f,.025f)));
                Assert.That(list.anchorMax,Is.EqualTo(new Vector2(.965f,.975f)));
                yield return Capture("skills",resolution);
                menu.Pick("Nested inventory target",new(){("Target item",()=>{})},false);yield return null;
                yield return Capture("picker",resolution);
                manager.OpenTargetingMenu(actor,actor.Skills.Single(s=>s.SkillName=="Fire Bolt"));yield return null;
                var prompt=manager.TargetDialog.SelectTargetPrompt.GetComponentInChildren<TMPro.TMP_Text>();
                Canvas.ForceUpdateCanvases();prompt.ForceMeshUpdate();
                Assert.That(prompt.isTextOverflowing,Is.False,"The targeting prompt must fit at every supported resolution.");
                foreach(var dialog in manager.DialogStack.Where(d=>d!=manager.TargetDialog))
                    Assert.That(dialog.GetComponent<Canvas>().enabled,Is.False,"Targeting must hide every underlying panel.");
                yield return Capture("targeting",resolution);
                manager.CurrentDialog.CloseDialog();yield return null;
                Assert.That(manager.CurrentDialog,Is.TypeOf<PartyMenuPicker>());
                Assert.That(manager.CurrentDialog.GetComponent<Canvas>().enabled,Is.True);
                manager.CloseAllMenus();
                yield return ClickPortrait(other);Assert.That(Game.Instance.PlayerController.ControlledAlly,Is.SameAs(other));
                yield return ClickPortrait(actor);
                MenuManager.Open(manager.InventoryMenu);
                manager.InventoryMenu.Setup(Game.Instance.PlayerController.Inventory.InventoryItems,actor,_=>{},"Select an inventory target");
                manager.InventoryMenu.SetNavigation();yield return null;
                Assert.That(((RectTransform)manager.InventoryMenu.scrollView.transform.parent).anchorMin,Is.EqualTo(menu.Panel.anchorMin));
                Assert.That(manager.InventoryMenu.GetComponentsInChildren<DungeonDialogClose>(true).All(close=>!close.gameObject.activeSelf),Is.True);
                yield return Capture("inventory-picker",resolution);manager.CloseAllMenus();
                GameMessages.ShowHistory();yield return null;
                var history=manager.CurrentDialog;GameMessages.ShowHistory();Assert.That(manager.CurrentDialog,Is.SameAs(history));
                var reader=(EventHistoryDialog)history;
                reader.Setup(System.Array.Empty<string>());
                Assert.That(reader.Entries.text,Is.EqualTo("No events yet."));
                reader.Setup(Enumerable.Range(0,60).Select(i=>"History entry "+i).ToArray());
                Assert.That(reader.scrollView.verticalNormalizedPosition,Is.EqualTo(0).Within(.001f));
                ExecuteEvents.Execute(reader.Reader.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Up},ExecuteEvents.moveHandler);
                Assert.That(reader.scrollView.verticalNormalizedPosition,Is.GreaterThan(0));
                yield return Capture("history",resolution);history.CloseDialog();yield return null;
                reader.Setup(System.Array.Empty<string>());
                Assert.That(reader.Panel.anchorMin,Is.EqualTo(new Vector2(.73f,.03f)));
                Assert.That(reader.Panel.GetComponent<Image>().sprite,Is.EqualTo(GameUITheme.Current.DungeonWood));
                Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(turns));Assert.That(actor.Vitals.SP,Is.EqualTo(sp));
                Assert.That(Camera.main.transform.position,Is.EqualTo(camera));
            }
        }
        static IEnumerator ClickPortrait(Ally actor)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            var portrait=Object.FindObjectsByType<CharacterStatsDisplay>(FindObjectsSortMode.None).First(d=>d.Character==actor).PortraitImage;
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,portrait.rectTransform.TransformPoint(portrait.rectTransform.rect.center)),button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count,Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(portrait.gameObject),"Portrait must receive the mouse click above the dock input shield.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
            yield return null;yield return null;
        }
        static IEnumerator Capture(string name,Vector2 size)
        {
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot($"Temp/DungeonDock/{name}-{size.x}x{size.y}.png");
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
#endif
