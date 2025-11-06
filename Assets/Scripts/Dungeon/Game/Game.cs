using DG.Tweening;
using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Game : SingletonMonoBehaviour<Game>
{
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
		ResetGame();
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

		foreach (Transform overworldAllyTransform in Common.Instance.OverworldAllyParent)
		{
			var overworldAlly = overworldAllyTransform.GetComponent<OverworldAlly>();
			var ally = Instantiate(AllyPrefab);
			ally.InitialzeModel(overworldAlly);
			ally.AllyStrategy = AllyStrategy.Aggresive;
			Allies.Add(ally);

			Destroy(overworldAlly);

			ally.CharacterName = overworldAlly.Name;

			foreach (var skill in overworldAlly.Skills)
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
		PlayerController.Floor = floor;

		PlayerController.Inventory.Clear();
		var items = Common.Instance.GameSaveData.OverworldSaveData.Inventory.Select(x => Common.Instance.ItemManager.GetAsInventoryItemByName(x));
		Common.Instance.ItemManager.StartingItems.ForEach(x => PlayerController.Inventory.Add(x.AsInventoryItem(null)));
		items.ToList().ForEach(x => PlayerController.Inventory.Add(x));

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

		yield return new WaitForSecondsRealtime(2.0f);
		NewFloorMessage.ShowNewFloor(PlayerController.Floor);

		PlayerController.Floor++;
		PlayerController.ControlledAlly.currentInteractable = null;
		Game.Instance.PlayerController.StartTurn();
		UpdateMiniMap();
	}

	public void UpdateMiniMap()
	{
		if (CurrentDungeon != null)
		{
			var aliveAllies = Allies.Where(a => a.Vitals.HP > 0).ToList();
			var visibleTiles = new HashSet<Vector3Int>();

			foreach (var ally in aliveAllies)
			{
				var bounds = Game.Instance.CurrentDungeon.GetVisionBounds(ally, ally.TilemapPosition);

				// Add all tiles inside this ally's vision bounds to the set
				for (int x = bounds.xMin; x < bounds.xMax; x++)
				{
					for (int y = bounds.yMin; y < bounds.yMax; y++)
					{
						if (x >= 0 && x < Game.Instance.CurrentDungeon.dungeonWidth &&
							y >= 0 && y < Game.Instance.CurrentDungeon.dungeonHeight)
						{
							// Optional: if you have LOS or shape filtering, apply it here
							visibleTiles.Add(new Vector3Int(x, y, 0));
						}
					}
				}
			}

			var minimap = FindFirstObjectByType<Minimap>();
			minimap.UpdateVision(visibleTiles);

			// You can still pass a rough bounding area for performance in UpdateMinimap
			// or just reuse all visible tiles again:
			minimap.UpdateMinimapWithVisibleTiles(visibleTiles);
		}

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