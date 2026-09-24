using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SongRules
{
	public const int BaseSlots = 2;

	public static int MaxSlots(Character performer) =>
		BaseSlots + PassiveModifiers.Sum<SongSlotBonus>(performer, b => b.ExtraSlots);

	public static List<SongStatusEffect> ActiveSongs(Character performer) =>
		performer == null ? new List<SongStatusEffect>() :
		performer.StatusEffects.OfType<SongStatusEffect>().Where(s => s != null && !s.IsExpired())
			.OrderBy(s => s.StartedOrder).ToList();

	// Same SongId refreshes; otherwise the oldest songs are evicted until a slot is free.
	public static SongStatusEffect AddOrRefresh(Character performer, string songId, string songName,
		StatModification modification, int healPerTurn, int radius, int turns)
	{
		var active = ActiveSongs(performer);
		var existing = active.FirstOrDefault(s => s.SongId == songId);
		if (existing != null)
		{
			existing.TurnsLeft = Math.Max(existing.TurnsLeft, Math.Max(1, turns));
			existing.Modification = modification ?? new StatModification();
			existing.HealPerTurn = healPerTurn;
			existing.Radius = radius;
			RefreshPartyStats(Game.Instance);
			return existing;
		}
		int max = Math.Max(1, MaxSlots(performer));
		while (active.Count >= max)
		{
			RemoveSong(performer, active[0]);
			active.RemoveAt(0);
		}
		var go = new GameObject($"Song {songName}");
		go.transform.SetParent(performer.VisualParent.transform, false);
		var song = go.AddComponent<SongStatusEffect>();
		song.SongId = songId;
		song.SongName = songName;
		song.Modification = modification ?? new StatModification();
		song.HealPerTurn = healPerTurn;
		song.Radius = radius;
		song.TurnsLeft = Math.Max(1, turns);
		song.StartedOrder = SongStatusEffect.NextOrder();
		performer.StatusEffects.Add(song);
		RefreshPartyStats(Game.Instance);
		return song;
	}

	public static void RemoveSong(Character performer, SongStatusEffect song)
	{
		if (performer == null || song == null) return;
		performer.RemoveStatusEffect(song);
		UnityEngine.Object.Destroy(song.gameObject);
		RefreshPartyStats(Game.Instance);
	}

	public static void RefreshPartyStats(Game game)
	{
		if (game == null || game.Allies == null) return;
		foreach (var ally in game.Allies)
		{
			if (ally == null) continue;
			ally.InvalidateCachedStats();
			ally.DisplayedStats.Sync(ally.FinalStats);
		}
	}
}

public static class SongAura
{
	// Per-stat strongest bonus of every song whose performer is within that song's radius of the recipient,
	// plus the recipient's own PerformingBonus passives while it is performing. Allies only.
	public static StatModification ModificationFor(Character recipient)
	{
		if (recipient is not Ally) return new StatModification();
		var game = Game.Instance;
		if (game == null || game.Allies == null) return new StatModification();
		var songMods = new List<StatModification>();
		var result = new StatModification();
		foreach (var performer in game.Allies)
		{
			if (performer == null || performer.Vitals == null || performer.Vitals.HP <= 0) continue;
			var songs = SongRules.ActiveSongs(performer);
			if (songs.Count == 0) continue;
			int distance = TileWorldDungeon.ChevDistance(performer.TilemapPosition, recipient.TilemapPosition);
			foreach (var song in songs)
				if (distance <= song.Radius) songMods.Add(song.Modification);
			if (performer == recipient)
				foreach (var bonus in PassiveModifiers.Of<PerformingBonus>(performer))
					result = result + bonus.Bonus;
		}
		return BuffStacking.Strongest(songMods) + result;
	}
}
