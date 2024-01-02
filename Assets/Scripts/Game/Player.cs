using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Character
{
	private float holdTime = 0f;
	private float repeatTime = 0.1f;
	private bool advancing;
	private GameAction pickedAction;
	public Camera Camera;

	internal InteractableTile currentInteractable;
	private Vector3 cameraOffset;

	public Animator HeroAnimator;

	public GameObject ThrownItemProjectilePrefab;

	private void Start()
	{
		cameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);
		HeroAnimator.Play("");
	}

	private void LateUpdate()
	{
		Camera.transform.position = this.transform.position + cameraOffset;
	}

	private void Update()
	{
		//TODO handle diagonal input
		if (Input.GetKey(KeyCode.W) ||
			Input.GetKey(KeyCode.A) ||
			Input.GetKey(KeyCode.S) ||
			Input.GetKey(KeyCode.D))
		{
			holdTime += Time.deltaTime;
		}

		var game = Game.Instance;
		if (Game.Instance.InventoryMenu.isActiveAndEnabled)
		{
			holdTime = 0;
			return;
		}

		if (game.TurnManager.CurrentTurnPhase == TurnPhase.Player)
		{
			DeterminePlayerAction();
		}
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
		if (!userAction.IsValid(this))
		{
			pickedAction = null;
			return;
		}

		pickedAction = userAction;
		ActionPicked?.Invoke(userAction);
	}

	public override List<GameAction> DetermineActions()
	{
		List<GameAction> actions = new();

		actions.Add(pickedAction);

		this.BaseStats.HungerAccumulate++;
		this.BaseStats.HPRegenAcccumlate++;

		if (RealStats.HungerAccumulate > RealStats.HungerAccumulateThreshold)
		{
			this.BaseStats.HungerAccumulate = 0;
			actions.Add(new ModifyStatAction(
				this,
				this,
				(character) =>
				{
					character.BaseStats.Hunger--;
				},
				false));
		}

		if (RealStats.Hunger <= 0)
		{
			actions.Add(new TakeDamageAction(this, this, 1, true));
		}

		if (RealStats.HPRegenAcccumlate > RealStats.HPRegenAcccumlateThreshold &&
			RealStats.Hunger > 0)
		{
			this.BaseStats.HPRegenAcccumlate = 0;
			actions.Add(new ModifyStatAction(
				this,
				this,
				(character) =>
				{
					character.BaseStats.HP++;
				},
				false));
		}

		return actions;
	}

	public override List<GameAction> ExecuteActionImmediate(GameAction action)
	{
		var primarySideEffects = action.ExecuteImmediate(this);
		return primarySideEffects;
	}

	public override IEnumerator ExecuteActionRoutine(GameAction action)
	{
		if (this == null) { yield break; }
		//if (RealStats.HP > 0)
		//{
			yield return StartCoroutine(action.ExecuteRoutine(this));
		//}
	}

	public override void StartTurn()
	{
		var game = Game.Instance;
		//check tile for interactable;
		currentInteractable = game.CurrentDungeon.GetInteractable(TilemapPosition);
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

	public delegate void ActionHandler(GameAction gameAction);
	public event ActionHandler ActionPicked;
}
