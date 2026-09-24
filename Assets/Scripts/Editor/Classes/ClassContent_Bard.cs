using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Bard kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Bard
{
	public const string Id = "bard";
	public const string Folder = "Bard";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Bard Novice Training", 1, MasteryCost(1), "Unlocks tier 1. Songs +1 turn.", null, new SongDurationBonus { Turns = 1 });
		k.Shared(1, SkillKind.Gathering, "Foraging", 1);
		k.Active(1, "Battle Hymn", 3, Cost(1), SpLow, "Song: +3 Strength to allies within 3 tiles.", SkillTargetSpec.OnSelf(), new StartSongAction { SongId = "battle-hymn", SongName = "Battle Hymn", Modification = new StatModification { Strength = 3 }, Radius = 3, Turns = 5 });
		k.Active(1, "Ballad of Stone", 3, Cost(1), SpLow, "Song: +3 Defense to allies within 3 tiles.", SkillTargetSpec.OnSelf(), new StartSongAction { SongId = "ballad-of-stone", SongName = "Ballad of Stone", Modification = new StatModification { Defense = 3 }, Radius = 3, Turns = 5 });
		k.Active(1, "Soothing Melody", 3, Cost(1), SpLow, "Song: allies within 3 tiles regain 2 HP each turn.", SkillTargetSpec.OnSelf(), new StartSongAction { SongId = "soothing-melody", SongName = "Soothing Melody", Modification = new StatModification(), HealPerTurn = 2, Radius = 3, Turns = 5 });
		k.Passive(1, SkillKind.Normal, "Nimble", 5, Cost(1), "+10% evasion.", new StatModification { Evasion = 0.1f });
		k.Passive(2, SkillKind.Mastery, "Bard Adept Training", 1, MasteryCost(2), "Unlocks tier 2. Songs +1 turn.", null, new SongDurationBonus { Turns = 1 });
		k.Active(2, "Evasive Rhythm", 3, Cost(2), SpMed, "Song: enemy hit chance against allies -20%.", SkillTargetSpec.OnSelf(), new StartSongAction { SongId = "evasive-rhythm", SongName = "Evasive Rhythm", Modification = new StatModification { Evasion = 0.2f }, Radius = 3, Turns = 5 });
		k.Active(2, "Quickstep", 1, Cost(2), SpHigh, "Song, 3 turns: +1 action per turn.", SkillTargetSpec.OnSelf(), new StartSongAction { SongId = "quickstep", SongName = "Quickstep", Modification = new StatModification { ActionsPerTurnMax = 1 }, Radius = 3, Turns = 3 });
		k.Active(2, "Encore", 3, Cost(2), SpLow, "Extends all active songs by 3 turns.", SkillTargetSpec.OnSelf(), new ExtendSongsAction { Turns = 3 });
		k.Active(2, "Blade Dance", 5, Cost(2), SpMed, "One hit per active song, at 70% each.", SkillTargetSpec.Melee(), new SongCountStrikeAction { PercentPerHit = 0.7f }).Weapon();
		k.Active(2, "Discord", 1, Cost(2), SpMed, "Removes buffs from enemies within 2 tiles.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 2), new CleanseAction { Buffs = true });
		k.Active(2, "War Drums", 5, Cost(2), SpMed, "Enemies within 3 tiles -3 Defense.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 3), Apply("DefenseDownStatus", 1f, ailment: true));
		k.Passive(3, SkillKind.Mastery, "Bard Master Training", 1, MasteryCost(3), "Unlocks tier 3. Songs +1 turn.", null, new SongDurationBonus { Turns = 1 });
		k.Active(3, "Grand Finale", 3, Cost(3), SpHigh, "Ends all songs. Heals the party and damages enemies per song ended.", SkillTargetSpec.OnSelf(), new GrandFinaleAction { HealPerSong = 10, DamagePerSong = 8 });
		k.Passive(3, SkillKind.SingleRank, "Crescendo", 1, Cost(3), "Song effects +50%.", null, new SongPowerBonus { Percent = 50f });
		k.Passive(3, SkillKind.SingleRank, "Harmony", 1, Cost(3), "Can keep 3 songs at once.", null, new SongSlotBonus { ExtraSlots = 1 });
		k.Active(3, "Rousing Chorus", 5, Cost(3), SpHigh, "Restores 2 SP to allies within 3 tiles (not the Bard).", SkillTargetSpec.AroundSelf(TargetTeam.Allies, 3), new RestoreSPAction { Amount = 2, ExcludeCaster = true });
		k.Passive(3, SkillKind.SingleRank, "Stage Presence", 1, Cost(3), "+2 Strength, +2 Defense while a song is active.", null, new PerformingBonus { Bonus = new StatModification { Strength = 2, Defense = 2 } });
		k.Shared(3, SkillKind.Normal, "Vigor", 5);
		k.Save(new StatModification { HPMax = 2, SPMax = 2 });
	}
}
