using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>Displays effective bindings and captures replacements without sending input to the game.</summary>
[DefaultExecutionOrder(-10)]
public sealed class ControlsRebindingScreen : MonoBehaviour
{
    public Button DeviceButton, ContextButton, ResetButton, CancelButton, ClearButton;
    public ScrollRect BindingScroll;
    public Button[] BindingButtons;
    public TMP_Text[] ActionLabels;
    public TMP_Text Status, CaptureLabel;
    public GameObject CapturePanel;
    public bool IsRebinding { get; private set; }
    private GlobalSettings settings;
    private bool gamepad, menus, initialized;
    private GameObject lastSelected;
    private Vector2 viewportSize;
    private readonly List<(InputAction action, int index)> entries = new();
    private InputActionRebindingExtensions.RebindingOperation operation;
    private Coroutine captureRoutine;
    private float deadline;
    private string pendingPath;
    private string captureName;
    private string captureInstruction;
    private InputAction captureAction;
    private int captureIndex;

    public void Initialize(GlobalSettings owner)
    {
        if (initialized) return;
        initialized = true;
        settings = owner;
        CapturePanel.SetActive(false);
        DeviceButton.onClick.AddListener(() => { gamepad = !gamepad; Refresh(); ScrollToTop(); });
        ContextButton.onClick.AddListener(() => { menus = !menus; Refresh(); ScrollToTop(); });
        ResetButton.onClick.AddListener(() =>
        {
            ControlBindings.ResetAll();
            Status.text = "All controls restored to defaults.";
            MenuUIInputModule.Active?.ConsumeInput();
        });
        CancelButton.onClick.AddListener(CancelRebind);
        ClearButton.onClick.AddListener(ClearCurrentBinding);
        for (int i = 0; i < BindingButtons.Length; i++) BindRow(i);
        Refresh();
    }

    private void OnEnable() { ControlBindings.Changed += Refresh; if (initialized) Refresh(); }
    private void OnDisable() { CancelRebind(); ControlBindings.Changed -= Refresh; }

    private void BindRow(int row) => BindingButtons[row].onClick.AddListener(() =>
    {
        if (row >= entries.Count) return;
        var entry = entries[row];
        BeginRebind(entry.action.actionMap.name + "/" + entry.action.name, entry.index);
    });

    private void EnsureRows(int count)
    {
        int existing = BindingButtons.Length;
        if (count <= existing) return;
        Array.Resize(ref BindingButtons, count);
        Array.Resize(ref ActionLabels, count);
        for (int i = existing; i < count; i++)
        {
            var row = Instantiate(BindingButtons[0].transform.parent, BindingScroll.content);
            row.name = "Binding row " + (i + 1);
            BindingButtons[i] = row.GetComponentInChildren<Button>(true);
            ActionLabels[i] = row.Find("Action").GetComponent<TMP_Text>();
            BindRow(i);
        }
    }

    private void ScrollToTop()
    {
        BindingScroll.StopMovement();
        BindingScroll.verticalNormalizedPosition = 1;
    }

    private static bool IsVisibleAction(InputAction action) => action.name != "Look" && action.name != "Fire" &&
        action.name != "Point" && action.name != "ScrollWheel" && action.name != "MiddleClick" &&
        !action.name.StartsWith("TrackedDevice", StringComparison.Ordinal);

