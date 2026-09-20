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

/// <summary>Standalone, in-memory campaign exploration using the same terrain and hero art as Town.</summary>
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
    public GridPoint Position { get; private set; }
    public TownAlly Player { get; private set; }
    public Campaign Campaign { get; private set; }
    public CapabilitySet Held => permanent.Union(CapabilitySet.From(Campaign.Companions.Where(c => active.Contains(c.Id)).Select(c => c.Capability)));
    public IEnumerable<string> CollectedKeys => Campaign.Routes.Where(r => r.KeyId != null && resolved.Contains(r.Id)).Select(r => r.KeyId);
    public string Message { get; private set; } = "Generating campaign…";
    private CapabilitySet permanent = CapabilitySet.Empty;
    private readonly HashSet<string> claimed = new(), recruited = new(), active = new(), resolved = new();
    private readonly Dictionary<string, List<GameObject>> gateVisuals = new();
    private readonly Dictionary<GridPoint, GameObject> locationVisuals = new();
    private Dictionary<GridPoint, CampaignLocation> locations;
    private TileWorldCreator creator;
    private bool moving;
    private float nextMove;

    private void Start()
    {
        creator = Map.GetComponent<TileWorldCreator>();
        Campaign = CampaignGenerator.Generate(Map.Seed);
        var grid = Map.Generate(Campaign);
        locations = Campaign.Locations.ToDictionary(l => grid.Locations[l.Id]);
        Position = grid.PlayerStart;
        creator.OnBuildLayersComplete += TerrainReady;
        Map.BuildMeshes();
    }

    private void TerrainReady(TileWorldCreator _)
    {
        creator.OnBuildLayersComplete -= TerrainReady;
        foreach (var location in Campaign.Locations)
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
        IsReady = true;
        Message = "Explore locations. Enter / A claims their rewards (simulated encounters).";
        FollowCamera();
    }

    public Vector3 CellToWorld(GridPoint point) => Map.transform.position + new Vector3(point.X, point.Y, 0) * creator.twcAsset.cellSize;
    public Vector3 CellCenterToWorld(GridPoint point) => CellToWorld(point) + CellVisualOffset;
    private Vector3 CellVisualOffset => new Vector3(.5f, .5f, 0) * creator.twcAsset.cellSize;

    private void Update()
    {
        if (!IsReady || moving) return;
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

    public bool CanStep(GridPoint from, GridPoint to) => IsReady && Map.CurrentGrid.CanStep(from, to, Held, resolved);

    public bool TryMove(int dx, int dy)
    {
        if (!IsReady || moving) return false;
        var next = new GridPoint(Position.X + dx, Position.Y + dy);
        if (!Map.CurrentGrid.CanStep(Position, next, Held, resolved))
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
        RefreshGates();
        Message = locations.TryGetValue(Position, out var location) ? location.Id + " — " + location.Kind + " | Enter / A: claim rewards" : "";
        foreach (string id in OverworldMovement.Neighbors(Position).Select(Map.CurrentGrid.LockAt).Where(g => g != null).Select(g => g.RouteId).Distinct())
            if (!Campaign.Routes.First(r => r.Id == id).CanTraverse(Held, resolved)) Message += " | " + GateDescription(id);
        foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && Map.CurrentGrid.Locations[r.KeyLocationId].Equals(Position) && !resolved.Contains(r.Id)))
            Message += " | Enter / A: Collect " + route.KeyId;
        foreach (var route in Map.CurrentGrid.WarpsAt(Position)) Message += " | " + WarpLabel(route);
        StartCoroutine(Walk());
        return true;
    }

    private IEnumerator Walk()
    {
        moving = true;
        var from = Player.transform.position;
        var to = CellToWorld(Position);
        Vector3 direction = to - from;
        Player.VisualParent.transform.eulerAngles = new Vector3(0, 0, -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg);
        Player.HeroAnimator.PlayWalkAnimation();
        Player.TilemapPosition = new Vector3Int(Position.X, Position.Y, 0);
        float elapsed = 0;
        while (elapsed < .15f)
        {
            elapsed += Time.deltaTime;
            Player.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / .15f));
            yield return null;
        }
        Player.transform.position = to;
        Player.HeroAnimator.PlayIdleAnimation();
        moving = false;
    }

    private string GateDescription(string id)
    {
        var route = Campaign.Routes.First(r => r.Id == id);
        return route.GateHint;
    }

    private string WarpLabel(CampaignRoute route)
    {
        string destination = Map.CurrentGrid.Locations[route.From].Equals(Position) ? route.To : route.From;
        string region = Campaign.Locations.First(l => l.Id == destination).RegionId;
        return "Warp to biome " + Campaign.Regions.First(r => r.Id == region).Label +
            (route.CanTraverse(Held, resolved) ? "" : " | " + route.GateHint);
    }

    public bool Warp(string routeId)
    {
        if (!IsReady || moving) return false;
        if (!Map.CurrentGrid.TryWarp(routeId, Position, Held, resolved, out var destination))
        { Message = Map.CurrentGrid.WarpsAt(Position).FirstOrDefault(r => r.Id == routeId)?.GateHint ?? "Stand on a warp gate."; return false; }
        if (locationVisuals.TryGetValue(Position, out var previous)) previous.SetActive(true);
        Position = destination;
        if (locationVisuals.TryGetValue(Position, out var current)) current.SetActive(false);
        Player.transform.position = CellToWorld(Position);
        Player.TilemapPosition = new Vector3Int(Position.X, Position.Y, 0);
        RefreshGates(); FollowCamera();
        Message = "Warp complete. Choose a destination below to return.";
        return true;
    }

    public bool OpenShortcut()
    {
        if (!IsReady || moving || !locations.TryGetValue(Position, out var location)) return false;
        foreach (var route in Campaign.Routes)
            if (route.TryUnlock(location.Id, resolved)) { Message = "Opened shortcut: " + route.Id; RefreshGates(); return true; }
        return false;
    }

    public void ClaimRewards()
    {
        if (!IsReady || moving || !locations.TryGetValue(Position, out var location)) return;
        if (OpenShortcut()) return;
        var rewards = new List<string>();
        foreach (var route in Campaign.Routes)
            if (route.TryCollectKey(location.Id, resolved)) rewards.Add(route.KeyId + " (passage opened permanently)");
        foreach (var source in Campaign.Sources.Where(s => s.LocationId == location.Id && !claimed.Contains(s.Id)))
        {
            if (!source.Prerequisites.IsSatisfiedBy(Held)) continue;
            if (source.Capability.Kind() == CapabilityKind.Personal)
            {
                if (source.CompanionId == null) continue;
                recruited.Add(source.CompanionId);
            }
            else permanent = permanent.Union(CapabilitySet.Of(source.Capability));
            claimed.Add(source.Id);
            rewards.Add(source.Capability.ToString());
        }
        Message = rewards.Count == 0 ? "No eligible unclaimed rewards here." : "Acquired: " + string.Join(", ", rewards) + ". Equip companions at a town.";
        RefreshGates();
    }

    public bool ToggleCompanion(string id)
    {
        if (!IsReady || moving || !locations.TryGetValue(Position, out var location) || location.Kind != LocationKind.Town || !recruited.Contains(id)) return false;
        if (!active.Remove(id))
        {
            if (active.Count >= 3) { Message = "Three companion slots are full."; return false; }
            active.Add(id);
        }
        RefreshGates();
        return true;
    }

    private void RefreshGates()
    {
        foreach (var gate in Map.CurrentGrid.Locks)
            foreach (var visual in gateVisuals[gate.RouteId]) visual.SetActive(!Map.CurrentGrid.IsWalkable(gate.Cells[0], Held, resolved));
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
        GUILayout.Label("WASD / arrows / left stick: move   •   Enter / A: claim rewards");
        GUILayout.Label("Green: town   Red: dungeon   Gold: landmark   Purple: closed gate");
        GUILayout.Label(Message);
        if (IsReady)
        {
            GUILayout.Label("Biome: " + Map.CurrentGrid.BiomeAt(Position) + (Map.CurrentGrid.RequiresBoat(Position) ? " | Sailing" : "") +
                (Held.Contains(Capability.Boat) ? " | Boat acquired" : " | Water requires Boat"));
            GUILayout.Label("Capabilities: " + Held);
            GUILayout.Label("Keys: " + string.Join(", ", CollectedKeys));
            foreach (var route in Map.CurrentGrid.WarpsAt(Position))
                if (GUILayout.Button(WarpLabel(route))) Warp(route.Id);
            GUILayout.Label("Biomes: " + string.Join(" | ", Campaign.Regions.Select(r => r.Label + " " + Map.CurrentGrid.RegionBiomes[r.Id])));
            foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && Map.CurrentGrid.Locations[r.KeyLocationId].Equals(Position) && !resolved.Contains(r.Id)))
                if (GUILayout.Button("Collect " + route.KeyId)) ClaimRewards();
            foreach (var objective in Campaign.ReturnObjectives.Where(o => o.Required))
                GUILayout.Label("Required return: " + objective.RegionId + " | Requires " + objective.EnablingCapability + " | Reward " + objective.RewardCapability);
            foreach (var route in Campaign.Routes.Where(r => r.UnlockingEndpoint != null && Map.CurrentGrid.Locations[r.UnlockingEndpoint].Equals(Position) && !resolved.Contains(r.Id)))
                if (GUILayout.Button("Open shortcut")) OpenShortcut();
            if (locations.TryGetValue(Position, out var location) && location.Kind == LocationKind.Town)
                foreach (var companion in Campaign.Companions.Where(c => recruited.Contains(c.Id)))
                    if (GUILayout.Button((active.Contains(companion.Id) ? "Dismiss " : "Equip ") + companion.Id + " (" + companion.Capability + ")")) ToggleCompanion(companion.Id);
        }
        GUILayout.EndArea();
    }

    private void OnDestroy() { if (creator != null) creator.OnBuildLayersComplete -= TerrainReady; }
}
