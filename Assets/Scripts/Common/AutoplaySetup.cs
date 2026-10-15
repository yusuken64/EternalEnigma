using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Main-menu launcher, instantiated from the authored debug setup prefab.</summary>
public sealed class AutoplaySetup : MonoBehaviour
{
    public TMP_InputField Seed, RunCount;
    public TMP_Text Validation, SpeedLabel, GodmodeLabel, ResourcesLabel, CoverageLabel;
    public Button Launch, Back, Speed, Godmode, InfiniteResources, Coverage;
    private readonly AutoplayOptions options = new() { DebugPlaythrough = true, DebugCoverage = true, Speed = 1 };
    private GameObject previousSelection;
    private CanvasGroup menuGroup;

    public static void Show(MainMenu menu)
    {
        if (FindFirstObjectByType<AutoplaySetup>() != null || AutoplayRunner.Active != null) return;
        var view = Instantiate(Resources.Load<AutoplaySetup>("UI/AutoplaySetup"));
        view.previousSelection = EventSystem.current?.currentSelectedGameObject;
        view.menuGroup = menu.NavigationHandler.GetComponent<CanvasGroup>();
        if (view.menuGroup == null) view.menuGroup = menu.NavigationHandler.gameObject.AddComponent<CanvasGroup>();
        view.menuGroup.interactable = view.menuGroup.blocksRaycasts = false;
        view.Bind();
    }

    private void Bind()
    {
        Seed.text = options.Seed.ToString(CultureInfo.InvariantCulture); RunCount.text = options.RunCount.ToString(CultureInfo.InvariantCulture);
        Seed.onValueChanged.AddListener(_ => Refresh()); RunCount.onValueChanged.AddListener(_ => Refresh());
        Launch.onClick.AddListener(StartBatch); Back.onClick.AddListener(Close);
        Speed.onClick.AddListener(() => { int index = System.Array.IndexOf(AutoplayPanel.Rates,options.Speed); options.Speed = AutoplayPanel.Rates[(index+1)%AutoplayPanel.Rates.Length]; Refresh(); });
        Godmode.onClick.AddListener(() => { options.Godmode = !options.Godmode; options.InfiniteStrength = options.Godmode; Refresh(); });
        InfiniteResources.onClick.AddListener(() => { options.InfiniteResources = !options.InfiniteResources; Refresh(); });
        Coverage.onClick.AddListener(() => { options.DebugCoverage = !options.DebugCoverage; Refresh(); });
        Refresh();
        MenuUIInputModule.Active?.PushDialog(this,transform,Launch.gameObject,Close);
        Launch.Select();
    }

    private bool Read(out string reason)
    {
        if (!int.TryParse(Seed.text,NumberStyles.Integer,CultureInfo.InvariantCulture,out options.Seed)) { reason = "Enter a starting seed from -2147483648 to 2147483647."; return false; }
        if (!int.TryParse(RunCount.text,NumberStyles.Integer,CultureInfo.InvariantCulture,out options.RunCount)) { reason = "Enter a whole number of runs."; return false; }
        return options.Validate(out reason);
    }

    private void Refresh()
    {
        Launch.interactable = Read(out var reason);
        Validation.text = reason ?? $"Seeds {options.Seed} to {options.SeedForRun(options.RunCount-1)} · isolated campaigns";
        SpeedLabel.text = $"Speed {options.Speed:0.#}×";
        GodmodeLabel.text = "Invincibility and unlimited strength " + (options.Godmode ? "On" : "Off");
        ResourcesLabel.text = "Unlimited resources " + (options.InfiniteResources ? "On" : "Off");
        CoverageLabel.text = "Try items and abilities " + (options.DebugCoverage ? "On" : "Off");
    }

    public void StartBatch()
    {
        if (!Read(out _)) { Refresh(); return; }
        Close(); AutoplayRunner.WatchDemo(options);
    }

    public void Close()
    {
        MenuUIInputModule.Active?.PopDialog(this);
        if (menuGroup != null) menuGroup.interactable = menuGroup.blocksRaycasts = true;
        if (previousSelection != null) EventSystem.current?.SetSelectedGameObject(previousSelection);
        gameObject.SetActive(false); Destroy(gameObject);
    }
    private void OnDisable() => MenuUIInputModule.Active?.PopDialog(this);
}
