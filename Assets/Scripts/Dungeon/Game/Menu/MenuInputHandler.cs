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
		public PlayerInput PlayerInput;
		public InputAction MenuOpenCloseAction;
		public InputAction SubmitAction;

		protected override void Initialize()
		{
			base.Initialize();

			MenuOpenCloseAction = PlayerInput.actions["OpenMenu"];
			SubmitAction = PlayerInput.actions["Submit"];
		}

		private void Update()
		{
			MenuOpenClosedInput = MenuOpenCloseAction.WasPressedThisFrame();
		}
	}
}