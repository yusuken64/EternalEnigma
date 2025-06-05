using System.Collections.Generic;

public class Stairs : Interactable
{
	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
		return new()
		{
			new DynamicGameAction((character) =>
			{
				AudioManager.Instance.SoundEffects.Flee.PlayAsSound();
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
