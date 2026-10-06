using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

public class Town : MonoBehaviour
{
    public WalkableMap WalkableMap;
    public TownConfiguration Configuration { get; private set; }
    public TownServices Services { get; private set; }

    public TownPlayer TownPlayer;
    public TownAllyManager TownAllyManager;
    public List<TownAlly> TownAllies;
    public TownBuildingManager TownBuildingManager;
    public List<TownBuilding> TownBuildings;
    public List<ShopVendor> ShopVendors = new();
    public readonly List<TownNpc> Townsfolk = new();
    public bool IsOccupied(Vector3Int cell) => TownAllies.Any(a=>a!=null && a.TilemapPosition==cell) || ShopVendors.Any(v=>v!=null && v.TilemapPosition==cell) || Townsfolk.Any(n=>n!=null && n.Cell==cell) || GetComponentsInChildren<HomeBed>().Any(b=>b.Tile==cell);
    public bool CanEnter(Vector3Int cell) => Plan != null && Plan.IsWalkable(cell.ToGridPoint()) && !IsOccupied(cell);
    private readonly List<GameObject> shopWalls = new();
    private readonly List<TownRoofVisual> roofs = new();
    public IReadOnlyList<TownRoofVisual> Roofs => roofs;
    private TownAlly roofAlly;
    private Vector3Int roofCell;
    public bool IsReady { get; private set; }
    private string arrivalHeroId;
    public TownPlan Plan { get; private set; }
    private bool finishingGeneration;
    private TileWorldCreatorAsset runtimeAsset;

    // Start is called before the first frame update
    void Start()
    {
        Common.Instance.Travel.SceneReady();
        Common.Instance.ScreenTransition.HoldClosed();
        Debug.Log("Load Save Data");
        Configuration = Common.Instance.CurrentTownConfiguration ?? TownSceneLoader.ResolveSaved();
        TownSceneLoader.Configure(Configuration);
        TownAllyManager.Configure(Configuration);
        FindFirstObjectByType<TownMenu>().ValidateBindings(Configuration);
        Services = new TownServices(this);
        ResourceHUD.Ensure(this);
        ScenePresentation.Ensure(this);
        LoadSaveData();
        if (Common.Instance.CampaignContext != null)
        {
            gameObject.AddComponent<CampaignTownControls>().Town = this;
        }
        var twc = WalkableMap.TileWorldCreator;
        runtimeAsset = Instantiate(twc.twcAsset);
        runtimeAsset.hideFlags = HideFlags.DontSave;
        twc.twcAsset = runtimeAsset;
        CoreTownLayerGenerator.Configure(twc.twcAsset, Configuration);

        Debug.Log("WalkableMap type: " + (WalkableMap == null ? "NULL" : WalkableMap.GetType().FullName));
        Debug.Log("TileWorldCreator type: " + (WalkableMap.TileWorldCreator == null ? "NULL" : WalkableMap.TileWorldCreator.GetType().FullName));
        int seed = Common.Instance.GameSaveData.TownSaveData.TownSeed;
		WalkableMap.TileWorldCreator.SetCustomRandomSeed(seed);
        WalkableMap.TileWorldCreator.ExecuteAllBlueprintLayers();
    }

    private void LoadSaveData()
    {
        var save = Common.Instance.GameSaveData.TownSaveData;
        TownPlayer.Gold = save.Gold;
        TownPlayer.Inventory.Clear();
        TownPlayer.Inventory.AddRange(save.InventoryItems.Select(i => i.Restore(Common.Instance.ItemManager)));

        Debug.Log($"Town seed {Common.Instance.GameSaveData.TownSaveData.TownSeed}");
    }

    [ContextMenu("Write Data")]
    internal void WriteSaveData()
    {


		TownSaveData townSaveData = Common.Instance.GameSaveData.TownSaveData;
		townSaveData.Gold = TownPlayer.Gold;
        townSaveData.InventoryItems = ItemSaveData.Capture(TownPlayer.Inventory);
		townSaveData.RecruitedAlliesData = TownPlayer.RecruitedAllies
            .Select(ally => HeroClassBinding.FromPrefab(ally, new TownAllyData {
                AllyId = ally.Id, AllyName = ally.Name, Skills = new List<string>(ally.Skills),
                Equipment = ItemSaveData.Capture(ally.Equipment.GetEquippedItems()),
                SkillRanks = (ally.SkillRanks ?? new List<SkillRankSaveData>())
                    .Where(r => r != null && !string.IsNullOrEmpty(r.SkillName))
                    .Select(r => new SkillRankSaveData { SkillName = r.SkillName, Rank = r.Rank }).ToList(),
                HighestLevel = Mathf.Max(1, ally.HighestLevel), Level = ally.Level, Experience = ally.Experience,
                Hp = ally.Hp, Sp = ally.Sp, HasHunger = ally.HasHunger,
                Hunger = ally.Hunger, HungerAccumulate = ally.HungerAccumulate
            })).ToList();
        CampaignParty.Capture(Common.Instance);
    }

