using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ClassPickerFocus : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    public Action Focused;
    public void OnSelect(BaseEventData data) => Focused?.Invoke();
    public void OnPointerEnter(PointerEventData data) => Focused?.Invoke();
}
