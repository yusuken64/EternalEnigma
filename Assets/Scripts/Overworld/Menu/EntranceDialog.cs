using JuicyChickenGames.Menu;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EntranceDialog : Dialog
{
	public Button OkButton;
	internal override void SetFirstSelect()
	{
		OkButton.Select();
	}

	public void Ok_Clicked()
	{
		FindFirstObjectByType<Overworld>().WriteSaveData();
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SaveSystem.SaveData(Common.Instance.GameSaveData);
			SceneManager.LoadScene("DungeonScene");
		});
	}
	public void Cancel_Clicked()
	{
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}

	internal void Show()
	{
		this.gameObject.SetActive(true);
	}
}