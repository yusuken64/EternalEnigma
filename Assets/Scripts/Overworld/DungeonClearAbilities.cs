using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Permanent party passives, unlocked by the saved first-clear record.</summary>
public static class DungeonClearAbilities
{
    private static readonly Dictionary<string, Skill> definitions = new();

    public static Skill ForDungeon(string id)
    {
        if (definitions.TryGetValue(id, out var existing) && existing != null) return existing;
        var (name, bonus) = id switch
        {
            "story-0" => ("Gatekeeper's Resolve", new StatModification { HPMax = 6 }),
            "story-1" => ("Sentinel's Guard", new StatModification { Defense = 2 }),
            "story-2" => ("Ember Ward", new StatModification { FireResistance = 10 }),
            "story-3" => ("Storm Ward", new StatModification { LightningResistance = 10 }),
            "repeatable-0" => ("Trail Rations", new StatModification { HungerMax = 10 }),
            "repeatable-1" => ("Focused Breath", new StatModification { SPMax = 4 }),
            "repeatable-2" => ("Steady Hand", new StatModification { HitBonus = .03f }),
            "repeatable-3" => ("Winter Ward", new StatModification { IceResistance = 10 }),
            "repeatable-4" => ("Keen Edge", new StatModification { CritChance = .03f }),
            "repeatable-5" => ("Lightfoot", new StatModification { Evasion = .03f }),
            "repeatable-6" => ("Deep Reserves", new StatModification { HPMax = 3, SPMax = 2 }),
            "final-dungeon" => ("Enigma's Legacy", new StatModification { Strength = 3, Defense = 1 }),
            _ => ($"Dungeon Mastery: {id}", new StatModification { HPMax = 2, SPMax = 1 })
        };
        var skill = ScriptableObject.CreateInstance<Skill>();
        skill.name = skill.SkillName = name;
        skill.hideFlags = HideFlags.HideAndDontSave;
        skill.ActivationType = ActivationType.Passive;
        skill.MaxRank = 1;
        skill.PassiveStatModification = bonus;
        skill.Description = "Earned on this dungeon's first clear. Permanently benefits every party member.\n" +
            string.Join("\n", bonus.DescribeEffect());
        definitions[id] = skill;
        return skill;
    }

    public static IEnumerable<Skill> Unlocked => Common.Instance?.CampaignContext?.Completed
        .OrderBy(id => id).Select(ForDungeon) ?? Enumerable.Empty<Skill>();

    public static Skill Find(string name) => Unlocked.FirstOrDefault(skill => skill.SkillName == name);

    public static void Apply(TownAlly ally)
    {
        foreach (var skill in Unlocked)
            if (ally.GetRank(skill.SkillName) == 0) ally.SetRank(skill.SkillName, 1);
    }
}
