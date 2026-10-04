using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonHud : MonoBehaviour
{
    public Transform PartyRoot;
    public CharacterStatsDisplay[] PartySlots;
    [SerializeField] private Button options;
    private Game game;
    [SerializeField] private TMP_Text header, target;
    [SerializeField] private Button control;
    private float detailUntil;
    public static DungeonHud Ensure(Game game)
    {
        var hud=AuthoredUI.Require<DungeonHud>(game.transform);
        if(hud.game==null)
        {
            hud.game=game;
            hud.options.onClick.AddListener(()=>Common.Instance.GlobalSettings.ShowDialog());
            hud.control.onClick.AddListener(()=>DungeonPreferences.FullControl=!DungeonPreferences.FullControl);
            ResourceHUD.Ensure(game); ScenePresentation.Ensure(game);
        }
        return hud;
    }
    #if UNITY_EDITOR
    public void AuthorLayout(Game owner)
    {
        game = owner;
        var canvas = GameUISkin.Canvas("Dungeon HUD", transform, 1);
        PartyRoot = GameUISkin.Rect("Party", canvas.transform,new Vector2(.012f,.255f),new Vector2(.30f,.985f));
        header = GameUISkin.Label(canvas.transform,"",new Vector2(.81f,.018f),new Vector2(.985f,.12f),46);
        header.alignment = TextAlignmentOptions.BottomRight; WorldLabel(header);

        options = GameUISkin.Button(canvas.transform,"Options",new Vector2(.87f,.935f),new Vector2(.985f,.985f),()=>Common.Instance.GlobalSettings.ShowDialog());
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
#endif
    #if UNITY_EDITOR
    internal static void WorldLabel(TMP_Text text)
    {
        text.color = GameUITheme.LightInk;
        var shadow = text.gameObject.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,.9f); shadow.effectDistance = new Vector2(1.5f,-1.5f);
    }
#endif
    public void Inspect(string text) { target.text = text; detailUntil = Time.unscaledTime + 6; }
    public static string StatusSummary(Character c) => string.Join(", ", c.StatusEffects.Where(s=>s!=null&&!s.IsExpired()).Select(s=>
        s is GoopiRootStatusEffect ? "Rooted: defeat the holder" : $"{s.GetEffectName()} ({s.TurnsLeft})"));
    private void Update()
    {
        if (game == null || !game.IsReady || game.PlayerController.ControlledAlly == null) return;
        var player = game.PlayerController;
        var turns = game.TurnManager;
        header.text = $"{player.Floor}F";
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
