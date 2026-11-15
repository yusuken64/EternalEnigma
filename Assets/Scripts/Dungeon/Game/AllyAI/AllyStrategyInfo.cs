internal static class AllyStrategyInfo
{
    internal static readonly AllyStrategy[] Choices = { AllyStrategy.Follow, AllyStrategy.Aggresive, AllyStrategy.HoldPosition };

    internal static string Name(AllyStrategy strategy) => strategy switch
    {
        AllyStrategy.Follow => "Follow",
        AllyStrategy.Aggresive => "Aggressive",
        AllyStrategy.HoldPosition => "Hold Position",
        _ => strategy.ToString()
    };

    internal static string Description(AllyStrategy strategy) => strategy switch
    {
        AllyStrategy.Follow => "Fight from the current tile while the leader is visible. Otherwise follow the leader, or search their last seen tile if separated.",
        AllyStrategy.Aggresive => "Pursue visible enemies and use skills more freely. Otherwise follow the leader, or search their last seen tile if separated.",
        AllyStrategy.HoldPosition => "Fight and support allies without moving. Direct orders and forced movement still apply.",
        _ => ""
    };
}
