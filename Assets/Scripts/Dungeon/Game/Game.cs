using DG.Tweening;
using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public partial class Game : SingletonMonoBehaviour<Game>
{
	public bool IsReady { get; private set; }
	private DemoDungeonLoadout demoLoadout;
	public TileWorldDungeonGenerator DungeonGenerator;
	public TileWorldDungeon CurrentDungeon;
    public EternalEnigma.Core.Progression.DungeonRunPlan RunPlan { get; private set; }
    public EncounterRuntime Encounter { get; private set; }
    public PrisonEscrow Prison { get; private set; }
    private FloorEntrySnapshot entrySnapshot;
    private UnityEngine.Random.State floorGenerationRandom;
    private bool resettingFloor;
    public bool CanExitFloor => Finale != null ? Finale.Session.FloorCleared : Encounter == null || Encounter.CanExit;
    public bool CanResetFloor => IsReady && !resettingFloor && entrySnapshot != null && Encounter?.AllowsReset == true &&
        Common.Instance.CampaignContext?.Completed.Contains("repeatable-0") == true && !Common.Instance.Travel.IsTransitioning;
    public bool CheckFloorExit(bool announce = true)
    {
        if (CanExitFloor) return true;
        if (announce) GameMessages.Post(Finale?.ObjectiveText ?? Encounter.ObjectiveText);
        return false;
    }
    internal bool ResetFloor()
    {
        if (!CanResetFloor) return false;
        resettingFloor = true; IsReady = false; TurnManager.InteruptTurn();
        StartCoroutine(AdvanceFloorRoutine(true)); return true;
    }

	public TurnManager TurnManager;
	public LevelSystem LevelSystem;
	public EnemyManager EnemyManager;

	public PlayerController PlayerController;
	public GameObject ThrownItemProjectilePrefab;

	public List<Ally> Allies;
	public Ally AllyPrefab;

	// Allies at 0 HP: out of Allies/AllCharacters, not destroyed. Restored by Revive or the next floor.
	public List<Ally> DownedAllies = new();

	// Per-floor reveal flags (Floor Sense, Farsight, Treasure Hunter). Reset every floor.
	public FloorRevealState FloorReveal = new();

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
	public Transform CharacterStatsDisplayContainer;
	public CharacterStatsDisplay CharacterStatsDisplayPrefab;
	public List<CharacterStatsDisplay> CharacterStatsDisplays;

	// Start is called before the first frame update
	IEnumerator Start()
	{
#if UNITY_EDITOR
		// The editor bootstrap loads Common before reloading the requested scene.
		// Do not initialize the original scene while that asynchronous load is pending.
		while (FindFirstObjectByType<Common>() == null) yield return null;
		PrepareEditorParty();
#endif
        var common = Common.Instance;
        bool finale = common.CampaignContext != null &&
            common.CampaignContext.State.PendingDungeon == common.CampaignContext.Campaign.FinalLocationId;
        common.ScreenTransition.HoldClosed(finale ? FinaleRuntime.LoadingTitle(1) : "Loading...");
        yield return ScreenTransition.PresentLoadingScreen();
        common.Travel.SceneReady();
		ResetGame();
		StartCoroutine(RevealDungeonWhenReady());
		yield break;
	}

#if UNITY_EDITOR
	private void PrepareEditorParty()
	{
		var common = Common.Instance;
		if (common.TownAllyParent.GetComponentInChildren<TownAlly>() != null) return;

		// A direct scene launch has no party transferred from town. Use a fresh,
		// non-persistent run instead of changing the player's saved campaign.
		var configuration = TownSceneLoader.Default;
		configuration.Validate();
		common.CampaignContext = null;
		common.GameSaveData = new GameSaveData {
			IsSandbox = true,
			DungeonSaveData = new DungeonSaveData { StartFloor = 1, EndFloor = 5 }
		};
		common.GameSaveData.TownSaveData.ConfigurationId = configuration.Id;
		common.GameSaveData.TownSaveData.InventoryItems = ItemSaveData.Capture(common.ItemManager.StartingItems
			.Select(item => item.AsInventoryItem(null)));
		common.InstantiatedTownAllies.Clear();
		foreach (var prefab in configuration.StartingParty)
		{
			var ally = Instantiate(prefab, common.TownAllyParent);
			ally.EnsureStartingSkills();
			common.InstantiatedTownAllies.Add(ally);
			common.GameSaveData.TownSaveData.RecruitedAlliesData.Add(HeroClassBinding.FromPrefab(ally,
				new TownAllyData { AllyId = ally.Id, AllyName = ally.Name, Skills = new List<string>(ally.Skills) }));
		}
	}
#endif

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
		DownedAllies.Clear();
        var context = Common.Instance.CampaignContext;
        RunPlan = context != null && context.State.PendingDungeon.Length > 0 ? context.RunPlan(context.State.PendingDungeon) : null;
        if (context?.State.PendingDungeon == context?.Campaign.FinalLocationId && context != null)
        {
            Finale = new FinaleRuntime(this, context.BeginFinale());
            if (CurrentDungeon != null) Destroy(CurrentDungeon.gameObject);
            CurrentDungeon = null;
            Common.Instance.GameSaveData.DungeonSaveData.StartFloor = 1;
            Common.Instance.GameSaveData.DungeonSaveData.EndFloor = EternalEnigma.Core.Progression.FinalePlan.FloorCount;
        }

        var hud = DungeonHud.Ensure(this);
        foreach(var slot in hud.PartySlots) slot.gameObject.SetActive(false);
		CharacterStatsDisplays.Clear();

		foreach (Transform townAllyTransform in Common.Instance.TownAllyParent)
		{
			var townAlly = townAllyTransform.GetComponent<TownAlly>();
			var ally = Instantiate(AllyPrefab);
			ally.InitialzeModel(townAlly);
			ally.AllyStrategy = AllyStrategy.Aggresive;
			Allies.Add(ally);

            ally.CharacterName = townAlly.Name;
            ally.TownAllyId = townAlly.Id;
            ally.PrimaryClass = townAlly.PrimaryClass;
            ally.SecondaryClass = townAlly.SecondaryClass;
            ally.Attributes = townAlly.Attributes;
            foreach (var equipment in ItemSaveData.Capture(townAlly.Equipment.GetEquippedItems()))
                ally.Equipment.Equip((EquipableInventoryItem)equipment.Restore(Common.Instance.ItemManager));
            // Player-initiated dungeon equips (EquipAction / EquipEffectDefinition use CanEquip) respect the class.
            var dungeonAlly = ally;
            ally.Equipment.ClassFilter = item => HeroClass.AllowsItem(dungeonAlly.PrimaryClass, dungeonAlly.SecondaryClass, item);

			foreach (var skill in townAlly.Skills)
			{
				Skill skillInstance = Common.Instance.SkillManager.GetSkillInstanceByName(skill);
				skillInstance.Rank = Mathf.Max(1, townAlly.GetRank(skill));
				ally.Skills.Add(skillInstance);
			}
			ally.InvalidateCachedStats();

			ally.InitialzeVitalsFromStats();
			ally.Vitals.Level = Mathf.Max(1, townAlly.Level);
            ally.Vitals.Exp = townAlly.Experience;
            ally.BaseStats = HeroStatRules.BaseStats(ally.StartingStats, ally.PrimaryClass, ally.Vitals.Level, ally.Attributes);
            ally.InvalidateCachedStats();
            ally.Vitals.HP = ally.FinalStats.HPMax;
            ally.Vitals.SP = ally.FinalStats.SPMax;
			// Damage carries over between runs; the inn resets it. -1 (never hurt) and downed allies start at a sane value.
			if (townAlly.Hp >= 0) ally.Vitals.HP = Mathf.Max(1, townAlly.Hp);
			if (townAlly.Sp >= 0) ally.Vitals.SP = townAlly.Sp;
            ally.Vitals.Hunger = townAlly.HasHunger ? Mathf.Clamp(townAlly.Hunger, 0, ally.FinalStats.HungerMax) : ally.FinalStats.HungerMax;
            ally.Vitals.HungerAccumulate = townAlly.HasHunger ? Mathf.Max(0, townAlly.HungerAccumulate) : 0;

			ally.SyncDisplayedStats();

			var newItem = hud.PartySlots[CharacterStatsDisplays.Count];
            newItem.gameObject.SetActive(true);
			newItem.Setup(ally);
			CharacterStatsDisplays.Add(newItem);

			// InitialzeModel moves the animated model to the dungeon ally. Remove
			// the remaining town prefab, including its position circle, from Common.
			Destroy(townAlly.gameObject);
		}

		PlayerController.TakeControl(Allies[0]);

		var floor = Common.Instance.GameSaveData.DungeonSaveData.StartFloor;
		PlayerController.Floor = floor - 1;

		PlayerController.Inventory.Clear();
        var townSave = Common.Instance.GameSaveData.TownSaveData;
        PlayerController.Gold = townSave.Gold;
        var items = townSave.InventoryItems.Select(i => i.Restore(Common.Instance.ItemManager));
        items.ToList().ForEach(x => PlayerController.Inventory.Add(x));
        Prison = new PrisonEscrow();
        if (context?.Location?.Encounter?.Kind == EternalEnigma.Core.Progression.EncounterKind.Prison) Prison.Confiscate(this);
        if (context?.Completed.Contains("repeatable-0") == true)
            foreach (var ally in Allies) if (!ally.Skills.Any(s => s.SkillName == "Reset Floor")) ally.Skills.Add(ResetFloorAction.Skill());
		demoLoadout = Common.Instance.PendingDemoLoadout;
		Common.Instance.PendingDemoLoadout = null;
		if (demoLoadout != null) demoLoadout.Apply(this);

		UpdateUI();
		AdvanceFloor();
	}

	internal void ShowGameOver(bool victory = false)
	{
		if (victory && DungeonPreferences.AnimationMode != DungeonAnimationMode.NoAnimations)
			foreach (var ally in Allies.Where(a => a != null && a.Vitals.HP > 0)) ally.HeroAnimator?.PlayOneShot(AnimatedAction.Victory);
		if (victory) { IsReady = false; TurnManager.InteruptTurn(); }
		GameOverScreen.gameObject.SetActive(true);
		GameOverScreen.Setup(PlayerController, victory);

		MenuManager.Open(GameOverScreen);
	}

	public void AdvanceFloor()
	{
        if (Finale != null)
        {
            if (CurrentDungeon == null) { PlayerController.Floor = 1; StartCoroutine(StartFinale()); }
            else Finale.RequestExit();
            return;
        }
        if (CurrentDungeon != null && !CheckFloorExit()) return;
        if (resettingFloor || Common.Instance.Travel.IsTransitioning) return;
		if (DungeonPreferences.AnimationMode != DungeonAnimationMode.NoAnimations)
			foreach (var ally in Allies.Where(a => a != null && a.Vitals.HP > 0)) ally.HeroAnimator?.PlayOneShot(AnimatedAction.Victory);
		PlayerController.Floor++;
		IsReady = false;
		TurnManager.InteruptTurn();
		
		StartCoroutine(AdvanceFloorRoutine());

		NewFloorMessage.HideScreen(PlayerController.Floor);
	}

	private IEnumerator AdvanceFloorRoutine(bool reset = false)
	{
        if (reset) UnityEngine.Random.state = floorGenerationRandom;
        else { entrySnapshot?.Dispose(); entrySnapshot = null; floorGenerationRandom = UnityEngine.Random.state; }
        bool throneFloor = AutoplayRunner.SweepGeneration?.IsThroneFloor ?? (RunPlan != null ? RunPlan.Floor(PlayerController.Floor).Options.IsThroneFloor :
            PlayerController.Floor == Common.Instance.GameSaveData.DungeonSaveData.StartFloor || PlayerController.Floor == Common.Instance.GameSaveData.DungeonSaveData.EndFloor);
        Encounter = null;

		Enemies.ForEach(x => DestroyImmediate(x.gameObject));
		Enemies.Clear();

        foreach(var ally in Allies) if(ally!=null) ally.currentInteractable=null;
		SummonRules.DespawnClones(this);
		PartyRules.RestoreAllDowned(this, 1);
		FloorReveal = new FloorRevealState();

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

		CurrentDungeon = DungeonGenerator.GeneratedDungeon;
		CurrentDungeon.IsThroneFloor = throneFloor;
		CurrentDungeon.IsExitFloor = PlayerController.Floor >= Common.Instance.GameSaveData.DungeonSaveData.EndFloor;
		CurrentDungeon.InitializeCache();
		FindFirstObjectByType<FogOverlay>().Initialize(CurrentDungeon);
		FindFirstObjectByType<Minimap>().Initialize(CurrentDungeon);

		FloorReveal.TreasureRevealed = PassiveModifiers.PartyHas<RevealTreasurePassive>(this);

		yield return null;

		var floor = CurrentDungeon.Floor;
        Encounter = new EncounterRuntime(this);
		var startPosition = floor.Start.ToCell();
		PlayerController.ControlledAlly.SetPosition(startPosition);

		foreach (var ally in Allies)
		{
			if (ally != null && ally != PlayerController.ControlledAlly)
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

		CurrentDungeon.SetStairs(floor.Stairs.ToCell());
		Debug.Log("Stairs Created", this);

		if (!throneFloor)
		{
            int difficulty = RunPlan?.Floor(PlayerController.Floor).CombatDifficulty ?? PlayerController.Floor;
            Encounter.Populate(difficulty);
            // Spawn blockers before testing enemy footprints. A large creature can
            // overlap a neighboring prop even when its seeded center is free.
            foreach (var definition in floor.Scenery)
                DungeonProp.Create(CurrentDungeon, definition, DungeonGenerator.ThemeCatalog.Get(DungeonGenerator.CurrentVisuals));
            foreach (var floorLock in floor.Locks)
                DungeonProp.Create(CurrentDungeon, new EternalEnigma.Core.World.DungeonScenery(floorLock.Door,
                    EternalEnigma.Core.World.DungeonSceneryKind.Door, floorLock.HitPoints, 0, EternalEnigma.Core.World.SceneryReward.None),
                    DungeonGenerator.ThemeCatalog.Get(DungeonGenerator.CurrentVisuals));
            CurrentDungeon.DoorsChanged();
            var reservedSpawns = new HashSet<Vector3Int>(floor.Enemies.Select(p => p.Cell.ToCell())
                .Concat(floor.Scenery.Select(p => p.Cell.ToCell())).Concat(floor.Locks.Select(p => p.Door.ToCell())));
            reservedSpawns.Add(floor.Stairs.ToCell());
			foreach (var p in floor.Enemies)
			{
				EnemyManager.SpawnPack(EnemyManager.GetEnemyPrefab(difficulty, p.Roll), p.Cell.ToCell(), reservedSpawns);
			}

			foreach (var p in floor.Gold) CurrentDungeon.SetTreasure(p.Cell.ToCell(), p.Roll);

			foreach (var p in floor.Items) CurrentDungeon.SetDroppedItem(p.Cell.ToCell(), Common.Instance.ItemManager.GetRandomDrop(p.Roll, difficulty));

			foreach (var p in floor.Traps) CurrentDungeon.SetTrap(p.Cell.ToCell(), p.Roll);

			SpawnGatheringPoints(startPosition);
            if (PlayerController.Floor <= 3 && RunPlan?.IsEncounter != true) EarlyDungeonStatue.Place(CurrentDungeon, DungeonGenerator.ThemeCatalog.Get(DungeonGenerator.CurrentVisuals));
		}

		if (demoLoadout != null && PlayerController.Floor == Common.Instance.GameSaveData.DungeonSaveData.StartFloor)
			yield return demoLoadout.PreparePracticeRoom(this);
		if (!AutoplayRunner.SuppressesAnimations) yield return new WaitForSecondsRealtime(2.0f);
		NewFloorMessage.ShowNewFloor(PlayerController.Floor);

        if (!reset) ClassPassives.OnFloorStart(this);
        else entrySnapshot.Restore(this);
		PlayerController.ControlledAlly.currentInteractable = null;
        foreach (var actor in AllCharacters.Where(c=>c!=null && c.Vitals.HP>0))
        {
            actor.Vitals.ActionsPerTurnLeft=actor.FinalStats.ActionsPerTurnMax;
            actor.Vitals.AttacksPerTurnLeft=actor.FinalStats.AttacksPerTurnMax;
            actor.SyncDisplayedStats();
        }
		Game.Instance.PlayerController.StartTurn();
		UpdateMiniMap();
        if (AutoplayRunner.Sweeping)
        {
            AutoplayRunner.Active.LootLedger?.InspectPlacement(CurrentDungeon);
            AutoplayRunner.Active.LootLedger?.VerifyGeneration(CurrentDungeon);
        }
		IsReady = true;
        resettingFloor = false;
        if (!reset && Encounter.AllowsReset) entrySnapshot = new FloorEntrySnapshot(this);
        ScenePresentation.RegisterWorld(CurrentDungeon.transform);
        ScenePresentation.RegisterWorld(DungeonGenerator.transform);
	}

    internal readonly HashSet<Vector3Int> PartyVisibleTiles = new();
    private readonly Dictionary<Ally, Vector3Int> displayedSightOrigins = new();
    private TileWorldDungeon sightDungeon;
    internal readonly HashSet<Vector3Int> PlaybackVisibleTiles = new();

    private IEnumerable<Ally> SightAllies() => Allies.Concat(DownedAllies).Concat(DeadUnits.OfType<Ally>())
        .Where(a => a != null && a.DisplayedVitals.HP > 0);

    private void LateUpdate() => RefreshSight();

    internal void RefreshSight()
    {
        if (!IsReady || CurrentDungeon == null || !CurrentDungeon.EnsureRuntimeData()) return;
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
        using var measurement = new FinalePerformance.Scope("Minimap and sight");
        if (CurrentDungeon == null || !CurrentDungeon.EnsureRuntimeData()) return;
        sightDungeon = CurrentDungeon;
        PartyVisibleTiles.Clear();
        displayedSightOrigins.Clear();
        foreach (var ally in SightAllies())
        {
            var origin = CurrentDungeon.WorldToCell(ally.transform.position);
            displayedSightOrigins[ally] = origin;
            PartyVisibleTiles.UnionWith(CurrentDungeon.GetVisibleTiles(ally, origin));
        }
        var minimap = PlayerController.Minimap;
        minimap.UpdateVision(PartyVisibleTiles);
        minimap.UpdateMinimapWithVisibleTiles(PartyVisibleTiles);
    }

	private void Update()
	{
		UpdateUI();
	}

	public void UpdateUI()
	{
        // The loading screen renders before party/HUD initialization.
        if (PlayerController == null || PlayerController.ControlledAlly == null) return;
		CharacterStatsDisplays.ForEach(x => x.UpdateUI());
	}
	
	[ContextMenu("AdvanceFloor")]
	public void AdvanceFloorCommand()
	{
		AdvanceFloor();
	}

	private void SpawnGatheringPoints(Vector3Int startPosition)
	{
        if(Common.Instance.GameSaveData.DungeonSaveData.UseBiomeLayout)
        {
            foreach(var site in CurrentDungeon.Floor.GatheringSites)
            {
                if (AutoplayRunner.Sweeping) AutoplayRunner.Active.LootLedger?.DeclareGathering(site.Cell.ToCell());
                GatheringPoint.Spawn(CurrentDungeon,site.Cell.ToCell(),site.Kind,site.Roll);
            }
            return;
        }
		var stairs = CurrentDungeon.GetStairsCell();
		if (stairs == null) return;
		var floorLayer = new EternalEnigma.Core.World.GridLayer(CurrentDungeon.GetFloorMask());
		var occupied = new List<EternalEnigma.Core.World.GridPoint>();
		foreach (var interactable in CurrentDungeon.Interactables)
			if (interactable != null) occupied.Add(new EternalEnigma.Core.World.GridPoint(interactable.Position.x, interactable.Position.y));
		foreach (var character in AllCharacters)
			if (character != null) occupied.Add(new EternalEnigma.Core.World.GridPoint(character.TilemapPosition.x, character.TilemapPosition.y));
		var context = Common.Instance.CampaignContext;
		int seed = context != null
			? context.LocationSeed(context.State.LocationId, PlayerController.Floor)
			: UnityEngine.Random.Range(int.MinValue, int.MaxValue);
		var sites = EternalEnigma.Core.Generation.GatheringPlacement.Place(floorLayer,
			new EternalEnigma.Core.World.GridPoint(startPosition.x, startPosition.y),
			new EternalEnigma.Core.World.GridPoint(stairs.Value.x, stairs.Value.y),
			occupied, seed, EternalEnigma.Core.Generation.GatheringPlacement.DefaultCount);
		foreach (var site in sites)
		{
            if (AutoplayRunner.Sweeping) AutoplayRunner.Active.LootLedger?.DeclareGathering(site.Cell.ToCell());
			GatheringPoint.Spawn(CurrentDungeon, new Vector3Int(site.Cell.X, site.Cell.Y, 0), site.Kind, site.Roll);
		}
	}

    public void DoFloatingText(string message, Color color, Character subject)
    {
        if (subject == null) return;
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.Normal)
            DungeonFloatingText.Show(this, message, color, subject);
        GameMessages.ForCharacter(subject, $"{GameMessages.Name(subject)}\n{message}");
    }
	public void DoFloatingText(string message, Color color, Vector3 worldPosition)
	{
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.Normal)
            DungeonFloatingText.Show(this, message, color, worldPosition);
        var subject = AllCharacters.Where(c => c != null)
            .OrderBy(c => Vector3.SqrMagnitude((c.VisualParent != null ? c.VisualParent.transform.position : c.transform.position) - worldPosition))
            .FirstOrDefault();
        if (subject != null && Vector3.Distance((subject.VisualParent != null ? subject.VisualParent.transform.position : subject.transform.position), worldPosition) < 2f)
            GameMessages.ForCharacter(subject, $"{GameMessages.Name(subject)}\n{message}");
        else if (FogOverlay.Instance == null || FogOverlay.Instance.IsCurrentlyVisible(worldPosition)) GameMessages.Post(message);
	}
}
