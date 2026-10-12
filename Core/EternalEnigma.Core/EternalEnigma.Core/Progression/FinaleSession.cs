using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Progression;

/// <summary>Run-local death ledger; no finale checkpoints exist.</summary>
public sealed class FinaleSession
{
    public FinalePlan Plan { get; }
    public int CurrentFloor { get; private set; } = 1;
    public FinaleFloorPlan Floor => Plan.Floors[CurrentFloor - 1];
    readonly Dictionary<string, string> enemies = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> mimicFamilies = new(StringComparer.Ordinal);
    readonly HashSet<string> defeated = new(StringComparer.Ordinal), revealed = new(StringComparer.Ordinal),
        unlocked = new(StringComparer.Ordinal), cleared = new(StringComparer.Ordinal);
    readonly List<int> exits = new();
    public IReadOnlyDictionary<string, string> EnemyRegions => enemies;
    public IReadOnlyCollection<string> Defeated => defeated;
    public IReadOnlyCollection<string> Revealed => revealed;
    public IReadOnlyCollection<string> Unlocked => unlocked;
    public IReadOnlyCollection<string> ClearedRegions => cleared;
    public IReadOnlyList<int> StairTransitions => exits;
    public bool FinalExitCompleted { get; private set; }
    public bool FloorCleared => Floor.Regions.All(r => cleared.Contains(r.Id));
    public bool VictoryReady => FinalExitCompleted;
    public int Remaining => enemies.Count(p => p.Key.StartsWith($"floor-{CurrentFloor}/", StringComparison.Ordinal) && !defeated.Contains(p.Key));
    public int HiddenMimics => Floor.Enemies.Count(e => e.IsMimic && !revealed.Contains(e.Id));
    public FinaleSession(FinalePlan plan)
    {
        Plan = plan;
        foreach (var floor in plan.Floors) foreach (var enemy in floor.Enemies)
        {
            enemies.Add(enemy.Id, $"floor-{floor.Number}/{enemy.RegionId}");
            if (enemy.IsMimic) mimicFamilies.Add(enemy.Id, enemy.Family!);
        }
        unlocked.Add(Floor.Regions[0].RegionId);
    }
    public int RemainingIn(string regionId) => enemies.Count(p => p.Value == regionId && !defeated.Contains(p.Key));
    public bool IsWalkable(GridPoint cell) => Floor.IsWalkable(cell, unlocked);
    public bool CanStep(GridPoint from, GridPoint to) => Floor.CanStep(from, to, unlocked);
    public IEnumerable<(string RouteId, GridPoint Destination)> WarpDestinations(GridPoint from) => Floor.WarpDestinations(from, unlocked);
    public bool CanReveal(string id, GridPoint from)
    {
        var mimic = Floor.Enemies.FirstOrDefault(e => e.Id == id && e.IsMimic);
        return mimic != null && !revealed.Contains(id) && CanStep(from, mimic.Position);
    }
    public string MimicFamily(string id) => mimicFamilies[id];
    public bool Reveal(string id, GridPoint from) => Reveal(id, from, out _);
    public bool Reveal(string id, GridPoint from, out string? swappedWith)
    {
        swappedWith = null;
        if (!CanReveal(id, from)) return false;
        // Keep the region's bosses hidden until its ordinary building mimics have been revealed.
        // Swap only unrevealed identities, preserving every planned family and required death.
        if (FinaleRoster.IsBoss(mimicFamilies[id]))
        {
            swappedWith = Floor.Enemies.FirstOrDefault(e => e.IsMimic && enemies[e.Id] == enemies[id] &&
                !revealed.Contains(e.Id) && !FinaleRoster.IsBoss(mimicFamilies[e.Id]))?.Id;
            if (swappedWith != null)
                (mimicFamilies[id], mimicFamilies[swappedWith]) = (mimicFamilies[swappedWith], mimicFamilies[id]);
        }
        return revealed.Add(id);
    }
    public bool RegisterChild(string sourceId, string id)
    {
        if (enemies.ContainsKey(id) || !sourceId.StartsWith($"floor-{CurrentFloor}/", StringComparison.Ordinal) ||
            !id.StartsWith(sourceId + "/", StringComparison.Ordinal) || !enemies.TryGetValue(sourceId, out var region) || cleared.Contains(region)) return false;
        enemies.Add(id, region); return true;
    }
    // Called by actual death resolution, never conversion or disappearance.
    public bool RecordDeath(string id)
    {
        if (!id.StartsWith($"floor-{CurrentFloor}/", StringComparison.Ordinal) || !enemies.TryGetValue(id, out var region) ||
            !unlocked.Contains(region.Substring(region.IndexOf('/') + 1))) return false;
        if (Floor.Enemies.Any(e => e.Id == id && e.IsMimic) && !revealed.Contains(id)) return false;
        return defeated.Add(id);
    }
    // Reconcile after the turn: hit/death effects must first register their surviving summons.
    public IReadOnlyList<string> ReconcileClearance()
    {
        var opened = new List<string>();
        for (int i = 0; i < Floor.Regions.Count; i++)
        {
            var region = Floor.Regions[i];
            if (!unlocked.Contains(region.RegionId) || RemainingIn(region.Id) != 0 || !cleared.Add(region.Id)) continue;
            if (i + 1 < Floor.Regions.Count) { unlocked.Add(Floor.Regions[i + 1].RegionId); opened.Add(Floor.Regions[i + 1].Id); }
        }
        return opened;
    }
    public bool UseStairs(GridPoint position)
    {
        if (FinalExitCompleted || !FloorCleared || !position.Equals(Floor.Stairs)) return false;
        exits.Add(CurrentFloor);
        if (CurrentFloor == FinalePlan.FloorCount) FinalExitCompleted = true;
        else { CurrentFloor++; unlocked.Clear(); unlocked.Add(Floor.Regions[0].RegionId); }
        return true;
    }
}
