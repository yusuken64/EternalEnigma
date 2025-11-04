public class ShopOverworldBuilding : OverworldBuilding
{
	public override void Interact(OverworldPlayer overworldPlayer, OverworldAction reverse)
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		FindFirstObjectByType<OverworldMenuManager>().Open(overworldMenu.ShopDialog);
		overworldMenu.ShopDialog.Show(); //this should be done to all dialogs in Open()
		overworldMenu.ShopDialog.CloseAction = () =>
		{
			overworldPlayer.SetAction(reverse);
		};
	}
}
