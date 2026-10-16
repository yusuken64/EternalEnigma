using System;
using System.Linq;

[Serializable]
public sealed class CampaignCompletionRecord
{
    [Serializable]
    public sealed class Hero
    {
        public string Id, Name, PrimaryClass, SecondaryClass, Weapon;
        public int Level, MaxHP, MaxMana;
    }
    public Hero[] Party = Array.Empty<Hero>();
    public int Seed, DungeonsCleared, TownsVisited, CompanionsRecruited, KeysFound, Gold;
    public double PlaytimeSeconds;
    public string CompletedUtc;

    internal static CampaignCompletionRecord Capture(Common common, PlayerController player)
    {
        var context = common.CampaignContext;
        return new CampaignCompletionRecord
        {
            Seed = context.State.Seed, DungeonsCleared = context.Completed.Count,
            TownsVisited = context.Towns.Count, CompanionsRecruited = context.Roster.Count,
            KeysFound = context.Keys.Count, Gold = player.Gold,
            PlaytimeSeconds = common.GameSaveData.PlaytimeSeconds, CompletedUtc = DateTime.UtcNow.ToString("O"),
            Party = PartyRules.PartyMembers(Game.Instance).Where(a => !a.IsSummon).Select(a => new Hero
            {
                Id = a.TownAllyId, Name = GameMessages.Name(a), Level = a.Vitals.Level,
                PrimaryClass = a.PrimaryClass?.DisplayName ?? "Adventurer", SecondaryClass = a.SecondaryClass?.DisplayName ?? "",
                MaxHP = a.FinalStats.HPMax, MaxMana = a.FinalStats.ManaMax,
                // Unity can deserialize an empty slot as an item with no definition.
                Weapon = a.Equipment.Current().Weapon?.ItemName ?? "Unarmed"
            }).ToArray()
        };
    }

    internal string Statistics => $"{DungeonsCleared} dungeons cleared   ·   {TownsVisited} towns visited   ·   {CompanionsRecruited} companions recruited\n" +
        $"{KeysFound} keys found   ·   {Gold:N0} treasure   ·   {TimeSpan.FromSeconds(Math.Max(0, PlaytimeSeconds)).TotalHours:0.0} hours\n" +
        $"The reverse world conquered   ·   Journey seed {Seed}";
}
