using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Character : MonoBehaviour, Actor
{
	public Vector3Int TilemapPosition;
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

	internal void UpdateCachedStats()
	{
		cachedFinalStats = BaseStats + 
			Inventory?.GetEquipmentStatModification() +
			StatusEffects.Aggregate(new StatModification(), (accumulate, newa) => accumulate + newa.GetStatModification());
	}

	private Vitals vitals;
	public Vitals Vitals
	{
		get => vitals;
		set
		{
			vitals = value;
			vitals.LinkedStats = () => this.FinalStats;
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
			DisplayedVitals.LinkedStats = () => this.DisplayedStats;
		}
	}

	internal void InitialzeVitalsFromStats()
	{
		BaseStats.FromStartingStats(StartingStats);
		Vitals = new();
		DisplayedVitals = new();
		this.Vitals.HP = this.FinalStats.HPMax;
		this.Vitals.SP = this.FinalStats.SPMax;
		this.Vitals.Hunger = this.FinalStats.HungerMax;
	}

	public List<StatusEffect> StatusEffects = new();

	private void Awake()
	{
		BaseStats = new();
		if (Inventory != null)
		{
			Inventory.HandleEquipmentChanged += Inventory_HandleEquipmentChanged;
		}
	}

	private void OnDestroy()
	{
		BaseStats.OnStatChanged -= BaseStats_OnStatChanged;
		if (Inventory != null)
		{
			Inventory.HandleEquipmentChanged -= Inventory_HandleEquipmentChanged;
		}
	}

	private void BaseStats_OnStatChanged()
	{
		UpdateCachedStats();
	}

	public virtual void Inventory_HandleEquipmentChanged()
	{
		UpdateCachedStats();
		DisplayedStats.Sync(FinalStats);
	}

	public Inventory Inventory;

	public abstract bool IsBusy { get; }
	public abstract List<GameAction> GetDeterminedAction();
	public abstract	void DetermineAction();
	public abstract List<GameAction> ExecuteActionImmediate(GameAction action);
	public abstract IEnumerator ExecuteActionRoutine(GameAction action);
	public abstract void StartTurn();
	int Actor.ActionsLeft { get => Vitals.ActionsPerTurnLeft; }

	public Vector3Int? PursuitPosition; //only used in ai controlled
	public Character PursuitTarget; //only used in ai controlled
	public List<Skill> Skills;

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
		var direction = newMapPosition - this.TilemapPosition;
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

	internal BoundsInt GetAttackBounds()
	{
		switch (FootPrint)
		{
			case FootPrint.Size1x1:
				return ToBounds(this.TilemapPosition, new Vector3Int(3, 3));
			case FootPrint.Size3x3:
				return ToBounds(this.TilemapPosition, new Vector3Int(5, 5));
		}
		return ToBounds(this.TilemapPosition, new Vector3Int(3, 3));
	}

	//TODO cache this every time the character moves
	internal BoundsInt ToBounds()
	{
		var position = this.TilemapPosition;
		return ToBounds(this.FootPrint, position);
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
		return this.ToBounds().Overlaps2D(bounds);
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
		foreach(var statusEffect in StatusEffects)
		{
			statusEffect.Tick();
		}
	}

	public List<GameAction> GetStatusEffectSideEffects()
	{
		return StatusEffects.Where(x => x.IsExpired())
			.Select(x => new RemoveStatusEffectAction(this, x))
			.Cast<GameAction>()
			.ToList();
	}

	public T ApplyStatusEffect<T>(T newStatusPrefab) where T : StatusEffect
	{
		if (StatusEffects.OfType<T>().Any())
		{
			T existingStatus = StatusEffects.OfType<T>().FirstOrDefault();
			existingStatus.ReApply(newStatusPrefab);
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
		T existingStatus = StatusEffects.OfType<T>().FirstOrDefault();
		StatusEffects.Remove(existingStatus);
		UpdateCachedStats();
		DisplayedStats.Sync(FinalStats);

		return existingStatus;
	}

	internal List<GameAction> GetActionResponses(GameAction gameAction)
	{
		var responseBehaviors = this.GetComponents<IResponseBehavior>();
		var actionsResponses = responseBehaviors
			.Select(x => x.GetActionResponse(gameAction))
			.Where(x => x != null).ToList();
		actionsResponses.Add(new BigUnitStuckInHallway(Game.Instance.StatusEffectPrefabs.FirstOrDefault(x => x.GetEffectName() == "Stuck")));
		var ret = actionsResponses
			.SelectMany(x => x.GetResponseTo(this, gameAction));

		//TODO: check abilities, skills, weapons
		return ret.ToList();
	}

	public abstract IEnumerable<GameAction> GetResponseTo(GameAction sideEffectAction);
	public abstract List<GameAction> GetTrapSideEffects();

	public bool GetActionInterupt(GameAction action)
	{
		return StatusEffects.Any(x => x.Interupts(action));
	}

	internal List<AStar.Node> CalculatePursuitPath()
	{
		var game = Game.Instance;

		//TODO refactor cost out of the loop, do it in 2nd pass
		//move to a different class
		AStar.Node[,] grid = new AStar.Node[game.CurrentDungeon.dungeonWidth, game.CurrentDungeon.dungeonHeight];

		for(int i = 0; i < game.CurrentDungeon.dungeonWidth; i++)
		{
			for (int j = 0; j < game.CurrentDungeon.dungeonHeight; j++)
			{
				var isWalkable = game.CurrentDungeon.IsWalkable(new Vector3Int(i, j));

				var containsFriendly = game.Enemies.Any(x => x.TilemapPosition == new Vector3Int(i, j));
				var movePenalty = containsFriendly ? 5 : 0;
				grid[i, j] = new AStar.Node(i, j, isWalkable, movePenalty);
			}
		}
		
		AStar.Node startNode = grid[TilemapPosition.x, TilemapPosition.y];
		AStar.Node targetNode = grid[PursuitPosition.Value.x, PursuitPosition.Value.y];

		var path = AStar.FindPath(grid, startNode, targetNode);
		return path;
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