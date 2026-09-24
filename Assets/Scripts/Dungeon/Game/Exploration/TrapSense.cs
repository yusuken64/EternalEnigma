// Scout "Trap Sense": reveal hidden traps near the ally every turn.
public static class TrapSense
{
	public const int BaseRadius = 2;

	// +1 radius at ranks 3 and 5.
	public static int Radius(int rank) => rank <= 0 ? 0 : BaseRadius + (rank >= 3 ? 1 : 0) + (rank >= 5 ? 1 : 0);

	public static void RevealAround(Ally ally)
	{
		var dungeon = Game.Instance?.CurrentDungeon;
		if (ally == null || dungeon == null) return;
		int rank = ExplorationPassives.Rank(ally, ExplorationSkillNames.TrapSense);
		if (rank <= 0) return;
		dungeon.RevealTrapsAround(ally.TilemapPosition, Radius(rank));
	}
}
