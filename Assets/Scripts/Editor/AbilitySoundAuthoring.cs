using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit family assignments keep new active abilities from quietly shipping without audio.</summary>
public static class AbilitySoundAuthoring
{
    const string Pack = "Assets/Sounds/RPG_Essentials_Free/";
    static readonly Dictionary<string, AbilitySoundFamily> families = BuildFamilies();

    static Dictionary<string, AbilitySoundFamily> BuildFamilies()
    {
        var result = new Dictionary<string, AbilitySoundFamily>(StringComparer.Ordinal);
        void Add(AbilitySoundFamily family, string names)
        { foreach (string name in names.Split('|')) result.Add(name, family); }
        Add(AbilitySoundFamily.Fire, "Fire Bolt|Fireball|Inferno|Ember Scroll|Flame Follow-up|Blazing Arms|Flame Barrier");
        Add(AbilitySoundFamily.Frost, "Ice Bolt|Blizzard|Glacier|Frost Follow-up|Frost Arms|Frost Barrier");
        Add(AbilitySoundFamily.Lightning, "Lightning Bolt|Thunderclap|Tempest|Shock Follow-up|Storm Arms|Storm Barrier|Stunning Shot|Paralysis Hex");
        Add(AbilitySoundFamily.Poison, "Venom Hex|Venom Arrow|Plague|Blinding Mist");
        Add(AbilitySoundFamily.Heal, "Heal|Full Heal|Group Heal|Cure|Mass Cure|Renew|Unbind|Soothing Melody|Field Kitchen");
        Add(AbilitySoundFamily.Revive, "Revive|Mass Revive");
        Add(AbilitySoundFamily.Buff, "Rally|Rally Cry|Rousing Chorus|Battle Hymn|War Drums|Command: Advance|Command: Attack|Inspire|Decisive Order|Grand Finale|Amplify|Casting Chorus|Casting Dance");
        Add(AbilitySoundFamily.Protection, "Bulwark|Parry Stance|Sanctuary Wall|Ballad of Stone|Command: Guard|Command: Endure|Command: Hold|Safe Passage");
        Add(AbilitySoundFamily.Debuff, "Curse|Enfeeble|Silence Hex|Terror|Discord|Expose|Provoke|Blinding Flash|Rattle|Sunder");
        Add(AbilitySoundFamily.Sleep, "Slumber Hex|Sleep Arrow");
        Add(AbilitySoundFamily.Absorb, "Siphon|Exploit|Dominate");
        Add(AbilitySoundFamily.Teleport, "Shadow Step|Shadow Dance|Vanish|Smoke Bomb|Retreat|Clone|Grapple Line|Retreat Shot");
        Add(AbilitySoundFamily.Speed, "Evasive Rhythm|Quickstep|Encore");
        Add(AbilitySoundFamily.Ranged, "Aimed Shot|Disarming Shot|Multishot|Piercing Arrow|Pinning Shot|Rain of Arrows|Volley");
        Add(AbilitySoundFamily.Physical, "Cleave|Double Strike|Lunge|Piercing Thrust|Whirlwind|Shield Bash|Shield Smite|Heavy Strike|Assassinate|Hamstring|Throat Strike|Caltrops|Blade Dance");
        Add(AbilitySoundFamily.Utility, "Disarm|Farsight|Floor Sense|Find Exit");
        Add(AbilitySoundFamily.Heal, "Demo: Ally Mend|Demo: Party Mend");
        Add(AbilitySoundFamily.Ranged, "Demo: Arrow Shot");
        Add(AbilitySoundFamily.Physical, "Demo: Single Hit|Demo: Enemy Pulse|Demo: Area Burst");
        Add(AbilitySoundFamily.Buff, "Demo: Self Focus");
        Add(AbilitySoundFamily.Utility, "Demo: Inspect Item|Demo: Inspect Weapon");
        Add(AbilitySoundFamily.Physical, "ShieldBash|Damage");
        Add(AbilitySoundFamily.Heal, "Hot|Healing");
        Add(AbilitySoundFamily.Poison, "Dot");
        Add(AbilitySoundFamily.Buff, "Anger");
        return result;
    }

    public static IEnumerable<Skill> AllSkills() => AssetDatabase.FindAssets("t:Skill")
        .Select(AssetDatabase.GUIDToAssetPath).Distinct()
        .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Skill>().Distinct();