    public void SaveProgress()
    {
        WriteSaveData();
        SaveSystem.Capture(Common.Instance);
    }

    public void RefreshCampaignParty()
    {
        foreach (var ally in TownPlayer.RecruitedAllies.Concat(TownAllies).ToArray())
        { ally.gameObject.SetActive(false); Destroy(ally.gameObject); }
        TownPlayer.RecruitedAllies.Clear(); TownAllies.Clear();
        CampaignParty.PrepareActive(Common.Instance, Configuration);
        GenerateAllies(); TownPlayer.ControllingTownAlly = null; TownPlayer.EnsureControlledAlly();
        RefreshRoofs();
        SaveProgress();
    }

    public void GenerateAllies()
	{
        Common.Instance.InstantiatedTownAllies.Clear();
        foreach(Transform child in Common.Instance.TownAllyParent)
		{
            Destroy(child.gameObject);
		}

        var allyPositions = Plan.AllySlots.Select(p => p.Cell.ToCell()).ToList();
        var rolls = Plan.AllySlots.Select(p => p.Roll).ToList();
		var allies = TownAllyManager.GenerateRandomAllies(rolls, (Common.Instance.CampaignContext != null ? Common.Instance.CampaignContext.Roster.Append(Common.Instance.GameSaveData.ProtagonistId) : Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData.Select(a => a.AllyId).Concat(Configuration.StartingParty.Select(a => a.Id))));
		for (int i = 0; i < Mathf.Min(allies.Count, allyPositions.Count); i++)
		{
			var ally = allies[i];
			var worldPosition = WalkableMap.CellToWorld(allyPositions[i]);
			ally.TilemapPosition = allyPositions[i];
			ally.transform.position = worldPosition;
			ally.SetFacing(Facing.Down);
			TownAllies.Add(ally);
		}
        arrivalHeroId = null;
        //restore allies
        var startPosition = Configuration.PartySpawn;
        Facing arrivalFacing = Facing.Down;
        var campaignSave = Common.Instance.GameSaveData;
        if (campaignSave.NeedsInitialSave && Configuration.Id == "town-0") startPosition = GetComponentInChildren<HomeBed>().Tile - Vector3Int.up;
        else if (campaignSave.HasArrival && campaignSave.ArrivalTownId == Configuration.Id)
        {
            startPosition = new Vector3Int(campaignSave.ArrivalX, campaignSave.ArrivalY, 0);
            arrivalFacing = campaignSave.ArrivalFacing; arrivalHeroId = campaignSave.ArrivalHeroId;
            campaignSave.HasArrival = false;
        }
        if (Common.Instance.Travel.ConsumeDungeonReturn(Configuration.Id))
        {
            var entrance = TownBuildings.First(b => b.Definition.DialogId == "entrance").TilemapPosition;
            var arrival = new[]{Vector3Int.down,Vector3Int.left,Vector3Int.right,Vector3Int.up}
                .Select(direction => entrance + direction).Where(CanEnter).Cast<Vector3Int?>().FirstOrDefault();
            if (arrival.HasValue) startPosition = arrival.Value;
            arrivalFacing = Facing.Down;
        }
        var formation = new List<Vector3Int> { startPosition };
        foreach(var direction in new[]{Vector3Int.down,Vector3Int.left,Vector3Int.right,Vector3Int.up})
        {
            var cell=startPosition+direction;
            if(GridMovement.CanStep(startPosition,cell,c => Plan.Layers[TownLayers.Walkable].At(c.ToGridPoint())) && !IsOccupied(cell)) formation.Add(cell);
        }
        // Extend along walkable adjacent cells until a four-member arrival fits.
        for(int i=0;i<formation.Count && formation.Count<4;i++)
            foreach(var direction in new[]{Vector3Int.down,Vector3Int.left,Vector3Int.right,Vector3Int.up})
            {
                var cell=formation[i]+direction;
                if(!formation.Contains(cell) && GridMovement.CanStep(formation[i],cell,c => Plan.Layers[TownLayers.Walkable].At(c.ToGridPoint())) && !IsOccupied(cell)) formation.Add(cell);
                if(formation.Count>=4) break;
            }
        TownPlayer.WalkPositionHistory = formation.Take(4).Reverse().ToList();
        int formationIndex = 1;
        var previousAllies = Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData;
        if (previousAllies.Count == 0)
            previousAllies.AddRange(Configuration.StartingParty.Select(a => HeroClassBinding.FromPrefab(a,
                new TownAllyData { AllyId = a.Id, AllyName = a.Name, Skills = new() })));
        foreach(var allyData in previousAllies)
		{
            var prefab = TownAllyManager.GetAlly(allyData);

            var allyInstance = Instantiate(prefab, this.transform);
            if (Common.Instance.CampaignContext != null) allyInstance.Id = allyData.AllyId;
            HeroClassBinding.Apply(allyInstance, allyData, Common.Instance.GameSaveData);
            AllyRecruitDialog.Recruit(this, allyInstance);
            bool leader = allyData.AllyId == (arrivalHeroId ?? campaignSave.ProtagonistId) || previousAllies.Count == 1;
            allyInstance.TilemapPosition = leader ? startPosition : formation[Mathf.Min(formationIndex++,formation.Count-1)];
            allyInstance.SetFacing(arrivalFacing);
            allyInstance.transform.position = WalkableMap.CellToWorld(allyInstance.TilemapPosition);
            allyInstance.Skills = allyData.Skills != null ? new List<string>(allyData.Skills) : new();
            allyInstance.SkillRanks = (allyData.SkillRanks ?? new List<SkillRankSaveData>())
                .Where(r => r != null && !string.IsNullOrEmpty(r.SkillName))
                .Select(r => new SkillRankSaveData { SkillName = r.SkillName, Rank = r.Rank }).ToList();
            allyInstance.HighestLevel = Mathf.Max(1, allyData.HighestLevel);
            allyInstance.Level = Mathf.Max(1, allyData.Level); allyInstance.Experience = allyData.Experience;
            allyInstance.Hp = allyData.Hp; allyInstance.Sp = allyData.Sp;
            allyInstance.HasHunger = allyData.HasHunger; allyInstance.Hunger = allyData.Hunger; allyInstance.HungerAccumulate = allyData.HungerAccumulate;
            allyInstance.RecruitCost = Configuration.Recruits.FirstOrDefault(r => r.Ally == prefab)?.Cost ?? 0;
            allyInstance.Equipment.RestoreSaved(allyData.Equipment ?? new(), Common.Instance.ItemManager, item => TownPlayer.Inventory.Add(item));
            allyInstance.EnsureStartingSkills();
            allyInstance.RefreshEquipmentVisuals();
        }


	}

