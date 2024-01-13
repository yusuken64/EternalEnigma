using JuicyChickenGames.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatueDialog : Dialog
{
    public NumberInput NumberInput;
	public Button OkButton;
	public Button CancelButton;

	internal override void SetFirstSelect()
	{
		EventSystem.current.SetSelectedGameObject(NumberInput.SelectableDigits[0].gameObject);
	}

	internal void Show()
	{
		NumberInput.Setup(8);
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			NumberInput.ClearSelection();
			EventSystem.current.SetSelectedGameObject(OkButton.gameObject);
		}
	}

	public void Ok_Clicked()
	{
		int inputAmount = NumberInput.GetNumber();
		Debug.Log($"Donated {inputAmount}");

		var overworldPlayer = FindObjectOfType<OverworldPlayer>();

		var amount = Mathf.Min(overworldPlayer.Gold, inputAmount);
		overworldPlayer.Gold -= amount;

		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}

	public void Cancel_Clicked()
	{
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}
}
