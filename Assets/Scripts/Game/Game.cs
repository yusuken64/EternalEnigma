using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
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

	public StatsDisplay LevelDisplay;
	public StatsDisplay HpDisplay;
	public StatsDisplay HungerDisplay;
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

		LevelDisplay.Setup("Lv",
			() => PlayerCharacter.DisplayedVitals.Level.ToString(),
			() => { return LevelSystem.GetPercentageToNextLevel(PlayerCharacter.DisplayedVitals); });
		HpDisplay.Setup("HP",
			() => $"{PlayerCharacter.DisplayedVitals.HP}/{PlayerCharacter.DisplayedStats.HPMax}",
			() => (float)PlayerCharacter.DisplayedVitals.HP/PlayerCharacter.DisplayedStats.HPMax);
		HungerDisplay.Setup("Full",
			() => $"{PlayerCharacter.DisplayedVitals.Hunger}/{PlayerCharacter.DisplayedStats.HungerMax}",
			() => (float)PlayerCharacter.DisplayedVitals.Hunger / PlayerCharacter.DisplayedStats.HungerMax);
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
		PlayerCharacter.InitialzeVitalsFromStats();
		PlayerCharacter.Vitals.Level = 1;

		PlayerCharacter.SyncDisplayedStats();

		ItemManager.StartingItems.ForEach(x => PlayerCharacter.Inventory.Add(x.AsInventoryItem(null)));

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
		TurnManager.InteruptTurn();
		//TurnManager.SimultaneousCoroutines?.StopAllRunningCoroutines();

		TurnManager.CurrentTurnPhase = TurnPhase.Player;
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
		CurrentDungeon = DungeonGenerator.GeneratedDungeon;
		CurrentDungeon.InitializeCache();
		yield return null;

		var startPosition = CurrentDungeon.GetStartPositioon();
		PlayerCharacter.SetPosition(startPosition);

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


		yield return new WaitForSecondsRealtime(2.0f);
		NewFloorMessage.ShowNewFloor(PlayerCharacter.Vitals.Floor);
	}

	private void Update()
	{
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (PlayerCharacter == null) { return; }
		LevelDisplay.UpdateUI();
		HpDisplay.UpdateUI();
		HungerDisplay.UpdateUI();

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