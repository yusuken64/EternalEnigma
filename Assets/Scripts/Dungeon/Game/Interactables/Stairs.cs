using System.Collections.Generic;

public class Stairs : Interactable
{
	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
		return new()
		{
			new DynamicGameAction((character) =>
			{
				Game.Instance.AdvanceFloor();
				return new();
			},
			null,
			null)

		};
	}

	internal override string GetInteractionText()
	{
		return "Take Stairs";
	}
}
