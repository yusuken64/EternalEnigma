using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DungeonRowSelection : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public Image Frame;
    public void OnSelect(BaseEventData data) => Frame.enabled=true;
    public void OnDeselect(BaseEventData data) => Frame.enabled=false;
    private void OnDisable() { if(Frame!=null)Frame.enabled=false; }
}
