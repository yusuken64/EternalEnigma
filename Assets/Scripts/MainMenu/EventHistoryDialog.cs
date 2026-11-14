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
    private Vector2 layoutSize;
    private RectTransform compactPanel;
    private Vector2 buttonMin,buttonMax,buttonOffsetMin,buttonOffsetMax;
    private Vector2 viewportMin,viewportMax,viewportOffsetMin,viewportOffsetMax;
    private bool usesFeed;
    private float scrollPosition;

    internal void UseFeed(RectTransform feedPanel)
    {
        var original = Panel;
        // Match the compact feed's full-canvas coordinates. The dialog's safe-area
        // container would otherwise narrow only the expanded view on inset displays.
        Panel = Instantiate(feedPanel, transform);
        Panel.name = "Expanded events";
        original.gameObject.SetActive(false);
        Destroy(original.gameObject);
        var group = Panel.GetComponent<CanvasGroup>();
        group.alpha = 1;
        group.blocksRaycasts = group.interactable = true;
        scrollView = Panel.GetComponent<ScrollRect>();
        Entries = scrollView.content.GetComponent<TMP_Text>();
        Back = Panel.GetComponentInChildren<Button>();
        Back.onClick = new Button.ButtonClickedEvent();
        SelectToActivateButton.MarkCancel(Back);
        Reader = scrollView.viewport.gameObject.AddComponent<TrainerPreviewScroll>();
        Reader.Scroll = scrollView;
        Reader.targetGraphic = scrollView.viewport.GetComponent<Graphic>();
        compactPanel=feedPanel;usesFeed=true;
        var button=(RectTransform)Back.transform;
        buttonMin=button.anchorMin;buttonMax=button.anchorMax;
        buttonOffsetMin=button.offsetMin;buttonOffsetMax=button.offsetMax;
        viewportMin=scrollView.viewport.anchorMin;viewportMax=scrollView.viewport.anchorMax;
        viewportOffsetMin=scrollView.viewport.offsetMin;viewportOffsetMax=scrollView.viewport.offsetMax;
        Fit(Panel,feedPanel.anchorMin.x,feedPanel.anchorMin.y,feedPanel.anchorMax.x,.88f);
        Panel.offsetMin=feedPanel.offsetMin;Panel.offsetMax=new Vector2(feedPanel.offsetMax.x,0);
    }

    public void Setup(System.Collections.Generic.IReadOnlyList<string> entries)
    {
        Entries.text = GameplayText.Plain(entries.Count == 0 ? "No events yet." : string.Join("\n", entries));
        Entries.richText = false;
        Back.onClick.RemoveAllListeners(); Back.onClick.AddListener(CloseDialog);
        Reader.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=Back};
        Back.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=Reader,selectOnDown=Reader};
        RefreshLayout();
        scrollView.verticalNormalizedPosition = 0;
        scrollPosition = 0;
    }

    private void RefreshLayout()
    {
        Canvas.ForceUpdateCanvases();
        layoutSize = Panel.rect.size;
        if (usesFeed)
        {
            float normalHeight = compactPanel.rect.height;
            var button = (RectTransform)Back.transform;
            Fit(button,buttonMin.x,1,buttonMax.x,1);
            button.offsetMin=new Vector2(buttonOffsetMin.x,normalHeight*(buttonMin.y-1)+buttonOffsetMin.y);
            button.offsetMax=new Vector2(buttonOffsetMax.x,normalHeight*(buttonMax.y-1)+buttonOffsetMax.y);
            Fit(scrollView.viewport,viewportMin.x,0,viewportMax.x,1);
            scrollView.viewport.offsetMin=new Vector2(viewportOffsetMin.x,normalHeight*viewportMin.y+viewportOffsetMin.y);
            scrollView.viewport.offsetMax=new Vector2(viewportOffsetMax.x,normalHeight*(viewportMax.y-1)+viewportOffsetMax.y);
            Canvas.ForceUpdateCanvases();
        }
        Entries.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scrollView.viewport.rect.height, Entries.GetPreferredValues(Entries.text, scrollView.viewport.rect.width, 0).y));
        Canvas.ForceUpdateCanvases();
    }

    private void LateUpdate()
    {
        if (Owner == null) return;
        if (layoutSize != Panel.rect.size)
        {
            RefreshLayout();
            scrollView.verticalNormalizedPosition = scrollPosition;
        }
        scrollPosition = scrollView.verticalNormalizedPosition;
    }

    internal override void SetFirstSelect() { Reader.Select(); scrollView.verticalNormalizedPosition = 0; }
}
