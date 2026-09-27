using TMPro;
using UnityEngine;

/// <summary>Screen-sized combat callouts anchored above visible characters.</summary>
public sealed class DungeonFloatingText : MonoBehaviour
{
    private TMP_Text label;
    private Vector3 anchor;
    private float elapsed;
    private float height;
    private RectTransform canvasRect;
    public static void Show(Game game, string message, Color color, Character character, bool prominent = false)
    {
        if (character == null ||
            (FogOverlay.Instance != null && !FogOverlay.Instance.IsCurrentlyVisible(character.transform.position))) return;
        var position = character.VisualParent != null ? character.VisualParent.transform.position : character.transform.position;
        ShowAtPosition(game, message, color, position, prominent);
    }
    public static void Show(Game game, string message, Color color, Vector3 position, bool prominent = false)
    {
        if (FogOverlay.Instance != null && !FogOverlay.Instance.IsCurrentlyVisible(position)) return;
        ShowAtPosition(game, message, color, position, prominent);
    }
    private static void ShowAtPosition(Game game, string message, Color color, Vector3 position, bool prominent)
    {
        if (game == null || !game.IsReady || string.IsNullOrWhiteSpace(message)) return;
        var canvas = game.transform.Find("Combat callouts")?.GetComponent<Canvas>();
        if (canvas == null) canvas = GameUISkin.Canvas("Combat callouts",game.transform,0);
        var text = GameUISkin.Label(canvas.transform,message,new Vector2(.5f,.5f),new Vector2(.5f,.5f),prominent ? 72 : 54);
        text.rectTransform.sizeDelta = new Vector2(620,110);
        text.rectTransform.pivot = new Vector2(.5f,.5f);
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = color; text.outlineColor = new Color32(35,25,45,255); text.outlineWidth = .2f;
        text.raycastTarget = false;
        var callout = text.gameObject.AddComponent<DungeonFloatingText>();
        callout.label = text; callout.anchor = position; callout.height = prominent ? 230 : 180; callout.canvasRect = (RectTransform)canvas.transform;
        callout.LateUpdate();
    }
    private void LateUpdate()
    {
        var game = Game.Instance;
        if (label == null || game == null || !game.IsReady) { Destroy(gameObject); return; }
        var camera = game.PlayerController.CameraController?.Camera;
        if (camera == null) { Destroy(gameObject); return; }
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= 1.3f) { Destroy(gameObject); return; }
        var screen = camera.WorldToScreenPoint(anchor);
        if (screen.z <= 0) { label.enabled = false; return; }
        label.enabled = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out var point);
        label.rectTransform.anchoredPosition = point + new Vector2(0,height + elapsed*45);
        label.alpha = 1-Mathf.Clamp01((elapsed-.75f)/.55f);
    }
}
