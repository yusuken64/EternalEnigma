using System.Collections.Generic;
using UnityEngine;

public class Stairs : Interactable
{
    [SerializeField] private Transform stairBody;

    internal void Place(TileWorldDungeon dungeon, Vector3Int cell)
    {
        Position = cell;
        transform.position = dungeon.CellToWorld(cell);
        var center = dungeon.CellCenterToWorld(cell);
        center.z = DungeonPresentation.GroundPlaneZ;
        DungeonPickupFootprint.Place(stairBody, center, dungeon.CellSize);
    }

	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
        if (character != Game.Instance.PlayerController.ControlledAlly || !Game.Instance.CheckFloorExit()) return new();
		return new()
		{
			new DynamicGameAction((character) =>
			{
                if (!Game.Instance.CheckFloorExit()) return new();
                if (Game.Instance.Finale != null) { Game.Instance.Finale.RequestExit(); return new(); }
				if (Game.Instance.CurrentDungeon.IsExitFloor) { GameOverScreen.GoBackToTown(true,Game.Instance.PlayerController); return new(); }
				AudioManager.Instance?.PlayCue(SoundCue.Retreat);
				Game.Instance.AdvanceFloor();
				return new();
			},
			null,
			null)
		};
	}

	internal override string GetInteractionText()
	{
        if (Game.Instance?.Finale != null) return Game.Instance.Finale.Session.CurrentFloor == EternalEnigma.Core.Progression.FinalePlan.FloorCount ? "Ascend and return home" : "Ascend to the next floor";
		return Game.Instance?.CurrentDungeon?.Floor?.Variant == EternalEnigma.Core.World.DungeonVariant.Town ? "Pass through gate" : "Take Stairs";
	}
}
