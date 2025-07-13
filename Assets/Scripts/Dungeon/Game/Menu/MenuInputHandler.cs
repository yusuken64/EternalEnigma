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
		public bool CancelMenuInput;
		public bool OptionInput;
		public PlayerInput PlayerInput;
		public InputAction MenuOpenCloseAction;
		public InputAction OpenSkillMenuAction;
		public InputAction SubmitAction;
		public InputAction CancelAction;
		public InputAction OpenOptions;

		protected override void Initialize()
		{
			base.Initialize();

			MenuOpenCloseAction = PlayerInput.actions["OpenMenu"];
			OpenSkillMenuAction = PlayerInput.actions["OpenSkillMenu"];
			SubmitAction = PlayerInput.actions["Submit"];
			CancelAction = PlayerInput.actions["Cancel"];
			OpenOptions = PlayerInput.actions["Options"];
		}

		private void Update()
		{
			MenuOpenClosedInput = MenuOpenCloseAction.WasPressedThisFrame();
			OpenSkillMenuInput = OpenSkillMenuAction.WasPressedThisFrame();
			CancelMenuInput = CancelAction.WasPressedThisFrame();
			OptionInput = OpenOptions.WasPressedThisFrame();
		}

        internal void CloseMenu()
		{
			PlayerInput.SwitchCurrentActionMap("Player");
		}

        internal void OpenMenu()
		{
			PlayerInput.SwitchCurrentActionMap("UI");
		}
    }
}