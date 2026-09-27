using System.Collections.Generic;
using UnityEngine;

public class Gold : Interactable
{
	public DroppedItemVisual DroppedItemVisual;
    internal int? SeededAmount;
	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
		Game game = Game.Instance;
		int goldAmount = SeededAmount ?? GetGoldPickupAmount(game.PlayerController.Floor);
		game.CurrentDungeon.RemoveInteractable(this);
		AudioManager.Instance.SoundEffects.BuySell.PlayAsSound();
		GameMessages.Post($"Picked up {goldAmount} gold.");
		game.PlayerController.Gold += goldAmount;

		return new();
	}

	internal override string GetInteractionText()
	{
		return "";
		//return !Opened ? "Open Chest" : "";
	}

	public static int GetGoldPickupAmount(int floor)
	{
		floor = Mathf.Clamp(floor, 1, 30);
		int baseAmount = 5;
		int incrementPerFloor = 4;
		int randomBonus = Random.Range(0, 4);

		int gold = baseAmount + incrementPerFloor * (floor - 1) + randomBonus;
		return Mathf.Max(1, gold);
	}
}