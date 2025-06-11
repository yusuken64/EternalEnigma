//using JuicyChickenGames.Menu;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//public class Player : Character
//{
//	private float holdTime = 0f;
//	private float repeatTime = 0.1f;
//	private GameAction pickedAction;
//	public Camera Camera;
//	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);

//	internal Interactable currentInteractable;

//	public HeroAnimator HeroAnimator;

//	public GameObject ThrownItemProjectilePrefab;
//	private bool isWaitingForPlayerInput;
//	private float menuCooldown;

//	public List<GameAction> DeterminedActions { get; private set; }

//	public override bool IsWaitingForPlayerInput => isWaitingForPlayerInput;

//	public CameraMode CurrentCameraMode;
//	public Character FollowTarget;
//	public List<Character> Targetables;
//	public Skill TargetingSkill;
//	private CheatConsole _cheatConsole;

//	public Action<Player, Skill, Vector3Int> TargetSelected { get; private set; }

//	private void Start()
//	{
//		_cheatConsole = FindFirstObjectByType<CheatConsole>();
//		HeroAnimator.PlayIdleAnimation();
//	}

//	private void LateUpdate()
//	{
//		if (CurrentCameraMode == CameraMode.FollowPlayer)
//		{
//			Camera.transform.position = this.transform.position + CameraOffset;
//		}
//		else if (CurrentCameraMode == CameraMode.TargetSelect &&
//			FollowTarget != null)
//		{
//			Camera.transform.position = FollowTarget.transform.position + CameraOffset;
//		}
//	}

//	private void Update()
//	{
//		if (_cheatConsole.ScreenObject.activeSelf) { return; }

//		menuCooldown += Time.deltaTime;

//		if (PlayerInputHandler.Instance.isMoving)
//		{
//			holdTime += Time.deltaTime;
//		}

//		if (CurrentCameraMode == CameraMode.TargetSelect)
//		{
//			//if (PlayerInputHandler.Instance.isMoving)
//			//{
//			//	if (Mathf.Abs(PlayerInputHandler.Instance.moveInput.x) > Mathf.Abs(PlayerInputHandler.Instance.moveInput.y))
//			//	{
//			//		if (PlayerInputHandler.Instance.moveInput.x > 0.5f)
//			//			SelectTargetable(Facing.Right);
//			//		else if (PlayerInputHandler.Instance.moveInput.x < -0.5f)
//			//			SelectTargetable(Facing.Left);
//			//	}
//			//	else
//			//	{
//			//		if (PlayerInputHandler.Instance.moveInput.y > 0.5f)
//			//			SelectTargetable(Facing.Up);
//			//		else if (PlayerInputHandler.Instance.moveInput.y < -0.5f)
//			//			SelectTargetable(Facing.Down);
//			//	}
//			//}
//			//return;
//		}

//		if (Game.Instance.InventoryMenu.isActiveAndEnabled ||
//			Game.Instance.AllyMenu.isActiveAndEnabled ||
//			Game.Instance.SkillDialog.isActiveAndEnabled)
//		{
//			menuCooldown = 0;
//			holdTime = 0;
//			return;
//		}

//		if (IsWaitingForPlayerInput && menuCooldown > 0.2f)
//		{
//			DeterminePlayerAction();
//		}
//	}

//	public override void Inventory_HandleInventoryChanged()
//	{
//		base.Inventory_HandleInventoryChanged();

//		HeroAnimator.SetWeapon(
//			Equipment.EquipedWeapon?.ItemDefinition as EquipmentItemDefinition,
//			Equipment.EquipedShield?.ItemDefinition as EquipmentItemDefinition);
//	}

//	public override void Equipment_HandleEquipmentChanged(EquipChangeType equipChangeType, EquipableInventoryItem item)
//	{
//		base.Equipment_HandleEquipmentChanged(equipChangeType, item);

