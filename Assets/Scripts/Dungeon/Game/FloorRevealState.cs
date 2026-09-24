using System;

// Reset on every floor (Game.AdvanceFloorRoutine). Read by Minimap.
[Serializable]
public class FloorRevealState
{
	public bool LayoutRevealed;        // Floor Sense: whole layout + stairs on the minimap
	public int EnemiesRevealedTurns;   // Farsight: enemies drawn on the minimap while > 0
	public bool TreasureRevealed;      // Treasure Hunter: Gold interactables drawn on the minimap
}
