using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Shared campaign and sandbox scene; all progression is owned by Common.</summary>
public sealed class OverworldScene : MonoBehaviour
{
    public CampaignOverworld Map;
    public TownAlly PlayerPrefab;
    public Camera ViewCamera;
    public GameObject TownMarker;
    public GameObject DungeonMarker;
    public GameObject LandmarkMarker;
    public GameObject GateMarker;
    public Vector3 CameraOffset = new(0, -14, -20);
    public bool IsReady { get; private set; }
    public bool IsMoving => moving;
    public CampaignContext Context { get; private set; }
    public GridPoint Position { get => Context.Position; private set => Context.Position = value; }
    public TownAlly Player { get; private set; }
    public IReadOnlyList<TownAlly> Followers => followers.AsReadOnly();
    private readonly List<TownAlly> followers = new();
    private readonly List<GridPoint> walkHistory = new();
    public Campaign Campaign { get; private set; }
    public CapabilitySet Held => Context.Held;
    public IEnumerable<string> CollectedKeys => gates.CollectedKeys;
    private OverworldGates gates;
    public string Message { get; private set; } = "Generating campaign…";
    private HashSet<string> claimed => Context.Claimed;
    private HashSet<string> recruited => Context.Roster;
    private HashSet<string> active => Context.Active;
    private HashSet<string> resolved => Context.Resolved;
    private readonly Dictionary<string, List<GameObject>> gateVisuals = new();
    private readonly Dictionary<GridPoint, GameObject> locationVisuals = new();
    private Dictionary<GridPoint, CampaignLocation> locations;
    private TileWorldCreator creator;
    private bool moving;
    private float nextMove;

    private void Start()
    {
        creator = Map.GetComponent<TileWorldCreator>();
        var common = Common.Instance;
        if (common.CampaignContext == null) common.BeginSandbox(OverworldLaunch.TakeSeed(Map.Seed));
        Context = common.CampaignContext;
        Campaign = Context.Campaign;
        Map.Seed = Campaign.Seed;
        var grid = Context.Grid;
        Map.Apply(grid, Held, resolved);
        gates = Context.Gates;
        locations = Campaign.Locations.Where(l => l.ParentTownId == null).ToDictionary(l => grid.Locations[l.Id]);
        var controls = gameObject.AddComponent<OverworldSandboxControls>();
        controls.Scene = this; controls.enabled = Context.IsSandbox;
        creator.OnBuildLayersComplete += TerrainReady;
        Map.BuildMeshes();
    }

