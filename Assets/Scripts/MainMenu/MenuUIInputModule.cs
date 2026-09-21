using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

/// <summary>Owns the menu action map, dialog focus and input consumption for one scene.</summary>
public class MenuUIInputModule : InputSystemUIInputModule
{
    public static MenuUIInputModule Active { get; private set; }
    public DungeonControls.UIActions UI => controls.UI;
    public bool InputConsumed => consumedFrame == Time.frameCount;
    private DungeonControls controls;
    private int consumedFrame = -1;
    private GameObject rememberedSelection;
    private readonly List<InputActionReference> references = new();
    private readonly List<Scope> scopes = new();

    private sealed class Scope
    {
        public MonoBehaviour Owner;
        public Transform Root;
        public GameObject First;
        public GameObject Remembered;
        public GameObject ReturnTo;
        public Action Back;
        public Action Options;
    }

    protected override void Awake()
    {
        base.Awake();
        controls = new DungeonControls();
        actionsAsset = controls.asset;
        point = Reference(UI.Point);
        move = Reference(UI.Navigate);
        submit = Reference(UI.Submit);
        cancel = Reference(UI.Cancel);
        leftClick = Reference(UI.Click);
        rightClick = Reference(UI.RightClick);
        middleClick = Reference(UI.MiddleClick);
        scrollWheel = Reference(UI.ScrollWheel);
        trackedDevicePosition = Reference(UI.TrackedDevicePosition);
        trackedDeviceOrientation = Reference(UI.TrackedDeviceOrientation);
        moveRepeatDelay = 0.3f;
        moveRepeatRate = 0.1f;
        deselectOnBackgroundClick = false;
    }

    private InputActionReference Reference(InputAction action)
    {
        var reference = InputActionReference.Create(action);
        references.Add(reference);
        return reference;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Active = this;
        controls?.UI.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        controls?.UI.Disable();
        if (Active == this) Active = null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (var reference in references) Destroy(reference);
        controls?.Dispose();
    }

    public void ConsumeInput() => consumedFrame = Time.frameCount;

    public static bool IsUsable(GameObject obj)
    {
        if (obj == null || !obj.activeInHierarchy) return false;
        var selectable = obj.GetComponent<Selectable>();
        return selectable != null && selectable.IsActive() && selectable.IsInteractable();
    }

    public bool Allows(GameObject obj)
    {
        return obj != null && (scopes.Count == 0 ||
            (scopes[^1].Root != null && obj.transform.IsChildOf(scopes[^1].Root)));
    }

    public void PushDialog(MonoBehaviour owner, Transform root, GameObject first = null,
        Action back = null, Action options = null)
    {
        if (scopes.Exists(s => s.Owner == owner)) return;
        RememberSelection();
        scopes.Add(new Scope { Owner = owner, Root = root, First = first,
            ReturnTo = eventSystem.currentSelectedGameObject, Back = back, Options = options });
        ConsumeInput();
        eventSystem.SetSelectedGameObject(null);
        if (IsUsable(first)) eventSystem.SetSelectedGameObject(first);
    }

    public void PopDialog(MonoBehaviour owner)
    {
        int index = scopes.FindIndex(s => s.Owner == owner);
        if (index < 0) return;
        var scope = scopes[index];
        bool wasTop = index == scopes.Count - 1;
        scopes.RemoveAt(index);
        if (!wasTop) return;
        ConsumeInput();
        if (IsUsable(scope.ReturnTo) && Allows(scope.ReturnTo))
            eventSystem.SetSelectedGameObject(scope.ReturnTo);
        else
        {
            eventSystem.SetSelectedGameObject(null);
            RestoreFocus();
        }
    }

    private void RememberSelection()
    {
        var current = eventSystem.currentSelectedGameObject;
        if (!IsUsable(current) || !Allows(current)) return;
        rememberedSelection = current;
        if (scopes.Count > 0) scopes[^1].Remembered = current;
    }

    public void RestoreFocus(GameObject fallback = null)
    {
        if (IsUsable(eventSystem.currentSelectedGameObject) && Allows(eventSystem.currentSelectedGameObject)) return;
        GameObject next = null;
        if (scopes.Count > 0)
        {
            var scope = scopes[^1];
            if (IsUsable(scope.Remembered) && Allows(scope.Remembered)) next = scope.Remembered;
            else if (IsUsable(scope.First) && Allows(scope.First)) next = scope.First;
            else if (scope.Root != null)
                foreach (var selectable in scope.Root.GetComponentsInChildren<Selectable>())
                    if (IsUsable(selectable.gameObject)) { next = selectable.gameObject; break; }
        }
        else
        {
            if (IsUsable(rememberedSelection)) next = rememberedSelection;
            else if (IsUsable(fallback)) next = fallback;
            else if (IsUsable(eventSystem.firstSelectedGameObject)) next = eventSystem.firstSelectedGameObject;
        }
        eventSystem.SetSelectedGameObject(next);
    }

    public override void Process()
    {
        if (AutoplayRunner.Active != null) { consumedFrame = Time.frameCount; return; }
        scopes.RemoveAll(s => s.Owner == null || s.Root == null || !s.Root.gameObject.activeInHierarchy);
        // World targets use directional/confirm input in TargetDialog and MenuManager.
        // Keep EventSystem enabled so a settings dialog pushed above targeting still works.
        if (scopes.Count > 0 && scopes[^1].Owner is JuicyChickenGames.Menu.TargetDialog) return;
        RememberSelection();
        if (!InputConsumed && scopes.Count > 0)
        {
            var scope = scopes[^1];
            if (UI.Cancel.WasPressedThisFrame())
            {
                // Let native widgets (e.g. an expanded dropdown) handle Back first.
                var data = new BaseEventData(eventSystem);
                var selected = eventSystem.currentSelectedGameObject;
                bool collapsedDropdown = selected != null &&
                    ((selected.TryGetComponent<TMP_Dropdown>(out var tmp) && !tmp.IsExpanded) ||
                     selected.GetComponent<Dropdown>() != null);
                bool handled = !collapsedDropdown && ExecuteEvents.Execute(selected, data, ExecuteEvents.cancelHandler);
                if (!handled && !data.used) scope.Back?.Invoke();
                ConsumeInput();
            }
            else if (UI.Options.WasPressedThisFrame() && scope.Options != null)
            {
                scope.Options();
                ConsumeInput();
            }
        }

        bool recovering = !IsUsable(eventSystem.currentSelectedGameObject) || !Allows(eventSystem.currentSelectedGameObject);
        if (recovering && (UI.Navigate.ReadValue<Vector2>().sqrMagnitude > 0.1f || UI.Submit.WasPressedThisFrame()))
        {
            RestoreFocus();
            // Recover the highlight without also moving past or activating it.
            // With no open UI, these same keys belong to world movement.
            if (IsUsable(eventSystem.currentSelectedGameObject) && Allows(eventSystem.currentSelectedGameObject))
                ConsumeInput();
        }

        bool sendNavigation = eventSystem.sendNavigationEvents;
        if (InputConsumed) eventSystem.sendNavigationEvents = false;
        try { base.Process(); }
        finally { eventSystem.sendNavigationEvents = sendNavigation; }

        if (scopes.Count > 0 && !Allows(eventSystem.currentSelectedGameObject)) RestoreFocus();
        RememberSelection();
    }
}
