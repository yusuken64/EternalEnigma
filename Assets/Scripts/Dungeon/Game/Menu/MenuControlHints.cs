using TMPro;
using UnityEngine;

/// <summary>The same device-aware footer for party, shop and trainer dialogs.</summary>
public sealed class MenuControlHints : MonoBehaviour
{
    public TMP_Text Label;
    public bool Party;
    public bool DungeonDock;
    private bool? lastPad;
    public static MenuControlHints Bind(TMP_Text label,bool party=false)
    {
        var hints=label.GetComponent<MenuControlHints>()??label.gameObject.AddComponent<MenuControlHints>();
        hints.Label=label;hints.Party=party;return hints;
    }
    private void Update()
    {
        bool pad=MenuUIInputModule.Active?.UsingGamepad==true;
        if(lastPad==pad)return;lastPad=pad;
        if(DungeonDock)
        {
            Label.text=pad?"RB / LT: switch character\nA: actions   B: back   Right: details":"Tab / Shift+Tab: switch character\nEnter: actions   Esc: back   Right: details";
            return;
        }
        Label.text=Party ? (pad?"X / Square: Inventory   LB / L1: Skills\nA / Cross: actions   B / Circle: Back   RB / LT: hero":
            "Q: Inventory   R: Skills   Enter / Space: actions\nEscape: Back   Tab / Shift+Tab: hero   Wheel: scroll") :
            pad?"D-pad / stick: browse   A / Cross: choose   B / Circle: Back":"WASD / arrows: browse   Enter / Space: choose   Escape: Back";
    }
}
