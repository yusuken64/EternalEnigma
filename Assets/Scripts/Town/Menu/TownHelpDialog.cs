using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TownHelpDialog : Dialog
{
	public Button OkButton;

	internal override void SetFirstSelect()
	{
		OkButton.Select();
	}

	public void Ok_Clicked()
	{
		
		CloseDialog();
	}

	internal void Show()
	{
		this.gameObject.SetActive(true);
	}
}
