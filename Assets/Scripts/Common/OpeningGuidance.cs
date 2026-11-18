using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Opening guidance and a contextual prompt for the first interaction.</summary>
public sealed class OpeningGuidance : MonoBehaviour
{
    private Town town;
    private Game game;
    private OverworldScene world;
    private Canvas canvas;
    private TMP_Text label;
    private RectTransform panelRect;

    // Per-frame caches so LateUpdate does not scan the scene or rebuild strings every frame.
    private TownMenuManager townMenu;
    private DungeonHud hud;
    private readonly System.Collections.Generic.List<HomeBed> beds = new();
    private string formattedSource, formattedObjective, bookButton, bookPrompt;
    private string moveKey, moveText, bedKey, bedText, vendorKey, vendorText;

    private TownMenuManager TownMenu()
    {
        if (townMenu == null) townMenu = FindFirstObjectByType<TownMenuManager>();
        return townMenu;
    }
    private DungeonHud Hud()
    {
        if (hud == null) hud = game.GetComponent<DungeonHud>();
        return hud;
    }
    private string MovePrompt(string key)
    {
        if (key != moveKey) { moveKey = key; moveText = "Find the doorway. Use " + key + " to move."; }
        return moveText;
    }
    private string BedPrompt(string key)
    {
        if (key != bedKey) { bedKey = key; bedText = "Your bed saves progress. Press " + key + " to inspect it."; }
        return bedText;
    }
    private string VendorPrompt(string key)
    {
        if (key != vendorKey) { vendorKey = key; vendorText = "Press " + key + " to talk to the person in front of you."; }
        return vendorText;
    }

    public static void Ensure(MonoBehaviour owner)
    {
        if (Common.InstanceOrNull?.CampaignContext?.State.RequiresOpeningPreparation != true) return;
        var guidance = owner.GetComponent<OpeningGuidance>() ?? owner.gameObject.AddComponent<OpeningGuidance>();
        guidance.town = owner as Town; guidance.game = owner as Game; guidance.world = owner as OverworldScene;
        if (guidance.canvas != null) return;
        var root = new GameObject("Current objective", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(owner.transform, false);
        guidance.canvas = root.GetComponent<Canvas>();
        guidance.canvas.renderMode = RenderMode.ScreenSpaceOverlay; guidance.canvas.sortingOrder = 2;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600); scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Objective backing", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(root.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        // The event feed owns the bottom fifth of the screen. Current guidance
        // sits below the shortcut row and yields to target details/dialogs.
        rect.anchorMin = new Vector2(.31f, .72f); rect.anchorMax = new Vector2(.71f, .72f);
        rect.pivot = new Vector2(.5f, 0); rect.offsetMin = Vector2.zero; rect.offsetMax = new Vector2(0, 90);
        guidance.panelRect = rect;
        var backing = panel.GetComponent<Image>(); backing.color = new Color(.10f, .075f, .05f, .88f); backing.raycastTarget = false;
        var text = new GameObject("Objective", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(panel.transform, false);
        guidance.label = text.GetComponent<TextMeshProUGUI>();
        rect = guidance.label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18, 10); rect.offsetMax = new Vector2(-18, -10);
        guidance.label.fontSize = 17; guidance.label.color = GameUITheme.LightInk;
        guidance.label.alignment = TextAlignmentOptions.MidlineLeft; guidance.label.raycastTarget = false;
        guidance.label.textWrappingMode = TextWrappingModes.Normal;
    }

