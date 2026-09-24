// Whether enemies notice / wake up because of party members.
public static class EnemyAwareness
{
	public const int WakeRadius = 2;
	public const float DormantSpawnChance = 0.25f;

	public static bool CanNotice(Character enemy, Character target) =>
		enemy != null && target != null && target.Vitals != null && target.Vitals.HP > 0 &&
		EnemyTargeting.CanBeTargeted(target);

	// A party member moving near a dormant enemy wakes it, unless the party has Soft Step.
	public static bool ProximityWakes(Character enemy, Character mover) =>
		enemy != null && mover != null && mover.Team == Team.Player &&
		TileWorldDungeon.ChevDistance(enemy.TilemapPosition, mover.TilemapPosition) <= WakeRadius &&
		ExplorationPassives.PartyBestRank(ExplorationSkillNames.SoftStep) == 0;
}
