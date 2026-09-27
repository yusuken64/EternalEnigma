using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public sealed class DungeonRun
{
    private readonly OverworldBiome biome;
    private readonly int layoutVersion;
    public int CampaignSeed { get; }
    public CampaignLocation Location { get; }
    public int Floor { get; private set; }
    public (int Start, int End) Floors { get; }
    public DungeonFloor Current { get; private set; }
    public GridPoint Position { get; internal set; }
    public string Message { get; private set; } = "";
    public HashSet<GridPoint> Visited { get; } = new();

    public DungeonRun(int campaignSeed, CampaignLocation location, OverworldBiome biome = OverworldBiome.Grassland, int layoutVersion = 0)
    {
        this.biome=biome; this.layoutVersion=layoutVersion;
        CampaignSeed = campaignSeed;
        Location = location;
        Floors = CampaignContext.Floors(location.Tier);
        Floor = Floors.Start;
        Current = DungeonFloorGenerator.Generate(CampaignContext.DungeonFloorOptionsFor(
            campaignSeed, location.Id, Floor, location.Tier, biome, layoutVersion));
        Position = Current.Start;
        Visited.Add(Position);
    }

    public bool Move(int dx, int dy)
    {
        var target = new GridPoint(Position.X + dx, Position.Y + dy);

        if (!GridSteps.CanStep(Position, target, p=>Current.IsWalkable(p) && !Current.Scenery.Any(s=>s.Cell.Equals(p) && s.Kind!=DungeonSceneryKind.Hazard),DiagonalRule.RequireOpenSides))
        {
            Message = "Blocked.";
            return false;
        }

        Position = target;
        Visited.Add(Position);

        if (OnStairs)
        {
            Message = IsLastFloor ? "Stairs | Enter: finish dungeon" : "Stairs | Enter: descend";
        }
        else
        {
            Message = "";
        }

        return true;
    }

    public bool OnStairs => Position.Equals(Current.Stairs);

    public bool IsLastFloor => Floor == Floors.End;

    public void Descend()
    {
        if (IsLastFloor)
            throw new InvalidOperationException("Cannot descend from the last floor.");

        Floor++;
        Current = DungeonFloorGenerator.Generate(CampaignContext.DungeonFloorOptionsFor(
            CampaignSeed, Location.Id, Floor, Location.Tier, biome, layoutVersion));
        Position = Current.Start;
        Visited.Clear();
        Visited.Add(Position);
    }
}
