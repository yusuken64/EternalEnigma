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
            Label.text=GameplayText.Format(InputPrompts.Pick("Use Left or Right for tabs and Up or Down to browse.\nUse Tab or Shift+Tab to change heroes.\nPress {Confirm} for actions or {Back} to return.",
                "Use Left or Right for tabs and Up or Down to browse.\nUse RB or LT to change heroes.\nPress {Confirm} for actions or {Back} to return."));
            Label.text=InputPrompts.Format(Label.text);
            return;
        }
        Label.text=InputPrompts.Format("Use {Move} to browse. Press {Confirm} to choose or {Back} to return.");
    }
}
