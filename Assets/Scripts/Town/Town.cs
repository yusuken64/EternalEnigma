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
    private readonly List<GameObject> shopWalls = new();
    public bool IsReady { get; private set; }
    public TownPlan Plan { get; private set; }
    private bool finishingGeneration;

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
        LoadSaveData();
        if (Common.Instance.CampaignContext != null)
        {
            gameObject.AddComponent<CampaignTownControls>().Town = this;
        }
        var twc = WalkableMap.TileWorldCreator;
        twc.twcAsset = Instantiate(twc.twcAsset); twc.twcAsset.hideFlags = HideFlags.DontSave;
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
        TownPlayer.Inventory.AddRange(save.InventoryFormatVersion >= 1
            ? save.InventoryItems.Select(i => i.Restore(Common.Instance.ItemManager))
            : save.Inventory.Select(n => Common.Instance.ItemManager.GetAsInventoryItemByName(n)));

        Debug.Log($"Town seed {Common.Instance.GameSaveData.TownSaveData.TownSeed}");
    }

    [ContextMenu("Write Data")]
    internal void WriteSaveData()
    {


		TownSaveData townSaveData = Common.Instance.GameSaveData.TownSaveData;
		townSaveData.Gold = TownPlayer.Gold;
        townSaveData.InventoryItems = ItemSaveData.Capture(TownPlayer.Inventory);
        townSaveData.InventoryFormatVersion = 1;
		townSaveData.Inventory = TownPlayer.Inventory
            .Select(x => x.ItemName)
            .ToList();
		townSaveData.RecruitedAlliesData = TownPlayer.RecruitedAllies
            .Select(ally => HeroClassBinding.FromPrefab(ally, new TownAllyData {
                AllyId = ally.Id, AllyName = ally.Name, Skills = new List<string>(ally.Skills),
                Equipment = ItemSaveData.Capture(ally.Equipment.GetEquippedItems()),
                SkillRanks = (ally.SkillRanks ?? new List<SkillRankSaveData>())
                    .Where(r => r != null && !string.IsNullOrEmpty(r.SkillName))
                    .Select(r => new SkillRankSaveData { SkillName = r.SkillName, Rank = r.Rank }).ToList(),
                HighestLevel = Mathf.Max(1, ally.HighestLevel)
            })).ToList();
        CampaignParty.Capture(Common.Instance);
    }

    public void SaveProgress()
    {
        WriteSaveData();
        SaveSystem.SaveData(Common.Instance.GameSaveData);
    }

    public void RefreshCampaignParty()
    {
        foreach (var ally in TownPlayer.RecruitedAllies.Concat(TownAllies).ToArray())
        { ally.gameObject.SetActive(false); Destroy(ally.gameObject); }
        TownPlayer.RecruitedAllies.Clear(); TownAllies.Clear();
        CampaignParty.PrepareActive(Common.Instance, Configuration);
        GenerateAllies(); TownPlayer.ControllingTownAlly = null; TownPlayer.EnsureControlledAlly();
        SaveProgress();
    }

    public void GenerateAllies()
	{
        Common.Instance.InstantiatedTownAllies.Clear();
        foreach(Transform child in Common.Instance.TownAllyParent)
		{
            Destroy(child.gameObject);
		}

        //restore allies
        var startPosition = Configuration.PartySpawn;
        TownPlayer.WalkPositionHistory = new() { startPosition };
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
            allyInstance.TilemapPosition = startPosition;
            allyInstance.transform.position = WalkableMap.CellToWorld(allyInstance.TilemapPosition);
            allyInstance.Skills = allyData.Skills != null ? new List<string>(allyData.Skills) : new();
            allyInstance.SkillRanks = (allyData.SkillRanks ?? new List<SkillRankSaveData>())
                .Where(r => r != null && !string.IsNullOrEmpty(r.SkillName))
                .Select(r => new SkillRankSaveData { SkillName = r.SkillName, Rank = r.Rank }).ToList();
            allyInstance.HighestLevel = Mathf.Max(1, allyData.HighestLevel);
            allyInstance.RecruitCost = Configuration.Recruits.FirstOrDefault(r => r.Ally == prefab)?.Cost ?? 0;
            foreach (var item in allyData.Equipment ?? new())
                if (item.Restore(Common.Instance.ItemManager) is EquipableInventoryItem equipment)
                    allyInstance.Equipment.Equip(equipment);
            allyInstance.EnsureStartingSkills();
            allyInstance.RefreshEquipmentVisuals();
        }

        var allyPositions = Plan.AllySlots.Select(p => p.Cell.ToCell()).ToList();
        var rolls = Plan.AllySlots.Select(p => p.Roll).ToList();
		var allies = TownAllyManager.GenerateRandomAllies(rolls, (Common.Instance.CampaignContext != null ? Common.Instance.CampaignContext.Roster.Append(Common.Instance.GameSaveData.ProtagonistId) : TownPlayer.RecruitedAllies.Select(a => a.Id)));
		for (int i = 0; i < Mathf.Min(allies.Count, allyPositions.Count); i++)
		{
			var ally = allies[i];
			var worldPosition = WalkableMap.CellToWorld(allyPositions[i]);
			ally.TilemapPosition = allyPositions[i];
			ally.transform.position = worldPosition;
			ally.SetFacing(Facing.Down);
			TownAllies.Add(ally);
		}
	}

	[ContextMenu("Generate Entrance")]
    public void GenerateInteractableBuildings()
    {
        TownBuildings = TownBuildingManager.Spawn(Configuration, Plan.BuildingSlots.ToCells(), WalkableMap);
    }

    /// <summary>Spawns the carved rooms' wall visuals and a vendor per shop whose room actually exists this generation.</summary>
    public void GenerateShopInteriors()
    {
        foreach (var wall in shopWalls) if (wall != null) Destroy(wall);
        shopWalls.Clear();
        foreach (var vendor in ShopVendors) if (vendor != null) Destroy(vendor.gameObject);
        ShopVendors.Clear();

        var wallLayer = Plan.Layers[TownLayers.ShopWalls];
        for (int x = 0; x < wallLayer.Width; x++)
            for (int y = 0; y < wallLayer.Height; y++)
            {
                if (!wallLayer[x, y]) continue;
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
            if (building.Definition.ShopCatalog.Count == 0) continue;
            if (!Plan.TryGetVendorAnchor(building.TilemapPosition.ToGridPoint(), out var anchor)) continue;
            var anchorCell = anchor.ToCell();

            var vendor = building.Definition.VendorPrefab != null
                ? Instantiate(building.Definition.VendorPrefab, transform)
                : ShopVendor.CreateDefault(transform);
            vendor.Building = building.Definition;
            vendor.TilemapPosition = anchorCell;
            vendor.transform.position = WalkableMap.CellToWorld(anchorCell);
            vendor.SetFacing(Facing.Down);
            ShopVendors.Add(vendor);
            building.HasInterior = true;
        }
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
        if (WalkableMap.TileWorldCreator.twcAsset != null) Destroy(WalkableMap.TileWorldCreator.twcAsset);
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
        if (!CoreLayoutCache.TryGetTown(WalkableMap.TileWorldCreator, out var plan))
            throw new InvalidOperationException("The town TWC asset has no Core Town Layer actions; run Tools/Eternal Enigma/Core Layers/Rewrite Town Asset.");
        Plan = plan;

        Debug.Log("Generate Buildings");
        GenerateInteractableBuildings();
        GenerateShopInteriors();

        Debug.Log("Generate Allies");
        GenerateAllies();

        Debug.Log("Initialize Player");
        TownPlayer.Initialize();

        // Let newly spawned actors finish Start before placing the camera.
        yield return null;
        var camera = TownPlayer.CameraController;
        while (TownPlayer.ControllingTownAlly == null || camera._followTarget == null || camera.Camera == null)
            yield return null;
        camera.SnapToFollowTarget();
        IsReady = true;

        Debug.Log("Town done");
        Common.Instance.ScreenTransition.DoOpen();
    }
}