    private void LateUpdate()
    {
        var common = Common.InstanceOrNull; var context = common?.CampaignContext;
        if (canvas == null) return;
        bool ready = context != null && !TerminalMode.Effective && !common.GlobalSettings.IsOpen &&
            (town != null && town.IsReady && !town.TownPlayer.CutsceneLocked && TownMenu()?.Opened != true ||
             game != null && game.IsReady && !MenuManager.Instance.Opened && Hud()?.ShowingTargetDetails != true ||
             world != null && world.IsReady && MenuUIInputModule.Active?.HasDialog != true);
        if (town?.HomeLesson is { IsLocked: true } homeLesson)
        {
            ShowBookPrompt(ready, homeLesson);
            return;
        }
        string objective = ready ? context.OpeningObjective : "";
        if (ready && game != null && game.GetComponent<OpeningDungeonLesson>() is { } lesson && lesson.enabled)
            objective = lesson.Objective;
        if (ready && town != null && context.IsPreparingForOpening)
        {
            if (!context.State.MovedInOpening) objective = MovePrompt(InputPrompts.Move);
            else if (!context.State.InteractedInOpening)
            {
                var player = town.TownPlayer; var hero = player.ControllingTownAlly;
                var ahead = hero.TilemapPosition + Dungeon.GetFacingOffset(hero.CurrentFacing);
                bool facingBed = false;
                town.GetComponentsInChildren(false, beds);
                foreach (var b in beds) if (b.Tile == ahead) { facingBed = true; break; }
                beds.Clear();
                bool facingVendor = false;
                if (!facingBed)
                    foreach (var v in town.ShopVendors) if (v.TilemapPosition == ahead) { facingVendor = true; break; }
                if (facingBed) objective = BedPrompt(InputPrompts.Attack);
                else if (facingVendor) objective = VendorPrompt(InputPrompts.Attack);
            }
        }
        canvas.enabled = ready && !string.IsNullOrEmpty(objective);
        if (canvas.enabled)
        {
            panelRect.anchorMin = new Vector2(.31f, .72f); panelRect.anchorMax = new Vector2(.71f, .72f);
            panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = new Vector2(0, 90);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            if (objective != formattedSource) { formattedSource = objective; formattedObjective = GameplayText.Format(objective); }
            if (label.text != formattedObjective) label.text = formattedObjective;
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(label.preferredHeight + 24, 66, 124));
        }
    }

    private void ShowBookPrompt(bool ready, OpeningHomeLesson homeLesson)
    {
        canvas.enabled = false;
        if (!ready || Common.Instance.ScreenTransition.BlocksInput || ControlBindings.SuppressInput ||
            !homeLesson.TryGetBookPromptAnchor(out var anchor)) return;
        string button = InputPrompts.InteractionButton;
        if (string.IsNullOrEmpty(button)) return;
        var camera = town.TownPlayer.CameraController.Camera;
        var screen = camera.WorldToScreenPoint(anchor);
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return;
        if (button != bookButton) { bookButton = button; bookPrompt = $"[{button}] Read"; }
        if (label.text != bookPrompt) label.text = bookPrompt;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        // Normalized anchors keep the prompt on the prop while the canvas scaler catches up to a resize.
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(screen.x / Screen.width, screen.y / Screen.height);
        panelRect.sizeDelta = new Vector2(label.preferredWidth + 36, label.preferredHeight + 20);
        panelRect.anchoredPosition = new Vector2(0, 8);
        canvas.enabled = true;
    }

    internal static IEnumerator RevealOpenedGate(Town town)
    {
        if (!Common.Instance.Travel.ConsumeOpeningGateReveal()) yield break;
        GameMessages.Post("The town gate is open! Rest at home to save your victory, then set out for the overworld.");
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations || AutoplayRunner.BlocksPlayerInput) yield break;
        var player = town.TownPlayer; var camera = player.CameraController;
        while (Common.Instance.ScreenTransition.BlocksInput) yield return null;
        bool wasEnabled = camera.enabled; player.CutsceneLocked = true; camera.enabled = false;
        var origin = camera.Camera.transform.position; var rotation = camera.Camera.transform.rotation;
        var gate = town.WalkableMap.CellToWorld(town.Plan.Exit.ToCell());
        try
        {
            for (float t = 0; t < .65f; t += Time.unscaledDeltaTime)
            {
                camera.Camera.transform.position = Vector3.Lerp(origin, gate + camera.CameraOffset, Mathf.SmoothStep(0, 1, t / .65f));
                camera.Camera.transform.LookAt(camera.Camera.transform.position - camera.CameraOffset); yield return null;
            }
            yield return new WaitForSecondsRealtime(1.2f);
            var from = camera.Camera.transform.position;
            for (float t = 0; t < .55f; t += Time.unscaledDeltaTime)
            { camera.Camera.transform.position = Vector3.Lerp(from, origin, Mathf.SmoothStep(0, 1, t / .55f)); yield return null; }
        }
        finally
        {
            camera.Camera.transform.SetPositionAndRotation(origin, rotation); camera.enabled = wasEnabled;
            player.CutsceneLocked = false;
        }
    }
}