	[ContextMenu("Generate Entrance")]
    public void GenerateInteractableBuildings()
    {
        foreach(var old in TownBuildings ?? new List<TownBuilding>()) if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
        TownBuildings = TownBuildingManager.Spawn(Configuration, Plan.BuildingSlots.ToCells(), WalkableMap, Plan);
    }

    /// <summary>Spawns the carved rooms' wall visuals and a vendor per shop whose room actually exists this generation.</summary>
    public void GenerateShopInteriors()
    {
        foreach (var wall in shopWalls) if (wall != null) Destroy(wall);
        shopWalls.Clear();
        foreach (var vendor in ShopVendors) if (vendor != null) {vendor.gameObject.SetActive(false);Destroy(vendor.gameObject);}
        foreach(var bed in GetComponentsInChildren<HomeBed>()){bed.gameObject.SetActive(false);Destroy(bed.gameObject);}
        ShopVendors.Clear();

        var wallLayer = Plan.Layers[TownLayers.ShopWalls];
        bool authoredWalls = WalkableMap.TileWorldCreator.twcAsset.mapBuildLayers.OfType<TownEnvironmentLayer>().Any(l => l.active);
        for (int x = 0; x < wallLayer.Width; x++)
            for (int y = 0; y < wallLayer.Height; y++)
            {
                if (!wallLayer[x, y] || authoredWalls) continue;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "ShopWall";
                wall.transform.SetParent(transform);
                wall.transform.position = WalkableMap.CellToWorld(new Vector3Int(x, y, 0)) + Vector3.up * 0.5f;
                wall.transform.localScale = Vector3.one * WalkableMap.TileWorldCreator.twcAsset.cellSize;
                shopWalls.Add(wall);
            }

        foreach (var building in TownBuildings)
        {
            building.HasInterior = false;
            if (!building.Definition.HasInterior) continue;
            if (!Plan.TryGetVendorAnchor(building.TilemapPosition.ToGridPoint(), out var anchor))
            {
                if (Configuration.Layout != null)
                    throw new InvalidOperationException($"Service '{building.Definition.Id}' has no generated interior.");
                continue;
            }
            var anchorCell = anchor.ToCell();
            if (building.Definition.Id == "home")
            {
                HomeBed.Create(this, anchorCell);
                building.HasInterior = true;
                continue;
            }

            var vendor = building.Definition.VendorPrefab != null
                ? Instantiate(building.Definition.VendorPrefab, transform)
                : ShopVendor.CreateDefault(transform);
            vendor.Building = building.Definition;
            vendor.TilemapPosition = anchorCell;
            vendor.transform.position = WalkableMap.CellToWorld(anchorCell);
            if(building.Definition.Npc!=null)
            {
                float size=WalkableMap.TileWorldCreator.twcAsset.cellSize;
                vendor.VisualParent.transform.localPosition=new Vector3(.5f,.5f,0)*size;vendor.VisualParent.transform.localScale=Vector3.one*size;
            }
            vendor.SetFacing(Facing.Down);
            ShopVendors.Add(vendor);
            building.HasInterior = true;
        }
    }

