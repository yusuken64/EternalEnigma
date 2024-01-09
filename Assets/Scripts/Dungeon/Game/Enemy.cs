using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Enemy : Character
{
	public EnemyState CurrentEnemyState;
	public Animator Animator;

	public string Description { get; internal set; }

	public List<PolicyBase> Policies;

	private void Start()
	{
		var worldPosition = Game.Instance.CurrentDungeon.CellToWorld(TilemapPosition);
		this.transform.position = worldPosition;

		CurrentEnemyState = EnemyState.Pursuit;

		var game = Game.Instance;
		Policies = new();
		Policies.Add(new AttackPolicy(game, this, 0));
		Policies.Add(new PursuitPolicy(game, this, 1));
		Policies.Add(new WanderPolicy(game, this, 2));

		var policyOverrides = GetComponents<PolicyOverride>();
		foreach (var policyOverride in policyOverrides)
		{
			PolicyBase policy = policyOverride.GetOverridePolicy(game, this);
			Policies.Add(policy);
		}

		Policies = Policies.OrderBy(x => x.priority).ToList();
	}

	public override List<GameAction> DetermineActions()
	{
		if (this?.gameObject == null)
		{
			return null;
		}
		if (Vitals.HP <= 0)
		{
			return null;
		}

		//this should affect player the same way, to do in characerbase class?
		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
			.Where(x => x != null);
		if (actionOverrides.Any())
		{
			return actionOverrides.ToList();
		}

		var game = Game.Instance;
		this.Vitals.ActionsPerTurnLeft--;
		this.DisplayedVitals.ActionsPerTurnLeft--;
		PursuitTarget = GetTarget();

		if (PursuitTarget != null)
		{
			PursuitPosition = PursuitTarget.TilemapPosition;
		}

		if (PursuitPosition == this.TilemapPosition)
		{
			PursuitPosition = null;
		}

		if (!PursuitTarget ||
			PursuitPosition == this.TilemapPosition)
		{
			CurrentEnemyState = EnemyState.Wander;
		}

		var action = Policies.FirstOrDefault(x => x.ShouldRun());
		if (action != null)
		{
			return action.GetActions();
		}

		return new();
	}

	private Character GetTarget()
	{
		var game = Game.Instance;

		//determine if the monster can see any character player team
		BoundsInt visionBounds = game.CurrentDungeon.GetVisionBounds(TilemapPosition);

		var playerTeamCharacters = game.AllCharacters.Where(x => x.Team == Team.Player);
		var ordered = playerTeamCharacters
			.OrderBy(x => TileWorldDungeon.ChevDistance(x.TilemapPosition, TilemapPosition))
			.ThenBy(x => x.TilemapPosition == PursuitPosition).ToList();

		//TODO: prioritise this somehow?
		return playerTeamCharacters
			.OrderBy(x => TileWorldDungeon.ChevDistance(x.TilemapPosition, TilemapPosition))
			.ThenBy(x => x.TilemapPosition == PursuitPosition)
			.FirstOrDefault(x => Contains2D(visionBounds, x.TilemapPosition));
	}

	//BoundsInt.Contain doesn't work?
	private bool Contains2D(BoundsInt visionBounds, Vector3Int tilemapPosition)
	{
		return visionBounds.xMin <= tilemapPosition.x &&
			visionBounds.xMax >= tilemapPosition.x &&
			visionBounds.yMin <= tilemapPosition.y &&
			visionBounds.yMax >= tilemapPosition.y;
	}

	public override List<GameAction> ExecuteActionImmediate(GameAction action)
	{
		if (GetActionInterupt(action))
		{
			return new();
		}

		var sideEffects = action.ExecuteImmediate(this);
		sideEffects.AddRange(GetActionResponses(action));

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
		Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
		Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

		SyncDisplayedStats();
	}

	#region Animation
	internal override void PlayWalkAnimation()
	{
		if (HasAnimation(Animator, "WalkFWD"))
		{
			Animator.Play("WalkFWD", 0);
		}
		else
		{
			Animator.Play("Fly", 0);
		}
		//Animator.speed = 5f;
	}
	internal bool HasAnimation(Animator animator, string animationNameToCheck)
	{
		AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0); // Get current animation clips (Layer 0)

		foreach (var clipInfo in clips)
		{
			if (clipInfo.clip != null && clipInfo.clip.name == animationNameToCheck)
			{
				//Debug.Log("Animator contains the animation: " + animationNameToCheck);
				return true;
			}
		}

		return false;
	}
	internal override void PlayIdleAnimation()
	{
		if (HasAnimation(Animator, "IdleNormal"))
		{
			Animator.Play("IdleNormal", 0);
		}
		else
		{
			Animator.StopPlayback();
		}
	}
	internal override void PlayAttackAnimation()
	{
		var clips = Animator.runtimeAnimatorController.animationClips;
		var clip = clips
			.Where(x => x.name.Contains("Attack"))
			.OrderBy(x => Guid.NewGuid())
			.First();
		Animator.Play(clip.name);
	}
	internal override void PlayTakeDamageAnimation()
	{
		Animator.Play("GetHit", 0);
	}
	internal override void PlayDeathAnimation()
	{
		Animator.Play("Die", 0);
	}

	public override List<GameAction> GetTrapSideEffects()
	{
		//TODO adapt this when enemies set off traps
		return new();
	}
	#endregion
}

public enum EnemyState
{
	Idle,
	Sleep,
	Wander,
	Pursuit
}