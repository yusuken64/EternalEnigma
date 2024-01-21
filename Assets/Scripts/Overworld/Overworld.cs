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
    public GameObject EntrancePrefab;
    public GameObject ShopPrefab;
    public GameObject StatuePrefab;
    public GameObject BallistaPrefab;
    public Vector3Int EntrancePosition;
    public Vector3Int ShopPosition;
    public Vector3Int StatuePosition;
    public Vector3Int BallistaPosition;
    public OverworldData OverworldData;

    public List<Vector3Int> AllyPositions;

    public OverworldPlayer OverworldPlayer;
    public OverworldAllyManager OverworldAllyManager;
    public List<OverworldAlly> OverworldAllies;//instanciated

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Load Save Data");
        LoadSaveData();

        //LoadMap();
        Debug.Log("Generate Buildings");
        GenerateInteractableBuildings();

        Debug.Log("Generate Allies");
        GenerateAllies();

        Debug.Log("Initialize Player");
        OverworldPlayer.Initialize();
    }

    private void LoadSaveData()
    {
        var statueDialog = FindObjectOfType<StatueDialog>(true);
        var shopMenuDialog = FindObjectOfType<ShopMenuDialog>(true);
        shopMenuDialog.GenerateShop();

        OverworldPlayer.Gold = Common.Instance.GameSaveData.OverworldSaveData.Gold;
        statueDialog.DonatedAmount = Common.Instance.GameSaveData.OverworldSaveData.DonationTotal;
    }

    [ContextMenu("Write Data")]
    internal void WriteSaveData()
    {
        var statueDialog = FindObjectOfType<StatueDialog>(true);

		OverworldSaveData overworldSaveData = Common.Instance.GameSaveData.OverworldSaveData;
		overworldSaveData.Gold = OverworldPlayer.Gold;
		overworldSaveData.DonationTotal = statueDialog.DonatedAmount;
		overworldSaveData.Inventory = OverworldPlayer.Inventory.ToList();
		overworldSaveData.RecruitedAllies = OverworldPlayer.RecruitedAllies.ToList();
		overworldSaveData.RecruitedAllies.ForEach(x => x.transform.SetParent(Common.Instance.SceneTransferObjects.transform));

        var ballistaDialog = FindObjectOfType<BallistaDialog>(true);
        overworldSaveData.ActiveSkillNames = ballistaDialog.GetActiveSkillsSave();

        AllyPositions = OverworldAllies.Select(x => x.TilemapPosition).ToList();
    }

    public void GenerateAllies()
    {
        var usedPositions = new List<Vector3Int>()
        {
            EntrancePosition,
            ShopPosition,
            StatuePosition,
            BallistaPosition,
        };

        //var positions = WalkableMap.RandomEntrancePositions(13).Where(x => !usedPositions.Contains(x.Coord)).ToList();
        var allies = OverworldAllyManager.GenerateRandomAlly(AllyPositions.Count());
        for (int i = 0; i < AllyPositions.Count; i++)
        {
            var ally = allies[i];
            var worldPosition = WalkableMap.CellToWorld(AllyPositions[i]);
            ally.TilemapPosition = AllyPositions[i];
            ally.transform.position = worldPosition;
            ally.SetFacing(Facing.Down);
            OverworldAllies.Add(ally);
        }
    }

    [ContextMenu("Generate Entrance")]
    public void GenerateInteractableBuildings()
	{
		GenerateBuilding(EntrancePosition, EntrancePrefab);
		GenerateBuilding(ShopPosition, ShopPrefab);
		GenerateBuilding(StatuePosition, StatuePrefab);
		GenerateBuilding(BallistaPosition, BallistaPrefab);
	}

    private void GenerateBuilding(Vector3Int mapPosition, GameObject prefab)
	{
		var worldPosition = WalkableMap.CellToWorld(mapPosition);
		var building = Instantiate(prefab, this.transform);
		building.transform.position = worldPosition;
	}

	[ContextMenu("Generate Shop")]
    public void GenerateShop()
    {
        var worldPosition = WalkableMap.CellToWorld(EntrancePosition);
        var entrance = Instantiate(EntrancePrefab, this.transform);
        entrance.transform.position = worldPosition;
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
        GenerateInteractableBuildings();
    }

    [ContextMenu("SaveMap")]
    public void SaveMap()
	{
        List<(WorldMap, bool[,])> mapLayers = WalkableMap.TileWorldCreator.GetMapOutputFromBlueprintLayers();
        //var serializedWorldMap = new SerializableWorldMapLayer(mapLayers);
        //var bytes = TWC.OdinSerializer.SerializationUtility.SerializeValue(mapLayers, DataFormat.Binary);

        //OverworldData.WorldBytes = bytes;
        OverworldData.EntrancePosition = EntrancePosition;
        OverworldData.ShopPosition = ShopPosition;
        OverworldData.StatuePosition = StatuePosition;
    }

    [ContextMenu("LoadMap")]
    public void LoadMap()
    {
        //var bytes = OverworldData.WorldBytes;
        //List<(WorldMap, bool[,])> mapLayer = TWC.OdinSerializer.SerializationUtility.DeserializeValue<List<(WorldMap, bool[,])>>(bytes, DataFormat.Binary);

        //WalkableMap.TileWorldCreator.SetMapOutputFromBlueprintLayers(mapLayer);
        WalkableMap.TileWorldCreator.ExecuteAllBuildLayers(true);
        EntrancePosition = OverworldData.EntrancePosition;
    }

    [ContextMenu("SetEntrance")]
    public void SetEntrance()
	{
        //EntrancePosition = WalkableMap.RandomEntrancePosition().Coord;
        //ShopPosition = WalkableMap.RandomEntrancePosition().Coord;
        //StatuePosition = WalkableMap.RandomEntrancePosition().Coord;
        BallistaPosition = WalkableMap.RandomEntrancePosition().Coord;
    }
}