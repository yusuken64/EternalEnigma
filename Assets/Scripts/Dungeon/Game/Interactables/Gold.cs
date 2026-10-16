using System.Collections.Generic;
using UnityEngine;

public class Gold : Interactable
{
	public DroppedItemVisual DroppedItemVisual;
    internal int? SeededAmount;
	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
		Game game = Game.Instance;
        if (Opened || !game.CurrentDungeon.Interactables.Contains(this)) return new();
		int goldAmount = SeededAmount ?? GetGoldPickupAmount(game.PlayerController.Floor);
        using var receipt = AutoplayRunner.LootPickup(this, character, goldAmount);
        Opened = true;
		game.CurrentDungeon.RemoveInteractable(this);
		AudioManager.Instance?.PlayCue(SoundCue.Pickup);
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
