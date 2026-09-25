using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Progression;

public enum OverworldLaunchMode { Campaign, Sandbox }
public sealed class OverworldLaunchOptions
{
    public OverworldLaunchMode Mode { get; }
    public int Seed { get; }
    public OverworldLaunchOptions(OverworldLaunchMode mode, int seed = 42) { Mode = mode; Seed = seed; }
}

[Serializable]
public sealed class CampaignSnapshot
{
    public int Version = 1;
    public int GeneratorVersion = CampaignGenerator.Version;
    public int GridVersion = OverworldGrid.GenerationVersion;
    public int Seed;
    public string Identity = "";
    public string Fingerprint = "";
    public int X, Y;
    public string Scene = "Town";
    public string LocationId = "town-0";
    public string LastTownId = "town-0";
    public string PendingDungeon = "";
    public bool Finished;
    public string[] Keys = Array.Empty<string>(), Opened = Array.Empty<string>(), Resolved = Array.Empty<string>(),
        Claimed = Array.Empty<string>(), Roster = Array.Empty<string>(), Active = Array.Empty<string>(),
        Towns = Array.Empty<string>(), Completed = Array.Empty<string>();
    public Capability[] Permanent = Array.Empty<Capability>();
}

/// <summary>The single mutable progression authority for either launch mode.</summary>
public sealed class CampaignContext
{
    public Campaign Campaign { get; }
    private OverworldGrid? grid;
    private OverworldGates? gates;
    private bool positionInitialized;
    public bool IsGridGenerated => grid != null;
    public OverworldGrid Grid
    {
        get
        {
            if (grid == null)
            {
                grid = OverworldGridGenerator.Generate(Campaign);
                if (!positionInitialized)
                {
                    var location = Campaign.Locations.Single(l => l.Id == State.LocationId);
                    Position = grid.Locations[location.ParentTownId ?? location.Id];
                }
            }
            return grid;
        }
    }
    public OverworldLaunchMode Mode { get; }
    public bool IsSandbox => Mode == OverworldLaunchMode.Sandbox;
    public CampaignSnapshot State { get; }
    public HashSet<string> Keys { get; }
public HashSet<string> Opened { get; }
public HashSet<string> Resolved { get; }
public HashSet<string> Claimed { get; }
public HashSet<string> Roster { get; }
public HashSet<string> Active { get; }
public HashSet<string> Towns { get; }
public HashSet<string> Completed { get; }
    public CapabilitySet Permanent { get; private set; }
    public CapabilitySet Held => Permanent.Union(CapabilitySet.From(Campaign.Companions.Where(c => Active.Contains(c.Id)).Select(c => c.Capability)));
    public OverworldGates Gates => gates ??= new OverworldGates(Campaign, Grid, Resolved, Keys, Opened, Completed);
    public GridPoint Position
    {
        get { if (!positionInitialized) _ = Grid; return new(State.X, State.Y); }
        set { State.X = value.X; State.Y = value.Y; positionInitialized = true; }
    }
    public CampaignLocation? Location => State.PendingDungeon.Length != 0
        ? Campaign.Locations.First(l => l.Id == State.PendingDungeon)
        : !positionInitialized && State.Scene == "Town" ? Campaign.Locations.First(l => l.Id == State.LocationId)
        : Campaign.Locations.FirstOrDefault(l => l.ParentTownId == null && Grid.Locations[l.Id].Equals(Position));