    private static bool IsDeviceBinding(InputBinding binding, bool pad)
    {
        // Empty optional slots have no control path, so retain their authored device group.
        if (string.IsNullOrEmpty(binding.path))
            return (binding.groups ?? "").Split(';').Contains(pad ? "Gamepad" : "Keyboard&Mouse");
        return pad ? binding.path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase) :
            binding.path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) ||
            binding.path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase) || binding.path == "*/{Cancel}";
    }

    private void Refresh()
    {
        if (!initialized) return;
        // Hiding a row clears Unity's current selection.
        var selected = EventSystem.current?.currentSelectedGameObject;
        entries.Clear();
        foreach (var action in ControlBindings.DisplayAsset.FindActionMap(menus ? "UI" : "Player").actions)
        {
            if (!IsVisibleAction(action)) continue;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite) continue;
                // Keep a binding in its original device column after an override.
                if (IsDeviceBinding(binding, gamepad)) entries.Add((action, i));
            }
        }
        EnsureRows(entries.Count);
        Label(DeviceButton, gamepad ? "Controller" : "Keyboard / mouse");
        Label(ContextButton, menus ? "Menus" : "Gameplay");
        for (int row = 0; row < BindingButtons.Length; row++)
        {
            bool visible = row < entries.Count;
            BindingButtons[row].transform.parent.gameObject.SetActive(visible);
            if (!visible) continue;
            var entry = entries[row];
            ActionLabels[row].text = ActionName(entry.action, entry.index);
            Label(BindingButtons[row], ControlBindings.BindingLabel(entry.action, entry.index));
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(BindingScroll.content);
        settings.TabGroup.RefreshNavigation();
        if (selected != null && selected.transform.IsChildOf(transform) && !MenuUIInputModule.IsUsable(selected))
            DeviceButton.Select();
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        var selected = EventSystem.current?.currentSelectedGameObject;
        bool resized = viewportSize != BindingScroll.viewport.rect.size;
        viewportSize = BindingScroll.viewport.rect.size;
        if (selected == lastSelected && !resized) return;
        lastSelected = selected;
        if (!IsRebinding && selected != null)
        {
            int index = Array.FindIndex(BindingButtons, button => button.gameObject == selected);
            Status.text = index >= 0 && index < entries.Count && entries[index].action.name == "FastWalk"
                ? "Dungeon: hold Fast walk and choose a direction. At each stop, release/repress or change direction."
                : "Select a binding. Changes save automatically.";
        }
        // Wheel and drag scrolling must not snap back to an old selection.
        if (selected == null || !selected.transform.IsChildOf(BindingScroll.content) ||
            MenuUIInputModule.Active?.UI.Click.IsPressed() == true) return;
        Canvas.ForceUpdateCanvases();
        var viewport = BindingScroll.viewport;
        var row = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
        float overflow = BindingScroll.content.rect.height - viewport.rect.height;
        float offset = row.max.y > viewport.rect.yMax ? viewport.rect.yMax - row.max.y :
            row.min.y < viewport.rect.yMin ? viewport.rect.yMin - row.min.y : 0;
        if (overflow <= 0 || Mathf.Approximately(offset, 0)) return;
        BindingScroll.StopMovement();
        BindingScroll.verticalNormalizedPosition = Mathf.Clamp01(BindingScroll.verticalNormalizedPosition - offset / overflow);
    }

    private static void Label(Button button, string value) => button.GetComponentInChildren<TMP_Text>(true).text = value;
    private static string ActionName(InputAction action, int index)
    {
        string name = action.name switch
        {
            "Move" => "Move", "Navigate" => "Browse", "Attack" => "Attack / talk", "HoldPosition" => "Turn in place",
            "MoveUpLeft" => "Move up-left", "MoveUpRight" => "Move up-right",
            "MoveDownLeft" => "Move down-left", "MoveDownRight" => "Move down-right",
            "FastWalk" => "Fast walk (dungeon)",
            "Menu" or "OpenMenu" => "Inventory", "Skills" or "OpenSkillMenu" => "Skills",
            "Map" => "Map", "SwapAlly" or "InspectNextHero" => "Next hero", "InspectPreviousHero" => "Previous hero",
            "Options" => "Settings", "Wait" => "Wait a turn", "Submit" => "Confirm", "Cancel" => "Back",
            "Click" => "Select / activate", "RightClick" => "Cancel target", "OpenEquipment" => "Equipment",
            "OpenStats" => "Stats", "ToggleFullControl" => "Full Control", "Presentation" => "3D / terminal view",
            "History" => "Event history", "Interact" => "Interact / enter", _ => action.name
        };
        var binding = action.bindings[index];
        if (binding.isPartOfComposite)
        {
            int ordinal = 0;
            for (int i = 0; i <= index; i++)
                if (action.bindings[i].isPartOfComposite && action.bindings[i].name == binding.name &&
                    action.bindings[i].path.StartsWith("<Keyboard>")) ordinal++;
            name += " " + binding.name + (ordinal > 1 ? " (alternate)" : "");
        }
        else if (!string.IsNullOrEmpty(binding.path))
        {
            int ordinal = 0;
            string device = binding.path.Split('/')[0];
            for (int i = 0; i <= index; i++)
                if (!action.bindings[i].isComposite && action.bindings[i].path.StartsWith(device + "/")) ordinal++;
            if (ordinal > 1) name += " (alternate)";
        }
        return name;
    }

    public void BeginRebind(string actionPath, int bindingIndex)
    {
        if (IsRebinding || !isActiveAndEnabled) return;
        var action = ControlBindings.DisplayAsset.FindAction(actionPath, true);
        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite) return;
        IsRebinding = ControlBindings.IsCapturing = true;
        pendingPath = null;
        captureAction = action;
        captureIndex = bindingIndex;
        ClearButton.gameObject.SetActive(ControlBindings.IsOptionalBinding(action.bindings[bindingIndex]));
        deadline = Time.unscaledTime + 15;
        captureName = ActionName(action, bindingIndex);
        CapturePanel.SetActive(true);
        MenuUIInputModule.Active?.PushDialog(this, CapturePanel.transform, CancelButton.gameObject, CancelRebind, CancelRebind);
        captureRoutine = StartCoroutine(Capture(action, bindingIndex));
    }

    private IEnumerator Capture(InputAction action, int index)
    {
        bool pad = IsDeviceBinding(action.bindings[index], true);
        bool clearRequested = false;
        captureInstruction = pad ? (action.expectedControlType == "Vector2" ? "Move a controller stick or D-pad." : "Press a controller button.") : "Press a key or mouse button.";
        CaptureLabel.text = $"{captureName}\nRelease the current controls…";
        yield return new WaitForSecondsRealtime(.15f);
        while (AnyHeld()) yield return null;
        operation = action.PerformInteractiveRebinding(index)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Pointer>/position").WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/scroll").WithControlsExcluding("<Keyboard>/anyKey")
            .WithControlsExcluding("<Gamepad>/start")
            .WithExpectedControlType(action.bindings[index].isPartOfComposite ? "Button" :
                action.expectedControlType == "Vector2" ? "Vector2" : "Button")
            .OnMatchWaitForAnother(.1f)
            .OnApplyBinding((op, path) =>
            {
                // Dialog button clicks must never become bindings.
                clearRequested = op.selectedControl?.device is Mouse clearMouse &&
                    op.selectedControl == clearMouse.leftButton && ContainsPointer(ClearButton, clearMouse);
                bool dialogClick = op.selectedControl?.device is Mouse mouse &&
                    (ContainsPointer(CancelButton, mouse) || ContainsPointer(ClearButton, mouse));
                pendingPath = dialogClick ? null : path;
            })
            .OnCancel(_ => FinishCapture("Binding unchanged."))
            .OnComplete(_ =>
            {
                // Capture can finish before uGUI receives the pointer release.
                if (clearRequested) { ClearCurrentBinding(); return; }
                if (pendingPath == null) { FinishCapture("Binding unchanged."); return; }
                string path = pendingPath;
                FinishCapture("Binding saved.");
                ControlBindings.ApplyBinding(action.actionMap.name + "/" + action.name, index, path);
                bool shared = action.actionMap.actions.Any(other => other != action &&
                    other.bindings.Any(binding => !binding.isComposite && binding.effectivePath == path));
                Status.text = shared ? "Saved. This input is also used by another action." : "Binding saved.";
            });
        if (pad) operation.WithControlsHavingToMatchPath("<Gamepad>");
        else operation.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
        if (ControlBindings.IsOptionalBinding(action.bindings[index]))
            operation.WithControlsExcluding("<Keyboard>/delete").WithControlsExcluding("<Gamepad>/select");
        operation.Start();
        captureRoutine = null;
    }

    private static bool AnyHeld() => Keyboard.current?.anyKey.isPressed == true ||
        Mouse.current?.allControls.OfType<ButtonControl>().Any(button => button.isPressed) == true ||
        Gamepad.current?.allControls.OfType<ButtonControl>().Any(button => button.isPressed) == true;

    private static bool ContainsPointer(Button button, Mouse mouse) => button.gameObject.activeInHierarchy &&
        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, mouse.position.ReadValue());

    private void Update()
    {
        if (!IsRebinding) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.startButton.wasPressedThisFrame == true)
        { CancelRebind(); return; }
        if (ClearButton.gameObject.activeSelf && (Keyboard.current?.deleteKey.wasPressedThisFrame == true ||
            Gamepad.current?.selectButton.wasPressedThisFrame == true))
        { ClearCurrentBinding(); return; }
        if (Time.unscaledTime >= deadline) { FinishCapture("No input received. Binding unchanged."); return; }
        if (operation != null)
            CaptureLabel.text = $"{captureName}\n{captureInstruction}\n" +
                (ClearButton.gameObject.activeSelf ? "Delete / controller View: clear\n" : "") +
                $"Esc / controller Menu: cancel   •   {Mathf.CeilToInt(deadline - Time.unscaledTime)}s";
    }

    public void ClearCurrentBinding()
    {
        if (!IsRebinding || !ControlBindings.IsOptionalBinding(captureAction.bindings[captureIndex])) return;
        string actionPath = captureAction.actionMap.name + "/" + captureAction.name;
        int index = captureIndex;
        FinishCapture("Binding cleared.");
        ControlBindings.ClearBinding(actionPath, index);
    }

    public void CancelRebind() { if (IsRebinding) FinishCapture("Binding unchanged."); }
    private void FinishCapture(string message)
    {
        if (!IsRebinding) return;
        IsRebinding = ControlBindings.IsCapturing = false;
        ControlBindings.SuppressForFrame();
        if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
        operation?.Dispose(); operation = null;
        captureAction = null;
        CapturePanel.SetActive(false);
        MenuUIInputModule.Active?.PopDialog(this);
        Status.text = message;
        Refresh();
    }
}
