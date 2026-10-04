#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonVisualAuditTests
    {
        private GameTestHarness harness;
        private DungeonAnimationMode? previousAnimation;
        private float previousTimeScale;
        private UnityEditor.EditorWindow gameView;
        private int previousViewSize;
        private const System.Reflection.BindingFlags ViewFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator Setup()
        {
            previousAnimation = DungeonPreferences.AnimationOverride;
            previousTimeScale = Time.timeScale;
            gameView=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            previousViewSize=(int)gameView.GetType().GetProperty("selectedSizeIndex",ViewFlags).GetValue(gameView);
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario {
                AdditionalAllies = new[] { "Avery", "Reese", "Sage" },
                IncludeStartingItems = true, Skills = new[] { "Damage", "Fire Bolt" }
            });
            foreach(var root in harness.Game.gameObject.scene.GetRootGameObjects())
            foreach(var component in root.GetComponentsInChildren<Component>(true))
            {
                Assert.That(component,Is.Not.Null,"Dungeon scene must not contain missing scripts.");
                var properties=new UnityEditor.SerializedObject(component).GetIterator();
                while(properties.Next(true))
                    if(properties.propertyType==UnityEditor.SerializedPropertyType.ObjectReference && properties.objectReferenceValue==null)
                        Assert.That(properties.objectReferenceInstanceIDValue,Is.Zero,$"Missing reference on {component.name}.{properties.propertyPath}");
            }
            Directory.CreateDirectory("Temp/UIAudit");
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = previousTimeScale;
            DungeonPreferences.AnimationOverride = previousAnimation;
            gameView.GetType().GetProperty("selectedSizeIndex",ViewFlags).SetValue(gameView,previousViewSize);
            yield return harness.Cleanup();
        }

        [UnityTest] public IEnumerator HudDecorationsRespectVisibilityAndMenusKeepWorking()
        {
            GameMessages.Post("Rowan found a treasure chest.");
            GameMessages.Post("Avery recovered 12 HP.");
            GameMessages.Post("The stairs lead to the next floor.");
            yield return null;
            var events = AuthoredUI.Require<GameMessages>(harness.Game.transform);
            var group = events.GetComponentInChildren<CanvasGroup>(true);
            var source = group.GetComponent<Image>();
            var paper = source.transform.Find("Dungeon parchment").GetComponent<Image>();
            Assert.That(paper.GetComponentInParent<CanvasGroup>(), Is.SameAs(group),
                "The authored paper and wood must share the panel's visibility and fade.");
            source.gameObject.SetActive(false);
            yield return null;
            Assert.That(paper.gameObject.activeInHierarchy, Is.False, "Closing a panel must also hide its decoration.");
            source.gameObject.SetActive(true);
            group.alpha = 1;
            yield return null;
            Assert.That(paper.gameObject.activeInHierarchy, Is.True);
            Assert.That(paper.raycastTarget, Is.False);
            yield return Capture("hud", 1280, 720);
            yield return Capture("hud-narrow", 960, 720);
            GameMessages.ShowHistory();
            yield return Capture("event-history",960,720);
            GameMessages.ToggleHistory();
            Common.Instance.GlobalSettings.ShowDialog();
            yield return Capture("options",960,720);
            Common.Instance.GlobalSettings.Exit_Clicked();

            var manager = MenuManager.Instance;
            int actions = harness.Ally.Vitals.ActionsPerTurnLeft;
            manager.OpenPartyMenu(PartyMenuTab.Inventory);
            yield return null;
            yield return Capture("inventory", 1280, 720);
            manager.PartyMenu.Shortcut(PartyMenuTab.Skills);
            yield return null;
            yield return Capture("skills-narrow", 960, 720);
            int chosen = 0;
            manager.PartyMenu.Pick("Choose an action", new() { ("Use item", () => chosen++), ("Inspect details", () => {}) });
            var picker = (PartyMenuPicker)manager.CurrentDialog;
            var button = picker.Rows.GetComponentInChildren<Button>();
            Assert.That(button.colors.pressedColor, Is.EqualTo(new Color(.90f, .77f, .58f)),
                "New rows must receive their dungeon appearance before their first rendered frame.");
            yield return null;
            yield return Capture("action-picker", 1280, 720);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(chosen, Is.EqualTo(1));
            Assert.That(manager.CurrentDialog, Is.SameAs(manager.PartyMenu));
            manager.CloseAllMenus();
            Assert.That(harness.Ally.Vitals.ActionsPerTurnLeft, Is.EqualTo(actions));

            manager.OpenAllyMenu(harness.Ally);
            yield return Capture("ally-actions",960,720);
            manager.AllyActionDialog.Strategy_Clicked();
            yield return Capture("strategy",960,720);
            foreach(var strategyButton in manager.AllyActionDialog.DynamicActionDialog.ButtonContainer.GetComponentsInChildren<Button>())
                Assert.That(strategyButton.GetComponentInChildren<TMP_Text>().isTextOverflowing,Is.False);
            manager.CloseAllMenus();

            manager.OpenTargetingMenu(harness.Ally,harness.Ally.Skills.Single(s=>s.SkillName=="Fire Bolt"));
            yield return Capture("target-selection",960,720);
            Assert.That(manager.TargetDialog.SelectTargetPrompt.GetComponentInChildren<TMP_Text>().isTextOverflowing,Is.False);
            manager.CloseAllMenus();

            manager.StairDialog.Setup("Descend to the next floor?", () => {}, () => {});
            MenuManager.Open(manager.StairDialog);
            yield return null;
            yield return Capture("confirmation", 960, 720);
            Assert.That(manager.StairDialog.PromptText.isTextOverflowing,Is.False);
            manager.CloseAllMenus();
            DungeonHud.Ensure(harness.Game).Inspect("Rowan\nPoisoned (3), Rooted: defeat the holder");
            yield return Capture("target-details", 1280, 720);

            var result = harness.Game.GameOverScreen;
            foreach (bool victory in new[] { false, true })
            {
                result.gameObject.SetActive(true);
                result.Setup(harness.Game.PlayerController, victory);
                yield return Capture(victory ? "victory" : "defeat", 960, 720);
                Assert.That(result.MessageText.isTextOverflowing, Is.False);
                Assert.That(result.OkButton.GetComponentInChildren<TMP_Text>().isTextOverflowing, Is.False);
                result.gameObject.SetActive(false);
            }
        }

        [UnityTest] public IEnumerator FloorBorderCoversHudFitsNarrowScreensAndFadesWithBlackout()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            var message = harness.Game.NewFloorMessage;
            message.HideScreen(12);
            yield return new WaitForSecondsRealtime(1.1f);
            var frame = message.transform.Find("Floor transition frame").GetComponent<Image>();
            Assert.That(frame.transform.GetSiblingIndex(), Is.GreaterThan(message.BackgroundColor.transform.GetSiblingIndex()));
            var overlay = message.GetComponent<Canvas>();
            Assert.That(overlay.isRootCanvas || overlay.overrideSorting, Is.True);
            Assert.That(overlay.sortingOrder, Is.EqualTo(ScreenTransition.FloorOverlayOrder));
            Assert.That(frame.preserveAspect, Is.True);
            Assert.That(frame.raycastTarget, Is.False);
            yield return Capture("floor-blackout", 1280, 720);
            yield return Capture("floor-blackout-narrow", 960, 720);
            Time.timeScale = 0;
            message.ShowNewFloor(12);
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(frame.canvasRenderer.GetAlpha(), Is.InRange(.2f, .7f));
            Assert.That(frame.canvasRenderer.GetAlpha(), Is.EqualTo(message.BackgroundColor.canvasRenderer.GetAlpha()).Within(.03f));
            // An interrupted opening must not let its old completion callback close the next blackout.
            message.HideScreen(13);
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(message.gameObject.activeSelf, Is.True);
            Assert.That(frame.canvasRenderer.GetAlpha(), Is.EqualTo(1).Within(.01f));
            message.ShowNewFloor(13);
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(message.gameObject.activeSelf, Is.False);
        }

        private IEnumerator Capture(string name, int width, int height)
        {
            gameView.GetType().GetMethod("SetCustomResolution",ViewFlags)
                .Invoke(gameView,new object[]{new Vector2(width,height),$"Dungeon audit {width}x{height}"});
            gameView.Repaint();
            yield return null;
            yield return new WaitForSecondsRealtime(.15f);
            Canvas.ForceUpdateCanvases();
            Assert.That(Screen.width,Is.EqualTo(width));Assert.That(Screen.height,Is.EqualTo(height));
            if(name.StartsWith("floor-blackout"))
            {
                var frame=harness.Game.NewFloorMessage.transform.Find("Floor transition frame") as RectTransform;
                var corners=new Vector3[4];frame.GetWorldCorners(corners);
                foreach(var corner in corners)
                { Assert.That(corner.x,Is.InRange(1,width-1));Assert.That(corner.y,Is.InRange(1,height-1)); }
            }
            ScreenCapture.CaptureScreenshot($"Temp/UIAudit/{name}.png");
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
#endif
