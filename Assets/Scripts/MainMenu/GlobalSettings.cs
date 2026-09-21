using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GlobalSettings : MonoBehaviour
{
    public GameObject SettingsCanvas;
    public GameObject FirstSelected;
    public NavigationHandler NavigationHandler;
    public Action CloseAction;
    public TabGroup TabGroup;
    public Button ResumeButton;
    public Button ReturntoMainButton;
    public bool IsOpen => SettingsCanvas != null && SettingsCanvas.activeInHierarchy;
    private bool returnToGameplay;

    private void Start()
    {
        TabGroup.Setup();
        SetupTabNavigation();
        SettingsCanvas.SetActive(false);
        gameObject.SetActive(false);
    }

    private void OnEnable() => TabGroup.TabClicked += HandleTabClicked;
    private void OnDisable() => TabGroup.TabClicked -= HandleTabClicked;

    private void SetupTabNavigation()
    {
        var tabs = TabGroup.TabContents;
        if (tabs.Count == 0) return;
        ResumeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = tabs[0].TabButton };
        ReturntoMainButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = tabs[^1].TabButton };
        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var contents = tab.Content.GetComponentsInChildren<Selectable>()
                .Where(s => s.IsActive() && s.IsInteractable()).ToArray();
            tab.TabButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? ResumeButton : tabs[i - 1].TabButton,
                selectOnDown = i == tabs.Count - 1 ? ReturntoMainButton : tabs[i + 1].TabButton,
                selectOnRight = tab == TabGroup.SelectedTab ? contents.FirstOrDefault() : null
            };
            for (int j = 0; j < contents.Length; j++)
            {
                var selectable = contents[j];
                selectable.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = j == 0 ? tab.TabButton : contents[j - 1],
                    selectOnDown = j == contents.Length - 1 ? ReturntoMainButton : contents[j + 1],
                    // Horizontal sliders retain Left/Right for value adjustment.
                    selectOnLeft = selectable is Slider ? null : tab.TabButton
                };
            }
        }
    }

    public void HandleTabClicked(TabContent tabContent)
    {
        // Activating a category keeps the highlight on it. Right enters its controls.
        SetupTabNavigation();
    }

    public void ShowDialog()
    {
        if (IsOpen) return;
        returnToGameplay = Common.Instance.MenuInputHandler.PlayerInput.currentActionMap?.name != "UI";
        gameObject.SetActive(true);
        SettingsCanvas.SetActive(true);
        TabGroup.Setup();
        SetupTabNavigation();
        NavigationHandler.Init();
        MenuUIInputModule.Active?.PushDialog(this, SettingsCanvas.transform, FirstSelected, Back, Exit_Clicked);
        Common.Instance.MenuInputHandler.SwitchToUIInput();
    }

    private void Back()
    {
        var tab = TabGroup.SelectedTab;
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (tab != null && selected != null && selected.transform.IsChildOf(tab.Content.transform))
            tab.TabButton.Select();
        else Exit_Clicked();
    }

    public void Exit_Clicked()
    {
        if (!IsOpen) return;
        SettingsCanvas.SetActive(false);
        MenuUIInputModule.Active?.PopDialog(this);
        if (returnToGameplay) Common.Instance.MenuInputHandler.SwitchToPlayerInput();
        else Common.Instance.MenuInputHandler.ClearInputThisFrame();
        var callback = CloseAction;
        CloseAction = null;
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    public void MainMenu_Clicked()
    {
        if (Common.Instance.CampaignContext != null)
        { Exit_Clicked(); Common.Instance.Travel.ReturnToMenu(); return; }
        var town = FindFirstObjectByType<Town>();
        if (town != null)
        {
            town.WriteSaveData();
            SaveSystem.SaveData(Common.Instance.GameSaveData);
        }
        else
        {
            var dungeon = FindFirstObjectByType<Game>();
            if (dungeon != null) GameOverScreen.CommitAbandonedRun(dungeon.PlayerController);
        }
        Exit_Clicked();
        SceneManager.LoadScene("MainMenu");
    }
}
