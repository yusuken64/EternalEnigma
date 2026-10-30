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
            int turns=actor.Vitals.ActionsPerTurnLeft,mana=actor.Vitals.Mana;
            var camera=Camera.main.transform.position;
            foreach(var resolution in new[]{new Vector2(960,600),new Vector2(1280,720),new Vector2(1920,1080),new Vector2(1280,800),new Vector2(2560,1080)})
            {
                view.GetType().GetMethod("SetCustomResolution",Flags).Invoke(view,new object[]{resolution,"Dungeon dock "+resolution});view.Repaint();
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("hud",resolution);
                manager.OpenPartyMenu(PartyMenuTab.Inventory);yield return null;
                var menu=manager.PartyMenu;
                var exit=menu.Panel.GetComponentsInChildren<DungeonDialogClose>().Single();
                var exitRect=(RectTransform)exit.transform;
                var corners=new Vector3[4];exitRect.GetWorldCorners(corners);
                Assert.That(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,corners[0]),RectTransformUtility.WorldToScreenPoint(null,corners[3])),
                    Is.GreaterThanOrEqualTo(28),"The pointer exit remains a usable click target at the smallest resolution.");
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
                Assert.That(manager.ActionDialog.Panel.GetComponentsInChildren<DungeonDialogClose>().Length,Is.EqualTo(1),"Item actions retain one visible pointer exit.");
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
                Assert.That(menu.HeroText.gameObject.activeSelf,Is.True);
                Assert.That(menu.HeroText.text,Is.EqualTo(GameplayText.Format(actor.CharacterName)));
                Assert.That(menu.DetailsControl.Scroll.gameObject.activeInHierarchy,Is.True);
                Assert.That(menu.Details.text,Does.Contain(longSkill.Description));
                Assert.That(menu.Hints.gameObject.activeSelf,Is.False);
                Assert.That(menu.BackButton.gameObject.activeSelf,Is.False);
                Assert.That(menu.Panel.GetComponentsInChildren<DungeonDialogClose>().Length,Is.EqualTo(1));
                var list=(RectTransform)menu.scrollView.transform;
                Assert.That(list.anchorMin,Is.EqualTo(new Vector2(.035f,.48f)));
                Assert.That(list.anchorMax,Is.EqualTo(new Vector2(.965f,.89f)));
                yield return Capture("skills",resolution);
                float listPosition=menu.scrollView.verticalNormalizedPosition;
                menu.DetailsControl.Select();
                ExecuteEvents.Execute(menu.DetailsControl.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Down},ExecuteEvents.moveHandler);
                Assert.That(menu.DetailsControl.Scroll.verticalNormalizedPosition,Is.LessThan(1));
                Assert.That(menu.scrollView.verticalNormalizedPosition,Is.EqualTo(listPosition).Within(.001f));
                menu.EntryButtons.First(button=>button.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Fire Bolt")).Select();
                yield return null;Canvas.ForceUpdateCanvases();
                Assert.That(menu.Details.text,Does.Contain("Fire Bolt"));
                var viewport=menu.DetailsControl.Scroll.viewport;
                var descriptionBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,menu.Details.transform);
                Assert.That(descriptionBounds.max.y,Is.EqualTo(viewport.rect.yMax).Within(1),"Changing skill returns the description to its top, including when the text fits without scrolling.");
                yield return Capture("skill-description",resolution);
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
                Assert.That(manager.InventoryMenu.GetComponentsInChildren<DungeonDialogClose>().Length,Is.EqualTo(1));
                yield return Capture("inventory-picker",resolution);manager.CloseAllMenus();
                GameMessages.ShowHistory();yield return null;
                var history=manager.CurrentDialog;GameMessages.ShowHistory();Assert.That(manager.CurrentDialog,Is.SameAs(history));
                var reader=(EventHistoryDialog)history;
                Assert.That(reader.GetComponentsInChildren<DungeonDialogClose>().Length,Is.EqualTo(1));
                Assert.That(reader.Reader.navigation.selectOnRight,Is.SameAs(reader.Back));
                Assert.That(reader.Back.navigation.selectOnLeft,Is.SameAs(reader.Reader));
                reader.Setup(System.Array.Empty<string>());
                Assert.That(reader.Entries.text,Is.EqualTo("No events yet."));
                reader.Setup(Enumerable.Range(0,60).Select(i=>"History entry "+i).ToArray());
                Assert.That(reader.scrollView.verticalNormalizedPosition,Is.EqualTo(0).Within(.001f));
                ExecuteEvents.Execute(reader.Reader.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Up},ExecuteEvents.moveHandler);
                Assert.That(reader.scrollView.verticalNormalizedPosition,Is.GreaterThan(0));
                yield return Capture("history",resolution);history.CloseDialog();yield return null;
                reader.Setup(System.Array.Empty<string>());
                Assert.That(reader.Panel.anchorMin,Is.EqualTo(Vector2.zero));
                Assert.That(reader.Panel.GetComponent<Image>().sprite,Is.EqualTo(GameUITheme.Current.DungeonWood));
                Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(turns));Assert.That(actor.Vitals.Mana,Is.EqualTo(mana));
                Assert.That(Camera.main.transform.position,Is.EqualTo(camera));
            }
        }
        [UnityTest] public IEnumerator StatAndEquipmentSummariesExplainWithoutItemCommands()
        {
            var manager=MenuManager.Instance;
            int actions=harness.Ally.Vitals.ActionsPerTurnLeft;
            manager.OpenPartyMenu(PartyMenuTab.Stats);yield return null;
            var menu=manager.PartyMenu;
            foreach(var tab in new[]{PartyMenuTab.Stats,PartyMenuTab.Equipment})
            {
                menu.SwitchTab(tab);yield return null;
                var row=tab==PartyMenuTab.Stats
                    ?menu.EntryButtons.First(button=>button.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("STR "))
                    :menu.EntryButtons.Last();
                row.Select();row.onClick.Invoke();yield return null;
                Assert.That(manager.CurrentDialog,Is.TypeOf<PartyMenuPicker>());
                var picker=(PartyMenuPicker)manager.CurrentDialog;
                Assert.That(picker.Rows.GetComponentsInChildren<AuthoredButton>(),Is.Empty,"A stat breakdown never offers Use, Throw or Drop.");
                var description=picker.Rows.GetComponentsInChildren<TMPro.TMP_Text>().Single();
                Assert.That(description.text,Does.Not.Contain("for actions"),"A read-only description must not advertise actions.");
                var close=picker.GetComponentsInChildren<DungeonDialogClose>().Single().GetComponent<Button>();
                Assert.That(close.navigation.selectOnLeft,Is.Not.Null,"The exit and description are connected for keyboard/controller navigation.");
                Canvas.ForceUpdateCanvases();
                var rect=(RectTransform)close.transform;
                var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
                    position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
                var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                Assert.That(hits.Count,Is.GreaterThan(0));
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(close.gameObject));
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
                yield return null;yield return null;
                Assert.That(manager.CurrentDialog,Is.SameAs(menu));
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(row.gameObject));
            }
            Assert.That(harness.Ally.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));
        }
        [UnityTest] public IEnumerator MouseTargetingPreviewsRejectsHudClicksCancelsAndConfirmsOnce()
        {
            var mouse=InputSystem.AddDevice<Mouse>();
            var module=MenuUIInputModule.Active;
            module.actionsAsset.devices=new InputDevice[]{keyboard,pad,mouse};
            var manager=MenuManager.Instance;var actor=harness.Ally;
            var target=manager.TargetDialog;
            int mana=actor.Vitals.Mana,actions=actor.Vitals.ActionsPerTurnLeft;
            try
            {
                manager.OpenPartyMenu(PartyMenuTab.Skills);yield return null;
                manager.OpenTargetingMenu(actor,actor.Skills.Single(skill=>skill.SkillName=="Fire Bolt"));
                Vector2 point=Camera.main.WorldToScreenPoint(harness.Game.CurrentDungeon.CellToWorld(actor.TilemapPosition+Vector3Int.right*2));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
                Assert.That(manager.CurrentDialog,Is.SameAs(target),"Holding the click that opened targeting never casts.");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                point=Camera.main.WorldToScreenPoint(harness.Game.CurrentDungeon.CellToWorld(actor.TilemapPosition+Vector3Int.left*2));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                Assert.That(target.Direction,Is.EqualTo(Vector3Int.left),"Mouse motion previews missile aim.");
                Assert.That(actor.Vitals.Mana,Is.EqualTo(mana));
                // Party headers are hidden during targeting; probe the visible Options
                // control instead of treating their former position as a dead zone.
                var options=Object.FindFirstObjectByType<DungeonHud>().OptionsButton;
                Assert.That(options.gameObject.activeInHierarchy && options.GetComponentInParent<Canvas>().enabled,Is.True);
                Canvas.ForceUpdateCanvases();
                var header=(RectTransform)options.transform;
                var hudPoint=RectTransformUtility.WorldToScreenPoint(null,header.TransformPoint(header.rect.center));
                var hits=new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=hudPoint},hits);
                Assert.That(hits.Any(hit=>hit.gameObject.GetComponentInParent<Button>()==options),Is.True,
                    "The guard probe must hit a visible HUD control beneath the targeting shield.");
                Assert.That(target.TryPointTarget(hudPoint),Is.False);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=hudPoint,buttons=1});yield return null;yield return null;
                Assert.That(manager.CurrentDialog,Is.SameAs(target),"Clicking a HUD control cannot cast through it.");
                Assert.That(target.Direction,Is.EqualTo(Vector3Int.left));
                Assert.That(Common.Instance.GlobalSettings.IsOpen,Is.False);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=hudPoint});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=hudPoint,buttons=2});yield return null;yield return null;
                Assert.That(manager.CurrentDialog,Is.SameAs(manager.PartyMenu),"Right-click returns to the skill menu.");
                Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));Assert.That(actor.Vitals.Mana,Is.EqualTo(mana));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=hudPoint});yield return null;yield return null;

                // A valid target click enters the ordinary validated action path once.
                var friend=harness.Game.Allies.First(ally=>ally!=actor);
                var offset=new[]{Vector3Int.left,Vector3Int.right,Vector3Int.up,Vector3Int.down}
                    .First(direction=>harness.Game.CurrentDungeon.IsWalkable(actor.TilemapPosition+direction));
                friend.SetPosition(actor.TilemapPosition+offset);
                int commits=0;Character chosen=null;
                MenuManager.Open(target);
                target.Setup(actor,new System.Collections.Generic.List<Character>{actor,friend},(selected,_)=>{
                    commits++;chosen=selected;return new WaitAction();
                });
                target.CloseAction=target.Close;
                yield return null;yield return null;
                // A raised visible body takes priority over the other actor's ground cell.
                var projectedPoint=Camera.main.WorldToScreenPoint(harness.Game.CurrentDungeon.CellToWorld(actor.TilemapPosition));
                var ray=Camera.main.ScreenPointToRay(projectedPoint);
                var ground=new Plane(Vector3.forward,new Vector3(0,0,DungeonPresentation.GroundPlaneZ));
                Assert.That(ground.Raycast(ray,out float groundDistance),Is.True);
                Assert.That(harness.Game.CurrentDungeon.WorldToCell(ray.GetPoint(groundDistance)),Is.EqualTo(actor.TilemapPosition));
                var raisedBody=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    raisedBody.transform.SetParent(friend.VisualParent.transform,false);
                    raisedBody.transform.position=ray.GetPoint(groundDistance*.5f);
                    raisedBody.transform.localScale=Vector3.one*.5f;
                    Assert.That(target.TryPointTarget(projectedPoint),Is.True);
                    Assert.That(target.CameraTarget,Is.SameAs(friend),"A visible foreground body wins over another target's floor cell.");
                }
                finally { Object.DestroyImmediate(raisedBody); }
                point=Camera.main.WorldToScreenPoint(friend.transform.position);
                Assert.That(target.TryPointTarget(point),Is.True);
                Assert.That(target.CameraTarget,Is.SameAs(friend));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
                Assert.That(commits,Is.EqualTo(1));Assert.That(chosen,Is.SameAs(friend));
                Assert.That(manager.Opened,Is.False);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
                yield return harness.WaitForIdle();
                Assert.That(commits,Is.EqualTo(1));
            }
            finally {InputSystem.RemoveDevice(mouse);module.actionsAsset.devices=new InputDevice[]{keyboard,pad};}
        }
        [UnityTest] public IEnumerator TargetingDiscardsHudClicksBeforeRestoringMenus()
        {
            var mouse=InputSystem.AddDevice<Mouse>();
            var module=MenuUIInputModule.Active;
            module.actionsAsset.devices=new InputDevice[]{keyboard,pad,mouse};
            var manager=MenuManager.Instance;var actor=harness.Ally;
            var settings=Common.Instance.GlobalSettings;
            var hud=Object.FindFirstObjectByType<DungeonHud>();
            var options=(Button)typeof(DungeonHud).GetField("options",Flags).GetValue(hud);
            var rect=(RectTransform)options.transform;
            Canvas.ForceUpdateCanvases();
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            int mana=actor.Vitals.Mana,actions=actor.Vitals.ActionsPerTurnLeft,opened=0;
            UnityEngine.Events.UnityAction countOpen=()=>opened++;
            options.onClick.AddListener(countOpen);
            try
            {
                foreach(bool fromSkills in new[]{false,true})
                {
                    if(fromSkills){manager.OpenPartyMenu(PartyMenuTab.Skills);yield return null;}
                    manager.OpenTargetingMenu(actor,actor.Skills.Single(skill=>skill.SkillName=="Fire Bolt"));
                    yield return null;yield return null;
                    var target=manager.TargetDialog;
                    int before=opened;
                    Assert.That(target.TryPointTarget(point),Is.False,"The target shield must still reject the Options control beneath it.");
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                    Assert.That(manager.CurrentDialog,Is.SameAs(target));
                    Assert.That(settings.IsOpen,Is.False);Assert.That(opened,Is.EqualTo(before));

                    if(fromSkills)InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));
                    else InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=2});
                    yield return null;yield return null;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                    Assert.That(settings.IsOpen,Is.False,"Cancelling targeting must not replay an earlier HUD click.");
                    Assert.That(opened,Is.EqualTo(before));
                    if(fromSkills)
                    {
                        Assert.That(manager.CurrentDialog,Is.SameAs(manager.PartyMenu));
                        Assert.That(manager.PartyMenu.Tab,Is.EqualTo(PartyMenuTab.Skills));
                    }
                    else Assert.That(manager.Opened,Is.False,"Quick-skill targeting returns to gameplay.");
                    Assert.That(actor.Vitals.Mana,Is.EqualTo(mana));Assert.That(actor.Vitals.ActionsPerTurnLeft,Is.EqualTo(actions));

                    // Fresh input works in gameplay and over the restored party dock.
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                    Assert.That(settings.IsOpen,Is.True);Assert.That(opened,Is.EqualTo(before+1));
                    settings.Exit_Clicked();yield return null;yield return null;
                    if(fromSkills)Assert.That(manager.CurrentDialog,Is.SameAs(manager.PartyMenu));
                }
            }
            finally
            {
                options.onClick.RemoveListener(countOpen);
                InputSystem.RemoveDevice(mouse);module.actionsAsset.devices=new InputDevice[]{keyboard,pad};
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
