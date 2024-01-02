using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Character : MonoBehaviour, Actor
{
	public Vector3Int TilemapPosition;
	
	public GameObject VisualParent;
	public Facing CurrentFacing;

	[field: SerializeField]
	private Stats baseStats = new();

	public Stats BaseStats
	{
		get => baseStats;
		set
		{
			baseStats = value;
			cachedFinalStats = null;
		}
	}

	private Stats cachedFinalStats = null;
	public Stats FinalStats
	{
		get
		{
			if (cachedFinalStats == null)
			{
				cachedFinalStats = BaseStats + Inventory?.GetEquipmentStatModification();
			}

			return cachedFinalStats;
		}
	}

	public Vitals Vitals;

	public Stats DisplayedStats = new();
	public Vitals DisplayedVitals = new();

	internal void InitialzeVitalsFromStats()
	{
		Vitals.LinkedStats = FinalStats;
		DisplayedVitals.LinkedStats = DisplayedStats;
		this.Vitals.HP = this.FinalStats.HPMax;
		this.Vitals.Hunger = this.FinalStats.HungerMax;
	}

	private void Awake()
	{
		if (Inventory != null)
		{
			Inventory.HandleEquipmentChanged += Inventory_HandleEquipmentChanged;
		}
	}


	private void OnDestroy()
	{
		if (Inventory != null)
		{
			Inventory.HandleEquipmentChanged -= Inventory_HandleEquipmentChanged;
		}
	}

	private void Inventory_HandleEquipmentChanged()
	{
		cachedFinalStats = null;
		Vitals.LinkedStats = FinalStats;
		SyncDisplayedStats();
	}

	public Inventory Inventory;

	public abstract List<GameAction> DetermineActions();
	public abstract List<GameAction> ExecuteActionImmediate(GameAction action);
	public abstract IEnumerator ExecuteActionRoutine(GameAction action);
	public abstract void StartTurn();
	int Actor.ActionsPerTurn { get => ActionsPerTurnMax; }
	int Actor.AttacksPerTurn { get => AttacksPerTurnMax; }
	public int ActionsPerTurnMax = 1;
	public int AttacksPerTurnMax = 1;
	internal int actionsPerTurnLeft;
	internal int attacksPerTurnLeft;

	internal void SetPosition(Vector3Int newPosition)
	{
		TilemapPosition = newPosition;
		var position = Game.Instance.CurrentDungeon.TileMap_Floor.CellToWorld(newPosition);
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

	public Facing GetFacing(Vector3Int direction)
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
}