//		switch (equipChangeType)
//		{
//			case EquipChangeType.Equip:
//				Inventory.InventoryItems.Remove(item);
//				break;
//			case EquipChangeType.UnEquip:
//				Inventory.InventoryItems.Add(item);
//				break;
//		}

//		HeroAnimator.SetWeapon(
//			Equipment.EquipedWeapon?.ItemDefinition as EquipmentItemDefinition,
//			Equipment.EquipedShield?.ItemDefinition as EquipmentItemDefinition);
//	}

//	internal bool CanOpenMenu()
//	{
//		return !StatusEffects.Any(x => x.PreventsMenu());
//	}

//	private void DeterminePlayerAction()
//	{
//		var originalPosition = new Vector3Int(TilemapPosition.x, TilemapPosition.y);
//		var newMapPosition = new Vector3Int(TilemapPosition.x, TilemapPosition.y);

//		Vector2 move = PlayerInputHandler.Instance.moveInput;

//		if (move.sqrMagnitude >= 0.01f)
//		{
//			// Normalize the input so diagonal directions are consistent
//			move.Normalize();

//			if (move.x < -0.5f && move.y > 0.5f)
//				SetFacing(Facing.UpLeft);
//			else if (move.x > 0.5f && move.y > 0.5f)
//				SetFacing(Facing.UpRight);
//			else if (move.x < -0.5f && move.y < -0.5f)
//				SetFacing(Facing.DownLeft);
//			else if (move.x > 0.5f && move.y < -0.5f)
//				SetFacing(Facing.DownRight);
//			else if (move.y > 0.5f)
//				SetFacing(Facing.Up);
//			else if (move.x < -0.5f)
//				SetFacing(Facing.Left);
//			else if (move.y < -0.5f)
//				SetFacing(Facing.Down);
//			else if (move.x > 0.5f)
//				SetFacing(Facing.Right);
//		}

//		if (!PlayerInputHandler.Instance.holdPosition)
//		{
//			if (holdTime > repeatTime)
//			{
//				holdTime = 0f;
//				var offset = Dungeon.GetFacingOffset(CurrentFacing);
//				if (Game.Instance.CurrentDungeon.CanWalkTo(newMapPosition, newMapPosition + offset))
//				{
//					newMapPosition += offset;
//					SetAction(new MovementAction(this, originalPosition, newMapPosition));
//					return;
//				}
//			}
//		}

//		if (PlayerInputHandler.Instance.attackPressed)
//		{
//			var offset = Dungeon.GetFacingOffset(CurrentFacing);
//			newMapPosition += offset;
//			SetAction(new AttackAction(this, originalPosition, newMapPosition));
//			return;
//		}
//		else if (PlayerInputHandler.Instance.interactPressed)
//		{
//			if (currentInteractable != null)
//			{
//				SetAction(new InteractAction(currentInteractable));
//				return;
//			}
//		}
//		if (Input.GetKeyDown(KeyCode.Z))
//		{
//			//pass turn
//			SetAction(new WaitAction());
//			return;
//		}
//	}

//	internal void InitialzeSkillsFromSave()
//	{
//		var activeSkills = Common.Instance.GameSaveData.OverworldSaveData.ActiveSkillNames;
//		foreach (var skillName in activeSkills)
//		{
//			Skill skill = Common.Instance.SkillManager.GetSkillByName(skillName);
//			Skill newSkill = Instantiate(skill, this.transform);

//			Skill item = newSkill;
//			this.Skills.Add(item);
//		}
//	}

//	internal void SetAction(GameAction userAction)
//	{
//		if (userAction is MovementAction movementAction)
//		{
//			var swapAlly = Game.Instance.AllCharacters
//				.Where(x => x != this)
//				.Where(x => x.Team == Team.Player)
//				.FirstOrDefault(x => x.TilemapPosition == movementAction.newMapPosition);

