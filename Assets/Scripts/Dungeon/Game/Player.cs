﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Player : Character
{
	public List<Skill> Skills;

	private float holdTime = 0f;
	private float repeatTime = 0.1f;
	private GameAction pickedAction;
	public Camera Camera;
	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);

	internal Interactable currentInteractable;

	public Animator HeroAnimator;

	public GameObject ThrownItemProjectilePrefab;
	private bool isBusy;
	private float menuCooldown;

	public List<GameAction> DeterminedActions { get; private set; }

	public override bool IsBusy => isBusy;
	private void Start()
	{
		HeroAnimator.Play("");
	}

	private void LateUpdate()
	{
		Camera.transform.position = this.transform.position + CameraOffset;
	}

	private void Update()
	{
		menuCooldown += Time.deltaTime;
		//Debug.Log($"Player is busy {IsBusy}");

		//TODO handle diagonal input
		if (Input.GetKey(KeyCode.W) ||
			Input.GetKey(KeyCode.A) ||
			Input.GetKey(KeyCode.S) ||
			Input.GetKey(KeyCode.D))
		{
			holdTime += Time.deltaTime;
		}

		var game = Game.Instance;
		if (Game.Instance.InventoryMenu.isActiveAndEnabled ||
			Game.Instance.AllyMenu.isActiveAndEnabled)
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
		if (!Input.GetKey(KeyCode.LeftShift))
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
			Input.GetKeyDown(KeyCode.D)) &&
			!Input.GetKey(KeyCode.LeftShift))
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

		if (Input.GetKeyDown(KeyCode.Space))
		{
			var offset = Dungeon.GetFacingOffset(CurrentFacing);
			newMapPosition += offset;
			SetAction(new AttackAction(this, originalPosition, newMapPosition));
			return;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			SetAction(new SkillAction(this, Skills[0]));
			return;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			SetAction(new SkillAction(this, Skills[1]));
			return;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha3))
		{
			SetAction(new SkillAction(this, Skills[2]));
			return;
		}
		else if (Input.GetKeyDown(KeyCode.E))
		{
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
		HeroAnimator.Play("Walk_SwordShield", 0);
		HeroAnimator.speed = 5f;
	}

	internal override void PlayIdleAnimation()
	{
		HeroAnimator.Play("Idle_SwordShield", 0);
	}

	internal override void PlayAttackAnimation()
	{
		var attack1 = "NormalAttack01_SwordShield";
		var attack2 = "NormalAttack02_SwordShield";

		var attack = UnityEngine.Random.value > 0.5f ? attack1 : attack2;
		HeroAnimator.Play(attack, 0, 0f);
	}

	internal override void PlayTakeDamageAnimation()
	{
		HeroAnimator.Play("GetHit_SwordShield", 0);
	}

	internal override void PlayDeathAnimation()
	{
		HeroAnimator.Play("Die_SwordShield", 0);
	}
}
