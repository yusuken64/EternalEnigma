using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GlobalSettings : MonoBehaviour
{
	public GameObject SettingsCanvas;
	public GameObject FirstSelected;

	public NavigationHandler NavigationHandler;
	public Action CloseAction;

	public TabGroup TabGroup;
	public Button ResumeButton;
	public Button ReturntoMainButton;

	private void Start()
	{
		SettingsCanvas.gameObject.SetActive(false);
		NavigationHandler.gameObject.SetActive(false);

		SetupTabNavigation();
	}

	private void SetupTabNavigation()
	{
		var resumeNav = new Navigation
		{
			mode = Navigation.Mode.Explicit
		};
		resumeNav.selectOnDown = TabGroup.TabContents.First().TabButton;
		ResumeButton.navigation = resumeNav;

		var returntoMainButtonNav = new Navigation
		{
			mode = Navigation.Mode.Explicit
		};
		returntoMainButtonNav.selectOnUp = TabGroup.TabContents.Last().TabButton;
		ReturntoMainButton.navigation = returntoMainButtonNav;

		var tabs = TabGroup.TabContents;
		for (int i = 0; i < tabs.Count; i++)
		{
			var button = tabs[i].TabButton;
			var nav = new Navigation
			{
				mode = Navigation.Mode.Explicit
			};

			if (i == 0)
			{
				nav.selectOnUp = ResumeButton;
			}

			if (i == tabs.Count - 1)
			{
				nav.selectOnDown = ReturntoMainButton;
			}

			// Set Up
			if (i > 0)
				nav.selectOnUp = tabs[i - 1].TabButton;

			// Set Down
			if (i < tabs.Count - 1)
				nav.selectOnDown = tabs[i + 1].TabButton;

			button.navigation = nav;
		}
	}

	private void OnEnable()
	{
		TabGroup.TabClicked += HandleTabClicked;
	}

	private void OnDisable()
	{
		TabGroup.TabClicked -= HandleTabClicked;
	}

	public void HandleTabClicked(TabContent tabContent)
	{
		GameObject content = tabContent.Content;
		// Try to find the first selectable child
		var firstSelectable = content.GetComponentInChildren<Selectable>(includeInactive: false);

		if (firstSelectable != null)
		{
			firstSelectable.Select();
		}
	}

	public void ShowDialog()
	{
		this.gameObject.SetActive(true);
		SettingsCanvas.gameObject.SetActive(true);
		NavigationHandler.gameObject.SetActive(true);
		NavigationHandler.Init();
		NavigationHandler.PushDialog(this, FirstSelected);
    }

	public void Exit_Clicked()
	{
		SettingsCanvas.gameObject.SetActive(!SettingsCanvas.gameObject.activeSelf);
		NavigationHandler.PopDialog(this);
		NavigationHandler.gameObject.SetActive(false);
		CloseAction?.Invoke();
		CloseAction = null;
	}

	public void MainMenu_Clicked()
	{
		SettingsCanvas.gameObject.SetActive(!SettingsCanvas.gameObject.activeSelf);
		NavigationHandler.PopDialog(this);
		SceneManager.LoadScene("MainMenu");
		NavigationHandler.gameObject.SetActive(false);
	}
}