//			if (swapAlly != null)
//			{
//				userAction = new SwapAllyPositionAction(this, swapAlly);
//			}
//		}

//		if (userAction is AttackAction attackAction)
//		{
//			var attackedAlly = Game.Instance.AllCharacters
//				.Where(x => x != this)
//				.Where(x => x.Team == Team.Player)
//				.FirstOrDefault(x => x.TilemapPosition == attackAction.attackPosition)
//				as Ally;

//			if (attackedAlly != null)
//			{
//				//open ally menu instead
//				var menuManager = FindObjectOfType<MenuManager>(true);
//				menuManager.OpenAllyMenu(attackedAlly);

//				holdTime = 0;
//				pickedAction = null;
//				return;
//			}
//		}

//		if (!userAction.IsValid(this))
//		{
//			pickedAction = null;
//			return;
//		}

//		pickedAction = userAction;
//		isWaitingForPlayerInput = false;
//	}

//	public override List<GameAction> GetDeterminedAction()
//	{
//		//isBusy = true;
//		this.Vitals.ActionsPerTurnLeft--;
//		this.DisplayedVitals.ActionsPerTurnLeft--;
//		return this.DeterminedActions;
//	}
//	public override void DetermineAction()
//	{
//		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
//			.Where(x => x != null);
//		if (actionOverrides.Any())
//		{
//			this.DeterminedActions = actionOverrides.ToList();
//			return;
//		}

//		DeterminedActions = new();

//		DeterminedActions.Add(pickedAction);

//		DeterminedActions.Add(
//			new ModifyStatAction(
//			this,
//			this,
//			(stats, vitals) =>
//			{
//				vitals.HungerAccumulate++;

//				if (vitals.HP < stats.HPMax)
//				{
//					vitals.HPRegenAcccumlate++;
//				}

//				if (vitals.SP < stats.SPMax)
//				{
//					vitals.SPRegenAcccumlate++;
//				}
//			},
//			false));

//		if (Vitals.HungerAccumulate > FinalStats.HungerAccumulateThreshold)
//		{
//			DeterminedActions.Add(new ModifyStatAction(
//				this,
//				this,
//				(stats, vitals) =>
//				{
//					vitals.HungerAccumulate = 0;
//					vitals.Hunger--;
//				},
//				false));
//		}

//		if (Vitals.Hunger <= 0)
//		{
//			DeterminedActions.Add(new TakeDamageAction(this, this, 1, true));
//		}

//		if (Vitals.HPRegenAcccumlate > FinalStats.HPRegenAcccumlateThreshold &&
//			Vitals.Hunger > 0)
//		{
//			DeterminedActions.Add(new ModifyStatAction(
//				this,
//				this,
//				(stats, vitals) =>
//				{
//					vitals.HPRegenAcccumlate = 0;
//					vitals.HP++;
//				},
//				false));
//		}

//		if (Vitals.SPRegenAcccumlate > FinalStats.SPRegenAcccumlateThreshold &&
//			Vitals.Hunger > 0)
//		{
//			DeterminedActions.Add(new ModifyStatAction(
//				this,
//				this,
//				(stats, vitals) =>
//				{
//					vitals.SPRegenAcccumlate = 0;
//					vitals.SP++;
//				},
//				false));
//		}
//	}

//	public override List<GameAction> ExecuteActionImmediate(GameAction action)
//	{
//		if (GetActionInterupt(action))
//		{
//			return new();
//		}

//		var sideEffects = action.ExecuteImmediate(this);
//		var actionResponses = GetActionResponses(action);
//		var actionResponseEffects = actionResponses.SelectMany(x => x.ExecuteImmediate(this));
//		sideEffects.AddRange(actionResponseEffects);

//		return sideEffects;
//	}

//	public override IEnumerable<GameAction> GetResponseTo(GameAction action)
//	{
//		return GetActionResponses(action);
//	}

//	public override IEnumerator ExecuteActionRoutine(GameAction action)
//	{
//		if (this == null) { yield break; }

