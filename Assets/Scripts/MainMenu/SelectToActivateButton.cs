using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pointer and directional navigation share selection. A pointer press can
/// activate only a button already selected when that press began.
/// Submit uses the standard Button behavior to activate the selected button.
/// </summary>
[AddComponentMenu("UI/Select To Activate Button")]
public class SelectToActivateButton : Button
{
    public enum SoundRole { Confirm, Cancel, Silent }
    [SerializeField] private SoundRole soundRole;
    public SoundRole AudioRole { get => soundRole; set => soundRole = value; }
    [SerializeField] private bool activateOnFirstPointerPress;
    public bool ActivateOnFirstPointerPress { get => activateOnFirstPointerPress; set => activateOnFirstPointerPress = value; }
    private bool canActivatePointer;
    internal bool PointerActivationPending => canActivatePointer;
    private int pressedPointerId;

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (MenuUIInputModule.Active != null && !MenuUIInputModule.Active.Allows(gameObject)) return;

        var eventSystem = EventSystem.current;
        canActivatePointer = IsActive() && IsInteractable() && eventSystem != null
            && (ActivateOnFirstPointerPress || eventSystem.currentSelectedGameObject == gameObject);
        pressedPointerId = eventData.pointerId;

        // Capture selection before Button.OnPointerDown changes it.
        base.OnPointerDown(eventData);
        if (IsActive() && IsInteractable() && eventSystem != null
            && eventSystem.currentSelectedGameObject != gameObject)
            eventSystem.SetSelectedGameObject(gameObject, eventData);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        bool activate = canActivatePointer && pressedPointerId == eventData.pointerId
            && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == gameObject
            && (MenuUIInputModule.Active == null || MenuUIInputModule.Active.Allows(gameObject));
        canActivatePointer = false;
        if (activate && IsActive() && IsInteractable())
        {
            PlayActivation();
            base.OnPointerClick(eventData);
        }
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        canActivatePointer = false;
        base.OnDeselect(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        var module = MenuUIInputModule.Active;
        if (IsActive() && IsInteractable() && (module == null || (!module.InputConsumed && module.Allows(gameObject))))
        {
            PlayActivation();
            base.OnSubmit(eventData);
        }
    }

    private void PlayActivation()
    {
        if (soundRole != SoundRole.Silent)
            AudioManager.Instance?.PlayCue(soundRole == SoundRole.Cancel ? SoundCue.Cancel : SoundCue.Confirm);
    }

    public static void MarkCancel(Button button)
    {
        if (button is SelectToActivateButton control) control.AudioRole = SoundRole.Cancel;
    }

    protected override void OnDisable()
    {
        canActivatePointer = false;
        base.OnDisable();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // Hover is not selection: only the selected button gets the highlight.
        base.DoStateTransition(state == SelectionState.Highlighted ? SelectionState.Normal : state, instant);
    }
}
