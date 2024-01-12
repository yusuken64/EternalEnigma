using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectableDigit : Selectable
{
	public TextMeshProUGUI DigitText;
	public int DisplayedDigit = 1;
	private int _data;

	public Action<SelectableDigit> SelectedHandler { get; internal set; }

	internal void Setup(int data)
	{
		this._data = data;
		UpdateUI();
	}

	public int GetNumber()
	{
		return _data * DisplayedDigit;
	}

	public void Up_Pressed()
	{
		DisplayedDigit++;
		DisplayedDigit %= 10;
		UpdateUI();
	}

	public void Down_Pressed()
	{
		DisplayedDigit--;
		DisplayedDigit += 10;
		DisplayedDigit %= 10;
		UpdateUI();
	}

	public void UpdateUI()
	{
		DigitText.text = DisplayedDigit.ToString();
	}

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		SelectedHandler?.Invoke(this);
	}
}
