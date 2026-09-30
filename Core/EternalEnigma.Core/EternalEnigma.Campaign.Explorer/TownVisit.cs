using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public sealed class TownVisit
{
    public string TownId { get; }
    public TownPlan Plan { get; }
    public GridPoint Position { get; internal set; }
    public string Message { get; private set; } = "";

    /// <summary>Service for each building slot (null = entrance or statue); null for a town without services.</summary>
    public IReadOnlyList<TownService?>? SlotServices { get; }

    public TownVisit(string townId, TownPlan plan, IReadOnlyList<TownService?>? slotServices = null)
    {
        TownId = townId;
        Plan = plan;
        SlotServices = slotServices;
        Position = plan.PartySpawn;
    }

    public static TownVisit Create(int campaignSeed, CampaignLocation town) =>
        new(town.Id, TownPlanGenerator.Generate(TownLayout.OptionsFor(campaignSeed, town)),
            TownLayout.ForLocation(campaignSeed, town)?.SlotServices);

    /// <summary>The service whose door is at this cell, or null.</summary>
    public TownService? ServiceAt(GridPoint door) =>
        SlotServices != null && Plan.BuildingIndexAt(door) is int slot && slot < SlotServices.Count ? SlotServices[slot] : null;

    /// <summary>Corner-cutting movement over Plan.Walkable. Sets Message to "Blocked." on failure, or a context hint on success.</summary>
    public bool Move(int dx, int dy)
    {
        var newPos = new GridPoint(Position.X + dx, Position.Y + dy);

        if (!Plan.CanStep(Position, newPos))
        {
            Message = "Blocked.";
            return false;
        }

        Position = newPos;

        // Determine message based on new position
        if (OnExit)
            Message = "Exit | Enter: leave town";
        else if (OnDungeonEntrance)
            Message = "Dungeon entrance | Enter: descend";
        else if (BuildingIndexHere is int i)
            Message = $"Building {i}";
        else if (Plan.Layers[TownLayers.ShopFloor].At(Position))
            Message = "Shop interior";
        else
            Message = "";

        return true;
    }

    public bool OnExit => Position.Equals(Plan.Exit);

    public bool OnDungeonEntrance =>
        Position.Equals(Plan.DungeonEntrance) ||
        (Plan.BuildingSlots.Count > 0 && Position.Equals(Plan.BuildingSlots[0]));

    public int? BuildingIndexHere => Plan.BuildingIndexAt(Position);
}
