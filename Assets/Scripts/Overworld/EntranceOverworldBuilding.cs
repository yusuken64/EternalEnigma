using UnityEngine.SceneManagement;

public class EntranceOverworldBuilding : OverworldBuilding
{
	public override void Interact(OverworldPlayer overworldPlayer, OverworldAction reverse)
	{
		FindFirstObjectByType<Overworld>().WriteSaveData();
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SceneManager.LoadScene("DungeonScene");
		});
	}
}
