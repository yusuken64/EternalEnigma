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
    public int GeneratorVersion;
    public int NamingVersion;
    // Versioned separately from campaign generation so saved town arrivals keep their geometry.
    public int TownLayoutVersion;
    // Optional content never changes the original campaign graph, fingerprint, or town geometry.
    public int BookContentVersion;
    public TownDisplayName[] TownNames = Array.Empty<TownDisplayName>();
    public int Seed;
    public string Identity = "";
    public string Fingerprint = "";
    public int X, Y;
    public string Scene = "Town";
    public string LocationId = "town-0";
    public string LastTownId = "town-0";
    public string PendingDungeon = "";
    public bool Finished;
    // Enabled by the new-journey host. Existing saves and isolated starts have no prologue.
    public bool RequiresOpeningPreparation, BoughtOpeningBread, LearnedOpeningSkill;
    public int HomeLessonVersion;
    public bool ReadHomeBook, HomeDoorUnlocked;
    public int OpeningLessonVersion;
    public string OpeningSkillId = "";
    public bool PractisedOpeningSkill, UsedOpeningRecovery, MovedInOpening, InteractedInOpening;
    public bool OpeningClearNeedsSave;
    public string[] Keys = Array.Empty<string>(), Opened = Array.Empty<string>(), Resolved = Array.Empty<string>(),
        Claimed = Array.Empty<string>(), Roster = Array.Empty<string>(), Active = Array.Empty<string>(),
        Towns = Array.Empty<string>(), Completed = Array.Empty<string>(), CollectedPickups = Array.Empty<string>();
    public Capability[] Permanent = Array.Empty<Capability>();
}

[Serializable]
public sealed class TownDisplayName
{
    public string TownId = "";
    public string Name = "";
}

/// <summary>The single mutable progression authority for either launch mode.</summary>
public sealed class CampaignContext
{
    public Campaign Campaign { get; }
    public BookDungeonDefinition BookDungeon { get; }
    /// <summary>Resolves playable locations without adding optional interiors to the overworld graph.</summary>
    public CampaignLocation? ResolveDungeonLocation(string locationId) => locationId == BookDungeon.Id
        ? BookDungeon.Location : Campaign.Locations.FirstOrDefault(l => l.Id == locationId && l.IsDungeon);
    public FinaleSession? Finale { get; private set; }
    public FinaleSession BeginFinale()
    {
        if (State.Finished || State.PendingDungeon != Campaign.FinalLocationId) throw new InvalidOperationException("The final dungeon is not active.");
        return Finale ??= new FinaleSession(new FinalePlan(Campaign, Grid, Held.Contains(Capability.Boat),
            Grid.Warps.Where(r => r.CanTraverse(Held, Resolved) || (r.KeyId != null && Keys.Contains(r.KeyId))).Select(r => r.Id)));
    }
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
                    var location = ResolveDungeonLocation(State.LocationId) ?? Campaign.Locations.Single(l => l.Id == State.LocationId);
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
    public HashSet<string> CollectedPickups { get; }
    public bool ResetFloorUnlocked => Completed.Contains("repeatable-0");
    public const string OpeningDayKey = "Opening day";
    public bool IsPreparingForOpening => !IsSandbox && State.RequiresOpeningPreparation && !Keys.Contains(OpeningDayKey);
    public bool HasHomeLesson => !IsSandbox && State.RequiresOpeningPreparation && State.HomeLessonVersion > 0;
    public bool HasHomeKey => Permanent.Contains(Capability.HomeKey);
    private bool HomeDoorIsLocked => HasHomeLesson && !State.HomeDoorUnlocked;
    private string HomeLessonObjective => HasHomeKey ? "Use the Home Key to unlock your front door." :
        !State.ReadHomeBook ? "Read the book in your home." : "Open the chest to find your Home Key.";
    public bool ReadyForOpeningDay => !HomeDoorIsLocked && IsPreparingForOpening && State.BoughtOpeningBread && State.LearnedOpeningSkill;
    public bool CanEnterDungeon(string dungeonId) => !HomeDoorIsLocked && (dungeonId != "story-0" || !IsPreparingForOpening) &&
        (dungeonId != BookDungeon.Id || Completed.Contains(BookDungeon.RequiredCompletionId));
    public bool HasOpeningLesson => !IsSandbox && State.RequiresOpeningPreparation && State.OpeningLessonVersion > 0;
    public string OpeningObjective => !State.RequiresOpeningPreparation || IsSandbox ? "" :
        HomeDoorIsLocked ? HomeLessonObjective :
        IsPreparingForOpening ? !State.BoughtOpeningBread ? "Collect your free bread at the bakery." :
            !State.LearnedOpeningSkill ? "Learn your first ability at the trainer." : "Sleep and save in your bed at home." :
        State.OpeningClearNeedsSave ? "The town gate is open! Rest at home to save your victory." :
        Completed.Contains("story-0") ? State.Scene == "Town" && State.LocationId == "town-0" ? "Follow the open town gate to the overworld." : "" :
        State.PendingDungeon == "story-0" ? "Reach Floor 5 and clear the town dungeon." : "Enter the town dungeon for your first adventure.";
    public string OpeningPreparationHint => HomeDoorIsLocked ? HomeLessonObjective : ReadyForOpeningDay
        ? "You're ready for tomorrow. Sleep and save in your bed at home to open the town dungeon."
        : "The town dungeon opens tomorrow. " +
            (!State.BoughtOpeningBread ? "Collect your free bread at the bakery. " : "") +
            (!State.LearnedOpeningSkill ? "Learn an active skill at the trainer. " : "") +
            "Then sleep and save at home.";
    public CapabilitySet Permanent { get; private set; }
    public CapabilitySet Held => Permanent.Union(CapabilitySet.From(Campaign.Companions.Where(c => Active.Contains(c.Id)).Select(c => c.Capability)));
    public OverworldGates Gates => gates ??= new OverworldGates(Campaign, Grid, Resolved, Keys, Opened, Completed);
    public GridPoint Position
    {
        get { if (!positionInitialized) _ = Grid; return new(State.X, State.Y); }
        set { State.X = value.X; State.Y = value.Y; positionInitialized = true; }
    }
    public CampaignLocation? Location => State.PendingDungeon.Length != 0
        ? ResolveDungeonLocation(State.PendingDungeon) ?? Campaign.Locations.First(l => l.Id == State.PendingDungeon)
        : !positionInitialized && State.Scene == "Town" ? Campaign.Locations.First(l => l.Id == State.LocationId)
        : Campaign.Locations.FirstOrDefault(l => l.ParentTownId == null && Grid.Locations[l.Id].Equals(Position));

