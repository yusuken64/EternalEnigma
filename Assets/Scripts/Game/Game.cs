using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Game : SingletonMonoBehaviour<Game>
{
	public DungeonGenerator DungeonGenerator;
	public Dungeon CurrentDungeon;

	public TurnManager TurnManager;
	public LevelSystem LevelSystem;
	public ItemManager ItemManager;
	public EnemyManager EnemyManager;

	public Player PlayerCharacter;
	public Player PlayerCharacterPrefab;

	public List<Enemy> Enemies;

	public TextMeshPro FloatingTextPrefab;

	public InventoryMenu InventoryMenu;
	public GameOverScreen GameOverScreen;
	public NewFloorMessage NewFloorMessage;

	internal List<Character> AllCharacters
	{
		get
		{
			var ret = new List<Character>();
			ret.Add(PlayerCharacter);
			ret.AddRange(Enemies);

			return ret;
		}
	}

	public List<Character> DeadUnits; //dead units are added to this, destroy at end turn;

	public TextMeshProUGUI StatsText;
	public TextMeshProUGUI ActionText;
	public TextMeshProUGUI InventoryText;

	// Start is called before the first frame update
	void Start()
	{
		ResetGame();
	}

	internal void ResetGame()
	{
		if (PlayerCharacter != null)
		{
			PlayerCharacter.ActionPicked -= PlayerCharacter_ActionPicked;
			Destroy(PlayerCharacter.gameObject);
		}
		PlayerCharacter = Instantiate(PlayerCharacterPrefab);
		PlayerCharacter.Camera = Camera.main;
		GameOverScreen.gameObject.SetActive(false);
		InitializeGame();

		PlayerCharacter.ActionPicked += PlayerCharacter_ActionPicked;
	}

	private void PlayerCharacter_ActionPicked(GameAction gameAction)
	{
		List<Actor> actors = new();
		actors.Add(PlayerCharacter);
		actors.AddRange(Enemies);
		TurnManager.ProcessTurn(actors);
	}

	private void InitializeGame()
	{
		PlayerCharacter.BaseStats.Level = 1;
		PlayerCharacter.BaseStats.HPMax = 20;
		PlayerCharacter.BaseStats.HP = 20;
		PlayerCharacter.BaseStats.Strength = 5;
		PlayerCharacter.BaseStats.Hunger = 100;
		PlayerCharacter.BaseStats.Gold = 0;
		PlayerCharacter.BaseStats.EXP = 0;
		PlayerCharacter.SyncStats();
		PlayerCharacter.Inventory.Add(ItemManager.GetAsInventoryItemByName("Bread"));
		//PlayerCharacter.Inventory.Add(ItemManager.GetAsInventoryItemByName("Wooden Arrows"));
		//PlayerCharacter.Inventory.Add(ItemManager.GetAsInventoryItemByName("Excalibur"));

		UpdateUI();
		AdvanceFloor();
	}

	internal void ShowGameOver()
	{
		PlayerCharacter.ActionPicked -= PlayerCharacter_ActionPicked;
		GameOverScreen.gameObject.SetActive(true);
		GameOverScreen.Setup(PlayerCharacter);

		MenuManager.Open(GameOverScreen);
	}

	public void AdvanceFloor()
	{
		TurnManager.StopAllCoroutines();
		TurnManager.SimultaneousCoroutines?.StopAllRunningCoroutines();

		TurnManager.CurrentTurnPhase = TurnPhase.Player;
		PlayerCharacter.BaseStats.Floor++;
		PlayerCharacter.currentInteractable = null;
		StartCoroutine(AdvanceFloorRoutine());

		NewFloorMessage.ShowNewFloor(PlayerCharacter.BaseStats.Floor);
	}

	private IEnumerator AdvanceFloorRoutine()
	{
		yield return null;
		if (CurrentDungeon != null)
		{
			Destroy(CurrentDungeon.gameObject);
		}
		CurrentDungeon = DungeonGenerator.GenerateDungeon();
		var startPosition = DungeonGenerator.GetStartPositioon();
		PlayerCharacter.SetPosition(startPosition);

		Enemies.ForEach(x => DestroyImmediate(x.gameObject));
		Enemies.Clear();

		for (int i = 0; i < 10; i++)
		{
			var enemyPrefab = EnemyManager.GetEnemyPrefab(PlayerCharacter.RealStats.Floor);
			var enemy = Instantiate(enemyPrefab, this.transform);
			enemy.TilemapPosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomEnemyPosition()).Value;
			Enemies.Add(enemy);
		}

		for(int i = 0; i < 5; i++)
		{
			var treasurePosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomEnemyPosition()).Value;
			CurrentDungeon.SetTreasure(treasurePosition, DungeonGenerator.TreasureTile);
		}

		for (int i = 0; i < 5; i++)
		{
			var treasurePosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomEnemyPosition()).Value;
			var item = ItemManager.GetRandomDrop(null);
			CurrentDungeon.SetDroppedItem(treasurePosition, item,  DungeonGenerator.DroppedItemTile);
		}
	}

	private void Update()
	{
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (PlayerCharacter == null) { return; }
		PlayerCharacter.SyncStats();

		StatsText.text = @$"Floor : {PlayerCharacter.DisplayedStats.Floor}
Level : {PlayerCharacter.DisplayedStats.Level}
HP : {PlayerCharacter.DisplayedStats.HP}/{PlayerCharacter.DisplayedStats.HPMax}
Hunger: {PlayerCharacter.DisplayedStats.Hunger}
Treasure: {PlayerCharacter.DisplayedStats.Gold}";

		if (PlayerCharacter.currentInteractable != null)
		{
			ActionText.text = PlayerCharacter.currentInteractable.GetInteractionText();
		}
		else
		{
			ActionText.text = "";
		}

		var inventoryText = $"Bag {PlayerCharacter.Inventory.InventoryItems.Count}/{PlayerCharacter.Inventory.MaxItems}";

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