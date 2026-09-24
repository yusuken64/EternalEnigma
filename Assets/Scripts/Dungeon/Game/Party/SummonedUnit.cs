using UnityEngine;

public enum SummonKind { Clone, Dominated }

// Marks a temporary party-side unit. Summons never count as party members.
public class SummonedUnit : MonoBehaviour
{
	public SummonKind Kind;
	public Character Summoner;
	public int TurnsLeft;
	public Team OriginalTeam;
	public int Order;

	private static int nextOrder;
	public static int NextOrder() => ++nextOrder;
}
