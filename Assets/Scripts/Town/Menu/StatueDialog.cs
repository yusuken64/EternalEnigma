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
    private string rejection;
    public int DonatedAmount
    {
        get => Common.Instance.GameSaveData.TownSaveData.DonationTotal;
        set => Common.Instance.GameSaveData.TownSaveData.DonationTotal = value;
    }
    public override void PrepareTown(TownInteractionContext context)
    {
        rejection = null;
        DonatedAmountText.enableAutoSizing = true;
        DonatedAmountText.fontSizeMin = 16;
        DonatedAmountText.textWrappingMode = TextWrappingModes.Normal;
        NumberInput.Setup(8, CancelButton);
    }
    internal override void SetFirstSelect() => NumberInput.SelectableDigits[0].Select();
    private void Update()
    {
        var next = FindFirstObjectByType<Town>().Configuration.DungeonTiers
            .Where(t => t.RequiredDonation > DonatedAmount).OrderBy(t => t.RequiredDonation).FirstOrDefault();
        DonatedAmountText.text = GameplayText.Format($"Donated {DonatedAmount}g" +
            (next != null ? $"\nNext blessing at {next.RequiredDonation}g" : "\nAll tiers unlocked") +
            (rejection == null ? "" : "\n" + rejection));
    }
    public void Ok_Clicked()
    {
        var amount = FindFirstObjectByType<Town>().Services.Donate(NumberInput.GetNumber(), out var reason);
        if (amount <= 0)
        {
            rejection = reason;
            Update();
            GameMessages.Reject(reason);
            return;
        }
        CloseDialog();
        TownMenu.ShowMessage($"Donated {amount} gold. You have donated {DonatedAmount} gold in total.");
    }
    public void Cancel_Clicked() => CloseDialog();
}
