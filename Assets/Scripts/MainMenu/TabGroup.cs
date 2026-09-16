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

    public event Action<TabContent> TabClicked;
    public void NotifyTabClicked(TabContent tab)
    {
        TabClicked?.Invoke(tab);
    }

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
            tab.TabButton.onClick.AddListener(() => OnTabSelected(tab));
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
        SelectedTab = selectedTab;
        foreach (var tab in TabContents)
        {
            bool isSelected = tab == selectedTab;
            tab.Content.SetActive(isSelected);
            SetCanvasGroupState(tab.Content, isSelected);

            var tabText = tab.TabButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tabText != null)
            {
                tabText.DOKill();
                tabText.DOColor(isSelected ? SelectedTextColor : NormalTextColor, 0.12f).SetUpdate(true);
            }

            // Animate scale for a nice pop effect
            Transform tabTransform = tab.TabButton.transform;
            tabTransform.DOKill();
            tabTransform.DOScale(isSelected ? 1.05f : 1f, 0.12f).SetUpdate(true);
        }

        NotifyTabClicked(selectedTab);
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
