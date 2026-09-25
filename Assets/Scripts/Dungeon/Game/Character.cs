using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Character : MonoBehaviour, Actor
{
	[SerializeField]
	private Vector3Int tilemapPosition;

	public Vector3Int TilemapPosition
	{
		get => tilemapPosition;
		set
		{
			if (tilemapPosition != value)
			{
				tilemapPosition = value;
				MovedThisTurn = true;
			}
		}
	}

	public bool MovedThisTurn { get; internal set; }
	public Team Team;

	public GameObject VisualParent;
	public Facing CurrentFacing;

	public StartingStats StartingStats;
	public FootPrint FootPrint;

	private Stats baseStats;

	public Stats BaseStats
	{
		get => baseStats;
		set
		{
			if (baseStats?.GetHashCode() != value.GetHashCode())
			{
				if (baseStats != null)
				{
					baseStats.OnStatChanged -= BaseStats_OnStatChanged;
				}

				baseStats = value;
				baseStats.OnStatChanged += BaseStats_OnStatChanged;
				cachedFinalStats = null;
			}
		}
	}

	private Stats cachedFinalStats = null;

	internal bool CanCast(Skill skill, out string reason)
	{
		if (skill == null || !Skills.Contains(skill))
		{
			reason = "Skill not learned";
			return false;
		}
		if (Vitals.HP <= 0 || StatusEffects.Any(x => !x.IsExpired() &&
			(x.PreventsMenu() || x.Interupts(new SkillAction(this, skill, this)))))
		{
			reason = "Cannot cast while incapacitated or silenced";
			return false;
		}
		if (skill.ActivationType != ActivationType.Active)
		{
			reason = "Not active skill";
			return false;
		}

		bool inventoryTargeting = skill.Targeting == SkillTargeting.InventoryItem;
		if (skill.SPCost < 0 || !skill.TargetingRules.IsConfigured || skill.ActionEffects == null ||
			(!inventoryTargeting && skill.ActionEffects.Any(effect => effect is InventorySkillEffect)))
		{
			reason = "Invalid skill configuration";
			return false;
		}

		foreach (var precondition in skill.ActionEffects.OfType<ISkillEffectPrecondition>())
		{
			if (!precondition.CanUse(this, out string preconditionReason))
			{
				reason = string.IsNullOrEmpty(preconditionReason) ? "Cannot use this now" : preconditionReason;
				return false;
			}
		}

		if (Vitals.SP < skill.SPCost)
		{
			reason = "Not enough SP";
			return false;
		}

		foreach (var condition in skill.ActionEffects.OfType<ISkillCastCondition>())
		{
			if (!condition.CanCast(this, out var conditionReason))
			{
				reason = string.IsNullOrEmpty(conditionReason) ? "Cannot cast now" : conditionReason;
				return false;
			}
		}

		if (skill.UsesArrows)
		{
			if (!ArrowSupply.HasBow(this))
			{
				reason = "Needs a bow";
				return false;
			}
			if (ArrowSupply.Count(this) < ArrowSupply.RequiredToCast(skill))
			{
				reason = "Not enough arrows";
				return false;
			}
		}

		bool hasTargets = skill.Targeting == SkillTargeting.Missile || (inventoryTargeting ? skill.GetInventoryTargets(this).Any() :
			(skill.RequiresTargetSelection ? skill.GetTargetCharacters(this) : skill.GetAffectedCharacters(this, this)).Any());
		if (!hasTargets)
		{
			reason = "No valid targets";
			return false;
		}

		reason = "";
		return true;
	}

	public Stats FinalStats
	{
		get
		{
			if (cachedFinalStats == null)
			{
				UpdateCachedStats();
			}

			return cachedFinalStats;
		}
	}

	internal void InvalidateCachedStats()
	{
		cachedFinalStats = null;
	}

	internal void UpdateCachedStats()
	{
		var passiveSkillStats = Skills
			.Where(x => x != null && x.ActivationType == ActivationType.Passive)
				.Aggregate(new StatModification(), (accumulate, passiveSkill) => accumulate + passiveSkill.GetScaledPassiveModification());

		cachedFinalStats = BaseStats +
			Equipment?.GetEquipmentStatModification() +
			passiveSkillStats +
			StatusEffects.Aggregate(new StatModification(), (accumulate, statusEffect) => accumulate + statusEffect.GetStatModification())
			+ SongAura.ModificationFor(this)
			+ CommandStatusEffect.ModificationFor(this)
			+ ClassPassives.ConditionalStats(this);
	}

	private Vitals vitals;
	public Vitals Vitals
	{
		get => vitals;
		set
		{
			vitals = value;
			vitals.LinkedStats = () => FinalStats;
		}
	}

	public Stats DisplayedStats = new();
	private Vitals displayedVitals;
	public Vitals DisplayedVitals
	{
		get => displayedVitals;
		set
		{
			displayedVitals = value;
			DisplayedVitals.LinkedStats = () => DisplayedStats;
		}
	}

	internal void InitialzeVitalsFromStats()
	{
		BaseStats.FromStartingStats(StartingStats);
		var startingBonus = GetStartingStatBonus();
		if (startingBonus != null) BaseStats = BaseStats + startingBonus;
		Vitals = new();
		DisplayedVitals = new();
		Vitals.HP = FinalStats.HPMax;
		Vitals.SP = FinalStats.SPMax;
		Vitals.Hunger = FinalStats.HungerMax;
		Vitals.Level = 1;
	}

	// Extra base stats applied once when vitals are initialised (e.g. an ally's class bonus). Null means none.
	protected virtual StatModification GetStartingStatBonus() => null;

	public List<StatusEffect> StatusEffects = new();

	private void Awake()
	{
		BaseStats = new();
		if (Equipment != null)
		{
			Equipment.HandleEquipmentChanged += Equipment_HandleEquipmentChanged;
		}
	}

	private void OnDestroy()
	{
		BaseStats.OnStatChanged -= BaseStats_OnStatChanged;
		if (Equipment != null)
		{
			Equipment.HandleEquipmentChanged -= Equipment_HandleEquipmentChanged;
		}
	}

	private void BaseStats_OnStatChanged()
	{
		UpdateCachedStats();
	}

	public virtual void Inventory_HandleInventoryChanged()
	{
		UpdateCachedStats();
		DisplayedStats.Sync(FinalStats);
	}

	public virtual void Equipment_HandleEquipmentChanged(EquipChangeType equipChangeType, EquipableInventoryItem item)
	{
		UpdateCachedStats();
		DisplayedStats.Sync(FinalStats);
	}

	public Equipment Equipment;

	public abstract bool IsWaitingForPlayerInput { get; set; }
	public abstract List<GameAction> GetDeterminedAction();
	public abstract void DetermineAction();
	public abstract List<GameAction> ExecuteActionImmediate(GameAction action);
	public abstract IEnumerator ExecuteActionRoutine(GameAction action);
	public abstract void StartTurn();
	int Actor.ActionsLeft { get => Vitals.ActionsPerTurnLeft; }
	public string CharacterName { get; internal set; }

	public Vector3Int? PursuitPosition; //only used in ai controlled
	public Character PursuitTarget; //only used in ai controlled
	public List<Skill> Skills;
	public List<GameAction> determinedActions = new();
	internal GameAction _forcedAction;

	internal void SetPosition(Vector3Int newPosition)
	{
		TilemapPosition = newPosition;
		var position = Game.Instance.CurrentDungeon.CellToWorld(newPosition);
		transform.position = position;
	}

	//TODO move to "GameAnimation" class
	internal abstract void PlayWalkAnimation();
	internal abstract void PlayIdleAnimation();
	internal abstract void PlayAttackAnimation();
	internal abstract void PlayTakeDamageAnimation();
	internal abstract void PlayDeathAnimation();

	internal void SetFacing(Facing facing)
	{
		CurrentFacing = facing;
		var multiplier = 0;
		switch (facing)
		{
			case Facing.Up:
				multiplier = 0;
				break;
			case Facing.Down:
				multiplier = 4;
				break;
			case Facing.Left:
				multiplier = 6;
				break;
			case Facing.Right:
				multiplier = 2;
				break;
			case Facing.UpLeft:
				multiplier = 7;
				break;
			case Facing.UpRight:
				multiplier = 1;
				break;
			case Facing.DownLeft:
				multiplier = 5;
				break;
			case Facing.DownRight:
				multiplier = 3;
				break;
		}
		Vector3 desiredRotation = new Vector3(0, 0, -45 * multiplier);
		VisualParent.transform.eulerAngles = desiredRotation;
	}

	internal void SyncDisplayedStats()
	{
		DisplayedStats.Sync(FinalStats);
		DisplayedVitals.Sync(Vitals);
	}

	internal void SetFacingByTargetPosition(Vector3Int newMapPosition)
	{
		var direction = newMapPosition - TilemapPosition;
		var facing = GetFacing(direction);
		SetFacing(facing);
	}

	public static Facing GetFacing(Vector3Int direction)
	{
		direction = new Vector3Int(Mathf.Clamp(direction.x, -1, 1),
							  Mathf.Clamp(direction.y, -1, 1),
							  direction.z);

		if (direction == Vector3Int.up)
		{
			return Facing.Up;
		}
		else if (direction == Vector3Int.down)
		{
			return Facing.Down;
		}
		else if (direction == Vector3Int.left)
		{
			return Facing.Left;
		}
		else if (direction == Vector3Int.right)
		{
			return Facing.Right;
		}
		else if (direction == new Vector3Int(-1, 1, 0))
		{
			return Facing.UpLeft;
		}
		else if (direction == new Vector3Int(1, 1, 0))
		{
			return Facing.UpRight;
		}
		else if (direction == new Vector3Int(-1, -1, 0))
		{
			return Facing.DownLeft;
		}
		else if (direction == new Vector3Int(1, -1, 0))
		{
			return Facing.DownRight;
		}
		else
		{
			// Return a default facing or handle an unknown direction
			return Facing.Up; // Change this default return as needed
		}
	}

	internal bool CanMove()
	{
		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
			.Where(x => x != null);
		return !actionOverrides.Any();
	}

	internal BoundsInt GetAttackBounds()
	{
		switch (FootPrint)
		{
			case FootPrint.Size1x1:
				return ToBounds(TilemapPosition, new Vector3Int(3, 3, 1));
			case FootPrint.Size3x3:
				return ToBounds(TilemapPosition, new Vector3Int(5, 5, 1));
		}
		return ToBounds(TilemapPosition, new Vector3Int(3, 3, 1));
	}

	//TODO cache this every time the character moves
	internal BoundsInt ToBounds()
	{
		var position = TilemapPosition;
		return ToBounds(FootPrint, position);
	}

	public static BoundsInt ToBounds(Vector3Int position)
	{
		return ToBounds(FootPrint.Size1x1, position);
	}

	public static BoundsInt ToBounds(FootPrint footPrint, Vector3Int position)
	{
		switch (footPrint)
		{
			case FootPrint.Size1x1:
				// Assuming Size1x1 footprint is a single tile
				//return new BoundsInt(position, new Vector3Int(1, 1, 1));
				return ToBounds(position, new Vector3Int(1, 1, 1));

			case FootPrint.Size3x3:
				// Assuming Size3x3 footprint is a 3x3 grid of tiles
				// Calculate the bounds for a 3x3 area
				//Vector3Int size3x3 = new Vector3Int(3, 3, 1);
				//Vector3Int min = position - new Vector3Int(1, 1, 0); // Adjust based on the center tile
				//return new BoundsInt(min, size3x3);
				return ToBounds(position, new Vector3Int(3, 3, 1));
		}

		return new BoundsInt(position, new Vector3Int(1, 1, 1));
	}

	public static BoundsInt ToBounds(Vector3Int position, Vector3Int size)
	{
		// Adjust position based on the center of the bounds
		Vector3Int min = position - size / 2;

		// Make sure the size is odd
		size.x = size.x % 2 == 0 ? size.x + 1 : size.x;
		size.y = size.y % 2 == 0 ? size.y + 1 : size.y;
		size.z = size.z % 2 == 0 ? size.z + 1 : size.z;

		return new BoundsInt(min, size);
	}

	internal bool OverlapsWith(Character character)
	{
		return OverlapsWith(character.ToBounds());
	}

	internal bool OverlapsWith(BoundsInt bounds)
	{
		return ToBounds().Overlaps2D(bounds);
	}

	[ContextMenu("Debug Vitals")]
	public void Debug_Stats()
	{
		var realVitals = Vitals.ToDebugString();
		var displayedVitals = DisplayedVitals.ToDebugString();
		Debug.Log($@"Debug Vitals
real: {realVitals}
disp: {displayedVitals}");
	}

	public void TickStatusEffects()
	{
		foreach (var statusEffect in StatusEffects)
		{
			statusEffect.Tick();
		}
	}

	public List<GameAction> GetStatusEffectSideEffects()
	{
		var result = new List<GameAction>();

		foreach (var effect in StatusEffects)
		{
			if (effect.IsExpired())
			{
				result.Add(new RemoveStatusEffectAction(this, effect));
				continue;
			}
			var tickEffects = effect.GetTickEffects(this);
			if (tickEffects != null && tickEffects.Count > 0)
			{
				foreach (var tickAction in tickEffects)
				{
					result.Add(tickAction);
				}
			}
			if (effect.TurnsLeft == 1) result.Add(new RemoveStatusEffectAction(this, effect));

		}

		return result;
	}

	public T ApplyStatusEffect<T>(T newStatusPrefab) where T : StatusEffect
	{
		if (newStatusPrefab != null && ClassPassives.IsImmune(this, newStatusPrefab)) return null;
		var matchingStatus = StatusEffects.FirstOrDefault(x => x != null && x.StackKey == newStatusPrefab.StackKey);
		if (matchingStatus != null)
		{
			matchingStatus.ReApply(newStatusPrefab);
			UpdateCachedStats();
			DisplayedStats.Sync(FinalStats);

			return null;
		}
		else
		{
			var newStatus = Instantiate(newStatusPrefab, VisualParent.transform);
			newStatus.Apply();
			StatusEffects.Add(newStatus);
			UpdateCachedStats();
			DisplayedStats.Sync(FinalStats);

			return newStatus;
		}
	}

	public T RemoveStatusEffect<T>(T expiredStatus) where T : StatusEffect
	{
		var existing = StatusEffects.FirstOrDefault(x => ReferenceEquals(x, expiredStatus))
			?? StatusEffects.FirstOrDefault(x => x != null && expiredStatus != null && x.GetType() == expiredStatus.GetType());
		T existingStatus = existing as T;
		StatusEffects.Remove(existing);
		UpdateCachedStats();
		DisplayedStats.Sync(FinalStats);

		return existingStatus;
	}

	internal List<GameAction> GetActionResponses(GameAction gameAction)
	{
		var responseBehaviors = GetComponents<IResponseBehavior>();
		var actionsResponses = responseBehaviors
			.Select(x => x.GetActionResponse(gameAction))
			.Where(x => x != null).ToList();
		actionsResponses.Add(new BigUnitStuckInHallway(Game.Instance.StatusEffectPrefabs.FirstOrDefault(x => x.GetEffectName() == "Stuck")));
		var ret = actionsResponses
			.SelectMany(x => x.GetResponseTo(this, gameAction));

		//TODO: check abilities, skills, weapons
		return ret.ToList();
	}

	// Status-effect and passive-skill responses. Called once per actor per executed action by TurnManager.
	internal List<GameAction> GetClassResponses(GameAction action)
	{
		var result = new List<GameAction>();
		if (this == null || Vitals == null || Vitals.HP <= 0 || action == null) return result;
		foreach (var status in StatusEffects.ToList())
		{
			if (status == null || status.IsExpired()) continue;
			var responses = status.GetResponseTo(this, action);
			if (responses != null) result.AddRange(responses.Where(x => x != null));
		}
		foreach (var skill in (Skills ?? new List<Skill>()).ToList())
		{
			if (skill == null || skill.ActivationType != ActivationType.Passive || skill.PassiveResponses == null) continue;
			foreach (var passive in skill.PassiveResponses)
			{
				if (passive == null) continue;
				var responses = passive.Respond(this, skill, action);
				if (responses != null) result.AddRange(responses.Where(x => x != null));
			}
		}
		return result;
	}

	// Lets this character's statuses and passive skills adjust damage about to be dealt to anyone.
	internal void InterceptDamage(DamageContext context)
	{
		if (context == null || this == null || Vitals == null || Vitals.HP <= 0) return;
		foreach (var status in StatusEffects.ToList())
			if (status != null && !status.IsExpired()) status.ModifyIncomingDamage(this, context);
		foreach (var skill in (Skills ?? new List<Skill>()).ToList())
		{
			if (skill == null || skill.ActivationType != ActivationType.Passive || skill.PassiveResponses == null) continue;
			foreach (var passive in skill.PassiveResponses)
				passive?.ModifyIncomingDamage(this, skill, context);
		}
	}

	public abstract IEnumerable<GameAction> GetResponseTo(GameAction sideEffectAction);
	public abstract List<GameAction> GetTrapSideEffects();
	public abstract List<GameAction> GetInteractableSideEffects();

	public bool GetActionInterupt(GameAction action)
	{
		return StatusEffects.Any(x => !x.IsExpired() && x.Interupts(action));
	}

	internal List<AStar.Node> CalculatePursuitPath()
	{
		var game = Game.Instance;

		//TODO refactor cost out of the loop, do it in 2nd pass
		//move to a different class
		AStar.Node[,] grid = new AStar.Node[game.CurrentDungeon.dungeonWidth, game.CurrentDungeon.dungeonHeight];

		for (int i = 0; i < game.CurrentDungeon.dungeonWidth; i++)
		{
			for (int j = 0; j < game.CurrentDungeon.dungeonHeight; j++)
			{
				var isWalkable = game.CurrentDungeon.IsWalkable(new Vector3Int(i, j));

				var containsCharacter = game.AllCharacters.Any(x => x.TilemapPosition == new Vector3Int(i, j));
				var containsTrap = game.CurrentDungeon.Interactables
					.OfType<Trap>()
					.Where(x => x.VisualObject.activeInHierarchy)
					.Any(x => x.Position == new Vector3Int(i, j));
				var movePenalty = containsCharacter || containsTrap ? 5 : 0;
				grid[i, j] = new AStar.Node(i, j, isWalkable, movePenalty);
			}
		}

		AStar.Node startNode = grid[TilemapPosition.x, TilemapPosition.y];
		AStar.Node targetNode = grid[PursuitPosition.Value.x, PursuitPosition.Value.y];

		var path = AStar.FindPath(grid, startNode, targetNode);
		return path;
	}

	internal List<AStar.Node> CalculateRangedAttackPath(Vector3Int targetPosition, AStar.Node[,] grid)
	{
		AStar.Node startNode = grid[TilemapPosition.x, TilemapPosition.y];
		AStar.Node targetNode = grid[targetPosition.x, targetPosition.y];

		var path = AStar.FindPath(grid, startNode, targetNode);
		return path;
	}

	internal static AStar.Node[,] GetAStarGrid()
	{
		var game = Game.Instance;

		//TODO refactor cost out of the loop, do it in 2nd pass
		//move to a different class
		AStar.Node[,] grid = new AStar.Node[game.CurrentDungeon.dungeonWidth, game.CurrentDungeon.dungeonHeight];

		for (int i = 0; i < game.CurrentDungeon.dungeonWidth; i++)
		{
			for (int j = 0; j < game.CurrentDungeon.dungeonHeight; j++)
			{
				var isWalkable = game.CurrentDungeon.IsWalkable(new Vector3Int(i, j));

				var containsCharacter = game.AllCharacters.Any(x => x.TilemapPosition == new Vector3Int(i, j));
				var movePenalty = containsCharacter ? 5 : 0;
				grid[i, j] = new AStar.Node(i, j, isWalkable, movePenalty);
			}
		}

		return grid;
	}

	internal Character GetPursuitTarget()
	{
		var taunt = StatusEffects.OfType<TauntStatusEffect>().FirstOrDefault(x => x.Taunter != null);
		if (taunt != null)
			return taunt.Taunter;

		var game = Game.Instance;
		var playerTeamCharacters = game.AllCharacters.Where(x => x.Team != Team && x.Team != Team.Neutral);

		var visible = playerTeamCharacters
			.OrderBy(x => TileWorldDungeon.ChevDistance(x.TilemapPosition, TilemapPosition))
			.ThenBy(x => x.TilemapPosition == PursuitPosition)
			.Where(x => EnemyAwareness.CanNotice(this, x) && game.CurrentDungeon.CanSee(this, x));
		return EnemyTargeting.SelectTarget(this, visible);
	}

	// Match BoundsInt's exclusive maximum edges while ignoring the Z dimension.
	public static bool Contains2D(BoundsInt visionBounds, Vector3Int tilemapPosition)
	{
		return visionBounds.xMin <= tilemapPosition.x &&
			visionBounds.xMax > tilemapPosition.x &&
			visionBounds.yMin <= tilemapPosition.y &&
			visionBounds.yMax > tilemapPosition.y;
	}

	public void SetAction(GameAction forcedAction)
	{
		if (forcedAction is SkillAction && (Game.Instance.TurnManager.IsProcessingTurn || !forcedAction.IsValid(this))) return;
		_forcedAction = forcedAction;
		if (Game.Instance.PlayerController.ControlledAlly == this)
		{
			Game.Instance.TurnManager.ProcessTurn();
			IsWaitingForPlayerInput = false;
		}
	}
}

public enum Team
{
	Player,
	Enemy,
	Neutral
}

public enum FootPrint
{
	Size1x1,
	Size3x3
}