//		yield return StartCoroutine(action.ExecuteRoutine(this));
//		action.UpdateDisplayedStats();
//	}

//	public override void StartTurn()
//	{
//		isWaitingForPlayerInput = true;
//		Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
//		Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

//		SyncDisplayedStats();

//		//update minimap
//		if (Game.Instance.CurrentDungeon != null)
//		{
//			var visibleTiles = Game.Instance.CurrentDungeon.GetVisionBounds(this, this.TilemapPosition);
//			Minimap minimap = FindFirstObjectByType<Minimap>();
//			minimap.UpdateVision(visibleTiles);
//			minimap.UpdateMinimap(visibleTiles);
//		}
//	}

//	public override List<GameAction> GetTrapSideEffects()
//	{
//		if (currentInteractable is Trap trap)
//		{
//			currentInteractable = null;
//			Game.Instance.DoFloatingText(trap.GetInteractionText(), Color.yellow, this.VisualParent.transform.position);
//			return trap.GetTrapSideEffects(this);
//		}

//		return new();
//	}

//	public override List<GameAction> GetInteractableSideEffects()
//	{
//		if (currentInteractable is not Trap and not null)
//		{
//			Game.Instance.DoFloatingText(currentInteractable.GetInteractionText(), Color.yellow, this.VisualParent.transform.position);
//			var effects =  currentInteractable.GetInteractionSideEffects(this);
//			currentInteractable = null;
//			return effects;
//		}

//		return new();
//	}

//	internal override void PlayWalkAnimation()
//	{
//		HeroAnimator.PlayWalkAnimation();
//	}

//	internal override void PlayIdleAnimation()
//	{
//		HeroAnimator.PlayIdleAnimation();
//	}

//	internal override void PlayAttackAnimation()
//	{
//		HeroAnimator.PlayAttackAnimation();
//	}

//	internal override void PlayTakeDamageAnimation()
//	{
//		HeroAnimator.PlayTakeDamageAnimation();
//	}

//	internal override void PlayDeathAnimation()
//	{
//		HeroAnimator.PlayDeathAnimation();
//	}


//	Character GetNextSelectableWithWrap(Character current, List<Character> allEntities, Vector3Int dir)
//	{
//		Character best = null;
//		float bestDist = float.MaxValue;

//		// 1. Try finding normally in the desired direction
//		best = FindInDirection(current, allEntities, dir);
//		if (best != null) return best;

//		return best;
//	}

//	Character FindInDirection(Character from, List<Character> entities, Vector3Int dir)
//	{
//		Character best = null;
//		float bestDist = float.MaxValue;

//		Vector2 direction = new Vector2(dir.x, dir.y).normalized;
//		float directionThreshold = 0.7f; // ~45 degree cone

//		foreach (var entity in entities)
//		{
//			if (entity == from) continue;

//			int dx = entity.TilemapPosition.x - from.TilemapPosition.x;
//			int dy = entity.TilemapPosition.y - from.TilemapPosition.y;

//			Vector2 toTarget = new Vector2(dx, dy);

//			// Skip if target is on or behind "from" in the given direction
//			if (Vector2.Dot(toTarget, direction) <= 0) continue;

//			Vector2 toTargetNormalized = toTarget.normalized;
//			float dot = Vector2.Dot(toTargetNormalized, direction);

//			// Use Manhattan distance as before
//			float dist = Mathf.Abs(dx) + Mathf.Abs(dy);

//			Debug.Log($"{entity} ({entity.TilemapPosition.x},{entity.TilemapPosition.y}) {dot} {dist}", entity);

//			// Check if target lies within the direction cone
//			if (dot < directionThreshold)
//			{
//				continue;
//			}


//			if (dist < bestDist)
//			{
//				bestDist = dist;
//				best = entity;
//			}
//		}

//		return best;
//	}
//}
