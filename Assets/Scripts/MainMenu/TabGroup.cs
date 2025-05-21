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
        foreach (var tab in TabContents)
        {
            bool isSelected = tab == selectedTab;
            tab.Content.SetActive(isSelected);
            SetCanvasGroupState(tab.Content, isSelected);

            var image = tab.TabButton.targetGraphic;
            var tabText = tab.TabButton.GetComponentInChildren<TextMeshProUGUI>();
            if (image != null)
            {
                image.DOColor(isSelected ? SelectedColor : NormalColor, 0.2f);
            }
            if (tabText != null)
            {
                tabText.DOColor(isSelected ? SelectedTextColor : NormalTextColor, 0.2f);
            }

            // Animate scale for a nice pop effect
            Transform tabTransform = tab.TabButton.transform;
            tabTransform.DOScale(isSelected ? 1.1f : 1f, 0.2f).SetEase(Ease.OutBack);
        }

        NotifyTabClicked(selectedTab);
    }

    private void SetCanvasGroupState(GameObject content, bool isVisible)
    {
        var group = content.GetComponent<CanvasGroup>();
        var rectTransform = content.GetComponent<RectTransform>();
        var duration = 0.3f;

        if (group != null && rectTransform != null)
        {
            // Slide in from the left
            if (isVisible)
            {
                // Move from the left side of the screen (you can adjust the X value if needed)
                rectTransform.DOLocalMoveX(0, duration).SetEase(Ease.OutBack);
            }
            else
            {
                // Move offscreen to the left
                rectTransform.DOLocalMoveX(Screen.width, duration).SetEase(Ease.InBack);
            }

            // Animate fade in/out
            group.DOFade(isVisible ? 1 : 0, duration);

            // Set interactable and raycastable based on visibility
            group.interactable = isVisible;
            group.blocksRaycasts = isVisible;
        }
    }
}

[System.Serializable]
public class TabContent
{
    public Button TabButton;
    public GameObject Content;
}
