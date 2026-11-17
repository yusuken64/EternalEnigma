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
	public int ManaReserve { get; private set; }
	public bool AllowMovement { get; private set; }
	public bool SearchFirst { get; private set; }
	public float NormalAttackValue { get; private set; }

	private AllySkillContext() { }

    internal AllySkillContext ForSkill(Skill skill)
    {
        var subset = (AllySkillContext)MemberwiseClone();
        subset.Castable = new[] { skill };
        return subset;
    }

	// Cast options per skill for this one decision. Created in Build so ForSkill clones share it.
	// Callers must treat the returned list as read-only.
	private Dictionary<Skill, List<SkillCastOption>> optionCache;

	internal List<SkillCastOption> Options(Skill skill)
	{
		if (skill == null || optionCache == null) return SkillCastOptions.Enumerate(Ally, skill);
		if (!optionCache.TryGetValue(skill, out var options))
		{
			options = SkillCastOptions.Enumerate(Ally, skill);
			optionCache[skill] = options;
		}
		return options;
	}

	public bool CanAfford(Skill skill) => AllySkillBudget.CanAfford(Ally.Vitals.Mana, skill, ManaReserve);

	public IEnumerable<Skill> WithIntent(SkillIntent intent) =>
		Castable.Where(s => SkillIntents.Classify(s).Has(intent) && CanAfford(s));

	public static AllySkillContext Build(Ally ally, Game game)
	{
		if (ally == null) throw new ArgumentNullException(nameof(ally));
		if (game == null) throw new ArgumentNullException(nameof(game));

		var context = new AllySkillContext();
		context.Ally = ally;
		context.Game = game;
		context.optionCache = new Dictionary<Skill, List<SkillCastOption>>();

		ally.Awareness.Refresh(game);
		context.AllowMovement = ally.AllyStrategy == AllyStrategy.Aggresive && !ally.IsMovementBlocked;
		context.SearchFirst = ally.Awareness.SearchFirst;

		context.VisibleEnemies = AllyCombat.VisibleHostiles(game, ally);

		context.Party = PartyRules.StandingMembers(game).Where(a => a == ally || game.CurrentDungeon.CanSee(ally, a)).ToList();
		context.Downed = (game.DownedAllies ?? new List<Ally>()).Where(a => a != null && game.CurrentDungeon.CanSee(ally, a)).ToList();

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

		// Reserve learned recovery costs even when Mana, range, targets or statuses prevent casting now.
		context.ManaReserve = AllySkillBudget.ManaReserve(allSkills, ally.AllyStrategy);
		context.NormalAttackValue = AllyCombat.AttacksFrom(game, ally, ally.TilemapPosition, context.VisibleEnemies)
			.Select(a => a.Value).DefaultIfEmpty(0f).Max();

		return context;
	}
}
