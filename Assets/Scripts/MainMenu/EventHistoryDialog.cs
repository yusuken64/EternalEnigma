using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EventHistoryDialog : Dialog
{
    public RectTransform Panel;
    public TMP_Text Entries;
    public TrainerPreviewScroll Reader;
    public Button Back;
    public void Setup(System.Collections.Generic.IReadOnlyList<string> entries, bool dungeon)
    {
        // The shared dialog keeps the established parchment treatment in travel scenes.
        foreach(var role in GetComponentsInChildren<DungeonUIRole>(true))
        {
            role.Apply();
            var image=role.GetComponent<Image>();
            if(role.Role==DungeonVisualRole.Heading)image.enabled=dungeon;
            if(!dungeon && (role.Role==DungeonVisualRole.Wood || role.Role==DungeonVisualRole.Paper))
                GameUITheme.Current.Surface(image,GameUITheme.Current.Panel,1);
            if(!dungeon && (role.Role==DungeonVisualRole.Secondary || role.Role==DungeonVisualRole.Close))
                GameUITheme.Current.Surface(image,GameUITheme.Current.Button,1);
            if(role.Role==DungeonVisualRole.Close)
                foreach(var label in role.GetComponentInParent<Button>(true).GetComponentsInChildren<TMP_Text>(true))label.text=dungeon?"":"X";
        }
        foreach(var style in GetComponentsInChildren<DungeonTextStyle>(true))
            style.GetComponent<TMP_Text>().color=dungeon && style.OnWood?GameUITheme.LightInk:GameUITheme.Ink;
        Panel.anchorMin = dungeon ? new Vector2(.60f,.03f) : new Vector2(.20f,.12f);
        Panel.anchorMax = dungeon ? new Vector2(.98f,.88f) : new Vector2(.80f,.88f);
        Entries.text = entries.Count == 0 ? "No events yet." : string.Join("\n\n", entries);
        Entries.richText = false;
        Back.onClick.RemoveAllListeners(); Back.onClick.AddListener(CloseDialog);
        Canvas.ForceUpdateCanvases();
        Entries.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scrollView.viewport.rect.height, Entries.GetPreferredValues(Entries.text, scrollView.viewport.rect.width, 0).y + 24));
        scrollView.verticalNormalizedPosition = 0;
    }
    internal override void SetFirstSelect() { Reader.Select(); scrollView.verticalNormalizedPosition = 0; }
}
