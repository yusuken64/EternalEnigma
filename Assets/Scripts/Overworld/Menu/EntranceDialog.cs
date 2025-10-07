using JuicyChickenGames.Menu;
using System.Linq;
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
		var overworld = FindFirstObjectByType<Overworld>();
		overworld.WriteSaveData();
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			Common.Instance.GameSaveData.OverworldSaveData.RecruitedAlliesData =
			overworld.OverworldPlayer.RecruitedAllies.Select(x => new OverworldAllyData()
			{
				AllyName = x.Name
			}).ToList();
			//TODO somehow make this harder over time;
			Common.Instance.GameSaveData.DungeonSaveData.StartFloor = 0;
			Common.Instance.GameSaveData.DungeonSaveData.EndFloor = 5;
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