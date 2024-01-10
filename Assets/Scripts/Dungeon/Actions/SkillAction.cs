using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal class SkillAction : GameAction
{
	private Character caster;
	private Skill skill;

	public SkillAction(Character caster, Skill skill)
	{
		this.caster = caster;
		this.skill = skill;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		AddMetricsModification(
			caster,
			(stats, vitals) =>
			{
				vitals.SP -= skill.SPCost;
			});

		return skill.GetEffects(caster);
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		return skill.ExecuteRoutine(caster);
	}

	internal override bool IsValid(Character character)
	{
		return skill.IsValid(caster);
	}
}
