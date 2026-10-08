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
    public void Setup(System.Collections.Generic.IReadOnlyList<string> entries)
    {
        UseGameplayDock(Panel);
        foreach(var role in GetComponentsInChildren<DungeonUIRole>(true))role.Apply();
        foreach(var style in GetComponentsInChildren<DungeonTextStyle>(true))
            style.GetComponent<TMP_Text>().color=style.OnWood?GameUITheme.LightInk:GameUITheme.Ink;
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
