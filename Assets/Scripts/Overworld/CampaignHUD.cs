using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Player-facing travel and party controls; diagnostic data stays in the sandbox.</summary>
public sealed class CampaignHUD : MonoBehaviour
{
    public OverworldScene Overworld;
    public Town Town;
    private TextMeshProUGUI message;
    private Transform actions;
    private GameObject rosterPanel;
    private Transform rosterContent;
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

    private void Start()
    {
        if (Town != null) townMenus = FindFirstObjectByType<TownMenuManager>();
        var canvas = GameUISkin.Canvas("Campaign HUD", transform, 50);
        var bar = GameUISkin.Panel(canvas.transform, new Vector2(.02f, .025f), new Vector2(.72f, .15f));
        message = GameUISkin.Label(bar.transform, "", new Vector2(.025f, .12f), new Vector2(.68f, .88f), 24);
        message.enableAutoSizing = true; message.fontSizeMin = 18; message.fontSizeMax = 24;
        actions = GameUISkin.Rect("Travel actions", bar.transform, new Vector2(.7f, .12f), new Vector2(.98f, .88f));
        if (Town != null)
        {
            var partyButton = GameUISkin.Button(actions, "Party  [P / B]", Vector2.zero, Vector2.one, () => SetPartyOpen(!IsPartyOpen));
            partyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            rosterPanel = GameUISkin.Panel(canvas.transform, new Vector2(.02f, .18f), new Vector2(.33f, .82f)).gameObject;
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
            GameUISkin.Button(rosterPanel.transform, "Done", new Vector2(.2f, .025f), new Vector2(.8f, .11f), () => SetPartyOpen(false));
            rosterPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (message == null) return;
        var common = Common.Instance;
        bool ready = common != null && !common.Travel.IsTransitioning && !AutoplayRunner.BlocksPlayerInput && !common.GlobalSettings.IsOpen;
        if (Overworld != null)
        {
            ready &= Overworld.IsReady;
            message.transform.parent.gameObject.SetActive(ready);
            if (!ready) return;
            message.text = Overworld.Context.State.Finished ? "Campaign complete!" :
                string.IsNullOrWhiteSpace(Overworld.Message) ? "Explore the world\nMove: WASD / arrows / left stick   |   Interact: Enter / A" : Overworld.Message;
            var warps = Overworld.Map.CurrentGrid.WarpsAt(Overworld.Position).ToArray();
            string key = string.Join("|", warps.Select(w => w.Id));
            if (key != actionKey)
            {
                actionKey = key;
                foreach (Transform child in actions) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                int count = warps.Length + 1;
                GameUISkin.Button(actions, "Interact", Vector2.zero, new Vector2(1, 1f / count), Overworld.ClaimRewards);
                for (int i = 0; i < warps.Length; i++)
                {
                    var route = warps[i];
                    GameUISkin.Button(actions, Overworld.WarpLabel(route), new Vector2(0, (i + 1f) / count), new Vector2(1, (i + 2f) / count), () => Overworld.Warp(route.Id));
                }
                // Directional/submit input belongs to overworld movement and interaction.
                foreach (var button in actions.GetComponentsInChildren<Button>())
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }
        else if (Town != null)
        {
            ready &= Town.IsReady && common.CampaignContext != null && (townMenus == null || !townMenus.Opened);
            message.transform.parent.gameObject.SetActive(ready);
            if (!ready) { if (IsPartyOpen) SetPartyOpen(false); return; }
            // B opens the party from town; once open, the dialog owns B as Back.
            if (MenuUIInputModule.Active?.InputConsumed != true &&
                (UnityEngine.InputSystem.Keyboard.current?.pKey.wasPressedThisFrame == true ||
                 (!IsPartyOpen && common.MenuInputHandler.PlayerInput.actions["Plan"].WasPressedThisFrame())))
                SetPartyOpen(!IsPartyOpen);
            var context = common.CampaignContext;
            message.text = context.CanLeaveTown(Town.Configuration.Id)
                ? "Town gate open\nFollow the south corridor to leave town."
                : "Town gate closed\nClear the dungeon inside town to open the way.";
            string key = string.Join("|", context.Roster.OrderBy(id => id)) + "/" + string.Join("|", context.Active.OrderBy(id => id));
            if (key == rosterKey) return;
            rosterKey = key;
            foreach (Transform child in rosterContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (string id in context.Roster.OrderBy(id => id))
            {
                bool selected = context.Active.Contains(id);
                string name = CampaignParty.Resolve(id, Town.Configuration)?.Name ?? "Companion";
                var button = GameUISkin.Button(rosterContent, (selected ? "Travelling: " : "Invite: ") + name, Vector2.zero, Vector2.one, () => {
                    Town.WriteSaveData();
                    var ids = selected ? context.Active.Where(x => x != id).ToArray() : context.Active.Concat(new[] { id }).ToArray();
                    if (context.SetParty(ids)) { Town.RefreshCampaignParty(); GameMessages.Post($"{name} {(selected ? "left the travelling party" : "joined the party")}."); }
                });
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                button.interactable = selected || context.Active.Count < 3;
            }
            if (context.Roster.Count == 0)
            {
                var empty = GameUISkin.Label(rosterContent, "Meet companions as you explore the campaign.", Vector2.zero, Vector2.one, 24);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            }
        }
    }
}
