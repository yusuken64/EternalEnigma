using DG.Tweening;
using JuicyChickenGames.Menu;
using System;
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
	//public Player PlayerCharacterPrefab;
	public List<OverworldAlly> DebugAllies;
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

	public StatsDisplay LevelDisplay;
	public StatsDisplay HpDisplay;
	public StatsDisplay SpDisplay;
	public StatsDisplay HungerDisplay;
	public TextMeshProUGUI SkilText;
	public TextMeshProUGUI FloorText;
	public TextMeshProUGUI InventoryText;

	// Start is called before the first frame update
	void Start()
	{
		ResetGame();
	}

	internal void ResetGame()
	{
		PlayerController.CameraController.Camera = Camera.main;

#if UNITY_EDITOR
		foreach (var overworldAlly in DebugAllies)
		{
			var ally = Instantiate(AllyPrefab);
			ally.InitialzeModel(overworldAlly);
			Allies.Add(ally);

			Destroy(overworldAlly);
		}
#endif

		foreach (var overworldAlly in Common.Instance.GameSaveData.OverworldSaveData.RecruitedAllies)
		{
			var ally = Instantiate(AllyPrefab);
			ally.InitialzeModel(overworldAlly);
			Allies.Add(ally);

			Destroy(overworldAlly);
		}

		GameOverScreen.gameObject.SetActive(false);
		InitializeGame();

		LevelDisplay.Setup("Lv",
			() => PlayerController.DisplayedVitals.Level.ToString(),
			() => { return LevelSystem.GetPercentageToNextLevel(PlayerController.DisplayedVitals); });
		HpDisplay.Setup("HP",
			() => $"{PlayerController.DisplayedVitals.HP}/{PlayerController.DisplayedStats.HPMax}",
			() => (float)PlayerController.DisplayedVitals.HP/PlayerController.DisplayedStats.HPMax);
		SpDisplay.Setup("SP",
			() => $"{PlayerController.DisplayedVitals.SP}/{PlayerController.DisplayedStats.SPMax}",
			() => (float)PlayerController.DisplayedVitals.SP/PlayerController.DisplayedStats.SPMax);
		HungerDisplay.Setup("Full",
			() => $"{PlayerController.DisplayedVitals.Hunger}/{PlayerController.DisplayedStats.HungerMax}",
			() => (float)PlayerController.DisplayedVitals.Hunger / PlayerController.DisplayedStats.HungerMax);
	}

	private void InitializeGame()
	{
		foreach (var ally in Allies)
		{
			ally.InitialzeVitalsFromStats();
			ally.Vitals.Level = 1;

			ally.SyncDisplayedStats();
		}

		PlayerController.TakeControl(Allies[0]);

		//PlayerCharacter.InitialzeVitalsFromStats();
		//PlayerCharacter.InitialzeSkillsFromSave();
		var floor = Common.Instance.GameSaveData.StartingFloor;
		PlayerController.Vitals.Floor = floor;
		Common.Instance.GameSaveData.StartingFloor = 0;
		//PlayerCharacter.SyncDisplayedStats();
		//PlayerCharacter.HeroAnimator.SetWeapon(null, null);

		var items = Common.Instance.GameSaveData.OverworldSaveData.Inventory.Select(x => Common.Instance.ItemManager.GetAsInventoryItemByName(x));
		Common.Instance.ItemManager.StartingItems.ForEach(x => PlayerController.Inventory.Add(x.AsInventoryItem(null)));
		items.ToList().ForEach(x => PlayerController.Inventory.Add(x));

		UpdateUI();
		AdvanceFloor();
	}

	internal void ShowGameOver()
	{
		GameOverScreen.gameObject.SetActive(true);
		GameOverScreen.Setup(PlayerController.ControlledAlly);

		MenuManager.Open(GameOverScreen);
	}

	public void AdvanceFloor()
	{
		TurnManager.InteruptTurn();
		//TurnManager.SimultaneousCoroutines?.StopAllRunningCoroutines();

		PlayerController.Vitals.Floor++;
		PlayerController.DisplayedVitals.Floor++;
		PlayerController.ControlledAlly.currentInteractable = null;
		StartCoroutine(AdvanceFloorRoutine());

		NewFloorMessage.HideScreen();
	}

	private IEnumerator AdvanceFloorRoutine()
	{
		Enemies.ForEach(x => DestroyImmediate(x.gameObject));
		Enemies.Clear();

		yield return null;
		if (CurrentDungeon != null)
		{
			Destroy(CurrentDungeon.gameObject);
			CurrentDungeon = null;
		}
		DungeonGenerator.GenerateDungeon();
		while(DungeonGenerator.GeneratedDungeon == null)
		{
			yield return null;
		}
		yield return null;

		var map = GameObject.Find("TileWorldCreator_Map");
		map.transform.position = new Vector3(0, 0, -1.50999999f);
		map.transform.localScale = new Vector3(1, 1, 3.3499999f);

		CurrentDungeon = DungeonGenerator.GeneratedDungeon;
		CurrentDungeon.InitializeCache();
		FindFirstObjectByType<FogOverlay>().Initialize(CurrentDungeon);
		FindFirstObjectByType<Minimap>().Initialize(CurrentDungeon);

		yield return null;

		var startPosition = CurrentDungeon.GetStartPositioon();
		PlayerController.ControlledAlly.SetPosition(startPosition);
		var visibleTiles = Game.Instance.CurrentDungeon.GetVisionBounds(PlayerController.ControlledAlly, PlayerController.TilemapPosition);
		Minimap minimap = FindFirstObjectByType<Minimap>();
		minimap.UpdateVision(visibleTiles);
		minimap.UpdateMinimap(visibleTiles);

		foreach (var ally in Allies)
		{
			//var allyPosition = CurrentDungeon.GetDropPosition(startPosition);
			if (ally != null)
			{
				ally.SetPosition(startPosition);
			}
		}

		CurrentDungeon.SetStairs(CurrentDungeon.GetRandomOpenEnemyPosition());
		Debug.Log("Stairs Created", this);

		for (int i = 0; i < 10; i++)
		{
			var enemyPrefab = EnemyManager.GetEnemyPrefab(PlayerController.Vitals.Floor);
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

		yield return new WaitForSecondsRealtime(2.0f);
		NewFloorMessage.ShowNewFloor(PlayerController.Vitals.Floor);

		TurnManager.ProcessTurn();
	}

	private void Update()
	{
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (PlayerController == null) { return; }
		//SkilText.text = string.Join(Environment.NewLine, PlayerCharacter.Skills.Select((skill, index) => $"{index + 1}:{skill.SkillName}({skill.SPCost})"));
		FloorText.text = $"{PlayerController.DisplayedVitals.Floor}F";
		LevelDisplay.UpdateUI();
		HpDisplay.UpdateUI();
		SpDisplay.UpdateUI();
		HungerDisplay.UpdateUI();

		var inventoryText = 
			@$"Gold {PlayerController.DisplayedVitals.Gold}g
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