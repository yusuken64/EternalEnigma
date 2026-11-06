using TMPro;
using UnityEngine;

/// <summary>The same device-aware footer for party, shop and trainer dialogs.</summary>
public sealed class MenuControlHints : MonoBehaviour
{
    public TMP_Text Label;
    public bool Party;
    public bool DungeonDock;
    public static MenuControlHints Bind(TMP_Text label,bool party=false)
    {
        var hints=label.GetComponent<MenuControlHints>()??label.gameObject.AddComponent<MenuControlHints>();
        hints.Label=label;hints.Party=party;return hints;
    }
    private void Update()
    {
        if (Label == null) return;
        if(DungeonDock || Party)
        {
            Label.text=InputPrompts.Format($"Use {InputPrompts.Navigate} for tabs and browsing.\nUse {InputPrompts.NextHero} / {InputPrompts.PreviousHero} to change heroes.\nPress {{Confirm}} for actions or {{Back}} to return.");
            return;
        }
        Label.text=InputPrompts.Format("Use {Navigate} to browse. Press {Confirm} to choose or {Back} to return.");
    }
}
