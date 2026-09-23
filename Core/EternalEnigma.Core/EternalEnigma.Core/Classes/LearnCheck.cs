namespace EternalEnigma.Core.Classes;

public sealed class LearnCheck
{
    internal LearnCheck(LearnRefusal refusal, string reason, int nextRank, int cost, int requiredLevel)
    {
        Refusal = refusal;
        Reason = reason ?? "";
        NextRank = nextRank;
        Cost = cost;
        RequiredLevel = requiredLevel;
    }

    public bool Allowed => Refusal == LearnRefusal.None;
    public LearnRefusal Refusal { get; }
    public string Reason { get; }
    public int NextRank { get; }
    public int Cost { get; }
    public int RequiredLevel { get; }
}
