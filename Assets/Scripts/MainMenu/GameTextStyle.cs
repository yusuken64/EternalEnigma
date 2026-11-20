using TMPro;
using UnityEngine;

public enum GameTextRole { Interface, Heading, Combat, Utility }

/// <summary>Semantic font assignment from TextMesh Pro/FontRules.txt.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
public sealed class GameTextStyle : MonoBehaviour
{
    public GameTextRole Role;
    private void OnEnable() => Apply();
    public void Apply() => GameTypography.Apply(GetComponent<TMP_Text>(), Role);
}

public static class GameTypography
{
    private static readonly string[] Names = { "Marcellus", "Uncial Antiqua", "Germania One", "Roboto Condensed" };
    private static readonly TMP_FontAsset[] Fonts = new TMP_FontAsset[4];

    public static TMP_FontAsset Font(GameTextRole role)
    {
        int index = (int)role;
        return Fonts[index] != null ? Fonts[index] : Fonts[index] =
            GameContent.Load<TMP_FontAsset>("Fonts & Materials/" + Names[index] + " SDF");
    }

    public static TMP_FontAsset Expected(TMP_Text label) => Font(label.TryGetComponent<GameTextStyle>(out var style)
        ? style.Role : GameTextRole.Interface);

    public static void Apply(TMP_Text label, GameTextRole role)
    {
        if (label == null) return;
        var style = label.GetComponent<GameTextStyle>() ?? label.gameObject.AddComponent<GameTextStyle>();
        style.Role = role;
        var font = Font(role);
        if (font != null && label.font != font) label.font = font;
        if (role == GameTextRole.Combat) label.fontStyle |= FontStyles.Bold;
    }

    public static void Apply(Transform root)
    {
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            Apply(label, label.TryGetComponent<GameTextStyle>(out var style) ? style.Role : GameTextRole.Interface);
    }
}
