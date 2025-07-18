public class BallistaOverworldBuilding : OverworldBuilding
{
	public override void Interact(OverworldPlayer overworldPlayer, OverworldAction reverse)
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		OverworldMenuManager.Open(overworldMenu.BallistaDialog);
		overworldMenu.BallistaDialog.Show(); //this should be done to all dialogs in Open()
		overworldMenu.BallistaDialog.CloseAction = () =>
		{
			overworldPlayer.SetAction(reverse);
		};
	}
}