    public CampaignContext(OverworldLaunchOptions options, CampaignSnapshot? snapshot = null)
    {
        Mode = options.Mode;
        State = snapshot ?? new CampaignSnapshot { Seed = options.Seed, Identity = Guid.NewGuid().ToString("N") };
        if (State.Version != 1 || State.GeneratorVersion != CampaignGenerator.Version || State.GridVersion != OverworldGrid.GenerationVersion)
            throw new InvalidOperationException("Unsupported campaign save version.");
        Campaign = CampaignGenerator.Generate(State.Seed);
        var fingerprint = CampaignFingerprint.Compute(Campaign);
        if (snapshot != null && State.Fingerprint != fingerprint) throw new InvalidOperationException("Campaign save fingerprint mismatch.");
        State.Fingerprint = fingerprint;
        positionInitialized = snapshot != null && State.Scene != "Town" &&
            !Campaign.Locations.Any(l => l.Id == State.PendingDungeon && l.ParentTownId != null);
        Keys = new(State.Keys); Opened = new(State.Opened); Resolved = new(State.Resolved); Claimed = new(State.Claimed);
        Roster = new(State.Roster); Active = new(State.Active); Towns = new(State.Towns); Completed = new(State.Completed);
        Permanent = CapabilitySet.From(State.Permanent);
        if (snapshot == null) Towns.Add(Campaign.StartLocationId);
    }
    public CampaignSnapshot Capture()
    {
        State.Keys = Keys.OrderBy(x => x).ToArray(); State.Opened = Opened.OrderBy(x => x).ToArray(); State.Resolved = Resolved.OrderBy(x => x).ToArray();
        State.Claimed = Claimed.OrderBy(x => x).ToArray(); State.Roster = Roster.OrderBy(x => x).ToArray(); State.Active = Active.ToArray();
        State.Towns = Towns.OrderBy(x => x).ToArray(); State.Completed = Completed.OrderBy(x => x).ToArray(); State.Permanent = Permanent.Values.ToArray();
        return State;
    }
    public bool Claim(string sourceId)
    {
        var source = Campaign.Sources.FirstOrDefault(s => s.Id == sourceId);
        if (source == null || Location?.Id != source.LocationId || Claimed.Contains(sourceId) || !source.Prerequisites.IsSatisfiedBy(Held)) return false;
        var kind = Location!.Kind;
        if ((kind == LocationKind.StoryDungeon || kind == LocationKind.FinalDungeon) && !Completed.Contains(source.LocationId)) return false;
        if (source.CompanionId != null) Roster.Add(source.CompanionId);
        else Permanent = Permanent.Union(CapabilitySet.Of(source.Capability));
        Claimed.Add(sourceId); return true;
    }
    public bool SetParty(params string[] ids)
    {
        if (Location?.Kind != LocationKind.Town || ids.Length > 3 || ids.Distinct().Count() != ids.Length || ids.Any(id => !Roster.Contains(id))) return false;
        Active.Clear(); Active.UnionWith(ids); return true;
    }
    public bool BeginDungeon()
    {
        var location = Location;
        if (State.PendingDungeon.Length != 0 || location == null ||
            !(location.Kind == LocationKind.StoryDungeon || location.Kind == LocationKind.RepeatableDungeon || location.Kind == LocationKind.FinalDungeon)) return false;
        State.PendingDungeon = location.Id; State.LocationId = location.Id; State.Scene = "DungeonScene"; return true;
    }
    public bool CanLeaveTown(string townId) => Campaign.Routes.Where(r => r.IsTownExit && r.From == townId)
        .All(r => Resolved.Contains(r.Id));
    public bool BeginTownDungeon(string dungeonId)
    {
        var town = Location;
        var dungeon = Campaign.Locations.FirstOrDefault(l => l.Id == dungeonId);
        if (State.PendingDungeon.Length != 0 || town?.Kind != LocationKind.Town || dungeon?.ParentTownId != town.Id ||
            (!IsSandbox && (State.Scene != "Town" || State.LocationId != town.Id))) return false;
        State.LastTownId = town.Id;
        State.PendingDungeon = dungeon.Id; State.LocationId = dungeon.Id; State.Scene = "DungeonScene";
        return true;
    }
    public bool CompleteDungeon(bool victory)
    {
        string id = State.PendingDungeon;
        if (id.Length == 0) return false;
        if (victory)
        {
            Completed.Add(id);
            foreach (var route in Campaign.Routes.Where(r => r.ShortcutKind == ShortcutKind.Keyed && r.KeyLocationId == id && r.KeyId != null))
                Keys.Add(route.KeyId!);
            foreach (var route in Campaign.Routes.Where(r => r.IsTownExit && r.KeyLocationId == id && r.KeyId != null && Keys.Contains(r.KeyId)))
            { Resolved.Add(route.Id); Opened.Add(route.Id); }
            foreach (var source in Campaign.Sources.Where(s => s.LocationId == id)) Claim(source.Id);
            State.Finished |= id == Campaign.FinalLocationId; State.Scene = "Overworld"; State.LocationId = id;
            var parent = Campaign.Locations.Single(l => l.Id == id).ParentTownId;
            if (parent != null) ReturnToTown(parent);
            else Position = Grid.Locations[id];
        }
        else ReturnToTown(State.LastTownId);
        State.PendingDungeon = "";
        return true;
    }
    private void ReturnToTown(string id)
    {
        State.LocationId = id; State.Scene = "Town";
        positionInitialized = false;
        if (grid != null) Position = grid.Locations[id];
    }
    public bool RecoverInterruptedRun()
    {
        if (State.PendingDungeon.Length == 0) return false;
        var dungeon = Campaign.Locations.Single(l => l.Id == State.PendingDungeon);
        State.LocationId = dungeon.ParentTownId ?? dungeon.Id;
        if (dungeon.ParentTownId != null) ReturnToTown(dungeon.ParentTownId);
        else Position = Grid.Locations[State.LocationId];
        State.PendingDungeon = ""; State.Scene = dungeon.ParentTownId == null ? "Overworld" : "Town"; return true;
    }
    public static (int Start, int End) Floors(int tier) => tier switch
    { 0 => (1, 5), 1 => (5, 10), 2 => (10, 20), 3 => (20, 30), 4 => (30, 40), _ => throw new ArgumentOutOfRangeException(nameof(tier)) };
    public static int LocationSeed(int campaignSeed, string location, int floor = 0)
    {
        unchecked { uint hash = 2166136261; foreach (char c in campaignSeed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + location + "/" + floor.ToString(System.Globalization.CultureInfo.InvariantCulture)) hash = (hash ^ c) * 16777619; return (int)(hash & 0x7fffffff); }
    }
    public int LocationSeed(string location, int floor = 0) => LocationSeed(State.Seed, location, floor);
    /// <summary>Throne floors are the first and last floor of the location's tier range.</summary>
    public static DungeonFloorOptions DungeonFloorOptionsFor(int campaignSeed, string locationId, int floor, int tier)
    {
        var (start, end) = Floors(tier);
        int seed = LocationSeed(campaignSeed, locationId, floor);
        return floor == start || floor == end ? DungeonFloorOptions.Throne(seed) : new DungeonFloorOptions(seed);
    }
    public DungeonFloor DungeonFloor(string locationId, int floor)
    {
        var location = Campaign.Locations.FirstOrDefault(l => l.Id == locationId) ?? throw new ArgumentException("Unknown location.", nameof(locationId));
        return DungeonFloorGenerator.Generate(DungeonFloorOptionsFor(State.Seed, locationId, floor, location.Tier));
    }
    public TownPlan Town(string townId, IReadOnlyList<bool>? shopFlags = null, int allyCount = 3) =>
        TownPlanGenerator.Generate(new TownPlanOptions(this.LocationSeed(townId), shopFlags: shopFlags, allyCount: allyCount));
}
