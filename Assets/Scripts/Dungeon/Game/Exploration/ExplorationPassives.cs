using System.Linq;

public static class ExplorationPassives
{
	// 0 when the character is missing, dead, or lacks the skill; otherwise the learned rank (at least 1).
	public static int Rank(Character character, string skillName)
	{
		if (character == null || character.Vitals == null || character.Vitals.HP <= 0 || character.Skills == null) return 0;
		var skill = character.Skills.FirstOrDefault(s => s != null && s.SkillName == skillName);
		return skill == null ? 0 : System.Math.Max(1, skill.Rank);
	}

	// Best rank among living party members.
	public static int PartyBestRank(string skillName)
	{
		var game = Game.Instance;
		if (game == null || game.Allies == null) return 0;
		return game.Allies.Where(a => a != null).Select(a => Rank(a, skillName)).DefaultIfEmpty(0).Max();
	}
}
