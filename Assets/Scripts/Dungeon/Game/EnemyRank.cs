public static class EnemyRank
{
	public static bool IsBoss(Character c) => c is Enemy e && e.IsBoss;
}
