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
	public ItemManager ItemManager;
	public EnemyManager EnemyManager;

	public Player PlayerCharacter;
	public Player PlayerCharacterPrefab;

	public List<Ally> Allies;
	public Ally AllyPrefab;

	public List<Character> Enemies;

	public TextMeshPro FloatingTextPrefab;

	public InventoryMenu InventoryMenu;
	public AllyActionDialog AllyMenu;
	public GameOverScreen GameOverScreen;
	public NewFloorMessage NewFloorMessage;

	public List<StatusEffect> StatusEffectPrefabs;

	internal List<Character> AllCharacters
	{
		get
		{
			var ret = new List<Character>();
			ret.Add(PlayerCharacter);
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
			Destroy(PlayerCharacter.gameObject);
		}
		PlayerCharacter = Instantiate(PlayerCharacterPrefab);
		PlayerCharacter.Camera = Camera.main;

		var ally = Instantiate(AllyPrefab);
		Allies.Add(ally);

		GameOverScreen.gameObject.SetActive(false);
		InitializeGame();

		LevelDisplay.Setup("Lv",
			() => PlayerCharacter.DisplayedVitals.Level.ToString(),
			() => { return LevelSystem.GetPercentageToNextLevel(PlayerCharacter.DisplayedVitals); });
		HpDisplay.Setup("HP",
			() => $"{PlayerCharacter.DisplayedVitals.HP}/{PlayerCharacter.DisplayedStats.HPMax}",
			() => (float)PlayerCharacter.DisplayedVitals.HP/PlayerCharacter.DisplayedStats.HPMax);
		SpDisplay.Setup("SP",
			() => $"{PlayerCharacter.DisplayedVitals.SP}/{PlayerCharacter.DisplayedStats.SPMax}",
			() => (float)PlayerCharacter.DisplayedVitals.SP/PlayerCharacter.DisplayedStats.SPMax);
		HungerDisplay.Setup("Full",
			() => $"{PlayerCharacter.DisplayedVitals.Hunger}/{PlayerCharacter.DisplayedStats.HungerMax}",
			() => (float)PlayerCharacter.DisplayedVitals.Hunger / PlayerCharacter.DisplayedStats.HungerMax);
	}

	private void InitializeGame()
	{
		PlayerCharacter.InitialzeVitalsFromStats();
		PlayerCharacter.Vitals.Level = 1;

		PlayerCharacter.SyncDisplayedStats();

		ItemManager.StartingItems.ForEach(x => PlayerCharacter.Inventory.Add(x.AsInventoryItem(null)));

		foreach(var ally in Allies)
		{
			ally.InitialzeVitalsFromStats();
			ally.Vitals.Level = 1;

			ally.SyncDisplayedStats();
		}

		UpdateUI();
		AdvanceFloor();
	}

	internal void ShowGameOver()
	{
		GameOverScreen.gameObject.SetActive(true);
		GameOverScreen.Setup(PlayerCharacter);

		MenuManager.Open(GameOverScreen);
	}

	public void AdvanceFloor()
	{
		TurnManager.InteruptTurn();
		//TurnManager.SimultaneousCoroutines?.StopAllRunningCoroutines();

		PlayerCharacter.Vitals.Floor++;
		PlayerCharacter.DisplayedVitals.Floor++;
		PlayerCharacter.currentInteractable = null;
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
		yield return null;

		var startPosition = CurrentDungeon.GetStartPositioon();
		PlayerCharacter.SetPosition(startPosition);

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
			var enemyPrefab = EnemyManager.GetEnemyPrefab(PlayerCharacter.Vitals.Floor);
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
			var item = ItemManager.GetRandomDrop(null);
			CurrentDungeon.SetDroppedItem(treasurePosition, item);
		}

		for (int i = 0; i < 5; i++)
		{
			var trapPosition = CurrentDungeon.GetDropPosition(CurrentDungeon.GetRandomOpenEnemyPosition());
			var item = ItemManager.GetRandomDrop(null);
			CurrentDungeon.SetTrap(trapPosition);
		}

		yield return new WaitForSecondsRealtime(2.0f);
		NewFloorMessage.ShowNewFloor(PlayerCharacter.Vitals.Floor);

		TurnManager.ProcessTurn();
	}

	private void Update()
	{
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (PlayerCharacter == null) { return; }
		SkilText.text = string.Join(Environment.NewLine, PlayerCharacter.Skills.Select((skill, index) => $"{index + 1}:{skill.SkillName}({skill.SPCost})"));
		FloorText.text = $"{PlayerCharacter.DisplayedVitals.Floor}F";
		LevelDisplay.UpdateUI();
		HpDisplay.UpdateUI();
		SpDisplay.UpdateUI();
		HungerDisplay.UpdateUI();

		if (PlayerCharacter.currentInteractable != null)
		{
			ActionText.text = PlayerCharacter.currentInteractable.GetInteractionText();
		}
		else
		{
			ActionText.text = "";
		}

		var inventoryText = 
			@$"Gold {PlayerCharacter.DisplayedVitals.Gold}g
Bag {PlayerCharacter.Inventory.InventoryItems.Count}/{PlayerCharacter.Inventory.MaxItems}";

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