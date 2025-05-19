using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public Player Player;
	private DungeonControls controls;

	public static bool MenuOpenClosedInput { get; internal set; }

	void Awake()
	{
		controls = new DungeonControls();

		controls.Player.Move.performed += Move_performed;
		controls.Player.Move.canceled += Move_canceled;
		controls.Player.HoldPosition.performed += HoldPosition_performed;
		controls.Player.HoldPosition.canceled += HoldPosition_canceled;

		controls.Player.Attack.performed += Attack_performed;
		controls.Player.Use.performed += Use_performed;
		controls.Player.Menu.performed += Menu_performed;
	}

	private void Update()
	{
		MenuOpenClosedInput = controls.Player.Menu.WasPerformedThisFrame();
	}

	private void Menu_performed(InputAction.CallbackContext obj)
	{
		if (controls.Player.Menu.WasPerformedThisFrame())
		{
			Player.ControllerMenuThisFrame = true;
		}
	}

	private void Use_performed(InputAction.CallbackContext obj)
	{
		if (controls.Player.Use.WasPerformedThisFrame())
		{
			Player.ControllerUseThisFrame = true;
		}
	}

	private void Attack_performed(InputAction.CallbackContext obj)
	{
		if (controls.Player.Attack.WasPerformedThisFrame())
		{
			Player.ControllerAttackThisFrame = true;
		}
	}

	private void Move_canceled(InputAction.CallbackContext obj)
	{
		Player.ControllerHeld = false;
	}

	private void Move_performed(InputAction.CallbackContext obj)
	{
		if (Game.Instance.InventoryMenu.isActiveAndEnabled ||
			Game.Instance.AllyMenu.isActiveAndEnabled ||
			Game.Instance.SkillDialog.isActiveAndEnabled)
		{
			return;
		}

		var direction = obj.ReadValue<Vector2>();
		var directionInt = new Vector3Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));
		var facing = Character.GetFacing(directionInt);
		Player.SetFacing(facing);
		Player.ControllerHeld = true;
	}

	private void HoldPosition_canceled(InputAction.CallbackContext obj)
	{
		Player.ControllerHoldPosition = false;
	}

	private void HoldPosition_performed(InputAction.CallbackContext obj)
	{
		Player.ControllerHoldPosition = true;
	}

	private void OnEnable()
	{
		controls.Player.Enable();
	}

	private void OnDisable()
	{
		controls.Player.Disable();
	}
}
