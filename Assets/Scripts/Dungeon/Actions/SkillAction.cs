using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal class SkillAction : GameAction
{
	private Player player;
	private Skill skill;

	public SkillAction(Player player, Skill skill)
	{
		this.player = player;
		this.skill = skill;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		AddMetricsModification(
			player,
			(stats, vitals) =>
			{
				vitals.SP -= skill.SPCost;
			});

		return skill.GetEffects(player);
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		return skill.ExecuteRoutine(player);
	}

	internal override bool IsValid(Character character)
	{
		return skill.IsValid(player);
	}
}
