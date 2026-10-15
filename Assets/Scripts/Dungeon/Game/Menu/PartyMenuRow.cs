using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Preview follows focus/hover. Only Submit/click invokes the button action.</summary>
public sealed class PartyMenuRow : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    public Action Selected;
    public static void FitHeight(AuthoredButton row)
    {
        var layout=row.GetComponent<LayoutElement>();
        if(layout==null)return;
        var rect=row.Label.rectTransform;
        float height=row.Label.GetPreferredValues(row.Label.text,Mathf.Max(1,rect.rect.width),0).y+8;
        float fraction=Mathf.Max(.1f,rect.anchorMax.y-rect.anchorMin.y);
        layout.preferredHeight=Mathf.Max(64,(height-rect.offsetMax.y+rect.offsetMin.y)/fraction);
    }
    public void OnSelect(BaseEventData data) => Selected?.Invoke();
    public void OnPointerEnter(PointerEventData data)
    {
        var button = GetComponent<Button>();
        if (button != null && button.IsActive() && button.IsInteractable() &&
            (MenuUIInputModule.Active == null || MenuUIInputModule.Active.Allows(gameObject)))
            EventSystem.current.SetSelectedGameObject(gameObject, data);
    }
}
