#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class MenuSelectionTests
    {
        private GameObject root;
        private EventSystem events;
        private MenuUIInputModule module;
        private Mouse mouse;
        private Gamepad pad;
        private SelectToActivateButton first;
        private SelectToActivateButton second;
        private int firstClicks;
        private int secondClicks;
        private CursorLockMode previousLock;
        private bool previousCursorVisible;
        private float previousTimeScale;
        private TestInputScope inputScope;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
            previousLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            previousTimeScale = Time.timeScale;
            Cursor.lockState = CursorLockMode.None;
            mouse = InputSystem.AddDevice<Mouse>();
            pad = InputSystem.AddDevice<Gamepad>();
            root = new GameObject("Menu selection test", typeof(Canvas), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var eventObject = new GameObject("EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(root.transform);
            events = eventObject.GetComponent<EventSystem>();
            module = eventObject.AddComponent<MenuUIInputModule>();
            module.actionsAsset.devices = new InputDevice[] { mouse, pad };
            module.deselectOnBackgroundClick = false;
            events.SendMessage("OnApplicationFocus", true);
            first = CreateButton("First", -120);
            second = CreateButton("Second", 120);
            first.onClick.AddListener(() => firstClicks++);
            second.onClick.AddListener(() => secondClicks++);
            first.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = second };
            second.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = first };
            firstClicks = secondClicks = 0;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private SelectToActivateButton CreateButton(string name, float x)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SelectToActivateButton));
            obj.transform.SetParent(root.transform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 60);
            rect.anchoredPosition = new Vector2(x, 0);
            var button = obj.GetComponent<SelectToActivateButton>();
            button.targetGraphic = obj.GetComponent<Image>();
            return button;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(pad);
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursorVisible;
            Time.timeScale = previousTimeScale;
            inputScope.Dispose();
        }

        private IEnumerator Pointer(SelectToActivateButton button, bool down)
        {
            var position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(down ? 1 : 0) });
            yield return null;
        }

        private IEnumerator Click(SelectToActivateButton button)
        {
            yield return Pointer(button, true);
            yield return Pointer(button, false);
        }

        private IEnumerator Pad(GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstClickSelectsSecondClickActivatesThroughInputModule()
        {
            yield return Click(second);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            Assert.That(secondClicks, Is.Zero);
            yield return Click(second);
            Assert.That(secondClicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HoverDoesNotSelectOrArmAnotherButton()
        {
            first.Select();
            yield return Pointer(second, false);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            yield return Click(second);
            Assert.That(secondClicks, Is.Zero);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));
        }

        [UnityTest]
        public IEnumerator DpadMovesHighlightAndConfirmActivatesExactlyOnce()
        {
            first.Select();
            yield return Pad(new GamepadState().WithButton(GamepadButton.DpadRight));
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            Assert.That(firstClicks + secondClicks, Is.Zero);
            yield return Pad(new GamepadState());
            yield return Pad(new GamepadState().WithButton(GamepadButton.South));
            yield return Pad(new GamepadState());
            Assert.That(secondClicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StickSelectionCanBeActivatedByOneMouseClick()
        {
            first.Select();
            yield return Pad(new GamepadState { leftStick = Vector2.right });
            yield return Pad(new GamepadState());
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            yield return Click(second);
            Assert.That(secondClicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SwitchingButtonsRequiresSelectingTheNewButtonFirst()
        {
            first.Select();
            yield return Click(second);
            yield return Click(first);
            Assert.That(firstClicks + secondClicks, Is.Zero);
            yield return Click(first);
            Assert.That(firstClicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisabledButtonsCannotActivateByClickOrSubmit()
        {
            first.Select();
            first.interactable = false;
            yield return Click(first);
            yield return Pad(new GamepadState().WithButton(GamepadButton.South));
            Assert.That(firstClicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReleasingOnAnotherButtonDoesNotActivateEither()
        {
            first.Select();
            yield return Pointer(first, true);
            yield return Pointer(second, false);
            Assert.That(firstClicks + secondClicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StickRecoversLostFocusWithoutMovingPastRememberedButton()
        {
            first.Select();
            yield return null;
            events.SetSelectedGameObject(null);
            yield return Pad(new GamepadState { leftStick = Vector2.right });
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            Assert.That(firstClicks + secondClicks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisabledRememberedButtonFallsBackInsideDialog()
        {
            module.PushDialog(first, root.transform, second.gameObject);
            first.Select();
            yield return null;
            first.interactable = false;
            events.SetSelectedGameObject(null);
            yield return Pad(new GamepadState { leftStick = Vector2.right });
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));
        }

        [UnityTest]
        public IEnumerator BackClosesOneDialogPerPressAndRestoresFocusWhilePaused()
        {
            int parentClosed = 0;
            int childClosed = 0;
            module.PushDialog(first, first.transform, first.gameObject, () => { parentClosed++; module.PopDialog(first); });
            module.PushDialog(second, second.transform, second.gameObject, () => { childClosed++; module.PopDialog(second); });
            Time.timeScale = 0;
            yield return null;
            yield return Pad(new GamepadState().WithButton(GamepadButton.East));
            Assert.That(childClosed, Is.EqualTo(1));
            Assert.That(parentClosed, Is.Zero);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            yield return Pad(new GamepadState());
            yield return Pad(new GamepadState().WithButton(GamepadButton.East));
            Assert.That(parentClosed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ModalBlocksClicksOnUnderlyingButtons()
        {
            module.PushDialog(second, second.transform, second.gameObject);
            yield return null;
            yield return Click(first);
            yield return Click(first);
            Assert.That(firstClicks, Is.Zero);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));
        }

        [UnityTest]
        public IEnumerator MouseWinsSimultaneousInputAndControllerNoiseDoesNotStealIt()
        {
            root.AddComponent<CursorManager>();
            Cursor.visible = true;
            yield return Pad(new GamepadState { leftStick = new Vector2(0.05f, 0) });
            Assert.That(Cursor.visible, Is.True);
            yield return Pad(new GamepadState().WithButton(GamepadButton.South));
            Assert.That(Cursor.visible, Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10, 10), delta = new Vector2(5, 0) });
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
            yield return null;
            Assert.That(Cursor.visible, Is.True);
            yield return Pad(new GamepadState { leftStick = Vector2.right });
            Assert.That(Cursor.visible, Is.True, "An unchanged held stick must not steal mouse ownership.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        }

        [UnityTest]
        public IEnumerator SelectionArrowArrivesWhileTimeScaleIsZero()
        {
            var navigation = root.AddComponent<NavigationHandler>();
            var arrow = new GameObject("Selection arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            arrow.SetParent(root.transform, false);
            navigation.selectionArrow = arrow;
            navigation.arrowOffset = Vector3.zero;
            arrow.gameObject.SetActive(false);
            first.Select();
            yield return null;
            Time.timeScale = 0;
            second.Select();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Vector3.Distance(arrow.position, second.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator AutoScrollLeavesVisibleRowsStillAndRevealsOnlyClippedRows()
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.sizeDelta = new Vector2(200, 100);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(200, 300);
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            var dialog = root.AddComponent<InventoryMenu>();
            dialog.scrollView = scroll;
            foreach (var button in new[] { first, second })
            {
                var rect = (RectTransform)button.transform;
                rect.SetParent(content, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(160, 30);
                rect.anchoredPosition = new Vector2(0, button == first ? -10 : -200);
            }
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
            dialog.ScrollToSelected(first.gameObject);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(0.001f));
            Time.timeScale = 0;
            dialog.ScrollToSelected(second.gameObject);
            yield return new WaitForSecondsRealtime(0.15f);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, (RectTransform)second.transform);
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 0.1f));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 0.1f));
            Assert.That(scroll.verticalNormalizedPosition, Is.GreaterThan(0), "Reveal the row without jumping to the list end.");
        }
    }
}
#endif
