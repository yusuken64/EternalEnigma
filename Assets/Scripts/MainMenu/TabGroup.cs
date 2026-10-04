using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TabGroup : MonoBehaviour
{
    public List<TabContent> TabContents;
    public TabContent SelectedTab { get; private set; }
    private bool initialized;

    public Color NormalColor = Color.white;
    public Color SelectedColor = Color.green;

    public Color NormalTextColor = Color.black;
    public Color SelectedTextColor = Color.black;

    public event Action<TabContent> TabSelected;
    public event Action<TabContent> TabActivated;
    public event Action NavigationChanged;
    public bool PreviewOnFocus;

    private void Bind(TabContent tab)
    {
        tab.TabButton.onClick.AddListener(() => { OnTabSelected(tab); TabActivated?.Invoke(tab); });
        if (PreviewOnFocus)
        {
            var preview = tab.TabButton.GetComponent<TabFocusPreview>() ?? tab.TabButton.gameObject.AddComponent<TabFocusPreview>();
            preview.Group = this;
        }
    }
    public void Preview(GameObject button)
    {
        var tab = TabContents.Find(t => t.TabButton.gameObject == button);
        if (tab != null) OnTabSelected(tab);
    }
    public void AddTab(TabContent tab)
    {
        TabContents.Add(tab);
        if (initialized) Bind(tab);
        tab.Content.SetActive(false);
    }
    public void RefreshNavigation() => NavigationChanged?.Invoke();

    private void Start()
    {
        Setup();
    }

    public void Setup()
    {
        if (initialized) return;
        initialized = true;
        foreach (var tab in TabContents)
        {
            Bind(tab);
        }

        if (TabContents.Any())
        {
            OnTabSelected(TabContents[0]);
        }
    }

    internal void SetToTab(int tabIndex)
    {
        OnTabSelected(TabContents[tabIndex]);
    }

    private void OnTabSelected(TabContent selectedTab)
    {
        if (SelectedTab == selectedTab) { RefreshNavigation(); return; }
        SelectedTab = selectedTab;
        foreach (var tab in TabContents)
        {
            bool isSelected = tab == selectedTab;
            tab.Content.SetActive(isSelected);
            SetCanvasGroupState(tab.Content, isSelected);

            var tabText = tab.TabButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tabText != null)
            {
                if (PreviewOnFocus)
                    tabText.fontStyle = isSelected ? tabText.fontStyle | FontStyles.Underline : tabText.fontStyle & ~FontStyles.Underline;
                tabText.DOKill();
                tabText.DOColor(isSelected ? SelectedTextColor : NormalTextColor, 0.12f).SetUpdate(true);
            }

            // Animate scale for a nice pop effect
            Transform tabTransform = tab.TabButton.transform;
            tabTransform.DOKill();
            tabTransform.DOScale(isSelected ? 1.05f : 1f, 0.12f).SetUpdate(true);
        }

        TabSelected?.Invoke(selectedTab);
        RefreshNavigation();
    }

    private void SetCanvasGroupState(GameObject content, bool isVisible)
    {
        var group = content.GetComponent<CanvasGroup>();
        if (group == null) return;
        group.DOKill();
        group.alpha = isVisible ? 1 : 0;
        group.interactable = isVisible;
        group.blocksRaycasts = isVisible;
    }

}

[System.Serializable]
public class TabContent
{
    public Button TabButton;
    public GameObject Content;
}
