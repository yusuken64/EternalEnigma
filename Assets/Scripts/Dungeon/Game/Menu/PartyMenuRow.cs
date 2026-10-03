using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Preview follows focus/hover. Only Submit/click invokes the button action.</summary>
public sealed class PartyMenuRow : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    public Action Selected;
    public void OnSelect(BaseEventData data) => Selected?.Invoke();
    public void OnPointerEnter(PointerEventData data)
    {
        if (MenuUIInputModule.Active == null || MenuUIInputModule.Active.Allows(gameObject))
            GetComponent<Button>().Select();
    }
}
