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
        bool pad=ControlDeviceState.Gamepad;
        if(lastPad==pad)return;lastPad=pad;
        if(DungeonDock || Party)
        {
            Label.text=InputPrompts.Pick("Left/Right: tabs   Up/Down: browse\nTab / Shift+Tab: hero\n{Confirm}: actions   {Back}: back",
                "Left/Right: tabs   Up/Down: browse\nRB / LT: hero\n{Confirm}: actions   {Back}: back");
            Label.text=InputPrompts.Format(Label.text);
            return;
        }
        Label.text=InputPrompts.Format("{Move}: browse   {Confirm}: choose   {Back}: Back");
    }
}
