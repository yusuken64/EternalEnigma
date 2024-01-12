using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatueDialog : Dialog
{
    public NumberInput NumberInput;
	public Button OkButton;
	public Button CancelButton;

	public Action CloseAction { get; internal set; }

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
		Debug.Log($"Donated {NumberInput.GetNumber()}");
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}

	public void Cancel_Clicked()
	{
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}
}
