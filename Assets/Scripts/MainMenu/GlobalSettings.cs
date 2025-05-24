using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
	public GameObject SettingsCanvas;
	public GameObject FirstSelected;

	private void Start()
	{
		SettingsCanvas.gameObject.SetActive(false);
	}

	public void ShowDialog()
	{
		SettingsCanvas.gameObject.SetActive(true);
		FindFirstObjectByType<NavigationHandler>().PushDialog(this, FirstSelected);
    }

    private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			SettingsCanvas.gameObject.SetActive(!SettingsCanvas.gameObject.activeSelf);
			if (SettingsCanvas.activeSelf)
            {
				ShowDialog();
            }
            else
			{
				FindFirstObjectByType<NavigationHandler>().PopDialog(this);
			}
		}
	}

	public void Exit_Clicked()
	{
		SettingsCanvas.gameObject.SetActive(!SettingsCanvas.gameObject.activeSelf);
		FindFirstObjectByType<NavigationHandler>().PopDialog(this);
	}
}
