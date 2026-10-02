using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine.UI;

/// <summary>Free inn: rest to restore the party's HP/SP (damage persists between runs) or save the game.</summary>
public class InnDialog : Dialog
{
    public TextMeshProUGUI StatusText;
    public Button RestButton;
    public Button SaveButton;
    public Button CancelButton;

    private TownServices services;

    public override void PrepareTown(TownInteractionContext context)
    {
        services = context.Services;
        StatusText.text = services.NeedsRest ? "Rest to restore your party's HP and SP?" : "Your party is fully rested.";
    }

    internal override void SetFirstSelect() => (services != null && services.NeedsRest ? RestButton : SaveButton).Select();

    public void Rest_Clicked()
    {
        services.Rest(out var reason);
        CloseDialog();
        TownMenu.ShowMessage(reason ?? "Your party is fully restored.");
    }

    public void Save_Clicked()
    {
        services.SaveGame(out _);
        CloseDialog();
        TownMenu.ShowMessage("Game saved.");
    }

    public void Cancel_Clicked() => CloseDialog();
}
