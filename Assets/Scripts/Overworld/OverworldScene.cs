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
    // The *Track names are filled by the deferred web build, which ships music separately and clears the clips.
    [System.Serializable] public sealed class BiomeMusicEntry { public OverworldBiome Biome; public AudioClip Clip; [HideInInspector] public string ClipTrack; }
    [HideInInspector] public string OverworldMusicTrack;
    public CampaignOverworld Map;
    public TownAlly PlayerPrefab;
    public Camera ViewCamera;
    public AudioClip OverworldMusic;
    public BiomeMusicEntry[] BiomeMusic = System.Array.Empty<BiomeMusicEntry>();
    public GameObject TownMarker;
    public GameObject DungeonMarker;
    public GameObject LandmarkMarker;
    public GameObject GateMarker;
    public bool ShowLocationMarkers = true;
    public bool ShowSandboxControls = true;
    public Vector3 CameraOffset = new(0, -14, -20);
    private bool isReady;
    public bool IsReady { get => isReady; private set { if (isReady == value) return; isReady = value; HudRefresh.Request(HudChange.All); } }
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
    private string message = "Generating campaign…";
    public string Message
    {
        get => message;
        private set
        {
            if (message == value) return;
            message = value;
            messageChanged = true;
        }
    }
    private bool messageChanged;
    private void FlushMessage()
    {
        if (!messageChanged) return;
        messageChanged = false;
        GameMessages.Post(message, coalesce: true);
    }
    private HashSet<string> claimed => Context.Claimed;
    private HashSet<string> recruited => Context.Roster;
    private HashSet<string> active => Context.Active;
    private HashSet<string> resolved => Context.Resolved;
    private readonly Dictionary<string, List<GameObject>> gateVisuals = new();
    private readonly Dictionary<string, OverworldLockVisual> waystoneVisuals = new();
    private readonly Dictionary<GridPoint, GameObject> locationVisuals = new();
    private readonly Dictionary<string,GameObject> clearedVisuals = new();
    private Dictionary<GridPoint, CampaignLocation> locations;
    private List<BiomeRoadSigns.Sign> roadSigns = new();
    private TileWorldCreator creator;
    private OverworldTerrainCache terrainCache;
    private bool restoredTerrain;
    private bool movingValue;
    private bool moving { get => movingValue; set { if (movingValue == value) return; movingValue = value; HudRefresh.Request(HudChange.Travel); } }
    private float nextMove;
    private OverworldBiome? playingBiome;

    // Terrain is built around the player first; the rest streams in and follows this point.
    private Vector2 TerrainFocus => new Vector2(Position.X + .5f, Position.Y + .5f) * creator.twcAsset.cellSize;
    private System.Diagnostics.Stopwatch terrainTimer;
    private double gridMilliseconds, layoutMilliseconds;

    private void RefreshBiomeMusic()
    {
        // Called on every move, so it also keeps the terrain around the player built.
        TerrainStreaming.Focus(TerrainFocus);
        var biome = Map.CurrentGrid?.BiomeAt(Position) ?? OverworldBiome.Grassland;
        if (playingBiome == biome) return;
        playingBiome = biome;
        var music = BiomeMusic.FirstOrDefault(entry => entry.Biome == biome);
        var clip = music?.Clip ?? OverworldMusic;
        string track = !string.IsNullOrEmpty(music?.ClipTrack) ? music.ClipTrack : OverworldMusicTrack;
        if (clip != null || !string.IsNullOrEmpty(track)) Common.Instance.AudioManager?.PlayMusic(clip, track);
    }

    private void Start()
    {
        // Unlike town/dungeon scenes, the generated overworld has no authored EventSystem.
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var input = new GameObject("Overworld UI input", typeof(UnityEngine.EventSystems.EventSystem), typeof(MenuUIInputModule));
            input.transform.SetParent(transform, false);
        }
        creator = Map.GetComponent<TileWorldCreator>();
        var common = Common.Instance;
        if (common.CampaignContext == null) common.BeginSandbox(OverworldLaunch.TakeSeed(Map.Seed));
        Context = common.CampaignContext;
        gameObject.AddComponent<OverworldMenuManager>();
        ResourceHUD.Ensure(this);
        TravelPartyHud.Ensure(this);
        ScenePresentation.Ensure(this);
        var campaignHud = AuthoredUI.Require<CampaignHUD>(transform);
        campaignHud.Overworld=this;
        campaignHud.enabled=!Context.IsSandbox;
        Campaign = Context.Campaign;
        Map.Seed = Campaign.Seed;
        var gridTimer = System.Diagnostics.Stopwatch.StartNew();
        bool gridExisted = Context.IsGridGenerated;
        var grid = Context.Grid;
        gridMilliseconds = gridExisted ? 0 : gridTimer.Elapsed.TotalMilliseconds;
        gates = Context.Gates;
        locations = Campaign.Locations.Where(l => l.ParentTownId == null).ToDictionary(l => grid.Locations[l.Id]);
        var controls = gameObject.AddComponent<OverworldSandboxControls>();
        controls.Scene = this; controls.enabled = Context.IsSandbox && ShowSandboxControls;
        Map.IncludeSettlements = ShowLocationMarkers;
        terrainCache = common.OverworldTerrain;
        restoredTerrain = terrainCache.TryRestore(Context, Map, this);
        if (restoredTerrain) { TerrainReady(creator); return; }
        terrainTimer = System.Diagnostics.Stopwatch.StartNew();
        Map.Apply(grid, Held, resolved);
        layoutMilliseconds = terrainTimer.Elapsed.TotalMilliseconds;
        Map.TerrainBuilt += TerrainReady;
        TerrainStreaming.Begin(TerrainFocus);
        try { Map.BuildMeshes(); }
        catch { TerrainStreaming.End(); throw; }
    }

    private void TerrainReady(TileWorldCreator _)
    {
        TerrainStreaming.End();
        if (terrainTimer != null)
        {
            double total = terrainTimer.Elapsed.TotalMilliseconds;
            Debug.Log($"[Timing] Overworld terrain ready in {total:0} ms (grid {gridMilliseconds:0} ms before that; layout {layoutMilliseconds:0} ms, " +
                $"meshes {total - layoutMilliseconds:0} ms); {TerrainStreaming.Pending} chunk builds deferred.");
            terrainTimer = null;
        }
        else Debug.Log($"[Timing] Overworld terrain restored from cache (grid {gridMilliseconds:0} ms); {TerrainStreaming.Pending} chunk builds still pending.");
        Map.TerrainBuilt -= TerrainReady;
        RefreshBiomeMusic();
        roadSigns = BiomeRoadSigns.Plan(Map.CurrentGrid);
        if (!restoredTerrain) terrainCache.Store(Context, Map, this);
        foreach (var location in Campaign.Locations.Where(l => l.ParentTownId == null))
        {
            if (!ShowLocationMarkers) continue;
            var prefab = location.Kind == LocationKind.Town || location.Variant == DungeonVariant.Town ? TownMarker :
                location.Kind == LocationKind.StoryDungeon || location.Kind == LocationKind.RepeatableDungeon || location.Kind == LocationKind.FinalDungeon
                    ? DungeonMarker : LandmarkMarker;
            var marker = Instantiate(prefab, CellCenterToWorld(Map.CurrentGrid.Locations[location.Id]), Quaternion.identity, transform);
            marker.name = location.Id;
            var cell = Map.CurrentGrid.Locations[location.Id];
            if(creator.twcAsset.mapBuildLayers.OfType<OverworldSettlementLayer>().Any(l=>l.active))
                foreach(var renderer in marker.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            else if (location.Kind != LocationKind.Town && location.Variant != DungeonVariant.Town) BuildLocationFootprint(marker, location.Kind, cell);
            BiomeModel.ApplyAll(marker, OverworldCosmetics.Biome(Map.CurrentGrid, cell.X, cell.Y));
            locationVisuals.Add(cell, marker);
            if(location.IsDungeon && DioramaCatalog.Load() is { } catalog)
            {
                var cleared=new GameObject("Cleared "+location.Id);
                cleared.transform.SetParent(transform,false);
                cleared.transform.position=CellCenterToWorld(cell);
                var batch=new EnvironmentBatch(cleared.transform);
                catalog.Add(batch,"CastleFlag",OverworldCosmetics.Biome(Map.CurrentGrid,cell.X,cell.Y),
                    new Vector3(creator.twcAsset.cellSize*.65f,0,-.1f),variation:.4f);
                batch.Finish();cleared.SetActive(Context.Completed.Contains(location.Id));
                clearedVisuals[location.Id]=cleared;
            }
        }
        foreach (var gate in Map.CurrentGrid.Locks)
        {
            var markers = new List<GameObject>();
            foreach (var cell in gate.Cells.Where(c => !Map.CurrentGrid.RequiresBoat(c)).Take(1))
            {
                if (Map.CurrentGrid.RequiresBoat(cell)) continue;
                var marker = Instantiate(GateMarker, CellCenterToWorld(cell), Quaternion.identity, transform);
                var dry=gate.Cells.Where(c=>!Map.CurrentGrid.RequiresBoat(c)).ToArray();
                marker.transform.position=dry.Select(CellCenterToWorld).Aggregate(Vector3.zero,(a,b)=>a+b)/dry.Length;
                marker.transform.rotation=OverworldLockVisual.GateRotation(Map.CurrentGrid,gate);
                marker.transform.localScale=Vector3.one*creator.twcAsset.cellSize;
                BiomeModel.ApplyAll(marker, OverworldCosmetics.Biome(Map.CurrentGrid, cell.X, cell.Y));
                var route = Campaign.Routes.First(r => r.Id == gate.RouteId);
                OverworldLockVisual.Build(marker,route,OverworldCosmetics.Biome(Map.CurrentGrid,cell.X,cell.Y));
                marker.name = (route.ShortcutKind == ShortcutKind.None ? "Gate " : "Shortcut ") + gate.RouteId;
                markers.Add(marker);
            }
            gateVisuals.Add(gate.RouteId, markers);
        }
        var protagonist = Context.IsSandbox ? PlayerPrefab : CampaignParty.Resolve(Common.Instance.GameSaveData.ProtagonistId,TownSceneLoader.Default) ?? PlayerPrefab;
        Player = Instantiate(protagonist, CellToWorld(Position), Quaternion.identity, transform);
        foreach (var endpoint in Map.CurrentGrid.Warps.SelectMany(r => new[] { r.From, r.To }).Distinct())
        {
            var pad = new GameObject();
            pad.name = "Warp gate " + endpoint;
            pad.transform.SetParent(transform);
            var cell = Map.CurrentGrid.Locations[endpoint];
            pad.transform.position = CellCenterToWorld(cell) + Vector3.up * creator.twcAsset.cellSize * .35f;
            pad.transform.localScale = Vector3.one * creator.twcAsset.cellSize * .7f;
            var route = Campaign.Routes.First(r => r.IsWarp && (r.From == endpoint || r.To == endpoint));
            OverworldLockVisual.Build(pad, route, OverworldCosmetics.Biome(Map.CurrentGrid, cell.X, cell.Y));
            waystoneVisuals[endpoint] = pad.GetComponent<OverworldLockVisual>();
        }
        Player.name = "Campaign Player";
        Player.SetFacing(Facing.Down);
        Player.SetToPlayer();
        Player.HeroAnimator?.PlayIdleAnimation();
        Player.TilemapPosition = new Vector3Int(Position.X, Position.Y, 0);
        walkHistory.Add(Position);
        gameObject.AddComponent<OverworldPickups>().Initialize(this);
        RebuildFollowers();
        ScenePresentation.RegisterWorld(creator.worldObject.transform);
        foreach (var data in Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData)
        {
            var live = data.AllyId == Common.Instance.GameSaveData.ProtagonistId ? Player : followers.FirstOrDefault(h => h.Id == data.AllyId);
            if (live != null) OverworldPartyMenuContext.RestoreHero(live, data);
        }
        RefreshGates();
        Message = "";
        FollowCamera();
        // Start the reveal only after followers, saved stats and camera placement
        // are complete, so their setup cannot consume part of the visible fade.
        IsReady = true;
        Common.Instance.Travel.SceneReady();
        Common.Instance.ScreenTransition.DoOpen();
    }

    private void BuildLocationFootprint(GameObject marker, LocationKind kind, GridPoint cell)
    {
        float size = creator.twcAsset.cellSize;
        var kit = EnvironmentKit.Load();
        var material = kit != null ? kit.BuildingMaterial(OverworldCosmetics.Biome(Map.CurrentGrid, cell.X, cell.Y)) : null;
        var meshes=marker.AddComponent<EnvironmentMeshOwner>();
        var baseObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseObject.name = "Point of interest plaza";
        baseObject.transform.SetParent(marker.transform, false);
        baseObject.transform.localPosition = new Vector3(0, 0, -.025f * size);
        baseObject.transform.localScale = new Vector3(3.5f * size, 3.5f * size, .04f * size);
        Destroy(baseObject.GetComponent<Collider>());
        if (kit != null)
        {
            baseObject.GetComponent<Renderer>().sharedMaterial = kit.Paving;
            var filter=baseObject.GetComponent<MeshFilter>();
            var mesh=Instantiate(filter.sharedMesh);mesh.name="Location paving";
            // Match the road's world projection instead of stretching a whole atlas over a cube.
            mesh.uv=mesh.vertices.Select(v=>{var p=baseObject.transform.TransformPoint(v)/2.5f;return new Vector2(p.x,p.y);}).ToArray();
            filter.sharedMesh=mesh;meshes.Meshes.Add(mesh);
        }
        Mesh stonePost=null;
        foreach (var offset in new[] { new Vector2(-1.5f, -1.5f), new Vector2(1.5f, -1.5f), new Vector2(-1.5f, 1.5f), new Vector2(1.5f, 1.5f) })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = kind + " perimeter post";
            post.transform.SetParent(marker.transform, false);
            post.transform.localPosition = new Vector3(offset.x, offset.y, -.25f) * size;
            post.transform.localScale = new Vector3(.34f, .34f, .65f) * size;
            Destroy(post.GetComponent<Collider>());
            if (material != null)
            {
                post.GetComponent<Renderer>().sharedMaterial = material;
                var filter=post.GetComponent<MeshFilter>();
                if(stonePost==null)
                {
                    stonePost=Instantiate(filter.sharedMesh);stonePost.name="Location stone post";
                    // Cell (3,0) is masonry; pad its edges against atlas filtering bleed.
                    stonePost.uv=stonePost.uv.Select(uv=>new Vector2((3+.065f+uv.x*.87f)/4,(.065f+uv.y*.87f)/4)).ToArray();
                    meshes.Meshes.Add(stonePost);
                }
                filter.sharedMesh=stonePost;
            }
        }
    }

    public Vector3 CellToWorld(GridPoint point) => Map.transform.position + new Vector3(point.X, point.Y, 0) * creator.twcAsset.cellSize;
    public Vector3 CellCenterToWorld(GridPoint point) => CellToWorld(point) + CellVisualOffset;
    internal IEnumerable<Renderer> GateRenderers(string routeId)
    {
        if (gateVisuals.TryGetValue(routeId, out var visuals))
            return visuals.Where(visual => visual != null && visual.activeInHierarchy)
                .SelectMany(visual => visual.GetComponentsInChildren<Renderer>()).Where(renderer => renderer.enabled);
        var route = Campaign.Routes.FirstOrDefault(candidate => candidate.Id == routeId && candidate.IsWarp);
        return route == null ? Enumerable.Empty<Renderer>() : new[] { route.From, route.To }
            .Where(endpoint => waystoneVisuals.ContainsKey(endpoint)).SelectMany(endpoint => waystoneVisuals[endpoint].GetComponentsInChildren<Renderer>());
    }
    private Vector3 CellVisualOffset => new Vector3(.5f, .5f, 0) * creator.twcAsset.cellSize;

    private void Update()
    {
        // Sample while interpolating too, so a release is latched before arrival.
        if (BlocksWalkInput()) { StopHeldWalk(); return; }
        Vector2 move = ReadWalkInput();
        if (move.sqrMagnitude < .1f) StopHeldWalk();
        if (moving) return;
        if (MenuUIInputModule.Active?.UI.Submit.WasPressedThisFrame() == true)
        { ClaimRewards(); return; }
        if (move.sqrMagnitude < .1f || Time.time < nextMove) return;
        nextMove = Time.time + .16f;
        TryMoveStep(Mathf.Abs(move.x) > .3f ? System.Math.Sign(move.x) : 0, Mathf.Abs(move.y) > .3f ? System.Math.Sign(move.y) : 0, true);
    }

    private bool heldWalk;
    private bool BlocksWalkInput() => !isActiveAndEnabled || !IsReady || AutoplayRunner.BlocksPlayerInput ||
        Common.Instance.Travel.IsTransitioning || Common.Instance.GlobalSettings.IsOpen ||
        MenuUIInputModule.Active?.HasDialog == true || MenuUIInputModule.Active?.InputConsumed == true || ControlBindings.SuppressInput;

    private static Vector2 ReadWalkInput()
    {
        return MenuUIInputModule.Active?.UI.Navigate.ReadValue<Vector2>() ?? Vector2.zero;
    }

    private void StopHeldWalk()
    {
        heldWalk = false;
        Player?.HeroAnimator?.StopWalkContinuation();
        foreach (var ally in followers) ally?.HeroAnimator?.StopWalkContinuation();
    }

    public bool CanStep(GridPoint from, GridPoint to) => IsReady && Map.CurrentGrid.CanStep(from, to, Held, resolved) && OverworldMovement.CanStep(from, to, cell => gates.IsWalkable(cell, Held));

    public bool TryMove(int dx, int dy) => TryMoveStep(dx, dy, false);

    private bool TryMoveStep(int dx, int dy, bool manual)
    {
        if (!manual) StopHeldWalk();
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || GetComponent<OverworldMenuManager>().Opened) return false;
        if (Context.Location?.Kind == LocationKind.Town && !Context.CanLeaveTown(Context.Location.Id))
        {
            StopHeldWalk();
            Message = "Clear the dungeon inside town to unlock the town gate.";
            if (manual) AudioManager.Instance?.PlayCue(SoundCue.Denied);
            return false;
        }
        var next = new GridPoint(Position.X + dx, Position.Y + dy);
        if (!CanStep(Position, next))
        {
            StopHeldWalk();
            var gate = Map.CurrentGrid.LockAt(next);
            Message = Map.CurrentGrid.RequiresBoat(next) && !Held.Contains(Capability.Boat) ? "You need a boat to sail." :
                gate == null ? "Blocked." : GateDescription(gate.RouteId);
            if (manual) AudioManager.Instance?.PlayCue(SoundCue.Denied);
            return false;
        }
        Position = next;
        GetComponent<OverworldPickups>()?.TryCollect();
        RefreshBiomeMusic();
        var crossed = Map.CurrentGrid.LockAt(next);
        if (crossed != null && Campaign.Routes.First(r => r.Id == crossed.RouteId).Latches) resolved.Add(crossed.RouteId);
        SaveProgress();
        RefreshGates();
        Message = locations.TryGetValue(Position, out var location) ? (location.Kind == LocationKind.Town ? Context.GetTownDisplayName(location.Id) : location.Id) + " — " + location.Kind : "";
        foreach (string id in OverworldMovement.Neighbors(Position).Select(Map.CurrentGrid.LockAt).Where(g => g != null).Select(g => g.RouteId).Distinct())
            if (gates.NeedsOpening(Campaign.Routes.First(r => r.Id == id))) Message += " | " + GateDescription(id);
        foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && Map.CurrentGrid.Locations[r.KeyLocationId].Equals(Position) && !gates.HasKey(r)))
            Message += route.KeyCondition == KeyAcquisition.AtLocation ? " | " + route.KeyLabel + " is here." : " | Clear this encounter to earn " + route.KeyLabel;
        foreach (var route in Map.CurrentGrid.WarpsAt(Position)) Message += " | " + WarpLabel(route);
        if (NearbySign() != null) Message += " | Road sign nearby.";
        walkHistory.Add(Position);
        if (walkHistory.Count > 4) walkHistory.RemoveAt(0);
        heldWalk = manual;
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
                party[i].HeroAnimator?.BeginWalk(heldWalk);
            }
            else party[i].HeroAnimator?.StopWalkContinuation();
            party[i].TilemapPosition = new Vector3Int(cell.X, cell.Y, 0);
        }
        RefreshLocationMarkers();
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
        {
            for (int i = 0; i < party.Length; i++)
            {
                party[i].transform.position = to[i];
                party[i].HeroAnimator?.PlayIdleAnimation();
            }
            moving = false;
            yield break;
        }
        float elapsed = 0;
        while (elapsed < .15f)
        {
            if (heldWalk && (BlocksWalkInput() || ReadWalkInput().sqrMagnitude < .1f)) StopHeldWalk();
            elapsed += Time.deltaTime;
            for (int i = 0; i < party.Length; i++)
                party[i].transform.position = Vector3.Lerp(from[i], to[i], Mathf.Clamp01(elapsed / .15f));
            yield return null;
        }
        if (heldWalk && (BlocksWalkInput() || ReadWalkInput().sqrMagnitude < .1f)) StopHeldWalk();
        for (int i = 0; i < party.Length; i++)
        {
            party[i].transform.position = to[i];
            party[i].HeroAnimator?.CompleteWalk(heldWalk);
        }
        moving = false;
    }

    private string GateDescription(string id)
    {
        var route = Campaign.Routes.First(r => r.Id == id);
        return gates.Hint(route, Held);
    }

    public string WarpLabel(CampaignRoute route)
    {
        string destination = Map.CurrentGrid.Locations[route.From].Equals(Position) ? route.To : route.From;
        var target = Campaign.Locations.First(l => l.Id == destination);
        string region = target.RegionId;
        return "Warp to " + (target.Kind==LocationKind.Town?Context.GetTownDisplayName(destination):Campaign.Regions.First(r => r.Id == region).DisplayName) +
            (route.CanTraverse(Held, resolved) ? "" : " | " + gates.Hint(route, Held));
    }

    public bool Warp(string routeId)
    {
        StopHeldWalk();
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || GetComponent<OverworldMenuManager>().Opened) return false;
        if (!Map.CurrentGrid.TryWarp(routeId, Position, Held, resolved, out var destination))
        {
            var blocked = Map.CurrentGrid.WarpsAt(Position).FirstOrDefault(r => r.Id == routeId);
            Message = blocked != null ? gates.Hint(blocked, Held) : "Stand on a warp gate.";
            AudioManager.Instance?.PlayCue(SoundCue.Denied);
            return false;
        }
        Position = destination;
        RefreshBiomeMusic();
        Player.transform.position = CellToWorld(Position);
        Player.TilemapPosition = new Vector3Int(Position.X, Position.Y, 0);
        walkHistory.Clear();
        walkHistory.Add(Position);
        PlaceFollowers();
        RefreshGates(); FollowCamera();
        SaveProgress();
        Message = "Warp complete.";
        AudioManager.Instance?.PlayCue(SoundCue.Teleport);
        return true;
    }

    public LockInteractionSession BeginGateInteraction(string routeId)
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return null;
        var context = Context;
        return gates.BeginInteraction(routeId, () => Position, () => Held,
            () => isActiveAndEnabled && IsReady && Common.Instance.CampaignContext == context && !moving && !Common.Instance.Travel.IsTransitioning);
    }

    public LockOutcome AttemptGate(LockInteractionSession session, LockAction action, bool feedback = true)
    {
        var outcome = session.Attempt(action);
        if (outcome == LockOutcome.Opened)
        {
            Message = "The way is now open!";
            RefreshGates();
            SaveProgress();
            AudioManager.Instance?.PlayCue(SoundCue.GateUnlock);
        }
        else if (feedback) AudioManager.Instance?.PlayCue(outcome == LockOutcome.Progress ? SoundCue.Status : SoundCue.Denied);
        return outcome;
    }

    public bool OpenGate(string routeId = null, bool announceLocked = true)
    {
        StopHeldWalk();
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return false;
        var route = gates.Nearby(Position).FirstOrDefault(r => (routeId == null || r.Id == routeId) && gates.NeedsOpening(r) && r.ShortcutKind != ShortcutKind.FarSide);
        return route != null && GetComponent<OverworldMenuManager>().ChooseGate(route);
    }

    public bool OpenShortcut()
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || GetComponent<OverworldMenuManager>().Opened || !locations.TryGetValue(Position, out var location)) return false;
        foreach (var route in Campaign.Routes)
            if (route.TryUnlock(location.Id, resolved))
            {
                Message = "Opened the shortcut to " + route.Id;
                RefreshGates();
                SaveProgress();
                AudioManager.Instance?.PlayCue(SoundCue.GateUnlock);
                return true;
            }
        return false;
    }

    public void ClaimRewards() => ClaimRewards(true);

    internal BiomeRoadSigns.Sign NearbySign() => roadSigns
        .Where(s => Mathf.Max(Mathf.Abs(s.Verge.x - Position.X), Mathf.Abs(s.Verge.y - Position.Y)) <= 1)
        .OrderBy(s => (s.Verge - new Vector2Int(Position.X, Position.Y)).sqrMagnitude).ThenBy(s => s.Verge.x).ThenBy(s => s.Verge.y).FirstOrDefault();

    internal bool ReadSign()
    {
        var sign = NearbySign();
        if (sign == null) return false;
        Message = BiomeRoadSigns.Directions(Context, sign);
        var dialog = Common.Instance.MessageDialog;
        dialog.PromptText.text = GameplayText.Format(Message);
        dialog.SetPortrait(null);
        GetComponent<OverworldMenuManager>().Open(dialog);
        return true;
    }

    public void ClaimRewards(bool announceLocked)
    {
        StopHeldWalk();
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return;
        if (GetComponent<OverworldMenuManager>().Opened) return;
        if (GetComponent<OverworldPickups>()?.TryCollect() == true) return;
        if (!locations.TryGetValue(Position, out var location))
        {
            if (announceLocked && ReadSign()) return;
            if (announceLocked && !OpenGate()) AudioManager.Instance?.PlayCue(SoundCue.Denied);
            return;
        }
        if (OpenShortcut()) return;
        if (!Context.IsSandbox && (announceLocked ? GetComponent<OverworldMenuManager>().ConfirmEntry() : Common.Instance.Travel.EnterLocation())) return;
        var rewards = new List<string>();
        var acquiredKeys = new List<string>();
        var acquiredCapabilities = new List<string>();
        foreach (var route in Campaign.Routes)
            if (gates.CollectKey(route, location.Id))
            {
                rewards.Add(route.KeyLabel + ". Use it at the gate.");
                acquiredKeys.Add(route.KeyId);
            }
        foreach (var source in Campaign.Sources.Where(s => s.LocationId == location.Id && !claimed.Contains(s.Id)))
        {
            if (!Context.Claim(source.Id)) continue;
            rewards.Add(CampaignRewardNames.Name(Context, source.Capability));
            acquiredCapabilities.Add(CampaignTravelService.DescribeCapabilityReward(Context, source));
        }
        SaveProgress();
        // Claim local rewards first; once collected, the same interaction key exposes travel.
        if (rewards.Count == 0 && announceLocked && GetComponent<OverworldMenuManager>().ChooseTravel()) return;
        Message = rewards.Count == 0 ? "No eligible unclaimed rewards here." : "Acquired " + string.Join(", ", rewards) + ". Equip companions at a town.";
        if (rewards.Count > 0) AudioManager.Instance?.PlayCue(SoundCue.Pickup);
        RefreshGates();
        Common.Instance.Travel.ShowRewardsAcquired(acquiredKeys.Distinct().ToArray(), acquiredCapabilities.ToArray());
        if (rewards.Count == 0 && announceLocked && !OpenGate()) AudioManager.Instance?.PlayCue(SoundCue.Denied);
    }

    public bool ToggleCompanion(string id)
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || GetComponent<OverworldMenuManager>().Opened || !Context.IsSandbox || !locations.TryGetValue(Position, out var location) || location.Kind != LocationKind.Town || !recruited.Contains(id)) return false;
        var ids = active.Contains(id) ? active.Where(x => x != id).ToArray() : active.Concat(new[] { id }).ToArray();
        if (!Context.SetParty(ids)) return false;
        GameMessages.Post("Travelling party updated.");
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
        HudRefresh.Request(HudChange.Party | HudChange.Resources | HudChange.Travel);
        if (!Context.IsSandbox) SaveSystem.Capture(Common.Instance);
    }
    public bool SimulateDungeonVictory()
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || GetComponent<OverworldMenuManager>().Opened || !Context.IsSandbox) return false;
        var interior = Context.Campaign.Locations.FirstOrDefault(l => l.ParentTownId != null && l.ParentTownId == Context.Location?.Id);
        if (!(interior != null ? Context.BeginTownDungeon(interior.Id) : Context.BeginDungeon())) return false;
        var previousOpenedGates = Context.Opened.ToArray();
        Context.CompleteDungeon(true); RefreshGates();
        if (Context.Opened.Except(previousOpenedGates).Any()) AudioManager.Instance?.PlayCue(SoundCue.GateUnlock);
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
            // Static companion prefabs have no animation component.
            followers[i].HeroAnimator?.PlayIdleAnimation();
        }
        RefreshLocationMarkers();
    }

    private void RefreshLocationMarkers()
    {
        foreach (var marker in locationVisuals)
            marker.Value.SetActive(true);
        foreach(var marker in clearedVisuals)marker.Value.SetActive(Context.Completed.Contains(marker.Key));
    }

    private void RefreshGates()
    {
        RefreshLocationMarkers();
        foreach (var waystone in waystoneVisuals)
            waystone.Value.SetOpen(Campaign.Routes.Any(route => route.IsWarp && (route.From == waystone.Key || route.To == waystone.Key) && !gates.NeedsOpening(route)));
        foreach (var gate in Map.CurrentGrid.Locks)
            foreach (var visual in gateVisuals[gate.RouteId])
            {
                bool open = gates.IsWalkable(gate.Cells[0], Held);
                if (visual.TryGetComponent<OverworldLockVisual>(out var obstacle)) obstacle.SetOpen(open);
                else visual.SetActive(!open);
            }
    }

    public bool RelocateSandbox(GridPoint destination)
    {
        if (!IsReady || moving || !Context.IsSandbox || GetComponent<OverworldMenuManager>().Opened ||
            !gates.IsWalkable(destination, Held)) return false;
        StopHeldWalk();
        Position = destination;
        Player.transform.position = CellToWorld(destination);
        Player.TilemapPosition = new Vector3Int(destination.X, destination.Y, 0);
        walkHistory.Clear(); walkHistory.Add(destination); PlaceFollowers();
        RefreshGates(); RefreshBiomeMusic(); FollowCamera();
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
        HudRefresh.Request(HudChange.All);
        return true;
    }

    private void LateUpdate()
    {
        FlushMessage();
        if (!IsReady) return;
        CameraZoom.Update(ViewCamera);
        FollowCamera();
    }
    private void FollowCamera()
    {
        if (ScreenTransition.ControlsCamera(ViewCamera)) return;
        var target = Player.transform.position + CellVisualOffset;
        ViewCamera.transform.position = target + CameraOffset;
        ViewCamera.transform.LookAt(target);
    }

    private void OnGUI()
    {
        GameUISkin.UseLegacySkin();
        if (Context == null || !Context.IsSandbox || !ShowSandboxControls || !IsReady || GetComponent<OverworldMenuManager>().Opened) return;
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !AutoplayRunner.BlocksPlayerInput;
        GameUISkin.LegacyBeginArea(new Rect(16, 16, 580, 430));
        GUILayout.Label("CAMPAIGN OVERWORLD  |  Seed " + Map.Seed);
        GUILayout.Label("Green: town   Red: dungeon   Gold: landmark   Purple: closed gate");
        GUILayout.Label(InputPrompts.Format(Message));
        if (IsReady)
        {
            GUILayout.Label("Biome: " + Map.CurrentGrid.BiomeAt(Position) + (Map.CurrentGrid.RequiresBoat(Position) ? " | Sailing" : "") +
                (Held.Contains(Capability.Boat) ? " | Boat acquired" : " | Water requires Boat"));
            if (Context.State.Finished) GUILayout.Label("Campaign complete!");
            GUILayout.Label("Capabilities: " + Held);
            GUILayout.Label("Keys: " + string.Join(", ", CollectedKeys.Select(Campaign.KeyLabel)));
            foreach (var route in gates.Nearby(Position).Where(gates.NeedsOpening))
                if (GameUISkin.LegacyButton(gates.Hint(route, Held))) OpenGate(route.Id);
            foreach (var route in Map.CurrentGrid.WarpsAt(Position))
                if (GameUISkin.LegacyButton(WarpLabel(route))) Warp(route.Id);
            GUILayout.Label("Biomes: " + string.Join(" | ", Campaign.Regions.Select(r => r.DisplayName + " " + Map.CurrentGrid.RegionBiomes[r.Id])));
            foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && Map.CurrentGrid.Locations[r.KeyLocationId].Equals(Position) && !gates.HasKey(r)))
                if (GameUISkin.LegacyButton("Collect " + route.KeyLabel)) ClaimRewards();
            foreach (var objective in Campaign.ReturnObjectives.Where(o => o.Required))
                GUILayout.Label("Required return: " + objective.RegionId + " | Requires " + objective.EnablingCapability + " | Reward " + objective.RewardCapability);
            foreach (var route in Campaign.Routes.Where(r => r.UnlockingEndpoint != null && Map.CurrentGrid.Locations[r.UnlockingEndpoint].Equals(Position) && !resolved.Contains(r.Id)))
                if (GameUISkin.LegacyButton("Open shortcut")) OpenShortcut();
        }
        GUILayout.EndArea();
        GUI.enabled = previousEnabled;
    }

    private void OnDisable() { StopHeldWalk(); terrainCache?.Hide(this); }
    private void OnDestroy() { if (Map != null) Map.TerrainBuilt -= TerrainReady; }
}
