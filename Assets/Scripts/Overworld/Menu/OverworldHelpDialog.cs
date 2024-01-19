using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OverworldHelpDialog : Dialog
{
	public Button OkButton;

	internal override void SetFirstSelect()
	{
		OkButton.Select();
	}

	public void Ok_Clicked()
	{
		this.CloseAction?.Invoke();
		OverworldMenuManager.Close(this);
	}

	internal void Show()
	{
		this.gameObject.SetActive(true);
	}
}
