using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TWC;
using TWC.OdinSerializer;
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
    public bool IsReady { get; private set; }
    private bool finishingGeneration;

    // Start is called before the first frame update
    void Start()
    {
        Common.Instance.ScreenTransition.HoldClosed();
        Debug.Log("Load Save Data");
        Configuration = Common.Instance.CurrentTownConfiguration ?? TownSceneLoader.ResolveSaved();
        TownSceneLoader.Configure(Configuration);
        TownAllyManager.Configure(Configuration);
        FindFirstObjectByType<TownMenu>().ValidateBindings(Configuration);
        Services = new TownServices(this);
        LoadSaveData();

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
            .Select(ally => new TownAllyData {
                AllyId = ally.Id, AllyName = ally.Name, Skills = new List<string>(ally.Skills),
                Equipment = ItemSaveData.Capture(ally.Equipment.GetEquippedItems())
            }).ToList();
    }

    public void SaveProgress()
    {
        WriteSaveData();
        SaveSystem.SaveData(Common.Instance.GameSaveData);
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
        var previousAllies = Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData;
        if (previousAllies.Count == 0)
            previousAllies.AddRange(Configuration.StartingParty.Select(a => new TownAllyData { AllyId = a.Id, AllyName = a.Name, Skills = new() }));
        foreach(var allyData in previousAllies)
		{
            var prefab = TownAllyManager.GetAlly(allyData);

            var allyInstance = Instantiate(prefab, this.transform);
            AllyRecruitDialog.Recruit(this, allyInstance);
            allyInstance.TilemapPosition = startPosition;
            allyInstance.transform.position = WalkableMap.CellToWorld(allyInstance.TilemapPosition);
            allyInstance.Skills = allyData.Skills != null ? new List<string>(allyData.Skills) : new();
            allyInstance.RecruitCost = Configuration.Recruits.FirstOrDefault(r => r.Ally == prefab)?.Cost ?? 0;
            foreach (var item in allyData.Equipment ?? new())
                if (item.Restore(Common.Instance.ItemManager) is EquipableInventoryItem equipment)
                    allyInstance.Equipment.Equip(equipment);
            allyInstance.RefreshEquipmentVisuals();
        }

        var allyPositions = GetPositions(Configuration.AllyLayer);

		int count = allyPositions.Count;
		var allies = TownAllyManager.GenerateRandomAllies(count, TownPlayer.RecruitedAllies.Select(a => a.Id));
		for (int i = 0; i < allies.Count; i++)
		{
			var ally = allies[i];
			var worldPosition = WalkableMap.CellToWorld(allyPositions[i]);
			ally.TilemapPosition = allyPositions[i];
			ally.transform.position = worldPosition;
			ally.SetFacing(Facing.Down);
			TownAllies.Add(ally);
		}
	}

	private List<Vector3Int> GetPositions(string bluePrintLayerName)
	{
        List<Vector3Int> positions = new();

		var map = WalkableMap.TileWorldCreator.GetMapOutputFromBlueprintLayer(bluePrintLayerName);
		int width = map.GetLength(0);
		int height = map.GetLength(1);

		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				if (map[x, y])
				{
                    positions.Add(new Vector3Int(x, y, 0));
				}
			}
		}

        return positions;
	}

	[ContextMenu("Generate Entrance")]
    public void GenerateInteractableBuildings()
    {
        TownBuildings = TownBuildingManager.Spawn(Configuration, GetPositions(Configuration.BuildingLayer), WalkableMap);
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
    }

    private void blueprintLayersComplete(TileWorldCreator _twc)
    {
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
        Debug.Log("Generate Buildings");
        GenerateInteractableBuildings();

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
