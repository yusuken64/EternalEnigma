using JuicyChickenGames.Menu;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatueDialog : Dialog
{
    public TextMeshProUGUI DonatedAmountText;
    public NumberInput NumberInput;
    public Button OkButton;
    public Button CancelButton;
    public int DonatedAmount
    {
        get => Common.Instance.GameSaveData.TownSaveData.DonationTotal;
        set => Common.Instance.GameSaveData.TownSaveData.DonationTotal = value;
    }
    public override void PrepareTown(TownInteractionContext context) => NumberInput.Setup(8);
    internal override void SetFirstSelect() => NumberInput.SelectableDigits[0].Select();
    private void Update()
    {
        var next = FindFirstObjectByType<Town>().Configuration.DungeonTiers
            .Where(t => t.RequiredDonation > DonatedAmount).OrderBy(t => t.RequiredDonation).FirstOrDefault();
        DonatedAmountText.text = $"Donated {DonatedAmount}g" +
            (next != null ? $"\nNext tier: {next.RequiredDonation}g" : "\nAll tiers unlocked");
    }
    public void Ok_Clicked()
    {
        var amount = FindFirstObjectByType<Town>().Services.Donate(NumberInput.GetNumber());
        CloseDialog();
        TownMenu.ShowMessage(amount > 0 ? $"Donated {amount}g. Total: {DonatedAmount}g." : "Enter an amount you can afford.");
    }
    public void Cancel_Clicked() => CloseDialog();
}
