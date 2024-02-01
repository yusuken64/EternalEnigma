using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
	public GameObject SettingsCanvas;

	private void Start()
	{
		SettingsCanvas.gameObject.SetActive(false);
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			SettingsCanvas.gameObject.SetActive(!SettingsCanvas.gameObject.activeSelf);
		}
	}
}
