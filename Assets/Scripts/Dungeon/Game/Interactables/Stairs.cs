using System.Collections.Generic;

public class Stairs : Interactable
{
	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
        if (character != Game.Instance.PlayerController.ControlledAlly || !Game.Instance.CheckFloorExit()) return new();
		return new()
		{
			new DynamicGameAction((character) =>
			{
                if (!Game.Instance.CheckFloorExit()) return new();
				if (Game.Instance.CurrentDungeon.IsExitFloor) { GameOverScreen.GoBackToTown(true,Game.Instance.PlayerController); return new(); }
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
