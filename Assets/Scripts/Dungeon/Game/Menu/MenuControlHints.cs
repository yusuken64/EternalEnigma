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
        if(DungeonDock)
        {
            Label.text=InputPrompts.Pick("Tab / Shift+Tab: switch character\n{Interact}: actions   {Back}: back   Right: details",
                "RB / LT: switch character\n{Interact}: actions   {Back}: back   Right: details");
            Label.text=InputPrompts.Format(Label.text);
            return;
        }
        Label.text=InputPrompts.Format(Party ? InputPrompts.Pick(
            "{Inventory}: Inventory   {Skills}: Skills   {Confirm}: actions\n{Back}: Back   Tab / Shift+Tab: hero   Wheel: scroll",
            "{Inventory}: Inventory   {Skills}: Skills\n{Confirm}: actions   {Back}: Back   RB / LT: hero") :
            "{Move}: browse   {Confirm}: choose   {Back}: Back");
    }
}
