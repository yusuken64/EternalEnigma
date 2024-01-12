using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class OverworldMenuManager : SingletonMonoBehaviour<OverworldMenuManager>
{
	public EventSystem EventSystem;
	public bool Opened;

	public Stack<Dialog> DialogStack = new();

	protected override void Initialize()
	{
		base.Initialize();
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			if (DialogStack.Count > 0)
			{
				DialogStack.Peek().Submit();
			}
		}
	}

	internal void CloseMenu()
	{
		DialogStack.Clear();
		Opened = false;
	}

	public Action LateAction;

	private void LateUpdate()
	{
		LateAction?.Invoke();
		LateAction = null;
	}

	internal static void Open(Dialog dialog)
	{
		if (OverworldMenuManager.Instance.DialogStack.Count > 0)
		{
			OverworldMenuManager.Instance.DialogStack.Peek().SaveSelection();
		}

		dialog.gameObject.SetActive(true);
		OverworldMenuManager.Instance.DialogStack.Push(dialog);
		OverworldMenuManager.Instance.Opened = true;

		OverworldMenuManager.Instance.LateAction = () =>
		{
			dialog.SetFirstSelect();
		};
	}

	internal static void Close(Dialog dialog)
	{
		dialog.gameObject.SetActive(false);
		OverworldMenuManager.Instance.DialogStack.Pop();

		if (OverworldMenuManager.Instance.DialogStack.Count <= 0)
		{
			OverworldMenuManager.Instance.Opened = false;
			return;
		}

		var top = OverworldMenuManager.Instance.DialogStack.Peek();
		OverworldMenuManager.Instance.LateAction = () =>
		{
			top.RestoreSelect();
		};
	}
}
