using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TabFocusPreview : MonoBehaviour, ISelectHandler
{
    public TabGroup Group;
    public void OnSelect(BaseEventData eventData) => Group?.Preview(gameObject);
}
