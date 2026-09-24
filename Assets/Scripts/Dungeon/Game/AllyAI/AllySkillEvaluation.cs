using System;

// One priority of the ally skill AI. Returns null when it has nothing worth casting.
public interface IAllySkillEvaluator
{
	string Name { get; }
	AllySkillChoice Evaluate(AllySkillContext context);
}

public sealed class AllySkillChoice
{
	public AllySkillChoice(string evaluator, SkillCastOption option, float score, string reason)
	{
		Evaluator = evaluator ?? "";
		Option = option ?? throw new ArgumentNullException(nameof(option));
		Score = score;
		Reason = reason ?? "";
	}

	public string Evaluator { get; }
	public SkillCastOption Option { get; }
	public float Score { get; }
	public string Reason { get; }

	public GameAction ToAction(Character caster) => Option.ToAction(caster);

	public override string ToString() => $"{Evaluator}: {Option.Skill?.SkillName} ({Reason}, {Score:0.##})";
}
