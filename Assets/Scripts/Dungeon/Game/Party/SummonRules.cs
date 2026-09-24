using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SummonRules
{
	public static int CloneLimit(Character summoner)
	{
		return 1 + PassiveModifiers.Sum<SummonLimitBonus>(summoner, b => b.ExtraClones);
	}

	public static IReadOnlyList<Ally> ClonesOf(Game game, Character summoner)
	{
		if (game == null) return new List<Ally>();
		return game.Allies
			.Where(a => a != null && a.GetComponent<SummonedUnit>() is { Kind: SummonKind.Clone, Summoner: var s } && s == summoner)
			.OrderBy(a => a.GetComponent<SummonedUnit>().Order)
			.Cast<Ally>()
			.ToList();
	}

	public static Ally SpawnClone(Game game, Ally summoner, float statPercent, int turns)
	{
		if (game == null) return null;

		var dungeon = game.CurrentDungeon;
		var start = summoner.TilemapPosition;

		var cell = dungeon.GetPositionWith(start, node =>
		{
			var p = new Vector3Int(node.X, node.Y, 0);
			return p != start && SkillMovement.CanOccupy(summoner, p);
		});

		if (cell == start || TileWorldDungeon.ChevDistance(cell, start) > 2)
			return null;

		var clone = UnityEngine.Object.Instantiate(summoner, summoner.transform.parent);

		// Reset copied state
		foreach (var statusEffect in clone.StatusEffects)
		{
			if (statusEffect?.gameObject != null)
				UnityEngine.Object.Destroy(statusEffect.gameObject);
		}
		clone.StatusEffects.Clear();
		clone.Skills = new List<Skill>();
		clone.TownAllyId = "";
		clone.CharacterName = summoner.CharacterName + " (Clone)";
		clone.IsDowned = false;
		clone.IsWaitingForPlayerInput = false;
		clone.AllyStrategy = AllyStrategy.Aggresive;
		clone.SetToCPU();

		// Add SummonedUnit component
		var unit = clone.gameObject.AddComponent<SummonedUnit>();
		unit.Kind = SummonKind.Clone;
		unit.Summoner = summoner;
		unit.TurnsLeft = Math.Max(1, turns);
		unit.OriginalTeam = summoner.Team;
		unit.Order = SummonedUnit.NextOrder();

		// Scale stats
		clone.BaseStats.Sync(summoner.BaseStats);
		clone.BaseStats.HPMax = Math.Max(1, (int)Math.Round(summoner.BaseStats.HPMax * statPercent));
		clone.BaseStats.Strength = Math.Max(0, (int)Math.Round(summoner.BaseStats.Strength * statPercent));
		clone.BaseStats.Defense = Math.Max(0, (int)Math.Round(summoner.BaseStats.Defense * statPercent));
		clone.InvalidateCachedStats();

		// Set vitals
		clone.Vitals = new Vitals();
		clone.DisplayedVitals = new Vitals();
		clone.Vitals.HP = clone.FinalStats.HPMax;
		clone.Vitals.SP = 0;
		clone.Vitals.Hunger = clone.FinalStats.HungerMax;
		clone.Vitals.Level = summoner.Vitals.Level;

		clone.SetPosition(cell);
		clone.SyncDisplayedStats();
		game.Allies.Add(clone);
		return clone;
	}

	public static void Despawn(Game game, SummonedUnit unit)
	{
		if (unit == null) return;

		if (unit.Kind == SummonKind.Clone)
		{
			var ally = unit.GetComponent<Ally>();
			if (ally != null)
				game.Allies.Remove(ally);
			UnityEngine.Object.Destroy(unit.gameObject);
		}
		else if (unit.Kind == SummonKind.Dominated)
		{
			var character = unit.GetComponent<Character>();
			if (character != null)
			{
				character.Team = unit.OriginalTeam;
				character.PursuitTarget = null;
				character.PursuitPosition = null;
			}
			UnityEngine.Object.Destroy(unit);
		}
	}

	public static void TickSummons(Game game)
	{
		if (game == null) return;

		var allSummons = game.Allies
			.Cast<Character>()
			.Concat(game.Enemies)
			.Where(c => c != null)
			.Select(c => c.GetComponent<SummonedUnit>())
			.Where(u => u != null)
			.ToList();

		foreach (var unit in allSummons)
		{
			unit.TurnsLeft--;
			if (unit.TurnsLeft <= 0)
				Despawn(game, unit);
		}
	}

	public static void DespawnClones(Game game)
	{
		if (game == null) return;

		foreach (var ally in game.Allies.ToList())
		{
			var unit = ally.GetComponent<SummonedUnit>();
			if (unit != null && unit.Kind == SummonKind.Clone)
				Despawn(game, unit);
		}
	}
}
