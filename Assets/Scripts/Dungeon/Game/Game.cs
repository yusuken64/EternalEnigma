using DG.Tweening;
using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Game : SingletonMonoBehaviour<Game>
{
	public bool IsReady { get; private set; }
	private DemoDungeonLoadout demoLoadout;
	public TileWorldDungeonGenerator DungeonGenerator;
	public TileWorldDungeon CurrentDungeon;

	public TurnManager TurnManager;
	public LevelSystem LevelSystem;
	public EnemyManager EnemyManager;

	public PlayerController PlayerController;
	public GameObject ThrownItemProjectilePrefab;

	public List<Ally> Allies;
	public Ally AllyPrefab;

	public List<Character> Enemies;

	public TextMeshPro FloatingTextPrefab;

	public InventoryMenu InventoryMenu;
	public AllyActionDialog AllyMenu;
	public SkillDialog SkillDialog;
	public GameOverScreen GameOverScreen;
	public NewFloorMessage NewFloorMessage;

	public List<StatusEffect> StatusEffectPrefabs;

	internal List<Character> AllCharacters
	{
		get
		{
			var ret = new List<Character>();
			ret.AddRange(Allies);
			ret.AddRange(Enemies);

			return ret;
		}
	}
	
	public List<Character> DeadUnits; //dead units are added to this, destroy at end turn;

	public TextMeshProUGUI SkilText;
	public TextMeshProUGUI FloorText;
	public TextMeshProUGUI InventoryText;

	public Transform CharacterStatsDisplayContainer;
	public CharacterStatsDisplay CharacterStatsDisplayPrefab;
	public List<CharacterStatsDisplay> CharacterStatsDisplays;

	// Start is called before the first frame update
	void Start()
	{
        Common.Instance.Travel.SceneReady();
		Common.Instance.ScreenTransition.HoldClosed();
		ResetGame();
		StartCoroutine(RevealDungeonWhenReady());
	}

	private IEnumerator RevealDungeonWhenReady()
	{
		while (!IsReady) yield return null;
		Common.Instance.ScreenTransition.DoOpen();
	}

	internal void ResetGame()
	{
		PlayerController.CameraController.Camera = Camera.main;

		GameOverScreen.gameObject.SetActive(false);
		InitializeGame();
	}

	private void InitializeGame()
	{
		foreach (Transform child in CharacterStatsDisplayContainer)
		{
			Destroy(child.gameObject);
		}
		CharacterStatsDisplays.Clear();

		foreach (Transform townAllyTransform in Common.Instance.TownAllyParent)
		{
			var townAlly = townAllyTransform.GetComponent<TownAlly>();
			var ally = Instantiate(AllyPrefab);
			ally.InitialzeModel(townAlly);
			ally.AllyStrategy = AllyStrategy.Aggresive;
			Allies.Add(ally);

			Destroy(townAlly);

            ally.CharacterName = townAlly.Name;
            ally.TownAllyId = townAlly.Id;
            foreach (var equipment in townAlly.Equipment.GetEquippedItems())
                ally.Equipment.Equip(equipment);

			foreach (var skill in townAlly.Skills)
			{
				Skill skillInstance = Common.Instance.SkillManager.GetSkillInstanceByName(skill);
				ally.Skills.Add(skillInstance);
			}
			ally.InvalidateCachedStats();

			ally.InitialzeVitalsFromStats();
			ally.Vitals.Level = 1;

			ally.SyncDisplayedStats();

			var newItem = Instantiate(CharacterStatsDisplayPrefab, CharacterStatsDisplayContainer);
			newItem.Setup(ally);
			CharacterStatsDisplays.Add(newItem);
		}

		PlayerController.TakeControl(Allies[0]);

		var floor = Common.Instance.GameSaveData.DungeonSaveData.StartFloor;
		PlayerController.Floor = floor - 1;

		PlayerController.Inventory.Clear();
        var townSave = Common.Instance.GameSaveData.TownSaveData;
        var items = townSave.InventoryFormatVersion >= 1
            ? townSave.InventoryItems.Select(i => i.Restore(Common.Instance.ItemManager))
            : townSave.Inventory.Select(n => Common.Instance.ItemManager.GetAsInventoryItemByName(n));
        items.ToList().ForEach(x => PlayerController.Inventory.Add(x));
		demoLoadout = Common.Instance.PendingDemoLoadout;
		Common.Instance.PendingDemoLoadout = null;
		if (demoLoadout != null) demoLoadout.Apply(this);

		UpdateUI();
		AdvanceFloor();
	}

	internal void ShowGameOver()
	{
		GameOverScreen.gameObject.SetActive(true);
		GameOverScreen.Setup(PlayerController);

		MenuManager.Open(GameOverScreen);
	}

	public void AdvanceFloor()
	{
		PlayerController.Floor++;
		IsReady = false;
		TurnManager.InteruptTurn();
		
		StartCoroutine(AdvanceFloorRoutine());

		NewFloorMessage.HideScreen();
	}

	private IEnumerator AdvanceFloorRoutine()
	{
		bool throneFloor = PlayerController.Floor == Common.Instance.GameSaveData.DungeonSaveData.StartFloor ||
			PlayerController.Floor == Common.Instance.GameSaveData.DungeonSaveData.EndFloor;

		Enemies.ForEach(x => DestroyImmediate(x.gameObject));
		Enemies.Clear();

		yield return null;
		if (CurrentDungeon != null)
		{
			Destroy(CurrentDungeon.gameObject);
			CurrentDungeon = null;
		}

		if (throneFloor)
		{
			DungeonGenerator.GenerateThroneRoom();
		}
		else
		{
			DungeonGenerator.GenerateDungeon();
		}
		while(DungeonGenerator.GeneratedDungeon == null)
		{
			yield return null;
		}
		yield return null;

		var map = GameObject.Find("TileWorldCreator_Map");
		map.transform.position = new Vector3(0, 0, -1.50999999f);
		map.transform.localScale = new Vector3(1, 1, 3.3499999f);

		CurrentDungeon = DungeonGenerator.GeneratedDungeon;
		CurrentDungeon.IsThroneFloor = throneFloor;
		CurrentDungeon.IsExitFloor = throneFloor && PlayerController.Floor >= Common.Instance.GameSaveData.DungeonSaveData.EndFloor;
		CurrentDungeon.InitializeCache();
		FindFirstObjectByType<FogOverlay>().Initialize(CurrentDungeon);
		FindFirstObjectByType<Minimap>().Initialize(CurrentDungeon);

		yield return null;

		var startPosition = CurrentDungeon.GetStartPosition(throneFloor);
		PlayerController.ControlledAlly.SetPosition(startPosition);

		foreach (var ally in Allies)
		{
			if (ally != null)
			{
				var dropPosition = CurrentDungeon.GetPositionWith(startPosition, 
					node =>
					{
						var first = AllCharacters.FirstOrDefault(x => x.TilemapPosition == new Vector3Int(node.X, node.Y));
						return first == null;
					});
				ally.SetPosition(dropPosition);
				ally.currentInteractable = null;
			}
		}

		CurrentDungeon.SetStairs(CurrentDungeon.GetStairPosition(throneFloor));
		Debug.Log("Stairs Created", this);

		if (!throneFloor)
		{
			for (int i = 0; i < 10; i++)
			{
				var enemyPrefab = EnemyManager.GetEnemyPrefab(PlayerController.Floor);
				var enemy = Instantiate(enemyPrefab, this.transform);
				enemy.UpdateCachedStats();
				enemy.InitialzeVitalsFromStats();
				enemy.TilemapPosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomOpenEnemyPosition());
				Enemies.Add(enemy);
			}

			for (int i = 0; i < 5; i++)
			{
				var treasurePosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomOpenEnemyPosition());
				CurrentDungeon.SetTreasure(treasurePosition);
			}

			for (int i = 0; i < 5; i++)
			{
				var treasurePosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomOpenEnemyPosition());
				var item = Common.Instance.ItemManager.GetRandomDrop(null);
				CurrentDungeon.SetDroppedItem(treasurePosition, item);
			}

			for (int i = 0; i < 5; i++)
			{
				var trapPosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomOpenEnemyPosition());
				var item = Common.Instance.ItemManager.GetRandomDrop(null);
				CurrentDungeon.SetTrap(trapPosition);
			}
		}

		if (demoLoadout != null && PlayerController.Floor == Common.Instance.GameSaveData.DungeonSaveData.StartFloor)
			yield return demoLoadout.PreparePracticeRoom(this);
		yield return new WaitForSecondsRealtime(2.0f);
		NewFloorMessage.ShowNewFloor(PlayerController.Floor);

		PlayerController.ControlledAlly.currentInteractable = null;
		Game.Instance.PlayerController.StartTurn();
		UpdateMiniMap();
		IsReady = true;
	}

    internal readonly HashSet<Vector3Int> PartyVisibleTiles = new();
    private readonly Dictionary<Ally, Vector3Int> displayedSightOrigins = new();
    private TileWorldDungeon sightDungeon;
    internal readonly HashSet<Vector3Int> PlaybackVisibleTiles = new();

    private IEnumerable<Ally> SightAllies() => Allies.Concat(DeadUnits.OfType<Ally>())
        .Where(a => a != null && a.DisplayedVitals.HP > 0);

    private void LateUpdate() => RefreshSight();

    internal void RefreshSight()
    {
        if (!IsReady || CurrentDungeon == null) return;
        int count = 0;
        bool changed = sightDungeon != CurrentDungeon;
        foreach (var ally in SightAllies())
        {
            count++;
            var cell = CurrentDungeon.WorldToCell(ally.transform.position);
            if (!displayedSightOrigins.TryGetValue(ally, out var oldCell) || oldCell != cell) changed = true;
        }
        if (changed || count != displayedSightOrigins.Count) UpdateMiniMap();
    }

    public void UpdateMiniMap()
    {
        if (CurrentDungeon == null) return;
        sightDungeon = CurrentDungeon;
        PartyVisibleTiles.Clear();
        displayedSightOrigins.Clear();
        foreach (var ally in SightAllies())
        {
            var origin = CurrentDungeon.WorldToCell(ally.transform.position);
            displayedSightOrigins[ally] = origin;
            PartyVisibleTiles.UnionWith(CurrentDungeon.GetVisibleTiles(ally, origin));
        }
        var minimap = FindFirstObjectByType<Minimap>();
        minimap.UpdateVision(PartyVisibleTiles);
        minimap.UpdateMinimapWithVisibleTiles(PartyVisibleTiles);
    }

	private void Update()
	{
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (PlayerController == null) { return; }
		FloorText.text = $"{PlayerController.Floor}F";
		CharacterStatsDisplays.ForEach(x => x.UpdateUI());

		var inventoryText = 
			@$"Gold {PlayerController.Gold}g
Bag {PlayerController.Inventory.InventoryItems.Count}/{PlayerController.Inventory.MaxItems}";

		InventoryText.text = inventoryText;
	}
	
	[ContextMenu("AdvanceFloor")]
	public void AdvanceFloorCommand()
	{
		AdvanceFloor();
	}

	public void DoFloatingText(string message, Color color, Vector3 worldPosition)
	{
		var text = Instantiate(FloatingTextPrefab, this.transform);
		text.text = message;
		text.color = color;
		text.gameObject.transform.position = worldPosition;

		Vector3 endValue = worldPosition + new Vector3(0, 0, -5.47f);
		text.gameObject.transform.DOMove(endValue, 1.0f)
			.SetEase(Ease.OutBounce);
		Destroy(text.gameObject, 1.3f);
	}
}