    private void TerrainReady(TileWorldCreator _)
    {
        creator.OnBuildLayersComplete -= TerrainReady;
        foreach (var location in Campaign.Locations.Where(l => l.ParentTownId == null))
        {
            var prefab = location.Kind == LocationKind.Town ? TownMarker :
                location.Kind == LocationKind.StoryDungeon || location.Kind == LocationKind.RepeatableDungeon || location.Kind == LocationKind.FinalDungeon
                    ? DungeonMarker : LandmarkMarker;
            var marker = Instantiate(prefab, CellCenterToWorld(Map.CurrentGrid.Locations[location.Id]), Quaternion.identity, transform);
            marker.name = location.Id;
            var cell = Map.CurrentGrid.Locations[location.Id];
            locationVisuals.Add(cell, marker);
            marker.SetActive(!cell.Equals(Position));
        }
        foreach (var gate in Map.CurrentGrid.Locks)
        {
            var markers = new List<GameObject>();
            foreach (var cell in gate.Cells)
            {
                if (Map.CurrentGrid.RequiresBoat(cell)) continue;
                var marker = Instantiate(GateMarker, CellCenterToWorld(cell), Quaternion.identity, transform);
                var route = Campaign.Routes.First(r => r.Id == gate.RouteId);
                marker.name = (route.ShortcutKind == ShortcutKind.None ? "Gate " : "Shortcut ") + gate.RouteId;
                if (route.ShortcutKind != ShortcutKind.None)
                    foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
                    {
                        var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                        Color color = route.ShortcutKind == ShortcutKind.FarSide || route.ShortcutKind == ShortcutKind.Keyed ? Color.cyan : Color.yellow;
                        properties.SetColor("_Color", color); properties.SetColor("_BaseColor", color); renderer.SetPropertyBlock(properties);
                    }
                markers.Add(marker);
            }
            gateVisuals.Add(gate.RouteId, markers);
        }
        Player = Instantiate(PlayerPrefab, CellToWorld(Position), Quaternion.identity, transform);
        foreach (var endpoint in Map.CurrentGrid.Warps.SelectMany(r => new[] { r.From, r.To }).Distinct())
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Warp gate " + endpoint;
            pad.transform.SetParent(transform);
            pad.transform.position = CellCenterToWorld(Map.CurrentGrid.Locations[endpoint]) + Vector3.back * .3f;
            pad.transform.rotation = Quaternion.Euler(90, 0, 0);
            pad.transform.localScale = new Vector3(.8f, .08f, .8f) * creator.twcAsset.cellSize;
            Destroy(pad.GetComponent<Collider>());
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", Color.cyan); properties.SetColor("_BaseColor", Color.cyan);
            pad.GetComponent<Renderer>().SetPropertyBlock(properties);
        }
        Player.name = "Campaign Player";
        Player.SetFacing(Facing.Down);
        Player.SetToPlayer();
        Player.HeroAnimator.PlayIdleAnimation();
        Player.TilemapPosition = new Vector3Int(Position.X, Position.Y, 0);
        walkHistory.Add(Position);
        IsReady = true;
        Common.Instance.Travel.SceneReady();
        Common.Instance.ScreenTransition.DoOpen();
        RebuildFollowers();
        RefreshGates();
        Message = "Enter / A: claim location rewards or use a key at a gate.";
        FollowCamera();
    }

    public Vector3 CellToWorld(GridPoint point) => Map.transform.position + new Vector3(point.X, point.Y, 0) * creator.twcAsset.cellSize;
    public Vector3 CellCenterToWorld(GridPoint point) => CellToWorld(point) + CellVisualOffset;
    private Vector3 CellVisualOffset => new Vector3(.5f, .5f, 0) * creator.twcAsset.cellSize;

    private void Update()
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return;
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        if (keyboard?.enterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true) ClaimRewards();
        Vector2 move = pad == null ? Vector2.zero : pad.dpad.ReadValue() + pad.leftStick.ReadValue();
        if (keyboard != null)
        {
            move.x += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            move.y += (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
        }
        if (move.sqrMagnitude < .1f || Time.time < nextMove) return;
        nextMove = Time.time + .16f;
        TryMove(Mathf.Abs(move.x) > .3f ? System.Math.Sign(move.x) : 0, Mathf.Abs(move.y) > .3f ? System.Math.Sign(move.y) : 0);
    }

    public bool CanStep(GridPoint from, GridPoint to) => IsReady && Map.CurrentGrid.CanStep(from, to, Held, resolved) && OverworldMovement.CanStep(from, to, cell => gates.IsWalkable(cell, Held));

    public bool TryMove(int dx, int dy)
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return false;
        if (Context.Location?.Kind == LocationKind.Town && !Context.CanLeaveTown(Context.Location.Id))
        { Message = "Clear the dungeon inside town to unlock the town gate."; return false; }
        var next = new GridPoint(Position.X + dx, Position.Y + dy);
        if (!CanStep(Position, next))
        {
            var gate = Map.CurrentGrid.LockAt(next);
            Message = Map.CurrentGrid.RequiresBoat(next) && !Held.Contains(Capability.Boat) ? "Requires: Boat to sail." :
                gate == null ? "Blocked." : GateDescription(gate.RouteId);
            return false;
        }
        if (locationVisuals.TryGetValue(Position, out var previousMarker)) previousMarker.SetActive(true);
        Position = next;
        if (locationVisuals.TryGetValue(Position, out var occupiedMarker)) occupiedMarker.SetActive(false);
        var crossed = Map.CurrentGrid.LockAt(next);
        if (crossed != null && Campaign.Routes.First(r => r.Id == crossed.RouteId).Latches) resolved.Add(crossed.RouteId);
        SaveProgress();
        RefreshGates();
        Message = locations.TryGetValue(Position, out var location) ? location.Id + " — " + location.Kind + " | Enter / A: interact" : "";
        foreach (string id in OverworldMovement.Neighbors(Position).Select(Map.CurrentGrid.LockAt).Where(g => g != null).Select(g => g.RouteId).Distinct())
            if (gates.NeedsOpening(Campaign.Routes.First(r => r.Id == id))) Message += " | " + GateDescription(id);
        foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && Map.CurrentGrid.Locations[r.KeyLocationId].Equals(Position) && !gates.HasKey(r)))
            Message += " | Enter / A: Collect " + route.KeyId;
        foreach (var route in Map.CurrentGrid.WarpsAt(Position)) Message += " | " + WarpLabel(route);
        walkHistory.Add(Position);
        if (walkHistory.Count > 4) walkHistory.RemoveAt(0);
        StartCoroutine(Walk());
        return true;
    }

    private IEnumerator Walk()
    {
        moving = true;
        var party = new[] { Player }.Concat(followers).ToArray();
        var from = party.Select(ally => ally.transform.position).ToArray();
        var to = new Vector3[party.Length];
        for (int i = 0; i < party.Length; i++)
        {
            var cell = TrailCell(i);
            to[i] = CellToWorld(cell);
            var direction = to[i] - from[i];
            if (direction.sqrMagnitude > .0001f)
            {
                party[i].SetFacing(Character.GetFacing(new Vector3Int(
                    System.Math.Sign(direction.x), System.Math.Sign(direction.y), 0)));
                party[i].HeroAnimator.PlayWalkAnimation();
            }
            party[i].TilemapPosition = new Vector3Int(cell.X, cell.Y, 0);
        }
        RefreshLocationMarkers();
        float elapsed = 0;
        while (elapsed < .15f)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < party.Length; i++)
                party[i].transform.position = Vector3.Lerp(from[i], to[i], Mathf.Clamp01(elapsed / .15f));
            yield return null;
        }
        for (int i = 0; i < party.Length; i++)
        {
            party[i].transform.position = to[i];
            party[i].HeroAnimator.PlayIdleAnimation();
        }
        moving = false;
    }

    private string GateDescription(string id)
    {
        var route = Campaign.Routes.First(r => r.Id == id);
        return gates.Hint(route, Held);
    }

    private string WarpLabel(CampaignRoute route)
    {
        string destination = Map.CurrentGrid.Locations[route.From].Equals(Position) ? route.To : route.From;
        string region = Campaign.Locations.First(l => l.Id == destination).RegionId;
        return "Warp to biome " + Campaign.Regions.First(r => r.Id == region).Label +
            (route.CanTraverse(Held, resolved) ? "" : " | " + gates.Hint(route, Held));
    }

    public bool Warp(string routeId)
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return false;
        if (!Map.CurrentGrid.TryWarp(routeId, Position, Held, resolved, out var destination))
        { Message = Map.CurrentGrid.WarpsAt(Position).FirstOrDefault(r => r.Id == routeId) is CampaignRoute blocked ? gates.Hint(blocked, Held) : "Stand on a warp gate."; return false; }
        if (locationVisuals.TryGetValue(Position, out var previous)) previous.SetActive(true);
        Position = destination;
        if (locationVisuals.TryGetValue(Position, out var current)) current.SetActive(false);
        Player.transform.position = CellToWorld(Position);
        Player.TilemapPosition = new Vector3Int(Position.X, Position.Y, 0);
        walkHistory.Clear();
        walkHistory.Add(Position);
        PlaceFollowers();
        RefreshGates(); FollowCamera();
        SaveProgress();
        Message = "Warp complete. Choose a destination below to return.";
        return true;
    }

    public bool OpenGate(string routeId = null)
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return false;
        foreach (var route in gates.Nearby(Position))
            if ((routeId == null || route.Id == routeId) && gates.TryOpen(route.Id, Position, Held))
            {
                Message = "Opened gate: " + route.Id + ". Used " + (route.KeyId ?? route.Requirement.ToString()) + ".";
                RefreshGates();
                SaveProgress();
                return true;
            }
        return false;
    }

    public bool OpenShortcut()
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || !locations.TryGetValue(Position, out var location)) return false;
        foreach (var route in Campaign.Routes)
            if (route.TryUnlock(location.Id, resolved)) { Message = "Opened shortcut: " + route.Id; RefreshGates(); SaveProgress(); return true; }
        return false;
    }

    public void ClaimRewards()
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return;
        if (OpenGate()) return;
        if (!locations.TryGetValue(Position, out var location)) return;
        if (OpenShortcut()) return;
        if (!Context.IsSandbox && Common.Instance.Travel.EnterLocation()) return;
        var rewards = new List<string>();
        foreach (var route in Campaign.Routes)
            if (gates.CollectKey(route, location.Id)) rewards.Add(route.KeyId + " (use it at the gate)");
        foreach (var source in Campaign.Sources.Where(s => s.LocationId == location.Id && !claimed.Contains(s.Id)))
        {
            if (!Context.Claim(source.Id)) continue;
            rewards.Add(source.Capability.ToString());
        }
        SaveProgress();
        Message = rewards.Count == 0 ? "No eligible unclaimed rewards here." : "Acquired: " + string.Join(", ", rewards) + ". Equip companions at a town.";
        RefreshGates();
    }

    public bool ToggleCompanion(string id)
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || !Context.IsSandbox || !locations.TryGetValue(Position, out var location) || location.Kind != LocationKind.Town || !recruited.Contains(id)) return false;
        var ids = active.Contains(id) ? active.Where(x => x != id).ToArray() : active.Concat(new[] { id }).ToArray();
        if (!Context.SetParty(ids)) return false;
        RebuildFollowers(); SaveProgress(); RefreshGates(); return true;
    }

    private void RebuildFollowers()
    {
        foreach (var ally in followers) { ally.gameObject.SetActive(false); Destroy(ally.gameObject); }
        followers.Clear();
        foreach (var id in active)
        {
            var prefab = CampaignParty.Resolve(id, TownSceneLoader.Default) ?? PlayerPrefab;
            var follower = Instantiate(prefab, CellToWorld(Position), Quaternion.identity, transform);
            follower.Id = id; follower.name = "Campaign Ally " + id;
            follower.SetToCPU(); follower.SetFacing(Player.CurrentFacing); followers.Add(follower);
        }
        PlaceFollowers();
    }
    private void SaveProgress()
    {
        if (!Context.IsSandbox) SaveSystem.SaveData(Common.Instance.GameSaveData);
    }
    public bool SimulateDungeonVictory()
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || !Context.IsSandbox) return false;
        var interior = Context.Campaign.Locations.FirstOrDefault(l => l.ParentTownId != null && l.ParentTownId == Context.Location?.Id);
        if (!(interior != null ? Context.BeginTownDungeon(interior.Id) : Context.BeginDungeon())) return false;
        Context.CompleteDungeon(true); RefreshGates();
        Message = Context.State.Scene == "Town" ? "Town dungeon cleared. The town gate is unlocked."
            : "Dungeon victory committed. Use any awarded key at its exit gate."; return true;
    }

    private GridPoint TrailCell(int index) => walkHistory[Mathf.Max(0, walkHistory.Count - index - 1)];

    private void PlaceFollowers()
    {
        for (int i = 0; i < followers.Count; i++)
        {
            var cell = TrailCell(i + 1);
            followers[i].transform.position = CellToWorld(cell);
            followers[i].TilemapPosition = new Vector3Int(cell.X, cell.Y, 0);
            followers[i].HeroAnimator.PlayIdleAnimation();
        }
        RefreshLocationMarkers();
    }

    private void RefreshLocationMarkers()
    {
        foreach (var marker in locationVisuals)
            marker.Value.SetActive(!marker.Key.Equals(Position) && !followers.Any(a =>
                a.TilemapPosition.x == marker.Key.X && a.TilemapPosition.y == marker.Key.Y));
    }

    private void RefreshGates()
    {
        foreach (var gate in Map.CurrentGrid.Locks)
            foreach (var visual in gateVisuals[gate.RouteId]) visual.SetActive(!gates.IsWalkable(gate.Cells[0], Held));
    }

    private void LateUpdate() { if (IsReady) FollowCamera(); }
    private void FollowCamera()
    {
        var target = Player.transform.position + CellVisualOffset;
        ViewCamera.transform.position = target + CameraOffset;
        ViewCamera.transform.LookAt(target);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 580, 430), GUI.skin.box);
        GUILayout.Label("CAMPAIGN OVERWORLD  |  Seed " + Map.Seed);
        GUILayout.Label("WASD / arrows / left stick: move   •   Enter / A: interact");
        GUILayout.Label("Green: town   Red: dungeon   Gold: landmark   Purple: closed gate");
        GUILayout.Label(Message);
        if (IsReady)
        {
            GUILayout.Label("Biome: " + Map.CurrentGrid.BiomeAt(Position) + (Map.CurrentGrid.RequiresBoat(Position) ? " | Sailing" : "") +
                (Held.Contains(Capability.Boat) ? " | Boat acquired" : " | Water requires Boat"));
            if (Context.State.Finished) GUILayout.Label("Campaign complete!");
            GUILayout.Label("Capabilities: " + Held);
            GUILayout.Label("Keys: " + string.Join(", ", CollectedKeys));
            foreach (var route in gates.Nearby(Position).Where(gates.NeedsOpening))
                if (GUILayout.Button(gates.Hint(route, Held))) OpenGate(route.Id);
            foreach (var route in Map.CurrentGrid.WarpsAt(Position))
                if (GUILayout.Button(WarpLabel(route))) Warp(route.Id);
            GUILayout.Label("Biomes: " + string.Join(" | ", Campaign.Regions.Select(r => r.Label + " " + Map.CurrentGrid.RegionBiomes[r.Id])));
            foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && Map.CurrentGrid.Locations[r.KeyLocationId].Equals(Position) && !gates.HasKey(r)))
                if (GUILayout.Button("Collect " + route.KeyId)) ClaimRewards();
            foreach (var objective in Campaign.ReturnObjectives.Where(o => o.Required))
                GUILayout.Label("Required return: " + objective.RegionId + " | Requires " + objective.EnablingCapability + " | Reward " + objective.RewardCapability);
            foreach (var route in Campaign.Routes.Where(r => r.UnlockingEndpoint != null && Map.CurrentGrid.Locations[r.UnlockingEndpoint].Equals(Position) && !resolved.Contains(r.Id)))
                if (GUILayout.Button("Open shortcut")) OpenShortcut();
        }
        GUILayout.EndArea();
    }

    private void OnDestroy() { if (creator != null) creator.OnBuildLayersComplete -= TerrainReady; }
}
