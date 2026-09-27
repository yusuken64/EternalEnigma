using System.Collections.Generic;
using System.Linq;

public class BumpTrap : Trap
{
	internal override string GetInteractionText()
	{
		return "Bump Trap";
	}

	internal override List<GameAction> GetTrapSideEffects(Character character)
	{
		if (!CanTrigger(character)) return new();
		VisualObject.gameObject.SetActive(true);

		if (UnityEngine.Random.value > 0.5f)
		{
			var playerController = FindFirstObjectByType<PlayerController>();
			if (character == playerController.ControlledAlly)
			{
				var inventoryItems = playerController.Inventory.InventoryItems.Where(x => !PartyRules.PartyMembers(Game.Instance).Any(a => a.Equipment.IsEquipped(x))).Take(3).ToArray();
				var effects = inventoryItems.Select(x => new DropItemAction(playerController.Inventory, x, character.TilemapPosition))
					.Cast<GameAction>()
					.ToList();
				effects.Insert(0, new TrapFeedbackAction(character, "Bump Trap", $"{GameMessages.Name(character)} triggered the Bump Trap; loose items may fall nearby."));
				return effects;
			}
		}
		else return new() { new TrapFeedbackAction(character, "Evaded", $"{GameMessages.Name(character)} evaded the Bump Trap; no items were dropped.") };
		return new();
	}
}
