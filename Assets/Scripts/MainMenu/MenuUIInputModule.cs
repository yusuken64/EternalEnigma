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
    public bool HasDialog => scopes.Count > 0;
    public bool OwnsFocus(MonoBehaviour owner) => scopes.Count > 0 && scopes[^1].Owner == owner;
    public InputAction NextHero { get; private set; }
    public InputAction PreviousHero { get; private set; }
    public bool UsingGamepad => ControlDeviceState.Gamepad;
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
        var uiMap = controls.asset.FindActionMap("UI");
        NextHero = uiMap.AddAction("InspectNextHero", InputActionType.Button);
        NextHero.AddBinding("<Keyboard>/tab"); NextHero.AddBinding("<Gamepad>/rightShoulder");
        PreviousHero = uiMap.AddAction("InspectPreviousHero", InputActionType.Button);
        PreviousHero.AddBinding("<Gamepad>/leftTrigger");
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
        // Script reload restores the component, but not the generated action wrapper.
        if (controls == null) Awake();
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
        if (AutoplayRunner.BlocksPlayerInput)
            return obj != null && AutoplayRunner.Active.ShowPlaybackUI && obj.GetComponentInParent<AutoplayPanel>() != null;
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
        ControlDeviceState.Poll();
        if (AutoplayRunner.BlocksPlayerInput)
        {
            consumedFrame = Time.frameCount;
            if (!AutoplayRunner.Active.ShowPlaybackUI || AutoplayRunner.Active.GetComponentInChildren<AutoplayPanel>() == null) return;
            if (!Allows(eventSystem.currentSelectedGameObject)) eventSystem.SetSelectedGameObject(null);
            // The playback canvas shields underlying UI. Keep pointer controls alive while
            // the runner owns keyboard/controller shortcuts and gameplay stays blocked.
            bool navigation = eventSystem.sendNavigationEvents;
            eventSystem.sendNavigationEvents = false;
            try { base.Process(); }
            finally { eventSystem.sendNavigationEvents = navigation; }
            return;
        }
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
                bool handled = Allows(selected) && !collapsedDropdown && ExecuteEvents.Execute(selected, data, ExecuteEvents.cancelHandler);
                if (!handled && !data.used)
                {
                    var destination = CancelFocusScope.Resolve(selected, scope.Root);
                    if (destination != null) eventSystem.SetSelectedGameObject(destination);
                    else scope.Back?.Invoke();
                }
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
