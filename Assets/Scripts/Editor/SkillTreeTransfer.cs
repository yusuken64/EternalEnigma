using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Classes;
using UnityEditor;
using UnityEngine;

// Moves class skill trees between ClassDefinition assets and .skilltree text files
// (the format the Campaign Explorer edits). Tiers, ranks and kinds travel; skills are matched by SkillName.
public static class SkillTreeTransfer
{
    const string DefaultFolder = "Docs/SkillTrees";

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    [MenuItem("Tools/Eternal Enigma/Skill Trees/Export All To Docs")]
    public static void ExportAll()
    {
        string folder = Path.Combine(ProjectRoot, DefaultFolder);
        Directory.CreateDirectory(folder);
        int count = 0;
        foreach (var definition in AllClasses())
        {
            var document = SkillTreeDocument.FromTable(definition.ToSkillTable(), definition.DisplayName);
            File.WriteAllText(Path.Combine(folder, definition.Id + SkillTreeFormat.Extension), SkillTreeFormat.Write(document));
            count++;
        }

        Debug.Log($"Exported {count} skill trees to {folder}.");
    }

    [MenuItem("Tools/Eternal Enigma/Skill Trees/Import From Folder...")]
    public static void ImportFromFolder()
    {
        string start = Path.Combine(ProjectRoot, DefaultFolder);
        string folder = EditorUtility.OpenFolderPanel("Import skill trees", Directory.Exists(start) ? start : ProjectRoot, "");
        if (string.IsNullOrEmpty(folder)) return;

        var plan = Plan(folder, out var problems);
        string summary = plan.Count == 0 ? "Nothing would change." : string.Join("\n", plan.Select(p => p.Summary));
        if (problems.Count > 0) summary += "\n\nSkipped:\n" + string.Join("\n", problems);
        if (plan.Count == 0)
        {
            EditorUtility.DisplayDialog("Import skill trees", summary, "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Import skill trees", summary + "\n\nApply these changes to the class assets?", "Apply", "Cancel")) return;

        foreach (var change in plan)
        {
            Undo.RecordObject(change.Definition, "Import skill tree");
            change.Definition.Skills = change.Entries;
            EditorUtility.SetDirty(change.Definition);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Imported {plan.Count} skill trees from {folder}.");
    }

    sealed class Change
    {
        public ClassDefinition Definition;
        public List<ClassSkillEntryData> Entries;
        public string Summary;
    }

    static List<Change> Plan(string folder, out List<string> problems)
    {
        problems = new List<string>();
        var plan = new List<Change>();
        var classes = AllClasses().ToDictionary(c => c.Id);
        var skillsByName = AllSkills();

        foreach (string file in Directory.GetFiles(folder, "*" + SkillTreeFormat.Extension).OrderBy(f => f))
        {
            string name = Path.GetFileName(file);
            SkillTreeDocument document;
            try { document = SkillTreeFormat.Parse(File.ReadAllText(file)); }
            catch (SkillTreeFormatException ex) { problems.Add($"{name}: {ex.Message}"); continue; }

            if (!classes.TryGetValue(document.ClassId, out var definition)) { problems.Add($"{name}: no class '{document.ClassId}'."); continue; }

            var errors = document.Validate();
            if (errors.Count > 0) { problems.Add($"{name}: {string.Join(" ", errors)}"); continue; }

            var current = definition.Skills.Where(e => e?.Skill != null).ToDictionary(e => e.Skill.SkillName, e => e);
            var entries = new List<ClassSkillEntryData>();
            var missing = new List<string>();
            foreach (var entry in Enumerable.Range(1, SkillTreeDocument.TierCount).SelectMany(document.InTier))
            {
                var skill = current.TryGetValue(entry.SkillId, out var existing) ? existing.Skill
                    : skillsByName.TryGetValue(entry.SkillId, out var found) ? found : null;
                if (skill == null) { missing.Add(entry.SkillId); continue; }
                entries.Add(new ClassSkillEntryData { Skill = skill, Tier = entry.Tier, MaxRank = entry.MaxRank, Kind = entry.Kind });
            }

            if (missing.Count > 0) { problems.Add($"{name}: no Skill asset named {string.Join(", ", missing)}."); continue; }

            var before = definition.ToSkillTable().Skills.Select(s => s.ToString()).ToList();
            var after = entries.Select(e => new ClassSkillEntry(e.Skill.SkillName, e.Tier, e.MaxRank, e.Kind).ToString()).ToList();
            int added = after.Except(before).Count(), removed = before.Except(after).Count();
            if (added == 0 && removed == 0) continue;
            plan.Add(new Change { Definition = definition, Entries = entries, Summary = $"{definition.Id}: {added} added or changed, {removed} removed or changed" });
        }

        return plan;
    }

    static IEnumerable<ClassDefinition> AllClasses() =>
        AssetDatabase.FindAssets("t:ClassDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<ClassDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && !string.IsNullOrEmpty(c.Id))
            .OrderBy(c => c.Id);

    static Dictionary<string, Skill> AllSkills()
    {
        var map = new Dictionary<string, Skill>();
        foreach (string guid in AssetDatabase.FindAssets("t:Skill"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill != null && !map.ContainsKey(skill.SkillName)) map[skill.SkillName] = skill;
        }

        return map;
    }
}
