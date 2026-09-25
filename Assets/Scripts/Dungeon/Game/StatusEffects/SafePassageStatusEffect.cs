using System.Collections.Generic;
using System.Linq;

// Party-wide stealth (Scout "Safe Passage"). Enemies cannot target the holder.
// Ends for every holder as soon as any party member damages or attacks an enemy.
public class SafePassageStatusEffect : StatusEffect
{
	public override GameAction GetActionOverride(Character character) => null;
	internal override StatModification GetStatModification() => null;
	internal override string GetEffectName() => "Safe Passage";
	internal override bool PreventsMenu() => false;

	internal override IEnumerable<GameAction> GetResponseTo(Character owner, GameAction action)
	{
		if (action is TakeDamageAction damage && damage.Attacker != null && damage.Target != null &&
			damage.Attacker.Team == Team.Player && damage.Target.Team != Team.Player)
			TurnsLeft = 0;
		return Enumerable.Empty<GameAction>();
	}
}