    private void GenerateRoofs()
    {
        roofs.Clear();
        roofAlly = null;
        var world = WalkableMap.TileWorldCreator.worldObject;
        if (world != null)
            foreach (var output in world.GetComponentsInChildren<TownRoofTileOutput>(true))
                roofs.AddRange(output.Roofs.Where(r => r != null));
        RefreshRoofs();
    }

    public void RefreshRoofs()
    {
        var ally = TownPlayer != null ? TownPlayer.ControllingTownAlly : null;
        roofAlly = ally;
        roofCell = ally != null ? ally.TilemapPosition : default;
        var cell = roofCell.ToGridPoint();
        foreach (var roof in roofs)
            if (roof != null)
                roof.gameObject.SetActive(ally == null || roof.Room == null || !roof.Room.Floor.Contains(cell));
    }

    private void Update()
    {
        var ally = TownPlayer != null ? TownPlayer.ControllingTownAlly : null;
        if (ally != roofAlly || (ally != null && ally.TilemapPosition != roofCell)) RefreshRoofs();
    }

    private void Awake()
	{
        WalkableMap.TileWorldCreator.OnBlueprintLayersComplete += blueprintLayersComplete;
        WalkableMap.TileWorldCreator.OnBuildLayersComplete += buildLayersComplete;
    }

	private void OnDestroy()
    {
        WalkableMap.TileWorldCreator.OnBlueprintLayersComplete -= blueprintLayersComplete;
        WalkableMap.TileWorldCreator.OnBuildLayersComplete -= buildLayersComplete;
        if (runtimeAsset != null) Destroy(runtimeAsset);
    }

    private void blueprintLayersComplete(TileWorldCreator _twc)
    {
        CoreLayoutCache.ClearResultFlags(_twc.twcAsset);
        WalkableMap.TileWorldCreator.ExecuteAllBuildLayers(false);
    }

    private void buildLayersComplete(TileWorldCreator _twc)
    {
        if (finishingGeneration) return;
        finishingGeneration = true;
        StartCoroutine(FinishGeneration());
    }

    private IEnumerator FinishGeneration()
    {
        if(IsReady)
        {
            WriteSaveData();
            foreach(var ally in TownPlayer.RecruitedAllies.Concat(TownAllies).Distinct().ToArray())
                if(ally!=null){ally.gameObject.SetActive(false);Destroy(ally.gameObject);}
            TownPlayer.RecruitedAllies.Clear();TownAllies.Clear();TownPlayer.ControllingTownAlly=null;IsReady=false;
        }
        if (!CoreLayoutCache.TryGetTown(WalkableMap.TileWorldCreator, out var plan))
            throw new InvalidOperationException("The town TWC template requires Core Town Layer actions.");
        Plan = plan;

        Debug.Log("Generate Buildings");
        GenerateInteractableBuildings();
        GenerateShopInteriors();
        GenerateRoofs();
        TownInteriorRendering.SpawnTownsfolk(this);

        Debug.Log("Generate Allies");
        GenerateAllies();

        Debug.Log("Initialize Player");
        TownPlayer.Initialize();
        TownPlayer.SelectAlly(arrivalHeroId ?? Common.Instance.GameSaveData.ProtagonistId);
        RefreshRoofs();

        // Let newly spawned actors finish Start before placing the camera.
        yield return null;
        var camera = TownPlayer.CameraController;
        while (TownPlayer.ControllingTownAlly == null || camera._followTarget == null || camera.Camera == null)
            yield return null;
        camera.SnapToFollowTarget();
        IsReady = true;
        if (Common.Instance.GameSaveData.NeedsInitialSave)
        {
            WriteSaveData();
            var hero = TownPlayer.ControllingTownAlly;
            if (!CampaignSaving.Commit(Common.Instance, "town-0/home", hero.TilemapPosition, hero.CurrentFacing, out var error))
                TownMenu.ShowMessage(error);
        }
        ScenePresentation.RegisterWorld(WalkableMap.TileWorldCreator.worldObject.transform);

        Debug.Log("Town done");
        Common.Instance.ScreenTransition.DoOpen();
        finishingGeneration=false;
    }
}