    public static AudioClip ClipFor(AbilitySoundFamily family)
    {
        string relative = family switch
        {
            AbilitySoundFamily.Physical => "10_Battle_SFX/22_Slash_04.wav",
            AbilitySoundFamily.Ranged => "8_Atk_Magic_SFX/25_Wind_01.wav",
            AbilitySoundFamily.Fire => "8_Atk_Magic_SFX/04_Fire_explosion_04_medium.wav",
            AbilitySoundFamily.Frost => "8_Atk_Magic_SFX/13_Ice_explosion_01.wav",
            AbilitySoundFamily.Lightning => "8_Atk_Magic_SFX/18_Thunder_02.wav",
            AbilitySoundFamily.Poison => "8_Atk_Magic_SFX/46_Poison_01.wav",
            AbilitySoundFamily.Heal => "8_Buffs_Heals_SFX/02_Heal_02.wav",
            AbilitySoundFamily.Revive => "8_Buffs_Heals_SFX/30_Revive_03.wav",
            AbilitySoundFamily.Buff => "8_Buffs_Heals_SFX/16_Atk_buff_04.wav",
            AbilitySoundFamily.Protection => "8_Buffs_Heals_SFX/17_Def_buff_01.wav",
            AbilitySoundFamily.Debuff => "8_Buffs_Heals_SFX/21_Debuff_01.wav",
            AbilitySoundFamily.Sleep => "8_Buffs_Heals_SFX/44_Sleep_01.wav",
            AbilitySoundFamily.Absorb => "8_Buffs_Heals_SFX/39_Absorb_04.wav",
            AbilitySoundFamily.Teleport => "12_Player_Movement_SFX/88_Teleport_02.wav",
            AbilitySoundFamily.Speed => "8_Buffs_Heals_SFX/48_Speed_up_02.wav",
            AbilitySoundFamily.Utility => "10_UI_Menu_SFX/051_use_item_01.wav",
            _ => throw new InvalidOperationException("Unassigned ability sound family")
        };
        return AssetDatabase.LoadAssetAtPath<AudioClip>(Pack + relative)
            ?? throw new InvalidOperationException("Missing ability sound: " + relative);
    }

    static AbilitySoundFamily ProfileFamily(CombatEffectProfile profile)
    {
        string name = profile.name;
        if (name.StartsWith("Skill ") && families.TryGetValue(name.Substring(6), out var skillFamily)) return skillFamily;
        string family = name.Split(' ')[0];
        return family switch
        {
            "Fire" => AbilitySoundFamily.Fire, "Frost" => AbilitySoundFamily.Frost,
            "Lightning" => AbilitySoundFamily.Lightning, "Poison" => AbilitySoundFamily.Poison,
            "Life" => AbilitySoundFamily.Heal, "Shadow" or "Mimic" => AbilitySoundFamily.Debuff,
            "Protection" => AbilitySoundFamily.Protection, "Command" or "Song" => AbilitySoundFamily.Buff,
            "Portal" or "Summon" => AbilitySoundFamily.Teleport,
            "Arrow" or "Kunai" => AbilitySoundFamily.Ranged, "Melee" => AbilitySoundFamily.Physical,
            "Utility" => AbilitySoundFamily.Utility,
            _ => throw new InvalidOperationException("Assign a sound family for profile " + name)
        };
    }

    [MenuItem("Tools/Eternal Enigma/Audio/Assign Ability Sounds")]
    public static void Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring sounds.");
        var skills = AllSkills().ToArray();
        var unmapped = skills.Where(s => s.ActivationType == ActivationType.Active && !families.ContainsKey(s.SkillName))
            .Select(s => s.SkillName + " (" + AssetDatabase.GetAssetPath(s) + ")").ToArray();
        if (unmapped.Length > 0) throw new InvalidOperationException("Assign explicit sound families for active abilities:\n" + string.Join("\n", unmapped));
        foreach (var skill in skills.Where(s => s.ActivationType == ActivationType.Active))
        {
            var family = families[skill.SkillName];
            skill.SoundFamily = family;
            skill.CastSound = ClipFor(family);
            EditorUtility.SetDirty(skill);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:CombatEffectProfile", new[] { CombatEffectAuthoring.Root }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
            // Ordinary melee attacks already own their Slash cue; profiles must not double it.
            profile.CastSound = profile.name.StartsWith("Melee") ? null : ClipFor(ProfileFamily(profile));
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets();
        Validate();
        WriteReport(skills);
        Debug.Log($"Ability audio assigned: {skills.Count(s => s.ActivationType == ActivationType.Active)} active skill assets, including embedded demo skills.");
    }

    [MenuItem("Tools/Eternal Enigma/Audio/Validate Ability Sounds")]
    public static void Validate()
    {
        var errors = new List<string>();
        foreach (var skill in AllSkills().Where(s => s.ActivationType == ActivationType.Active))
            if (skill.SoundFamily == AbilitySoundFamily.Unassigned || skill.CastSound == null)
                errors.Add(AssetDatabase.GetAssetPath(skill) + ": " + skill.SkillName + " needs an explicit sound family and release clip.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
    }

    static void WriteReport(IEnumerable<Skill> skills)
    {
        var report = new StringBuilder("# Ability audio coverage\n\nGenerated by `AbilitySoundAuthoring.Author()`. Every active ability plays the universal start cue once after validation and its assigned family clip once on successful release, through Effects (Sound). Charging and interrupted casts never play a release early. Animations may be disabled without muting relevant audio; unseen enemy actions remain silent.\n\nThe existing RPG Essentials palette is retained. Distinct fire, frost, lightning, poison, support, physical, and utility families provide the retro cartoon character. Passive stat bonuses remain silent; observable passive effects use their resulting action cues.\n\n| Ability | Family | Release clip | Asset |\n|---|---|---|---|\n");
        foreach (var skill in skills.Where(s => s.ActivationType == ActivationType.Active).OrderBy(s => s.SkillName, StringComparer.Ordinal).ThenBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal))
            report.AppendLine($"| {skill.SkillName} | {skill.SoundFamily} | {skill.CastSound.name} | {AssetDatabase.GetAssetPath(skill)} |");
        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/AudioAbilityCoverage.md", report.ToString());
    }
}
