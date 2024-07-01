using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class NumberInput : MonoBehaviour
{
	public Transform Container;
	public SelectableDigit SelectableDigitPrefab;
	internal List<SelectableDigit> SelectableDigits;

	public Button OKButton;

	private int places;
	private Action lateAction;
	private SelectableDigit _selectedDigit;
	private DungeonControls controls;

	private void OnEnable()
	{
		controls = new DungeonControls();
		controls.UI.Submit.performed += Submit_performed;
		controls.UI.Cancel.performed += Cancel_performed;
		controls.UI.Enable();
	}

	private void OnDisable()
	{
		controls.UI.Submit.performed -= Submit_performed;
		controls.UI.Cancel.performed -= Cancel_performed;
		controls.UI.Disable();
	}

	public void Setup(int places)
	{
		this.places = places;

		var data = Enumerable.Repeat(10, places)
			.Select((digit, index) => (int)Mathf.Pow(digit, index))
			.ToList();

		Action<SelectableDigit, int> action = (view, data) =>
		{
			view.Setup(data);
			view.SelectedHandler = (x) =>
			{
				_selectedDigit = x;
			};
		};
		SelectableDigits = Container.RePopulateObjects(SelectableDigitPrefab, data, action);

		SetupNavigation();

		lateAction = () =>
		{
			EventSystem.current.SetSelectedGameObject(SelectableDigits[0].gameObject);
		};
	}

	internal int GetNumber()
	{
		var sum = SelectableDigits.Aggregate(0, (accumulate, selectableDigit) => { return accumulate += selectableDigit.GetNumber(); });
		return sum;
	}

	internal void ClearSelection()
	{
		_selectedDigit = null;
	}

	private void LateUpdate()
	{
		if (lateAction != null)
		{
			lateAction?.Invoke();
			lateAction = null;
		}
	}

	private void SetupNavigation()
	{
		for (int i = 0; i < SelectableDigits.Count(); i++)
		{
			var selectableDigit = SelectableDigits[i];

			Navigation navigation = new Navigation();
			navigation.mode = Navigation.Mode.Explicit;
			navigation.selectOnLeft = SelectableDigits[(i + 1) % SelectableDigits.Count];
			navigation.selectOnRight = SelectableDigits[(i - 1 + SelectableDigits.Count) % SelectableDigits.Count];
			navigation.selectOnDown = OKButton;

			selectableDigit.navigation = navigation;
		}
	}

	private void Cancel_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
	{
		if (obj.performed)
		{
			_selectedDigit?.Down_Pressed();
		}
	}

	private void Submit_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
	{
		if (obj.performed)
		{
			_selectedDigit?.Up_Pressed();
		}
	}


	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.W))
		{
			_selectedDigit?.Up_Pressed();
		}
		if (Input.GetKeyDown(KeyCode.S))
		{
			_selectedDigit?.Down_Pressed();
		}
	}
}
