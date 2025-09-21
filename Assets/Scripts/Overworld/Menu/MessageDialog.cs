using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MessageDialog : Dialog
{
	public TextMeshProUGUI PromptText;
	public Button OkButton;

	private void Awake()
	{
		this.gameObject.SetActive(false);
	}

	public void Ok_Clicked()
	{
		OverworldMenuManager.Close(this);
	}

	internal override void SetFirstSelect()
	{
		EventSystem.current.SetSelectedGameObject(null);
		EventSystem.current.SetSelectedGameObject(OkButton.gameObject);

		var nav = new Navigation
		{
			mode = Navigation.Mode.None
		};
		OkButton.navigation = nav;
	}
}