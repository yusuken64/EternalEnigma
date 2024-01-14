using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatueDialog : Dialog
{
    public TextMeshProUGUI DonatedAmountText;
    public NumberInput NumberInput;
	public Button OkButton;
	public Button CancelButton;

	public int DonatedAmount;

	internal override void SetFirstSelect()
	{
		EventSystem.current.SetSelectedGameObject(NumberInput.SelectableDigits[0].gameObject);
	}

	internal void Show()
	{
		NumberInput.Setup(places: 8);
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			NumberInput.ClearSelection();
			EventSystem.current.SetSelectedGameObject(OkButton.gameObject);
		}

		DonatedAmountText.text = $"Donated {DonatedAmount}";
	}

	public void Ok_Clicked()
	{
		int inputAmount = NumberInput.GetNumber();
		Debug.Log($"Donated {inputAmount}");

		var overworldPlayer = FindObjectOfType<OverworldPlayer>();

		var amount = Mathf.Min(overworldPlayer.Gold, inputAmount);
		overworldPlayer.Gold -= amount;
		DonatedAmount += amount;

		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}

	public void Cancel_Clicked()
	{
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}
}
