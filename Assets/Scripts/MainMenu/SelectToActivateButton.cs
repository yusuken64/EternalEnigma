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
    private bool canActivatePointer;
    private int pressedPointerId;

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (MenuUIInputModule.Active != null && !MenuUIInputModule.Active.Allows(gameObject)) return;

        var eventSystem = EventSystem.current;
        canActivatePointer = IsActive() && IsInteractable() && eventSystem != null
            && eventSystem.currentSelectedGameObject == gameObject;
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
        if (activate)
            base.OnPointerClick(eventData);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        canActivatePointer = false;
        base.OnDeselect(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        var module = MenuUIInputModule.Active;
        if (module == null || (!module.InputConsumed && module.Allows(gameObject)))
            base.OnSubmit(eventData);
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
