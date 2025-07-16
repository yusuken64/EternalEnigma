using System.Collections.Generic;
using UnityEngine;

public class Gold : Interactable
{
	public DroppedItemVisual DroppedItemVisual;
	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
		Game game = Game.Instance;
		int goldAmount = UnityEngine.Random.Range(3, 10);
		game.CurrentDungeon.RemoveInteractable(this);
		AudioManager.Instance.SoundEffects.BuySell.PlayAsSound();
		game.DoFloatingText($"{goldAmount} Gold", Color.yellow, character.transform.position);
		game.PlayerController.Gold += goldAmount;

		return new();
	}

	internal override string GetInteractionText()
	{
		return "";
		//return !Opened ? "Open Chest" : "";
	}
}