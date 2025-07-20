using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TWC;
using TWC.OdinSerializer;
using UnityEngine;

public class Overworld : MonoBehaviour
{
    public WalkableMap WalkableMap;
    public OverworldData OverworldData;

    public OverworldPlayer OverworldPlayer;
    public OverworldAllyManager OverworldAllyManager;
    public List<OverworldAlly> OverworldAllies;
    public OverworldBuildingManager OverworldBuildingManager;
    public List<OverworldBuilding> OverworldBuildings;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Load Save Data");
        LoadSaveData();

		int seed = Common.Instance.GameSaveData.OverworldSaveData.OverworldSeed;
		WalkableMap.TileWorldCreator.SetCustomRandomSeed(seed);
        WalkableMap.TileWorldCreator.ExecuteAllBlueprintLayers();
    }

    private void LoadSaveData()
    {
        var statueDialog = FindFirstObjectByType<StatueDialog>(FindObjectsInactive.Include);
        var shopMenuDialog = FindFirstObjectByType<ShopMenuDialog>(FindObjectsInactive.Include);
        shopMenuDialog.GenerateShop();

        OverworldPlayer.Gold = Common.Instance.GameSaveData.OverworldSaveData.Gold;
        statueDialog.DonatedAmount = Common.Instance.GameSaveData.OverworldSaveData.DonationTotal;

        if (Common.Instance.GameSaveData.OverworldSaveData.OverworldSeed == 0)
		{
            Common.Instance.GameSaveData.OverworldSaveData.OverworldSeed = UnityEngine.Random.Range(1, int.MaxValue);
        }

        Debug.Log($"Overworld seed {Common.Instance.GameSaveData.OverworldSaveData.OverworldSeed}");
    }

    [ContextMenu("Write Data")]
    internal void WriteSaveData()
    {
        var statueDialog = FindFirstObjectByType<StatueDialog>(FindObjectsInactive.Include);

		OverworldSaveData overworldSaveData = Common.Instance.GameSaveData.OverworldSaveData;
		overworldSaveData.Gold = OverworldPlayer.Gold;
		overworldSaveData.DonationTotal = statueDialog.DonatedAmount;
		overworldSaveData.Inventory = OverworldPlayer.Inventory.ToList();
		//overworldSaveData.RecruitedAlliesData = OverworldPlayer.RecruitedAllies.ToList();
		//overworldSaveData.RecruitedAlliesData.ForEach(x => x.transform.SetParent(Common.Instance.SceneTransferObjects.transform));

        var ballistaDialog = FindFirstObjectByType<BallistaDialog>(FindObjectsInactive.Include);
        overworldSaveData.ActiveSkillNames = ballistaDialog.GetActiveSkillsSave();
    }

    public void GenerateAllies()
	{
        Common.Instance.InstantiatedOverworldAllies.Clear();
        foreach(Transform child in Common.Instance.OverworldAllyParent)
		{
            Destroy(child.gameObject);
		}

        //restore allies
        var startPosition = new Vector3Int(10, 4, 0);
        var previousAllies = Common.Instance.GameSaveData.OverworldSaveData.RecruitedAlliesData;
        foreach(var allyData in previousAllies)
		{
            var prefab = OverworldAllyManager.GetAllyByName(allyData.AllyName);
            OverworldAllyManager.OverworldAllies.Remove(prefab);
            var allyInstance = Instantiate(prefab, this.transform);
            AllyRecruitDialog.Recruit(this, allyInstance);
            allyInstance.TilemapPosition = startPosition;
            allyInstance.transform.position = WalkableMap.CellToWorld(allyInstance.TilemapPosition);
        }

        var allyPositions = GetPositions("Allies");//TODO add count as param

		int count = allyPositions.Count() - previousAllies.Count();
		var allies = OverworldAllyManager.GenerateRandomAlly(count);
		for (int i = 0; i < allies.Count; i++)
		{
			var ally = allies[i];
			var worldPosition = WalkableMap.CellToWorld(allyPositions[i]);
			ally.TilemapPosition = allyPositions[i];
			ally.transform.position = worldPosition;
			ally.SetFacing(Facing.Down);
			OverworldAllies.Add(ally);
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
        OverworldBuildings.Clear();
        var buildingPositions = GetPositions("Buildings");
        OverworldBuildings.Add(GenerateBuilding(buildingPositions[0], OverworldBuildingManager.EntrancePrefab));
		OverworldBuildings.Add(GenerateBuilding(buildingPositions[1], OverworldBuildingManager.ShopPrefab));
		OverworldBuildings.Add(GenerateBuilding(buildingPositions[2], OverworldBuildingManager.StatuePrefab));
        OverworldBuildings.Add(GenerateBuilding(buildingPositions[3], OverworldBuildingManager.BallistaPrefab));
	}

    private OverworldBuilding GenerateBuilding(Vector3Int mapPosition, OverworldBuilding prefab)
	{
		var worldPosition = WalkableMap.CellToWorld(mapPosition);
		var building = Instantiate(prefab, this.transform);
		building.transform.position = worldPosition;
        building.TilemapPosition = mapPosition;

        return building;
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
        Debug.Log("Generate Buildings");
        GenerateInteractableBuildings();

        Debug.Log("Generate Allies");
        GenerateAllies();

        Debug.Log("Initialize Player");
        OverworldPlayer.Initialize();

        Debug.Log("Overworld done");
    }
}