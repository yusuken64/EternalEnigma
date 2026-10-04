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

    public void ShowEventHistory() { Exit_Clicked(); GameMessages.ShowHistory(); }

    private void Start()
    {
        DungeonOptions.AddTo(this);
        TabGroup.Setup();
        SetupTabNavigation();
        SettingsCanvas.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        TabGroup.PreviewOnFocus = true;
        NavigationHandler.FocusRoot = SettingsCanvas.transform;
        NavigationHandler.FocusOwner = this;
        foreach (var tab in TabGroup.TabContents)
        {
            var details = tab.Content.GetComponent<CancelFocusScope>() ?? tab.Content.AddComponent<CancelFocusScope>();
            details.ReturnTarget = tab.TabButton.gameObject;
            var category = tab.TabButton.GetComponent<CancelFocusScope>() ?? tab.TabButton.gameObject.AddComponent<CancelFocusScope>();
            category.ReturnTarget = ResumeButton.gameObject;
        }
    }
    private void OnEnable()
    {
        TabGroup.TabActivated += EnterTab;
        TabGroup.NavigationChanged += SetupTabNavigation;
    }
    private void OnDisable()
    {
        TabGroup.TabActivated -= EnterTab;
        TabGroup.NavigationChanged -= SetupTabNavigation;
    }

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

    private void EnterTab(TabContent tab)
    {
        SetupTabNavigation();
        var first = tab.Content.GetComponentsInChildren<Selectable>().FirstOrDefault(s => MenuUIInputModule.IsUsable(s.gameObject));
        if (first != null) first.Select();
        MenuUIInputModule.Active?.ConsumeInput();
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
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == ResumeButton.gameObject) Exit_Clicked();
        else ResumeButton.Select();
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
        if (AutoplayRunner.Active != null && AutoplayRunner.Active.PlayerControlled) { Exit_Clicked(); AutoplayRunner.Active.ExitDemo(); return; }
        if (Common.Instance.CampaignContext != null)
        { CampaignChoice.Show("Return to the main menu? Unsaved progress will be lost.", "Quit without saving", () => { Exit_Clicked(); Common.Instance.Travel.ReturnToMenu(); }); return; }
        var town = FindFirstObjectByType<Town>();
        if (town != null)
        {
            town.WriteSaveData();
            SaveSystem.Capture(Common.Instance);
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
