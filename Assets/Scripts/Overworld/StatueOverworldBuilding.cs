public class StatueOverworldBuilding : OverworldBuilding
{
	public override void Interact(OverworldPlayer overworldPlayer, OverworldAction reverse)
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		OverworldMenuManager.Open(overworldMenu.StatueDialog);
		overworldMenu.StatueDialog.Show(); //this should be done to all dialogs in Open()
		overworldMenu.StatueDialog.CloseAction = () =>
		{
			overworldPlayer.SetAction(reverse);
		};
	}
}
