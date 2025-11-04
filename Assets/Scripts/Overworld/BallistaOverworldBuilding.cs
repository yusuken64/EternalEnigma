public class BallistaOverworldBuilding : OverworldBuilding
{
	public override void Interact(OverworldPlayer overworldPlayer, OverworldAction reverse)
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		var overworldMenuManager = FindFirstObjectByType<OverworldMenuManager>();
		overworldMenuManager.Open(overworldMenu.BallistaDialog);
		overworldMenu.BallistaDialog.Character = overworldPlayer.ControllingOverworldAlly;
		overworldMenu.BallistaDialog.Show(); //this should be done to all dialogs in Open()
		overworldMenu.BallistaDialog.CloseAction = () =>
		{
			var activeSkills = overworldMenu.BallistaDialog.GetActiveSkillsSave();
			overworldPlayer.ControllingOverworldAlly.Skills = activeSkills;
			overworldPlayer.SetAction(reverse);
		};
	}
}