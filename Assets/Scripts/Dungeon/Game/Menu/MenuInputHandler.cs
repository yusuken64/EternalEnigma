using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JuicyChickenGames.Menu
{
    public class MenuInputHandler : MonoBehaviour
	{
		private Stack<Dialog> dialogStack = new();

		public bool IsBusy => dialogStack.Count > 0;

		public bool MenuOpenClosedInput;
		public bool OpenSkillMenuInput;
		public bool SubmitMenuInput;
		public bool CancelMenuInput;
		public bool OptionInput;
		public PlayerInput PlayerInput;
		private InputAction moveAction;
		public InputAction NavigateActions;
		public InputAction MenuOpenCloseAction;
		public InputAction OpenSkillMenuAction;
		public InputAction SubmitAction;
		public InputAction CancelAction;
		public InputAction OpenOptions;

		public Vector2 MoveInput;
		public bool IsMoving;

		private void Awake()
		{
			Initialize();
		}

		protected void Initialize()
		{
			NavigateActions = PlayerInput.actions["Navigate"];
			MenuOpenCloseAction = PlayerInput.actions["OpenMenu"];
			OpenSkillMenuAction = PlayerInput.actions["OpenSkillMenu"];
			SubmitAction = PlayerInput.actions["Submit"];
			CancelAction = PlayerInput.actions["Cancel"];
			OpenOptions = PlayerInput.actions["Options"];
		}

		private void Update()
		{
			MoveInput = NavigateActions.ReadValue<Vector2>();
			IsMoving = MoveInput.sqrMagnitude > 0.1f;

			MenuOpenClosedInput = MenuOpenCloseAction.WasPressedThisFrame();
			OpenSkillMenuInput = OpenSkillMenuAction.WasPressedThisFrame();
			SubmitMenuInput = SubmitAction.WasPressedThisFrame();
			CancelMenuInput = CancelAction.WasPressedThisFrame();
			OptionInput = OpenOptions.WasPressedThisFrame();
		}

        internal void SwitchToPlayerInput()
		{
			PlayerInput.SwitchCurrentActionMap("Player");
			ClearInputThisFrame();
		}

        internal void SwitchToUIInput()
		{
			PlayerInput.SwitchCurrentActionMap("UI");
			ClearInputThisFrame();
		}

		public void ClearInputThisFrame()
		{
			// Clears WasPressedThisFrame() flags for all actions
			var currentMap = PlayerInput.currentActionMap;
			currentMap.Disable();
			currentMap.Enable();

			// Also reset cached booleans
			MenuOpenClosedInput = false;
			OpenSkillMenuInput = false;
			SubmitMenuInput = false;
			CancelMenuInput = false;
			OptionInput = false;
		}

	}
}