using UnityEngine.SceneManagement;

public class EntranceOverworldBuilding : OverworldBuilding
{
	public override void Interact(OverworldPlayer overworldPlayer, OverworldAction reverse)
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		OverworldMenuManager.Open(overworldMenu.EntranceDialog);
		overworldMenu.EntranceDialog.Setup();
		overworldMenu.EntranceDialog.Show();
		overworldMenu.EntranceDialog.CloseAction = () =>
		{
			overworldPlayer.SetAction(reverse);
		};
	}
}