    public CampaignContext(OverworldLaunchOptions options, CampaignSnapshot? snapshot = null)
    {
        Mode = options.Mode;
        State = snapshot ?? new CampaignSnapshot { Seed = options.Seed, Identity = Guid.NewGuid().ToString("N"), GeneratorVersion = CampaignGenerator.Version, TownLayoutVersion = 1 };
        Campaign = CampaignGenerator.GenerateVersion(State.Seed, State.GeneratorVersion == 0 ? CampaignGenerator.LegacyVersion : State.GeneratorVersion);
        var fingerprint = CampaignFingerprint.Compute(Campaign);
        if (snapshot != null && State.Fingerprint != fingerprint) throw new InvalidOperationException("Campaign save fingerprint mismatch.");
        if (State.BookContentVersion < 0 || State.BookContentVersion > BookDungeonDefinition.CurrentVersion)
            throw new InvalidOperationException("Unsupported book content version.");
        // Upgrade only a validated, actively loaded campaign. Hosts persist this through normal explicit saving.
        State.BookContentVersion = BookDungeonDefinition.CurrentVersion;
        BookDungeon = new BookDungeonDefinition(Campaign);
        State.Fingerprint = fingerprint;
        State.GeneratorVersion = Campaign.GeneratorVersion;
        BackfillTownNames();
        positionInitialized = snapshot != null && State.Scene != "Town" &&
            ResolveDungeonLocation(State.PendingDungeon)?.ParentTownId == null;
        Keys = new(State.Keys); Opened = new(State.Opened); Resolved = new(State.Resolved); Claimed = new(State.Claimed);
        Roster = new(State.Roster); Active = new(State.Active); Towns = new(State.Towns); Completed = new(State.Completed);
        CollectedPickups = new(State.CollectedPickups ?? Array.Empty<string>());
        Permanent = CapabilitySet.From(State.Permanent);
        if (snapshot == null) Towns.Add(Campaign.StartLocationId);
    }
    public CampaignSnapshot Capture()
    {
        State.Keys = Keys.OrderBy(x => x).ToArray(); State.Opened = Opened.OrderBy(x => x).ToArray(); State.Resolved = Resolved.OrderBy(x => x).ToArray();
        State.Claimed = Claimed.OrderBy(x => x).ToArray(); State.Roster = Roster.OrderBy(x => x).ToArray(); State.Active = Active.ToArray();
        State.Towns = Towns.OrderBy(x => x).ToArray(); State.Completed = Completed.OrderBy(x => x).ToArray(); State.Permanent = Permanent.Values.ToArray();
        State.CollectedPickups = CollectedPickups.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return State;
    }
    private void BackfillTownNames()
    {
        var names = (State.TownNames ?? Array.Empty<TownDisplayName>()).Where(n => n != null &&
            !string.IsNullOrWhiteSpace(n.TownId) && !string.IsNullOrWhiteSpace(n.Name)).ToList();
        var reserved = new HashSet<string>(names.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var town in Campaign.Locations.Where(l => l.Kind == LocationKind.Town).OrderBy(l => l.Id, StringComparer.Ordinal))
            if (!names.Any(n => n.TownId == town.Id)) names.Add(new TownDisplayName { TownId = town.Id,
                Name = TownNameGenerator.Generate(State.Seed, town.Id,
                    OverworldGridGenerator.BiomeForRegion(Campaign, town.RegionId), reserved) });
        State.TownNames = names.ToArray();
        if (State.NamingVersion == 0) State.NamingVersion = TownNameGenerator.Version;
    }
    public string GetTownDisplayName(string townId) => State.TownNames.FirstOrDefault(n => n.TownId == townId)?.Name
        ?? throw new ArgumentException("Unknown town.", nameof(townId));
    public bool ReadOpeningBook()
    {
        if (!HasHomeLesson || State.ReadHomeBook || State.Scene != "Town" || State.LocationId != "town-0") return false;
        State.ReadHomeBook = true;
        return true;
    }
    public bool ClaimHomeKey()
    {
        if (!HasHomeLesson || HasHomeKey || State.Scene != "Town" || State.LocationId != "town-0") return false;
        Permanent = Permanent.Union(CapabilitySet.Of(Capability.HomeKey));
        return true;
    }
    public bool UnlockHomeDoor()
    {
        if (!HomeDoorIsLocked || !HasHomeKey || State.Scene != "Town" || State.LocationId != "town-0") return false;
        State.HomeDoorUnlocked = true;
        return true;
    }
    public bool Claim(string sourceId)
    {
        var source = Campaign.Sources.FirstOrDefault(s => s.Id == sourceId);
        if (source == null || Location?.Id != source.LocationId || Claimed.Contains(sourceId) || !source.Prerequisites.IsSatisfiedBy(Held)) return false;
        var kind = Location!.Kind;
        if ((Location!.Encounter != null || kind == LocationKind.StoryDungeon || kind == LocationKind.FinalDungeon) && !Completed.Contains(source.LocationId)) return false;
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
        if (State.PendingDungeon.Length != 0 || location == null || (State.Finished && location.Id == Campaign.FinalLocationId) ||
            !location.IsDungeon || !CanEnterDungeon(location.Id) || (location.Encounter != null && !location.Encounter.CanEnter(Held, Completed))) return false;
        State.PendingDungeon = location.Id; State.LocationId = location.Id; State.Scene = "DungeonScene"; return true;
    }
    public bool CanLeaveTown(string townId) => !HomeDoorIsLocked && Campaign.Routes.Where(r => r.IsTownExit && r.From == townId)
        .All(r => Resolved.Contains(r.Id));
    public bool BeginTownDungeon(string dungeonId)
    {
        var town = Location;
        var dungeon = ResolveDungeonLocation(dungeonId);
        if (State.PendingDungeon.Length != 0 || town?.Kind != LocationKind.Town || dungeon?.ParentTownId != town.Id || !CanEnterDungeon(dungeonId) ||
            (!IsSandbox && (State.Scene != "Town" || State.LocationId != town.Id))) return false;
        State.LastTownId = town.Id;
        State.PendingDungeon = dungeon.Id; State.LocationId = dungeon.Id; State.Scene = "DungeonScene";
        return true;
    }
    public bool CompleteDungeon(bool victory)
    {
        string id = State.PendingDungeon;
        if (id.Length == 0) return false;
        var dungeon = ResolveDungeonLocation(id) ?? throw new InvalidOperationException("Unknown active dungeon.");
        if (victory && id == Campaign.FinalLocationId && Finale?.VictoryReady != true && !IsSandbox) return false;
        if (victory)
        {
            if (id == "story-0" && !Completed.Contains(id) && State.RequiresOpeningPreparation)
                State.OpeningClearNeedsSave = true;
            Completed.Add(id);
            foreach (var route in Campaign.Routes.Where(r => r.CompletionLocationId == id)) { Resolved.Add(route.Id); Opened.Add(route.Id); }
            foreach (var route in Campaign.Routes.Where(r => r.ShortcutKind == ShortcutKind.Keyed && r.KeyLocationId == id && r.KeyId != null))
                Keys.Add(route.KeyId!);
            foreach (var route in Campaign.Routes.Where(r => r.IsTownExit && r.KeyLocationId == id && r.KeyId != null && Keys.Contains(r.KeyId)))
            { Resolved.Add(route.Id); Opened.Add(route.Id); }
            foreach (var source in Campaign.Sources.Where(s => s.LocationId == id)) Claim(source.Id);
            State.Finished |= id == Campaign.FinalLocationId; State.Scene = "Overworld"; State.LocationId = id;
            var parent = dungeon.ParentTownId;
            if (id == Campaign.FinalLocationId) { State.LastTownId = Campaign.StartLocationId; ReturnToTown(Campaign.StartLocationId); }
            else if (parent != null) ReturnToTown(parent);
            else
            {
                var destination = dungeon.Encounter?.ReturnLocationId ?? id;
                State.LocationId = destination; Position = Grid.Locations[destination];
            }
        }
        else ReturnToTown(State.LastTownId);
        State.PendingDungeon = "";
        Finale = null;
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
        Finale = null;
        var dungeon = ResolveDungeonLocation(State.PendingDungeon) ?? throw new InvalidOperationException("Unknown active dungeon.");
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
    public static DungeonFloorOptions DungeonFloorOptionsFor(int campaignSeed, string locationId, int floor, int tier, OverworldBiome biome = OverworldBiome.Grassland, int layoutVersion = 0, DungeonVariant variant = DungeonVariant.Standard)
    {
        var (start, end) = Floors(tier);
        int seed = LocationSeed(campaignSeed, locationId, floor);
        return DungeonLayoutProfile.Options(seed, biome, tier, floor == start ? DungeonFloorRole.Entry : floor == end ? DungeonFloorRole.Exit : DungeonFloorRole.Regular, layoutVersion, locationId, variant);
    }
    public DungeonFloor DungeonFloor(string locationId, int floor)
    {
        if (locationId == Campaign.FinalLocationId) return new FinalePlan(Campaign, Grid).Floors[0].CreateFloor();
        return DungeonFloorGenerator.Generate(RunPlan(locationId).Floor(floor).Options);
    }
    public DungeonRunPlan RunPlan(string locationId)
    {
        var location = ResolveDungeonLocation(locationId) ?? throw new ArgumentException("Unknown dungeon.", nameof(locationId));
        return new DungeonRunPlan(State.Seed, location, locationId == BookDungeon.Id ? BookDungeon.Biome :
            OverworldGridGenerator.BiomeForRegion(Campaign, location.RegionId));
    }
    /// <summary>Host inventory acceptance and persistent identity are committed together; a full bag leaves the pickup available.</summary>
    public bool TryCollectPickup(string pickupId, Func<OverworldPickup, bool> acceptItem)
    {
        var pickup = Grid.Pickups.FirstOrDefault(p => p.Id == pickupId);
        if (pickup == null || CollectedPickups.Contains(pickupId) || !Position.Equals(pickup.Position) || !acceptItem(pickup)) return false;
        CollectedPickups.Add(pickupId); return true;
    }
    /// <summary>The dungeon entrance is the only non-service building every town requires.</summary>
    public const int AuthoredTownBuildings = 1;
    /// <summary>Extra houses with no service, so a town reads as a settlement; about as many as it has services and authored buildings.</summary>
    public const int ResidentialTownBuildings = 7;
    /// <summary>Sizes the town for its services and assigns each one to a building slot.</summary>
    public TownLayout TownLayout(string townId, int allyCount = 3)
    {
        var location = Campaign.Locations.FirstOrDefault(l => l.Id == townId && l.Kind == LocationKind.Town) ?? throw new ArgumentException("Unknown town.", nameof(townId));
        return Generation.TownLayout.Create(this.LocationSeed(townId), location.Services, AuthoredTownBuildings, allyCount, ResidentialTownBuildings,
            openingNeighborhood: townId == "town-0" && State.TownLayoutVersion >= 1);
    }
    /// <summary>Generates the town's plan; explicit <paramref name="shopFlags"/> bypass the service layout.</summary>
    public TownPlan Town(string townId, IReadOnlyList<bool>? shopFlags = null, int allyCount = 3) =>
        TownPlanGenerator.Generate(shopFlags != null
            ? new TownPlanOptions(this.LocationSeed(townId), shopFlags: shopFlags, allyCount: allyCount)
            : TownLayout(townId, allyCount).Options);
}
