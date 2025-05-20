using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Player : Character
{
    private float holdTime = 0f;
	private float repeatTime = 0.1f;
	private GameAction pickedAction;
	public Camera Camera;
	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);

	internal Interactable currentInteractable;

	public HeroAnimator HeroAnimator;

	public GameObject ThrownItemProjectilePrefab;
	private bool isBusy;
	private float menuCooldown;

	public List<GameAction> DeterminedActions { get; private set; }

	public override bool IsBusy => isBusy;

	public bool ControllerHeld { get; internal set; }
	public bool ControllerAttackThisFrame { get; internal set; }
	public bool ControllerUseThisFrame { get; internal set; }
	public bool ControllerHoldPosition { get; internal set; }

    public bool ControllerMenuThisFrame { get; internal set; }

	public CameraMode CurrentCameraMode;
	public Character FollowTarget;
	public List<Character> Targetables;
	public Skill TargetingSkill;

    public Action<Player, Skill, Vector3Int> TargetSelected { get; private set; }

    private void Start()
	{
		HeroAnimator.PlayIdleAnimation();
	}

	private void LateUpdate()
	{
		if (CurrentCameraMode == CameraMode.FollowPlayer)
		{
			Camera.transform.position = this.transform.position + CameraOffset;
		}
		else if (CurrentCameraMode == CameraMode.TargetSelect &&
			FollowTarget != null)
		{
			Camera.transform.position = FollowTarget.transform.position + CameraOffset;
		}
	}

    private void Update()
	{
		menuCooldown += Time.deltaTime;
		//Debug.Log($"Player is busy {IsBusy}");

		//TODO handle diagonal input
		if (Input.GetKey(KeyCode.W) ||
			Input.GetKey(KeyCode.A) ||
			Input.GetKey(KeyCode.S) ||
			Input.GetKey(KeyCode.D) ||
			ControllerHeld)
		{
			holdTime += Time.deltaTime;
		}

		if (CurrentCameraMode == CameraMode.TargetSelect)
		{
			if (Input.GetKeyDown(KeyCode.W))
			{
				SelectTargetable(Facing.Up);
			}
			else if (Input.GetKeyDown(KeyCode.A))
			{
				SelectTargetable(Facing.Left);
			}
			else if (Input.GetKeyDown(KeyCode.S))
			{
				SelectTargetable(Facing.Down);
			}
			else if (Input.GetKeyDown(KeyCode.D))
			{
				SelectTargetable(Facing.Right);
			}
			return;
		}

		var game = Game.Instance;
		if (Game.Instance.InventoryMenu.isActiveAndEnabled ||
			Game.Instance.AllyMenu.isActiveAndEnabled ||
			Game.Instance.SkillDialog.isActiveAndEnabled)
		{
			menuCooldown = 0;
			holdTime = 0;
			return;
		}

		if (IsBusy && menuCooldown > 0.2f)
		{
			DeterminePlayerAction();
		}
	}

    public override void Inventory_HandleEquipmentChanged()
	{
		base.Inventory_HandleEquipmentChanged();

		HeroAnimator.SetWeapon(
			Inventory.EquipedWeapon?.ItemDefinition as EquipmentItemDefinition,
			Inventory.EquipedShield?.ItemDefinition as EquipmentItemDefinition);
	}

	internal bool CanOpenMenu()
	{
		return !StatusEffects.Any(x => x.PreventsMenu());
	}

	private void DeterminePlayerAction()
	{
		var originalPosition = new Vector3Int(TilemapPosition.x, TilemapPosition.y);
		var newMapPosition = new Vector3Int(TilemapPosition.x, TilemapPosition.y);

		if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.A))
		{
			SetFacing(Facing.UpLeft);
		}
		else if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.D))
		{
			SetFacing(Facing.UpRight);
		}
		else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.A))
		{
			SetFacing(Facing.DownLeft);
		}
		else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.D))
		{
			SetFacing(Facing.DownRight);
		}
		else if (Input.GetKey(KeyCode.W))
		{
			SetFacing(Facing.Up);
		}
		else if (Input.GetKey(KeyCode.A))
		{
			SetFacing(Facing.Left);
		}
		else if (Input.GetKey(KeyCode.S))
		{
			SetFacing(Facing.Down);
		}
		else if (Input.GetKey(KeyCode.D))
		{
			SetFacing(Facing.Right);
		}

		var game = Game.Instance;
		if (!Input.GetKey(KeyCode.LeftShift) &&
			!ControllerHoldPosition)
		{
			if (holdTime > repeatTime)
			{
				holdTime = 0f;
				var offset = Dungeon.GetFacingOffset(CurrentFacing);
				if (Game.Instance.CurrentDungeon.CanWalkTo(newMapPosition, newMapPosition + offset))
				{
					newMapPosition += offset;
					SetAction(new MovementAction(this, originalPosition, newMapPosition));
					return;
				}
			}
		}

		if ((Input.GetKeyDown(KeyCode.W) ||
			Input.GetKeyDown(KeyCode.A) ||
			Input.GetKeyDown(KeyCode.S) ||
			Input.GetKeyDown(KeyCode.D) ||
			ControllerHeld) &&
			(!Input.GetKey(KeyCode.LeftShift) &&
			!ControllerHoldPosition))
		{
			holdTime = 0f;
			var offset = Dungeon.GetFacingOffset(CurrentFacing);
			if (Game.Instance.CurrentDungeon.CanWalkTo(newMapPosition, newMapPosition + offset))
			{
				newMapPosition += offset;
				SetAction(new MovementAction(this, originalPosition, newMapPosition));
				return;
			}
		}

		if (Input.GetKeyDown(KeyCode.Space) ||
			ControllerAttackThisFrame)
		{
			ControllerAttackThisFrame = false;

			var offset = Dungeon.GetFacingOffset(CurrentFacing);
			newMapPosition += offset;
			SetAction(new AttackAction(this, originalPosition, newMapPosition));
			return;
		}
		else if (Input.GetKeyDown(KeyCode.E) ||
			ControllerUseThisFrame)
		{
			ControllerUseThisFrame = false;
			if (currentInteractable != null)
			{
				SetAction(new InteractAction(currentInteractable));
				return;
			}
		}
		if (Input.GetKeyDown(KeyCode.Z))
		{
			//pass turn
			SetAction(new WaitAction());
			return;
		}
	}

	internal void InitialzeSkillsFromSave()
	{
		var activeSkills = Common.Instance.GameSaveData.OverworldSaveData.ActiveSkillNames;
		foreach (var skillName in activeSkills)
		{
			Skill skill = Common.Instance.SkillManager.GetSkillByName(skillName);
			Skill newSkill = Instantiate(skill, this.transform);

			Skill item = newSkill;
			this.Skills.Add(item);
		}
	}

	internal void SetAction(GameAction userAction)
	{
		if (userAction is MovementAction movementAction)
		{
			var swapAlly = Game.Instance.AllCharacters
				.Where(x => x != this)
				.Where(x => x.Team == Team.Player)
				.FirstOrDefault(x => x.TilemapPosition == movementAction.newMapPosition);

			if (swapAlly != null)
			{
				userAction = new SwapAllyPositionAction(this, swapAlly);
			}
		}

		if(userAction is AttackAction attackAction)
		{
			var attackedAlly = Game.Instance.AllCharacters
				.Where(x => x != this)
				.Where(x => x.Team == Team.Player)
				.FirstOrDefault(x => x.TilemapPosition == attackAction.attackPosition)
				as Ally;

			if (attackedAlly != null)
			{
				//open ally menu instead
				var menuManager = FindObjectOfType<MenuManager>(true);
				menuManager.OpenAllyMenu(attackedAlly);

				holdTime = 0;
				pickedAction = null;
				return;
			}
		}

		if (!userAction.IsValid(this))
		{
			pickedAction = null;
			return;
		}

		pickedAction = userAction;
		isBusy = false;
	}

	public override List<GameAction> GetDeterminedAction()
	{
		//isBusy = true;
		this.Vitals.ActionsPerTurnLeft--;
		this.DisplayedVitals.ActionsPerTurnLeft--;
		return this.DeterminedActions;
	}
	public override void DetermineAction()
	{
		//this should affect player the same way, to do in characerbase class?
		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
			.Where(x => x != null);
		if (actionOverrides.Any())
		{
			this.DeterminedActions = actionOverrides.ToList();
			return;
		}

		DeterminedActions = new();

		DeterminedActions.Add(pickedAction);

		DeterminedActions.Add(
			new ModifyStatAction(
			this,
			this,
			(stats, vitals) =>
			{
				vitals.HungerAccumulate++;

				if (vitals.HP < stats.HPMax)
				{
					vitals.HPRegenAcccumlate++;
				}

				if (vitals.SP < stats.SPMax)
				{
					vitals.SPRegenAcccumlate++;
				}
			},
			false));

		if (Vitals.HungerAccumulate > FinalStats.HungerAccumulateThreshold)
		{
			DeterminedActions.Add(new ModifyStatAction(
				this,
				this,
				(stats, vitals) =>
				{
					vitals.HungerAccumulate = 0;
					vitals.Hunger--;
				},
				false));
		}

		if (Vitals.Hunger <= 0)
		{
			DeterminedActions.Add(new TakeDamageAction(this, this, 1, true));
		}

		if (Vitals.HPRegenAcccumlate > FinalStats.HPRegenAcccumlateThreshold &&
			Vitals.Hunger > 0)
		{
			DeterminedActions.Add(new ModifyStatAction(
				this,
				this,
				(stats, vitals) =>
				{
					vitals.HPRegenAcccumlate = 0;
					vitals.HP++;
				},
				false));
		}

		if (Vitals.SPRegenAcccumlate > FinalStats.SPRegenAcccumlateThreshold &&
			Vitals.Hunger > 0)
		{
			DeterminedActions.Add(new ModifyStatAction(
				this,
				this,
				(stats, vitals) =>
				{
					vitals.SPRegenAcccumlate = 0;
					vitals.SP++;
				},
				false));
		}
	}

	public override List<GameAction> ExecuteActionImmediate(GameAction action)
	{
		if (GetActionInterupt(action))
		{
			return new();
		}

		var sideEffects = action.ExecuteImmediate(this);
		var actionResponses = GetActionResponses(action);
		var actionResponseEffects = actionResponses.SelectMany(x => x.ExecuteImmediate(this));
		sideEffects.AddRange(actionResponseEffects);

		return sideEffects;
	}

	public override IEnumerable<GameAction> GetResponseTo(GameAction action)
	{
		return GetActionResponses(action);
	}

	public override IEnumerator ExecuteActionRoutine(GameAction action)
	{
		if (this == null) { yield break; }

		yield return StartCoroutine(action.ExecuteRoutine(this));
		action.UpdateDisplayedStats();
	}

	public override void StartTurn()
	{
		isBusy = true;
		Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
		Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

		SyncDisplayedStats();
	}

	public override List<GameAction> GetTrapSideEffects()
	{
		if (currentInteractable is Trap trap)
		{
			currentInteractable = null;
			Game.Instance.DoFloatingText(trap.GetInteractionText(), Color.yellow, this.VisualParent.transform.position);
			return trap.GetTrapSideEffects(this);
		}

		return new();
	}

	internal override void PlayWalkAnimation()
	{
		HeroAnimator.PlayWalkAnimation();
	}

	internal override void PlayIdleAnimation()
	{
		HeroAnimator.PlayIdleAnimation();
	}

	internal override void PlayAttackAnimation()
	{
		HeroAnimator.PlayAttackAnimation();
	}

	internal override void PlayTakeDamageAnimation()
	{
		HeroAnimator.PlayTakeDamageAnimation();
	}

	internal override void PlayDeathAnimation()
	{
		HeroAnimator.PlayDeathAnimation();
	}

	internal void InvokeTargetSelection(Skill skill, List<Character> possibleTargets, Action<Player, Skill, Vector3Int> targetSelected)
	{
		CurrentCameraMode = CameraMode.TargetSelect;
		Targetables = possibleTargets;
		TargetSelected = targetSelected;
		TargetingSkill = skill;

		FollowTarget = possibleTargets.First();
		MenuManager.Instance.TargetArrow.transform.position = FollowTarget.transform.position;
	}
	internal void ConfirmTarget()
	{
		CurrentCameraMode = CameraMode.FollowPlayer;
		TargetSelected?.Invoke(this, TargetingSkill, FollowTarget.TilemapPosition);
		Targetables = null;
		FollowTarget = null;
		TargetingSkill = null;
		TargetSelected = null;
	}


	private void SelectTargetable(Facing facing)
	{
		var dir = Dungeon.GetFacingOffset(facing);
		var next = GetNextSelectableWithWrap(FollowTarget, Targetables, dir);

		if (next == null) { return; }
		FollowTarget = next;
		MenuManager.Instance.TargetArrow.transform.position = FollowTarget.transform.position;
	}

	Character GetNextSelectableWithWrap(Character current, List<Character> allEntities, Vector3Int dir)
	{
		Character best = null;
		float bestDist = float.MaxValue;

		// 1. Try finding normally in the desired direction
		best = FindInDirection(current, allEntities, dir);
		if (best != null) return best;

		return best;
	}

	Character FindInDirection(Character from, List<Character> entities, Vector3Int dir)
	{
		Character best = null;
		float bestDist = float.MaxValue;

		Vector2 direction = new Vector2(dir.x, dir.y).normalized;
		float directionThreshold = 0.7f; // ~45 degree cone

		foreach (var entity in entities)
		{
			if (entity == from) continue;

			int dx = entity.TilemapPosition.x - from.TilemapPosition.x;
			int dy = entity.TilemapPosition.y - from.TilemapPosition.y;

			Vector2 toTarget = new Vector2(dx, dy);

			// Skip if target is on or behind "from" in the given direction
			if (Vector2.Dot(toTarget, direction) <= 0) continue;

			Vector2 toTargetNormalized = toTarget.normalized;
			float dot = Vector2.Dot(toTargetNormalized, direction);

			// Use Manhattan distance as before
			float dist = Mathf.Abs(dx) + Mathf.Abs(dy);

			Debug.Log($"{entity} ({entity.TilemapPosition.x},{entity.TilemapPosition.y}) {dot} {dist}", entity);

			// Check if target lies within the direction cone
			if (dot < directionThreshold)
			{
				continue;
			}


			if (dist < bestDist)
			{
				bestDist = dist;
				best = entity;
			}
		}

		return best;
	}
}

public enum CameraMode
{
	FollowPlayer,
	TargetSelect
}