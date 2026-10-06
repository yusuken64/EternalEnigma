using UnityEngine;
using UnityEngine.EventSystems;

// Portrait hit areas rise above the transparent dock shield only while browsing.
// They never intercept targeting, settings, or a pending item confirmation.
public sealed class DungeonPortraitControl : MonoBehaviour, IPointerClickHandler
{
    private Canvas portraitCanvas;
    private void Awake() => portraitCanvas=GetComponent<Canvas>();
    private void LateUpdate()
    {
        var manager=MenuManager.Instance;
        bool browsing=manager!=null && manager.CurrentDialog is PartyMenu && !Common.Instance.GlobalSettings.IsOpen;
        portraitCanvas.overrideSorting=browsing;
        if(browsing)portraitCanvas.sortingOrder=manager.CurrentDialog.GetComponent<Canvas>().sortingOrder+1;
    }
    public void OnPointerClick(PointerEventData data)
    {
        if(data.button!=PointerEventData.InputButton.Left || Game.Instance==null)return;
        var display=GetComponentInParent<CharacterStatsDisplay>();
        if(display?.Character is Ally ally)Game.Instance.PlayerController.TryControlFromUI(ally);
    }
}
