using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class AllySkillContext
{
	public Ally Ally { get; private set; }
	public Game Game { get; private set; }
	public IReadOnlyList<Skill> Castable { get; private set; }
	public IReadOnlyList<Character> VisibleEnemies { get; private set; }
	public IReadOnlyList<Ally> Party { get; private set; }
	public IReadOnlyList<Ally> Downed { get; private set; }
	public int SpReserve { get; private set; }
	public bool AllowMovement { get; private set; }
	public float NormalAttackValue { get; private set; }

	private AllySkillContext() { }

	public bool CanAfford(Skill skill) => AllySkillBudget.CanAfford(Ally.Vitals.SP, skill, SpReserve);

	public IEnumerable<Skill> WithIntent(SkillIntent intent) =>
		Castable.Where(s => SkillIntents.Classify(s).Has(intent) && CanAfford(s));

	public static AllySkillContext Build(Ally ally, Game game)
	{
		if (ally == null) throw new ArgumentNullException(nameof(ally));
		if (game == null) throw new ArgumentNullException(nameof(game));

		var context = new AllySkillContext();
		context.Ally = ally;
		context.Game = game;

		context.AllowMovement = ally.AllyStrategy != AllyStrategy.HoldPosition;

		context.VisibleEnemies = game.AllCharacters
			.Where(c => c != null && c != ally && c.Vitals.HP > 0 && c.Team != ally.Team && c.Team != Team.Neutral && game.CurrentDungeon.CanSee(ally, c))
			.ToList();

		context.Party = PartyRules.StandingMembers(game);
		context.Downed = (game.DownedAllies ?? new List<Ally>()).Where(a => a != null).ToList();

		var castableSkills = new List<Skill>();
		var allSkills = ally.Skills ?? new List<Skill>();

		foreach (var s in allSkills)
		{
			if (s == null || s.ActivationType != ActivationType.Active)
				continue;

			var intent = SkillIntents.Classify(s);
			if (intent == SkillIntent.None || intent.Has(SkillIntent.Never))
				continue;

			if (!context.AllowMovement && intent.Has(SkillIntent.Movement))
				continue;

			if (!ally.CanCast(s, out _))
				continue;

			if (!AllySkillBudget.ArrowsAllow(ArrowSupply.Count(ally), ArrowSupply.RequiredToCast(s)))
				continue;

			castableSkills.Add(s);
		}

		context.Castable = castableSkills;

		context.SpReserve = AllySkillBudget.SpReserve(context.Castable, ally.AllyStrategy);

		if (ally.IsRangedAttack(out _))
		{
			context.NormalAttackValue = context.VisibleEnemies.Count > 0
				? context.VisibleEnemies.Max(e => SkillEstimates.NormalAttackExpected(ally, e))
				: 0f;
		}
		else
		{
			var bounds = ally.GetAttackBounds();
			var visibleInRange = context.VisibleEnemies.Where(e => bounds.Overlaps2D(e.ToBounds()));
			context.NormalAttackValue = visibleInRange.Any()
				? visibleInRange.Max(e => SkillEstimates.NormalAttackExpected(ally, e))
				: 0f;
		}

		return context;
	}
}
