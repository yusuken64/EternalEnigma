using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class NavigationHandler : MonoBehaviour
{
	private DungeonControls controls;
	private EventSystem currentEventSystem;
	private GameObject lastSelected;

	private void Awake()
	{
		currentEventSystem = EventSystem.current;
		controls = new DungeonControls();
		controls.UI.Enable();
	}

	private void OnDestroy()
	{
		controls.UI.Disable();
	}

	private void Update()
	{
		if (lastSelected != currentEventSystem.currentSelectedGameObject)
		{
			lastSelected = currentEventSystem.currentSelectedGameObject;
			if (lastSelected != null)
			{
				Common.Instance.AudioManager.PlaySoundEffect(Common.Instance.AudioManager.SoundEffects.Hover);
			}
		}
	}
}