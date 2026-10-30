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
public sealed class ControlsRebindingScreen : MonoBehaviour
{
    public Button DeviceButton, ContextButton, PreviousPageButton, NextPageButton, ResetButton, CancelButton;
    public Button[] BindingButtons;
    public TMP_Text[] ActionLabels;
    public TMP_Text PageLabel, Status, CaptureLabel;
    public GameObject CapturePanel;
    public bool IsRebinding { get; private set; }
    private GlobalSettings settings;
    private bool gamepad, menus, initialized;
    private int page;
    private readonly List<(InputAction action, int index)> entries = new();
    private InputActionRebindingExtensions.RebindingOperation operation;
    private Coroutine captureRoutine;
    private float deadline;
    private string pendingPath;
    private string captureName;
    private string captureInstruction;

    public void Initialize(GlobalSettings owner)
    {
        if (initialized) return;
        initialized = true;
        settings = owner;
        CapturePanel.SetActive(false);
        DeviceButton.onClick.AddListener(() => { gamepad = !gamepad; page = 0; Refresh(); });
        ContextButton.onClick.AddListener(() => { menus = !menus; page = 0; Refresh(); });
        PreviousPageButton.onClick.AddListener(PreviousPage);
        NextPageButton.onClick.AddListener(NextPage);
        ResetButton.onClick.AddListener(() =>
        {
            ControlBindings.ResetAll();
            Status.text = "All controls restored to defaults.";
            MenuUIInputModule.Active?.ConsumeInput();
        });
        CancelButton.onClick.AddListener(CancelRebind);
        for (int i = 0; i < BindingButtons.Length; i++)
        {
            int row = i;
            BindingButtons[i].onClick.AddListener(() =>
            {
                int index = page * BindingButtons.Length + row;
                if (index >= entries.Count) return;
                var entry = entries[index];
                BeginRebind(entry.action.actionMap.name + "/" + entry.action.name, entry.index);
            });
        }
        Refresh();
    }

    private void OnEnable() { ControlBindings.Changed += Refresh; if (initialized) Refresh(); }
    private void OnDisable() { CancelRebind(); ControlBindings.Changed -= Refresh; }
    public void NextPage() { page++; Refresh(); }
    public void PreviousPage() { page--; Refresh(); }

    private static bool IsVisibleAction(InputAction action) => action.name != "Look" && action.name != "Fire" &&
        action.name != "Point" && action.name != "ScrollWheel" && action.name != "MiddleClick" &&
        !action.name.StartsWith("TrackedDevice", StringComparison.Ordinal);

    private void Refresh()
    {
        if (!initialized) return;
        entries.Clear();
        foreach (var action in ControlBindings.DisplayAsset.FindActionMap(menus ? "UI" : "Player").actions)
        {
            if (!IsVisibleAction(action)) continue;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite) continue;
                // Keep a binding in its original device column after an override.
                bool pad = binding.path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
                bool keyboard = binding.path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) ||
                    binding.path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase) || binding.path == "*/{Cancel}";
                if (gamepad ? pad : keyboard) entries.Add((action, i));
            }
        }
        int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)BindingButtons.Length));
        page = Mathf.Clamp(page, 0, pages - 1);
        Label(DeviceButton, gamepad ? "Controller" : "Keyboard / mouse");
        Label(ContextButton, menus ? "Menus" : "Gameplay");
        PageLabel.text = $"{page + 1} / {pages}";
        PreviousPageButton.interactable = page > 0;
        NextPageButton.interactable = page + 1 < pages;
        for (int row = 0; row < BindingButtons.Length; row++)
        {
            int index = page * BindingButtons.Length + row;
            bool visible = index < entries.Count;
            BindingButtons[row].gameObject.SetActive(visible);
            ActionLabels[row].gameObject.SetActive(visible);
            if (!visible) continue;
            var entry = entries[index];
            ActionLabels[row].text = ActionName(entry.action, entry.index);
            Label(BindingButtons[row], ControlBindings.BindingLabel(entry.action, entry.index));
        }
        settings.TabGroup.RefreshNavigation();
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected != null && selected.transform.IsChildOf(transform) && !MenuUIInputModule.IsUsable(selected))
            BindingButtons[0].Select();
    }

    private static void Label(Button button, string value) => button.GetComponentInChildren<TMP_Text>(true).text = value;
    private static string ActionName(InputAction action, int index)
    {
        string name = action.name switch
        {
            "Move" => "Move", "Navigate" => "Browse", "Attack" => "Attack / talk", "HoldPosition" => "Turn in place",
            "Plan" => "Dungeon orders", "Menu" or "OpenMenu" => "Inventory", "Skills" or "OpenSkillMenu" => "Skills",
            "Map" => "Map", "SwapAlly" or "InspectNextHero" => "Next hero", "InspectPreviousHero" => "Previous hero",
            "Options" => "Settings", "Wait" => "Wait a turn", "Submit" => "Confirm", "Cancel" => "Back",
            "Click" => "Select / activate", "RightClick" => "Cancel target", "OpenEquipment" => "Equipment",
            "OpenStats" => "Stats", "ToggleFullControl" => "Full Control", "Presentation" => "3D / terminal view",
            "Interact" => "Interact / enter", _ => action.name
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
        else
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
        deadline = Time.unscaledTime + 15;
        captureName = ActionName(action, bindingIndex);
        CapturePanel.SetActive(true);
        MenuUIInputModule.Active?.PushDialog(this, CapturePanel.transform, CancelButton.gameObject, CancelRebind, CancelRebind);
        captureRoutine = StartCoroutine(Capture(action, bindingIndex));
    }

    private IEnumerator Capture(InputAction action, int index)
    {
        bool pad = action.bindings[index].path.StartsWith("<Gamepad>");
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
                // A click on the visible Cancel button must never become a binding.
                bool cancelClick = op.selectedControl?.device is Mouse mouse &&
                    RectTransformUtility.RectangleContainsScreenPoint((RectTransform)CancelButton.transform, mouse.position.ReadValue());
                pendingPath = cancelClick ? null : path;
            })
            .OnCancel(_ => FinishCapture("Binding unchanged."))
            .OnComplete(_ =>
            {
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
        operation.Start();
        captureRoutine = null;
    }

    private static bool AnyHeld() => Keyboard.current?.anyKey.isPressed == true ||
        Mouse.current?.allControls.OfType<ButtonControl>().Any(button => button.isPressed) == true ||
        Gamepad.current?.allControls.OfType<ButtonControl>().Any(button => button.isPressed) == true;

    private void Update()
    {
        if (!IsRebinding) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.startButton.wasPressedThisFrame == true)
        { CancelRebind(); return; }
        if (Time.unscaledTime >= deadline) { FinishCapture("No input received. Binding unchanged."); return; }
        if (operation != null)
            CaptureLabel.text = $"{captureName}\n{captureInstruction}\nEsc / controller Menu: cancel   •   {Mathf.CeilToInt(deadline - Time.unscaledTime)}s";
    }

    public void CancelRebind() { if (IsRebinding) FinishCapture("Binding unchanged."); }
    private void FinishCapture(string message)
    {
        if (!IsRebinding) return;
        IsRebinding = ControlBindings.IsCapturing = false;
        ControlBindings.SuppressForFrame();
        if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
        operation?.Dispose(); operation = null;
        CapturePanel.SetActive(false);
        MenuUIInputModule.Active?.PopDialog(this);
        Status.text = message;
        Refresh();
    }
}
