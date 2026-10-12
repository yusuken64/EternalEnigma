using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ScoutNavigationAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Classes/Author Find Exit")]
    public static void Author()
    {
        const string path = "Assets/Resources/Classes/Skills/Scout/Find Exit.asset";
        var skill = AssetDatabase.LoadAssetAtPath<Skill>(path);
        if (skill == null) { skill = ScriptableObject.CreateInstance<Skill>(); AssetDatabase.CreateAsset(skill, path); }
        var source = AssetDatabase.LoadAssetAtPath<Skill>("Assets/Resources/Classes/Skills/Scout/Floor Sense.asset");
        skill.SkillName = "Find Exit"; skill.ActivationType = ActivationType.Active; skill.SPCost = 2;
        skill.LearnCost = 50; skill.MaxRank = 1; skill.Targeting = SkillTargeting.Self;
        skill.TargetSelector = source.TargetSelector; skill.Icon = source.Icon; skill.VisualProfile = source.VisualProfile;
        skill.CastSound = source.CastSound;
        skill.ActionEffects = new() { new FindExitAction() };
        skill.Description = "Guide this Scout to the exit, even when undiscovered. Walks one step per turn; stops at enemies, blocked paths or manual input. Does not leave the floor automatically.";
        var definition = ClassCatalog.Load().Get("scout");
        definition.Skills.RemoveAll(e => e.Skill == skill);
        int index = definition.Skills.FindIndex(e => e.Skill == source) + 1;
        definition.Skills.Insert(index, new ClassSkillEntryData { Skill = skill, Tier = 1, MaxRank = 1 });
        EditorUtility.SetDirty(skill); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
    }
}
