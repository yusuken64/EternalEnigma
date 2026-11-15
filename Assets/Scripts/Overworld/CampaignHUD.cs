using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Player-facing travel and party controls; diagnostic data stays in the sandbox.</summary>
public sealed class CampaignHUD : MonoBehaviour
{
    public OverworldScene Overworld;
    [SerializeField] private Transform actions;
    [SerializeField] private Transform rosterContent;
    public Button ActionTemplate, CompanionTemplate, PartyButton, DoneButton;
    public TMP_Text EmptyRoster;
    private string rosterKey;
    private string statusKey;
    private TownMenuManager townMenus;
    private void OnEnable() { HudRefresh.Changed += Refresh; HudRefresh.Request(HudChange.Travel | HudChange.Visibility); }
    private void OnDisable() => HudRefresh.Changed -= Refresh;

    private void SetPartyOpen(bool open)
    {
        MenuUIInputModule.Active?.PopDialog(this);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    private void OnDestroy() => MenuUIInputModule.Active?.PopDialog(this);

    private void Start()
    {
        if (Overworld != null) OpeningGuidance.Ensure(Overworld);
        if (actions != null) actions.gameObject.SetActive(false);
        if (ActionTemplate != null) ActionTemplate.gameObject.SetActive(false);
        HudRefresh.Request(HudChange.Travel | HudChange.Visibility);
    }
    private void PostStatus(string status)
    {
        if (status == statusKey) return;
        statusKey = status;
        GameMessages.Post(status);
    }
    private void Refresh(HudChange changes)
    {
        if ((changes & (HudChange.Travel | HudChange.Visibility)) == 0) return;
        var common = Common.Instance;
        // Visibility follows scene readiness; interaction remains locked throughout the reveal.
        bool ready = common != null && !AutoplayRunner.BlocksPlayerInput && !common.GlobalSettings.IsOpen;
        if (Overworld != null)
        {
            ready &= Overworld.IsReady && MenuUIInputModule.Active?.HasDialog != true;
            if (!ready) return;
            if (Overworld.Context.State.Finished) PostStatus("Campaign complete!");
        }
    }
}
