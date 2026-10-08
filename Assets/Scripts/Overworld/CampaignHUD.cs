using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Player-facing travel and party controls; diagnostic data stays in the sandbox.</summary>
public sealed class CampaignHUD : MonoBehaviour
{
    public OverworldScene Overworld;
    public Town Town;
    [SerializeField] private TextMeshProUGUI message;
    [SerializeField] private Transform actions;
    [SerializeField] private GameObject rosterPanel;
    [SerializeField] private Transform rosterContent;
    public Button ActionTemplate, CompanionTemplate, PartyButton, DoneButton;
    public TMP_Text EmptyRoster;
    private string actionKey;
    private string rosterKey;
    private TownMenuManager townMenus;
    public bool IsPartyOpen => rosterPanel != null && rosterPanel.activeSelf;

    private void SetPartyOpen(bool open)
    {
        rosterPanel.SetActive(open);
        if (open) MenuUIInputModule.Active?.PushDialog(this, rosterPanel.transform,
            rosterPanel.GetComponentInChildren<Button>()?.gameObject, () => SetPartyOpen(false));
        else
        {
            MenuUIInputModule.Active?.PopDialog(this);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }
    }

    private void OnDestroy() => MenuUIInputModule.Active?.PopDialog(this);

    #if UNITY_EDITOR
    public void AuthorLayout()
    {
        if (Town != null) townMenus = FindFirstObjectByType<TownMenuManager>();
        var canvas = GameUISkin.Canvas("Campaign HUD", transform, 50);
        var bar = GameUISkin.Panel(canvas.transform, new Vector2(.02f, .80f), new Vector2(.72f, .925f));
        message = GameUISkin.Label(bar.transform, "", new Vector2(.025f, .12f), new Vector2(.68f, .88f), 24);
        message.enableAutoSizing = true; message.fontSizeMin = 18; message.fontSizeMax = 24;
        actions = GameUISkin.Rect("Travel actions", bar.transform, new Vector2(.7f, .12f), new Vector2(.98f, .88f));
        if (Town != null)
        {
            var partyButton = PartyButton = GameUISkin.Button(actions, "Party  [P]", Vector2.zero, Vector2.one, () => SetPartyOpen(!IsPartyOpen));
            partyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            rosterPanel = GameUISkin.Panel(canvas.transform, new Vector2(.02f, .22f), new Vector2(.33f, .78f)).gameObject;
            GameUISkin.Label(rosterPanel.transform, "YOUR COMPANIONS", new Vector2(.06f, .88f), new Vector2(.94f, .97f), 28);
            GameUISkin.Label(rosterPanel.transform, "Choose up to three to travel with you.", new Vector2(.06f, .79f), new Vector2(.94f, .88f), 22);
            var viewport = GameUISkin.Rect("Roster viewport", rosterPanel.transform, new Vector2(.04f, .13f), new Vector2(.96f, .77f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            rosterContent = GameUISkin.Rect("Companions", viewport, new Vector2(0, 1), Vector2.one);
            ((RectTransform)rosterContent).pivot = new Vector2(.5f, 1);
            var layout = rosterContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            rosterContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = (RectTransform)rosterContent; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            DoneButton=GameUISkin.Button(rosterPanel.transform, "Done", new Vector2(.2f, .025f), new Vector2(.8f, .11f), () => SetPartyOpen(false));
            rosterPanel.SetActive(false);
        }
    }
#endif

    private void Start()
    {
        // Travel controls leave the shared event feed's bottom dock unobstructed.
        JuicyChickenGames.Menu.Dialog.Fit(message.transform.parent,.02f,.80f,.72f,.925f);
        if(Town!=null)
        {
            JuicyChickenGames.Menu.Dialog.Fit(rosterPanel.transform,.02f,.22f,.33f,.78f);
            townMenus=FindFirstObjectByType<TownMenuManager>();
            PartyButton.onClick.AddListener(()=>SetPartyOpen(!IsPartyOpen));
            DoneButton.onClick.AddListener(()=>SetPartyOpen(false));
        }
    }
    private Button Action(string label, System.Action callback)
    {
        var button=Instantiate(ActionTemplate,actions);button.gameObject.SetActive(true);
        button.GetComponentInChildren<TMP_Text>().text=label;button.onClick.AddListener(()=>callback());return button;
    }
    private void Update()
    {
        if (message == null) return;
        var common = Common.Instance;
        bool ready = common != null && !common.Travel.IsTransitioning && !AutoplayRunner.BlocksPlayerInput && !common.GlobalSettings.IsOpen;
        if (Overworld != null)
        {
            ready &= Overworld.IsReady && MenuUIInputModule.Active?.HasDialog != true;
            message.transform.parent.gameObject.SetActive(ready);
            if (!ready) return;
            message.text = InputPrompts.Format(Overworld.Context.State.Finished ? "Campaign complete!" :
                string.IsNullOrWhiteSpace(Overworld.Message) ? "Explore the world\nMove: {Move}   |   Interact: {Interact}" : Overworld.Message);
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
        else if (Town != null)
        {
            if (PartyButton != null) PartyButton.GetComponentInChildren<TMP_Text>().text = "Party  [" + InputPrompts.Party + "]";
            ready &= Town.IsReady && !Town.TownPlayer.CutsceneLocked && common.CampaignContext != null && (townMenus == null || !townMenus.Opened);
            message.transform.parent.gameObject.SetActive(ready);
            if (!ready) { if (IsPartyOpen) SetPartyOpen(false); return; }
            // B opens the party from town; once open, the dialog owns B as Back.
            if (MenuUIInputModule.Active?.InputConsumed != true &&
                (UnityEngine.InputSystem.Keyboard.current?.pKey.wasPressedThisFrame == true ||
                 (!IsPartyOpen && common.MenuInputHandler.PlayerInput.actions["Plan"].WasPressedThisFrame())))
                SetPartyOpen(!IsPartyOpen);
            var context = common.CampaignContext;
            message.text = context.GetTownDisplayName(Town.Configuration.Id)+"\n"+(context.CanLeaveTown(Town.Configuration.Id)
                ? "Town gate open\nFollow the south corridor to leave town."
                : "Town gate closed\nClear the dungeon inside town to open the way.");
            string key = string.Join("|", context.Roster.OrderBy(id => id)) + "/" + string.Join("|", context.Active.OrderBy(id => id));
            if (key == rosterKey) return;
            rosterKey = key;
            foreach (Transform child in rosterContent) { if(child==EmptyRoster.transform)continue; child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (string id in context.Roster.OrderBy(id => id))
            {
                bool selected = context.Active.Contains(id);
                string name = CampaignParty.Resolve(id, Town.Configuration)?.Name ?? "Companion";
                var button=Instantiate(CompanionTemplate,rosterContent);button.gameObject.SetActive(true);
                button.GetComponentInChildren<TMP_Text>().text=(selected ? "Travelling: " : "Invite: ")+name;
                button.onClick.AddListener(()=> {
                    Town.WriteSaveData();
                    var ids = selected ? context.Active.Where(x => x != id).ToArray() : context.Active.Concat(new[] { id }).ToArray();
                    if (context.SetParty(ids)) { Town.RefreshCampaignParty(); GameMessages.Post($"{name} {(selected ? "left the travelling party" : "joined the party")}."); }
                });

                button.interactable = selected || context.Active.Count < 3;
            }
            EmptyRoster.gameObject.SetActive(context.Roster.Count==0);
        }
    }
}
