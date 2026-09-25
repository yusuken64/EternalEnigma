using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A song the performer is playing. Lives only on the performer; allies in Radius get the bonus via SongAura.
public class SongStatusEffect : StatusEffect
{
	public string SongId;
	public string SongName;
	public StatModification Modification = new();
	public int HealPerTurn;
	public int Radius = 3;
	public int StartedOrder;

	private static int nextOrder;
	public static int NextOrder() => ++nextOrder;

	private void Awake() => Family = BuffFamily.Song;

	public override GameAction GetActionOverride(Character character) => null;
	internal override StatModification GetStatModification() => null; // applied through SongAura, not directly
	internal override string GetEffectName() => $"Song: {SongName}";
	internal override bool PreventsMenu() => false;

	internal override List<GameAction> GetTickEffects(Character owner)
	{
		if (HealPerTurn <= 0 || owner == null || Game.Instance == null) return null;
		return Game.Instance.Allies
			.Where(a => a != null && a.Vitals.HP > 0 &&
				TileWorldDungeon.ChevDistance(a.TilemapPosition, owner.TilemapPosition) <= Radius)
			.Select(a => (GameAction)new TakeHealAction(owner, a, HealPerTurn, false))
			.ToList();
	}
}
