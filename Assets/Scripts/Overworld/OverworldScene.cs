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
    [System.Serializable] public sealed class BiomeMusicEntry { public OverworldBiome Biome; public AudioClip Clip; }
    public CampaignOverworld Map;
    public TownAlly PlayerPrefab;
    public Camera ViewCamera;
    public AudioClip OverworldMusic;
    public BiomeMusicEntry[] BiomeMusic = System.Array.Empty<BiomeMusicEntry>();
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
        GameMessages.Post(InputPrompts.Format(message), coalesce: true);
    }
    private HashSet<string> claimed => Context.Claimed;
    private HashSet<string> recruited => Context.Roster;
    private HashSet<string> active => Context.Active;
    private HashSet<string> resolved => Context.Resolved;
    private readonly Dictionary<string, List<GameObject>> gateVisuals = new();
    private readonly Dictionary<GridPoint, GameObject> locationVisuals = new();
    private Dictionary<GridPoint, CampaignLocation> locations;
    private TileWorldCreator creator;
    private OverworldTerrainCache terrainCache;
    private bool restoredTerrain;
    private bool moving;
    private float nextMove;
    private OverworldBiome? playingBiome;

    private void RefreshBiomeMusic()
    {
        var biome = Map.CurrentGrid?.BiomeAt(Position) ?? OverworldBiome.Grassland;
        if (playingBiome == biome) return;
        playingBiome = biome;
        var clip = BiomeMusic.FirstOrDefault(entry => entry.Biome == biome)?.Clip ?? OverworldMusic;
        if (clip != null) Common.Instance.AudioManager?.PlayMusic(clip);
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
        ScenePresentation.Ensure(this);
        var campaignHud = AuthoredUI.Require<CampaignHUD>(transform);
        campaignHud.Overworld=this;
        campaignHud.enabled=!Context.IsSandbox;
        transform.Find("Campaign HUD").gameObject.SetActive(!Context.IsSandbox);
        Campaign = Context.Campaign;
        Map.Seed = Campaign.Seed;
        var grid = Context.Grid;
        gates = Context.Gates;
        locations = Campaign.Locations.Where(l => l.ParentTownId == null).ToDictionary(l => grid.Locations[l.Id]);
        var controls = gameObject.AddComponent<OverworldSandboxControls>();
        controls.Scene = this; controls.enabled = Context.IsSandbox;
        terrainCache = common.OverworldTerrain;
        restoredTerrain = terrainCache.TryRestore(Context, Map, this);
        if (restoredTerrain) { TerrainReady(creator); return; }
        Map.Apply(grid, Held, resolved);
        Map.TerrainBuilt += TerrainReady;
        Map.BuildMeshes();
    }

    private void TerrainReady(TileWorldCreator _)
    {
        Map.TerrainBuilt -= TerrainReady;
        RefreshBiomeMusic();
        if (!restoredTerrain) terrainCache.Store(Context, Map, this);
        foreach (var location in Campaign.Locations.Where(l => l.ParentTownId == null))
        {
            var prefab = location.Kind == LocationKind.Town ? TownMarker :
                location.Kind == LocationKind.StoryDungeon || location.Kind == LocationKind.RepeatableDungeon || location.Kind == LocationKind.FinalDungeon
                    ? DungeonMarker : LandmarkMarker;
            var marker = Instantiate(prefab, CellCenterToWorld(Map.CurrentGrid.Locations[location.Id]), Quaternion.identity, transform);
            marker.name = location.Id;
            var cell = Map.CurrentGrid.Locations[location.Id];
            if (location.Kind != LocationKind.Town) BuildLocationFootprint(marker, location.Kind, cell);
            BiomeModel.ApplyAll(marker, OverworldCosmetics.Biome(Map.CurrentGrid, cell.X, cell.Y));
            locationVisuals.Add(cell, marker);
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
                float dx=dry.Max(c=>c.X)-dry.Min(c=>c.X),dy=dry.Max(c=>c.Y)-dry.Min(c=>c.Y);
                marker.transform.rotation=Quaternion.Euler(0,0,dx>dy?90:0);
                marker.transform.localScale=Vector3.one*creator.twcAsset.cellSize;
                BiomeModel.ApplyAll(marker, OverworldCosmetics.Biome(Map.CurrentGrid, cell.X, cell.Y));
                var route = Campaign.Routes.First(r => r.Id == gate.RouteId);
                marker.name = (route.ShortcutKind == ShortcutKind.None ? "Gate " : "Shortcut ") + gate.RouteId;
                if (route.ShortcutKind != ShortcutKind.None)
                    foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
                    {
                        var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                        Color color = route.ShortcutKind == ShortcutKind.FarSide || route.ShortcutKind == ShortcutKind.Keyed ? Color.cyan : Color.yellow;
                        color=Color.Lerp(Color.white,color,.3f);
                        properties.SetColor("_Color", color); properties.SetColor("_BaseColor", color); renderer.SetPropertyBlock(properties);
                    }
                markers.Add(marker);
            }
            gateVisuals.Add(gate.RouteId, markers);
        }
        var protagonist = Context.IsSandbox ? PlayerPrefab : CampaignParty.Resolve(Common.Instance.GameSaveData.ProtagonistId,TownSceneLoader.Default) ?? PlayerPrefab;
        Player = Instantiate(protagonist, CellToWorld(Position), Quaternion.identity, transform);
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
        Player.HeroAnimator?.PlayIdleAnimation();
        Player.TilemapPosition = new Vector3Int(Position.X, Position.Y, 0);
        walkHistory.Add(Position);
        IsReady = true;
        Common.Instance.Travel.SceneReady();
        Common.Instance.ScreenTransition.DoOpen();
        RebuildFollowers();
        ScenePresentation.RegisterWorld(creator.worldObject.transform);
        foreach (var data in Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData)
        {
            var live = data.AllyId == Common.Instance.GameSaveData.ProtagonistId ? Player : followers.FirstOrDefault(h => h.Id == data.AllyId);
            if (live != null) OverworldPartyMenuContext.RestoreHero(live, data);
        }
        RefreshGates();
        Message = "{Interact}: claim location rewards or use a key at a gate.";
        FollowCamera();
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
    private Vector3 CellVisualOffset => new Vector3(.5f, .5f, 0) * creator.twcAsset.cellSize;

    private void Update()
    {
        // Sample while interpolating too, so a release is latched before arrival.
        if (BlocksWalkInput()) { StopHeldWalk(); return; }
        Vector2 move = ReadWalkInput();
        if (move.sqrMagnitude < .1f) StopHeldWalk();
        if (moving) return;
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        if (keyboard?.enterKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true)
        { ClaimRewards(); return; }
        if (move.sqrMagnitude < .1f || Time.time < nextMove) return;
        nextMove = Time.time + .16f;
        TryMoveStep(Mathf.Abs(move.x) > .3f ? System.Math.Sign(move.x) : 0, Mathf.Abs(move.y) > .3f ? System.Math.Sign(move.y) : 0, true);
    }

    private bool heldWalk;
    private bool BlocksWalkInput() => !isActiveAndEnabled || !IsReady || AutoplayRunner.BlocksPlayerInput ||
        Common.Instance.Travel.IsTransitioning || Common.Instance.GlobalSettings.IsOpen ||
        MenuUIInputModule.Active?.HasDialog == true || MenuUIInputModule.Active?.InputConsumed == true;

    private static Vector2 ReadWalkInput()
    {
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        Vector2 move = pad == null ? Vector2.zero : pad.dpad.ReadValue() + pad.leftStick.ReadValue();
        if (keyboard != null)
        {
            move.x += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            move.y += (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
        }
        return move;
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
        { StopHeldWalk(); Message = "Clear the dungeon inside town to unlock the town gate."; return false; }
        var next = new GridPoint(Position.X + dx, Position.Y + dy);
        if (!CanStep(Position, next))
        {
            StopHeldWalk();
            var gate = Map.CurrentGrid.LockAt(next);
            Message = Map.CurrentGrid.RequiresBoat(next) && !Held.Contains(Capability.Boat) ? "Requires: Boat to sail." :
                gate == null ? "Blocked." : GateDescription(gate.RouteId);
            return false;
        }
        Position = next;
        RefreshBiomeMusic();
        var crossed = Map.CurrentGrid.LockAt(next);
        if (crossed != null && Campaign.Routes.First(r => r.Id == crossed.RouteId).Latches) resolved.Add(crossed.RouteId);
        SaveProgress();
        RefreshGates();
        Message = locations.TryGetValue(Position, out var location) ? (location.Kind == LocationKind.Town ? Context.GetTownDisplayName(location.Id) : location.Id) + " — " + location.Kind + " | {Interact}: interact" : "";
        foreach (string id in OverworldMovement.Neighbors(Position).Select(Map.CurrentGrid.LockAt).Where(g => g != null).Select(g => g.RouteId).Distinct())
            if (gates.NeedsOpening(Campaign.Routes.First(r => r.Id == id))) Message += " | " + GateDescription(id);
        foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && Map.CurrentGrid.Locations[r.KeyLocationId].Equals(Position) && !gates.HasKey(r)))
            Message += " | {Interact}: Collect " + route.KeyLabel;
        foreach (var route in Map.CurrentGrid.WarpsAt(Position)) Message += " | " + WarpLabel(route);
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
        Message = "Warp complete. Choose a destination below to return.";
        return true;
    }

    public LockInteractionSession BeginGateInteraction(string routeId)
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return null;
        var context = Context;
        return gates.BeginInteraction(routeId, () => Position, () => Held,
            () => isActiveAndEnabled && IsReady && Common.Instance.CampaignContext == context && !moving && !Common.Instance.Travel.IsTransitioning);
    }

    public LockOutcome AttemptGate(LockInteractionSession session, LockAction action)
    {
        var outcome = session.Attempt(action);
        if (outcome == LockOutcome.Opened)
        {
            Message = "The way is now open!";
            RefreshGates();
            SaveProgress();
        }
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
            if (route.TryUnlock(location.Id, resolved)) { Message = "Opened shortcut: " + route.Id; RefreshGates(); SaveProgress(); return true; }
        return false;
    }

    public void ClaimRewards() => ClaimRewards(true);

    public void ClaimRewards(bool announceLocked)
    {
        StopHeldWalk();
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning) return;
        if (GetComponent<OverworldMenuManager>().Opened) return;
        if (!locations.TryGetValue(Position, out var location))
        {
            if (announceLocked) OpenGate();
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
                rewards.Add(route.KeyLabel + " (use it at the gate)");
                acquiredKeys.Add(route.KeyId);
            }
        foreach (var source in Campaign.Sources.Where(s => s.LocationId == location.Id && !claimed.Contains(s.Id)))
        {
            if (!Context.Claim(source.Id)) continue;
            rewards.Add(source.Capability.ToString());
            acquiredCapabilities.Add(CampaignTravelService.DescribeCapabilityReward(source));
        }
        SaveProgress();
        Message = rewards.Count == 0 ? "No eligible unclaimed rewards here." : "Acquired: " + string.Join(", ", rewards) + ". Equip companions at a town.";
        RefreshGates();
        Common.Instance.Travel.ShowRewardsAcquired(acquiredKeys.Distinct().ToArray(), acquiredCapabilities.ToArray());
        if (rewards.Count == 0 && announceLocked) OpenGate();
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
        if (!Context.IsSandbox) SaveSystem.Capture(Common.Instance);
    }
    public bool SimulateDungeonVictory()
    {
        if (!IsReady || moving || Common.Instance.Travel.IsTransitioning || GetComponent<OverworldMenuManager>().Opened || !Context.IsSandbox) return false;
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
            // Static companion prefabs have no animation component.
            followers[i].HeroAnimator?.PlayIdleAnimation();
        }
        RefreshLocationMarkers();
    }

    private void RefreshLocationMarkers()
    {
        foreach (var marker in locationVisuals)
            marker.Value.SetActive(true);
    }

    private void RefreshGates()
    {
        foreach (var gate in Map.CurrentGrid.Locks)
            foreach (var visual in gateVisuals[gate.RouteId]) visual.SetActive(!gates.IsWalkable(gate.Cells[0], Held));
    }

    private void LateUpdate() { FlushMessage(); if (IsReady) FollowCamera(); }
    private void FollowCamera()
    {
        var target = Player.transform.position + CellVisualOffset;
        ViewCamera.transform.position = target + CameraOffset;
        ViewCamera.transform.LookAt(target);
    }

    private void OnGUI()
    {
        GameUISkin.UseLegacySkin();
        if (Context == null || !Context.IsSandbox || !IsReady || GetComponent<OverworldMenuManager>().Opened) return;
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !AutoplayRunner.BlocksPlayerInput;
        GameUISkin.LegacyBeginArea(new Rect(16, 16, 580, 430));
        GUILayout.Label("CAMPAIGN OVERWORLD  |  Seed " + Map.Seed);
        GUILayout.Label(InputPrompts.Format("{Move}: move   •   {Interact}: interact"));
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
