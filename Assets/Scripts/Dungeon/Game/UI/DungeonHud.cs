using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonHud : MonoBehaviour
{
    public Transform PartyRoot { get; private set; }
    private Game game;
    private TMP_Text header, supplies, target;
    private Button control;
    private float detailUntil;
    public static DungeonHud Ensure(Game game)
    {
        var hud = game.GetComponent<DungeonHud>();
        if (hud == null) { hud = game.gameObject.AddComponent<DungeonHud>(); hud.Build(game); }
        return hud;
    }
    private void Build(Game owner)
    {
        game = owner;
        var canvas = GameUISkin.Canvas("Dungeon HUD", transform, 1);
        PartyRoot = GameUISkin.Rect("Party", canvas.transform,new Vector2(.012f,.255f),new Vector2(.30f,.985f));
        header = GameUISkin.Label(canvas.transform,"",new Vector2(.81f,.018f),new Vector2(.985f,.12f),46);
        header.alignment = TextAlignmentOptions.BottomRight; WorldLabel(header);
        supplies = GameUISkin.Label(canvas.transform,"",new Vector2(.018f,.09f),new Vector2(.23f,.20f),29);
        WorldLabel(supplies);
        var options = GameUISkin.Button(canvas.transform,"Options",new Vector2(.87f,.935f),new Vector2(.985f,.985f),()=>Common.Instance.GlobalSettings.ShowDialog());
        options.navigation = new Navigation { mode = Navigation.Mode.None };
        control = GameUISkin.Button(canvas.transform,"",new Vector2(.012f,.022f),new Vector2(.22f,.072f),()=>DungeonPreferences.FullControl=!DungeonPreferences.FullControl);
        control.navigation = new Navigation { mode = Navigation.Mode.None };
        target = GameUISkin.Label(canvas.transform,"",new Vector2(.29f,.79f),new Vector2(.70f,.90f),25);
        target.alignment = TextAlignmentOptions.Top;
        target.color = GameUITheme.LightInk;
        var shadow = target.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,.9f); shadow.effectDistance = new Vector2(1,-1);
        game.FloorText.gameObject.SetActive(false); game.InventoryText.gameObject.SetActive(false);
    }
    internal static void WorldLabel(TMP_Text text)
    {
        text.color = GameUITheme.LightInk;
        var shadow = text.gameObject.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,.9f); shadow.effectDistance = new Vector2(1.5f,-1.5f);
    }
    public void Inspect(string text) { target.text = text; detailUntil = Time.unscaledTime + 6; }
    public static string StatusSummary(Character c) => string.Join(", ", c.StatusEffects.Where(s=>s!=null&&!s.IsExpired()).Select(s=>
        s is GoopiRootStatusEffect ? "Rooted: defeat the holder" : $"{s.GetEffectName()} ({s.TurnsLeft})"));
    private void Update()
    {
        if (game == null || !game.IsReady || game.PlayerController.ControlledAlly == null) return;
        var player = game.PlayerController;
        var turns = game.TurnManager;
        var count = player.Inventory.InventoryItems.Count;
        string bag = $"Bag {count}/{player.Inventory.MaxItems}";
        if (count >= player.Inventory.MaxItems-1) bag = "<color=#963B20>" + bag + "</color>";
        header.text = $"{player.Floor}F";
        supplies.text = $"{bag}\n{player.Gold} gold";
        control.GetComponentInChildren<TMP_Text>().text = "Full Control: " + (DungeonPreferences.FullControl ? "On" : "Off") +
            (turns.IsProcessingTurn && turns.FullControlThisRound != DungeonPreferences.FullControl ? " (next round)" : "");
        var ally = player.ControlledAlly;
        var front = ally.TilemapPosition + Dungeon.GetFacingOffset(ally.CurrentFacing);
        if (Time.unscaledTime < detailUntil) return;
        var selected = MenuManager.Instance.TargetDialog.CameraTarget;
        if (selected == null && JuicyChickenGames.Menu.PlayerInputHandler.Instance.holdPosition)
            selected = game.AllCharacters.FirstOrDefault(c=>c!=null && c.OverlapsWith(Character.ToBounds(front)));
        target.text = selected != null && GameMessages.Visible(selected) ?
            EnemyBehavior.IsDisguised(selected) ? "Treasure chest" : $"{GameMessages.Name(selected)}\n{StatusSummary(selected)}" : "";
        var targeting = MenuManager.Instance.TargetDialog;
        if (player.CurrentControlMode == PlayerControlMode.TargetSelecting && !string.IsNullOrEmpty(targeting.RangeLabel))
            target.text += "\n" + targeting.RangeLabel;
    }
}

