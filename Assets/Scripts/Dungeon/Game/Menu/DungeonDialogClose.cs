using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonDialogClose : MonoBehaviour
{
    private void Awake() => GetComponent<Button>().onClick.AddListener(() => GetComponentInParent<Dialog>().CloseDialog());

    // Dock variants can contain a legacy footer Back as well as their corner close.
    // Keep one visible exit, including when there are no available actions.
    public static Button ShowSingle(Transform panel)
    {
        var controls = panel.GetComponentsInChildren<DungeonDialogClose>(true);
        DungeonDialogClose selected = null;
        foreach (var control in controls)
            if (selected == null || control.name == "Close") selected = control;
        foreach (var control in controls) control.gameObject.SetActive(control == selected);
        if (selected != null && selected.name == "Close")
        {
            var rect=(RectTransform)selected.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.98f,.98f);rect.pivot=Vector2.one;
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(64,64);
            foreach(var title in panel.GetComponentsInChildren<TMP_Text>(true))
                if(title.transform.parent==panel && title.rectTransform.anchorMin.y>=.84f && title.rectTransform.anchorMax.x>.80f)
                {
                    var anchor=title.rectTransform.anchorMax;anchor.x=.80f;title.rectTransform.anchorMax=anchor;
                }
        }
        return selected != null ? selected.GetComponent<Button>() : null;
    }
}
