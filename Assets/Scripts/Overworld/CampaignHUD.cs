using System.Linq;
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
    private string actionKey;
    private string rosterKey;
    private string statusKey;
    private TownMenuManager townMenus;

    private void SetPartyOpen(bool open)
    {
        MenuUIInputModule.Active?.PopDialog(this);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    private void OnDestroy() => MenuUIInputModule.Active?.PopDialog(this);

    private void Start()
    {
        // Travel controls leave the shared event feed's bottom dock unobstructed.
        if (actions != null)
        {
            JuicyChickenGames.Menu.Dialog.Fit(actions.parent,.51f,.80f,.72f,.925f);
            JuicyChickenGames.Menu.Dialog.Fit(actions,.06f,.12f,.94f,.88f);
        }
    }
    private Button Action(string label, System.Action callback)
    {
        var button=Instantiate(ActionTemplate,actions);button.gameObject.SetActive(true);
        button.GetComponentInChildren<TMP_Text>().text=label;button.onClick.AddListener(()=>callback());return button;
    }
    private void PostStatus(string status)
    {
        if (status == statusKey) return;
        statusKey = status;
        GameMessages.Post(status);
    }
    private void Update()
    {
        var common = Common.Instance;
        bool ready = common != null && !common.Travel.IsTransitioning && !AutoplayRunner.BlocksPlayerInput && !common.GlobalSettings.IsOpen;
        if (Overworld != null)
        {
            ready &= Overworld.IsReady && MenuUIInputModule.Active?.HasDialog != true;
            if (actions != null) actions.parent.gameObject.SetActive(ready);
            if (!ready) return;
            if (Overworld.Context.State.Finished) PostStatus("Campaign complete!");
            if (actions == null) return;
            var warps = Overworld.Map.CurrentGrid.WarpsAt(Overworld.Position).ToArray();
            string key = string.Join("|", warps.Select(w => w.Id));
            if (key != actionKey)
            {
                actionKey = key;
                foreach (Transform child in actions) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                Action("Interact", Overworld.ClaimRewards);
                for (int i = 0; i < warps.Length; i++)
                {
                    var route = warps[i];
                    Action(Overworld.WarpLabel(route), () => Overworld.Warp(route.Id));
                }
                // Directional/submit input belongs to overworld movement and interaction.
                foreach (var button in actions.GetComponentsInChildren<Button>())
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }
    }
}
