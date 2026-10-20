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
        if (Overworld != null) OpeningGuidance.Ensure(Overworld);
        if (actions == null && Overworld != null)
        {
            // The authored scene supplies a styled button template, but no actions container.
            // Keep the contextual mouse control on the same scaled HUD as party shortcuts.
            var launcher = AuthoredUI.Require<PartyMenuLauncher>(Overworld.transform);
            var holder = new GameObject("World interaction", typeof(RectTransform));
            holder.transform.SetParent(launcher.transform, false);
            var content = new GameObject("Actions", typeof(RectTransform));
            content.transform.SetParent(holder.transform, false);
            actions = content.transform;
        }
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
        JuicyChickenGames.Menu.Dialog.Fit(button.transform,0,0,1,1);
        var text=button.GetComponentInChildren<TMP_Text>();
        text.text=GameplayText.Format(label);text.enableAutoSizing=true;text.fontSizeMin=20;text.fontSizeMax=32;
        button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>callback());return button;
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
        // Visibility follows scene readiness; interaction remains locked throughout the reveal.
        bool ready = common != null && !AutoplayRunner.BlocksPlayerInput && !common.GlobalSettings.IsOpen;
        if (Overworld != null)
        {
            ready &= Overworld.IsReady && MenuUIInputModule.Active?.HasDialog != true;
            if (actions != null) actions.parent.gameObject.SetActive(ready);
            if (!ready) return;
            if (Overworld.Context.State.Finished) PostStatus("Campaign complete!");
            if (actions == null) return;
            bool canTravel = Overworld.Map.CurrentGrid.WarpsAt(Overworld.Position)
                .Any(route => route.CanTraverse(Overworld.Held, Overworld.Context.Resolved));
            string key = canTravel ? "travel" : "interact";
            if (key != actionKey)
            {
                actionKey = key;
                foreach (Transform child in actions) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                // One contextual entry keeps the HUD compact; the modal supports every input device.
                Action(canTravel ? "Interact / Travel" : "Interact", Overworld.ClaimRewards);
                // Directional/submit input belongs to overworld movement and interaction.
                foreach (var button in actions.GetComponentsInChildren<Button>())
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            foreach (var button in actions.GetComponentsInChildren<Button>())
                button.interactable = !Overworld.IsMoving && !common.Travel.IsTransitioning;
        }
    }
}
