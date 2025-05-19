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
		public PlayerInput PlayerInput;
		public InputAction MenuOpenCloseAction;
		public InputAction SubmitAction;
		public InputAction OpenSkillMenuAction;

		protected override void Initialize()
		{
			base.Initialize();

			MenuOpenCloseAction = PlayerInput.actions["OpenMenu"];
			SubmitAction = PlayerInput.actions["Submit"];
			OpenSkillMenuAction = PlayerInput.actions["OpenSkillMenu"];
		}

		private void Update()
		{
			MenuOpenClosedInput = MenuOpenCloseAction.WasPressedThisFrame();
			OpenSkillMenuInput = OpenSkillMenuAction.WasPressedThisFrame();
		}
	}
}