using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatsDisplay : MonoBehaviour
{
	public TextMeshProUGUI StatNameText;
	public TextMeshProUGUI ValueText;
	public Slider StatValueSlider;
	private string _statName;
	private Func<string> _statValueStringFunce;
	private Func<float> _statValueSliderFunc;

	public void Setup(string statName, Func<string> statValueStringFunc, Func<float> statValueSliderFunc)
	{
		this._statName = statName;
		this._statValueStringFunce = statValueStringFunc;
		this._statValueSliderFunc = statValueSliderFunc;
	}

	public void UpdateUI()
	{
		StatNameText.text = _statName;
		ValueText.text = _statValueStringFunce?.Invoke();
		if (_statValueSliderFunc != null)
		{
			StatValueSlider.value = (float)(_statValueSliderFunc.Invoke());
		}
	}
}
