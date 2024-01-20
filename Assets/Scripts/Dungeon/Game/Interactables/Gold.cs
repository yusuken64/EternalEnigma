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
		game.DoFloatingText($"{goldAmount} Gold", Color.yellow, character.transform.position);
		return new List<GameAction>()
		{
			new ModifyStatAction(
				character,
				character,
				(stats, vitals) => 
				{
					vitals.Gold += goldAmount;
				},
				false)
		};
	}

	internal override string GetInteractionText()
	{
		return !Opened ? "Open Chest" : "";
	}
}