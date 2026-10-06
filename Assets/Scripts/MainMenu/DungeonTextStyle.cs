using TMPro;
using UnityEngine;

// Explicit contrast for the parchment or wood behind this authored label.
public sealed class DungeonTextStyle : MonoBehaviour
{
    public bool OnWood;
    public void Apply() => GetComponent<TMP_Text>().color=OnWood?GameUITheme.LightInk:GameUITheme.Ink;
}
