using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class HomeBedDialog : Dialog
{
    [SerializeField] private Button sleep;
    [SerializeField] private Button back;
    [SerializeField] private TMP_Text description;
    #if UNITY_EDITOR
    public static HomeBedDialog AuthorLayout(Transform parent)
    {
        var panel = GameUISkin.Panel(parent,new Vector2(.3f,.3f),new Vector2(.7f,.7f));
        var dialog = panel.gameObject.AddComponent<HomeBedDialog>();
        GameUISkin.Label(panel.transform,"Home",new Vector2(.1f,.7f),new Vector2(.9f,.94f),34);
        dialog.sleep = GameUISkin.Button(panel.transform,"Sleep and Save",new Vector2(.1f,.4f),new Vector2(.9f,.6f), dialog.Activate);
        dialog.back=GameUISkin.Button(panel.transform,"Back",new Vector2(.1f,.1f),new Vector2(.9f,.3f),dialog.CloseDialog);
        dialog.AuthorPreparationLayout();
        panel.gameObject.SetActive(false); return dialog;
    }

    public void AuthorPreparationLayout()
    {
        Fit(transform, .22f, .20f, .78f, .80f);
        var title = transform.Find("Home") ?? transform.Find("Text");
        if (title != null) Fit(title, .08f, .82f, .92f, .96f);
        if (description == null)
            description = GameUISkin.Label(transform, "Rest and save your progress.", new Vector2(.08f,.42f), new Vector2(.92f,.79f), 24);
        description.name = "Preparation description";
        description.enableAutoSizing = true; description.fontSizeMin = 18; description.fontSizeMax = 24;
        description.textWrappingMode = TextWrappingModes.Normal;
        Fit(sleep.transform, .08f, .23f, .92f, .38f);
        Fit(back.transform, .08f, .05f, .92f, .20f);
        foreach (var button in new[] { sleep, back })
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 26;
        }
    }
#endif
    public static HomeBedDialog Create(Transform parent)=>AuthoredUI.Require<HomeBedDialog>(parent);
    private void Awake()
    {
        sleep.onClick.AddListener(Activate);
        back.onClick.AddListener(CloseDialog);
    }
    private void RefreshView()
    {
        var context = Common.Instance?.CampaignContext;
        bool preparing = context?.IsPreparingForOpening == true;
        bool ready = context?.ReadyForOpeningDay == true;
        sleep.GetComponentInChildren<TMP_Text>().text = GameplayText.Format(preparing
            ? ready ? "Sleep and save until morning" : "Save and quit"
            : "Sleep and Save");
        if (description != null)
            description.text = GameplayText.Format(preparing
                ? ready ? "You're ready for tomorrow.\nSleep to save your preparations and open the dungeon.\nDefeat returns you to this save."
                    : "Before morning\n" + (!context.State.BoughtOpeningBread ? "Collect your free bread at the bakery.\n" : "") +
                        (!context.State.LearnedOpeningSkill ? "Learn an active skill at the trainer.\n" : "") + "You can save and quit now."
                : "Sleep to restore your party and save your progress.\nDefeat returns you to your last save.");
    }
    private void Activate()
    {
        var context = Common.Instance?.CampaignContext;
        bool saveForLater = context?.IsPreparingForOpening == true && !context.ReadyForOpeningDay;
        CloseDialog();
        var bed = Object.FindFirstObjectByType<HomeBed>();
        if (saveForLater) bed.SaveAndQuit(); else bed.SleepAndSave();
    }
    internal override void SetFirstSelect() { RefreshView(); sleep.Select(); }
}
