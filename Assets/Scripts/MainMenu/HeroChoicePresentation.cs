using System.Collections.Generic;
using System.Linq;

/// <summary>Concrete examples for the hero picker, drawn from each hero's authored class kit.</summary>
public static class HeroChoicePresentation
{
    public static IReadOnlyList<TownAlly> Recommended(IReadOnlyList<TownAlly> heroes)
    {
        var result = new List<TownAlly>();
        foreach (string id in new[] { "warrior", "guardian", "elementalist" })
        {
            var hero = heroes.FirstOrDefault(h => h.PrimaryClass.Id == id);
            if (hero != null) result.Add(hero);
        }
        foreach (var hero in heroes)
            if (result.Count < 3 && !result.Contains(hero)) result.Add(hero);
        return result;
    }

    public static Skill Signature(TownAlly hero) => hero?.PrimaryClass?.Skills?
        .Where(entry => entry?.Skill != null && entry.Skill.ActivationType == ActivationType.Active)
        .OrderBy(entry => entry.Tier)
        .ThenBy(entry => hero.PrimaryClass.Id == "scout" && entry.Skill.SkillName == "Floor Sense" ? 0 : 1)
        .Select(entry => entry.Skill).FirstOrDefault();

    public static string Playstyle(ClassDefinition definition) => definition?.Id switch
    {
        "warrior" => "Stay close and finish enemies with powerful weapon attacks.",
        "guardian" => "Hold the front line and shield yourself and your companions.",
        "elementalist" => "Keep your distance and spend Mana on elemental spells.",
        "archer" => "Line up distant enemies and spend arrows on precise shots.",
        "healer" => "Restore health and keep the party fighting through long journeys.",
        "rogue" => "Reposition quickly and disrupt enemies before they can answer.",
        "scout" => "Find safer routes, reveal dangers and make supplies last.",
        "occultist" => "Weaken enemies with ailments, then exploit their vulnerability.",
        "bard" => "Strengthen companions with songs while fighting alongside them.",
        "commander" => "Direct and strengthen the party with battlefield commands.",
        _ => definition?.Role ?? "Explore the dungeon with your companions."
    };

    public static string Preview(TownAlly hero)
    {
        string classes = hero.PrimaryClass.DisplayName +
            (hero.SecondaryClass != null ? " / " + hero.SecondaryClass.DisplayName : "");
        var skill = Signature(hero);
        string example = skill == null ? "" : $"\n\n<b>First lesson: {skill.DisplayName}</b>\n{skill.Description}";
        return $"<b><size=120%>{hero.Name}</size></b>\n<size=85%>{classes}</size>\n{Playstyle(hero.PrimaryClass)}{example}";
    }
}
