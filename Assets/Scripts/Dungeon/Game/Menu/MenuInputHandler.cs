using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JuicyChickenGames.Menu
{
    public class MenuInputHandler : SingletonMonoBehaviour<MenuInputHandler>
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

		protected override void Initialize()
		{
			base.Initialize();

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
		}

        internal void SwitchToUIInput()
		{
			PlayerInput.SwitchCurrentActionMap("UI");
		}
    }
}