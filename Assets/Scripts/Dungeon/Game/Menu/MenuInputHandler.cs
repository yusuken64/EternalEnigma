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
		public PlayerInput PlayerInput;
		public InputAction MenuOpenCloseAction;
		public InputAction SubmitAction;
		public InputAction CancelAction;
		public InputAction OpenSkillMenuAction;

		protected override void Initialize()
		{
			base.Initialize();

			MenuOpenCloseAction = PlayerInput.actions["OpenMenu"];
			SubmitAction = PlayerInput.actions["Submit"];
			CancelAction = PlayerInput.actions["Cancel"];
			OpenSkillMenuAction = PlayerInput.actions["OpenSkillMenu"];
		}

		private void Update()
		{
			MenuOpenClosedInput = MenuOpenCloseAction.WasPressedThisFrame();
			OpenSkillMenuInput = OpenSkillMenuAction.WasPressedThisFrame();
			CancelMenuInput = CancelAction.WasPressedThisFrame();
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