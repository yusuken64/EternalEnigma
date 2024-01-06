using UnityEngine;

public class Gold : Interactable
{
	internal override void DoInteraction()
	{
		//give treasure to player
		int goldAmount = UnityEngine.Random.Range(3, 10);
		Game game = Game.Instance;
		Player playerCharacter = game.PlayerCharacter;
		playerCharacter.Vitals.Gold += goldAmount;
		playerCharacter.SyncDisplayedStats();
		game.DoFloatingText($"{goldAmount} Gold", Color.yellow, playerCharacter.transform.position);
	}

	internal override string GetInteractionText()
	{
		return !Opened ? "Open Chest" : "";
	}
}