public sealed class DungeonPartyCard : MonoBehaviour
{
    private CharacterStatsDisplay display;
    private Image panel;
    private Image portraitHighlight;
    private TMP_Text order;
    private Transform statuses;
    private string statusKey;
    public static void Build(CharacterStatsDisplay display)
    {
        if (display.GetComponent<DungeonPartyCard>() != null) return;
        var hud = DungeonHud.Ensure(Game.Instance);
        foreach (Transform child in display.transform) child.gameObject.SetActive(false);
        var oldBackground = display.GetComponent<Image>();
        if (oldBackground != null) oldBackground.enabled = false;
        display.transform.SetParent(hud.PartyRoot,false);
        var rect = (RectTransform)display.transform;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        int slot = hud.PartyRoot.childCount-1;
        rect.anchorMin = new Vector2(0,1-(slot+1)*.25f+.008f); rect.anchorMax = new Vector2(1,1-slot*.25f);
        rect.pivot = new Vector2(.5f,1); rect.offsetMin = rect.offsetMax = Vector2.zero;
        var card = display.gameObject.AddComponent<DungeonPartyCard>(); card.display=display;
        card.panel=GameUISkin.Panel(display.transform,Vector2.zero,Vector2.one);
        card.portraitHighlight = GameUISkin.Panel(card.panel.transform,new Vector2(0,.105f),new Vector2(.36f,1));
        card.portraitHighlight.name = "Turn highlight";
        card.portraitHighlight.sprite = GameUITheme.Current.Button;
        card.portraitHighlight.type = Image.Type.Sliced;
        card.portraitHighlight.fillCenter = false;
        card.portraitHighlight.pixelsPerUnitMultiplier = 6;
        card.portraitHighlight.raycastTarget = false;
        display.PortraitImage = GameUISkin.Rect("Portrait",card.panel.transform,new Vector2(.005f,.12f),new Vector2(.35f,.99f)).gameObject.AddComponent<Image>();
        display.PortraitImage.preserveAspect=true; display.PortraitImage.raycastTarget=false;
        display.NameText=GameUISkin.Label(card.panel.transform,"",new Vector2(.37f,.79f),new Vector2(.99f,.99f),26);
        DungeonHud.WorldLabel(display.NameText);
        display.HpDisplay=Bar(card.panel.transform,.49f,new Color(.37f,.80f,.20f));
        display.SpDisplay=Bar(card.panel.transform,.31f,new Color(.19f,.53f,.86f));
        display.HungerDisplay=Bar(card.panel.transform,.13f,new Color(.92f,.61f,.14f));
        display.LevelDisplay=Bar(card.panel.transform,.02f,new Color(.63f,.49f,.28f),.06f);
        display.LevelDisplay.ValueText.gameObject.SetActive(false);
        card.order=GameUISkin.Label(card.panel.transform,"",new Vector2(.37f,.66f),new Vector2(.99f,.80f),18);
        DungeonHud.WorldLabel(card.order);
        card.statuses=GameUISkin.Rect("Statuses",card.panel.transform,new Vector2(.01f,.01f),new Vector2(.34f,.17f));
    }
    private static StatsDisplay Bar(Transform parent,float y,Color color,float height=.16f)
    {
        var background=GameUISkin.Panel(parent,new Vector2(.37f,y),new Vector2(.99f,y+height));
        GameUITheme.Current.Surface(background, GameUITheme.Current.Track);
        var fill=GameUISkin.Panel(background.transform,Vector2.zero,Vector2.one);GameUITheme.Current.Surface(fill, GameUITheme.Current.Field);fill.color=color;
        var slider=background.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.fillRect=fill.rectTransform;slider.interactable=false;
        var data=background.gameObject.AddComponent<StatsDisplay>();data.StatValueSlider=slider;
        data.ValueText=GameUISkin.Label(background.transform,"",new Vector2(.04f,0),new Vector2(.96f,1),22);
        data.ValueText.color=GameUITheme.LightInk; data.ValueText.alignment=TextAlignmentOptions.Midline;
        return data;
    }
    private void Update()
    {
        if (display.Character is not Ally ally || Game.Instance == null) return;
        var turn=Game.Instance.TurnManager;
        bool ready = !turn.IsProcessingTurn || turn.AwaitingCommand;
        bool controlled = Game.Instance.PlayerController.ControlledAlly == ally;
        bool active = ally.Vitals.HP > 0 && (ready ? controlled : turn.ActiveActor == ally);
        portraitHighlight.enabled = active;
        portraitHighlight.color = ready ? new Color(1f,.84f,.30f) : new Color(.35f,.9f,.72f);
        panel.color=active ? new Color(.15f,.38f,.33f,.58f) : new Color(.09f,.21f,.20f,.32f);
        order.text=ally.IsDowned ? "Downed" : turn.UsesFullControl ?
            turn.IsProcessingTurn && ally.Vitals.ActionsPerTurnLeft==0 ? "Done" : active && (!turn.IsProcessingTurn||turn.AwaitingCommand) ? "Your order" : $"{ally.Vitals.ActionsPerTurnLeft} actions" :
            controlled ? "Leader" : ally.AllyStrategy.ToString().Replace("Aggresive","Aggressive").Replace("HoldPosition","Hold");
        string key=DungeonHud.StatusSummary(ally);
        if (key==statusKey) return; statusKey=key;
        foreach (Transform child in statuses) Destroy(child.gameObject);
        var effects=ally.StatusEffects.Where(s=>s!=null&&!s.IsExpired()).ToArray();
        for(int i=0;i<Mathf.Min(4,effects.Length);i++)
        {
            var effect=effects[i];string name=effect.GetEffectName();
            string badge=i==3&&effects.Length>4 ? "+"+(effects.Length-3) : new string(name.Where(char.IsUpper).Take(2).ToArray());
            if (badge.Length<2) badge=name.Substring(0,Mathf.Min(2,name.Length));
            var button=GameUISkin.Button(statuses,badge,new Vector2(i*.25f,0),new Vector2((i+1)*.25f-.01f,1),()=>DungeonHud.Ensure(Game.Instance).Inspect(ally.CharacterName+"\n"+DungeonHud.StatusSummary(ally)));
            button.GetComponentInChildren<TMP_Text>().fontSize=17;
            button.navigation=new Navigation {mode=Navigation.Mode.None};
        }
    }
}
