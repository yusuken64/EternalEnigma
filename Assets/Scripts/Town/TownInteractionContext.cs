public sealed class TownInteractionContext
{
    public Town Town { get; }
    public TownPlayer Player => Town.TownPlayer;
    public TownServices Services => Town.Services;
    public TownBuildingDefinition Building { get; }

    public TownInteractionContext(Town town, TownBuildingDefinition building)
    {
        Town = town;
        Building = building;
    }
}
