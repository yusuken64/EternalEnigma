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
    public override bool IsWaitingForPlayerInput { get; set; }

    private void Start()
	{
        gameObject.AddComponent<FogHiddenVisual>();
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

	public override void DetermineAction()
	{
		if (this?.gameObject == null)
		{
			determinedActions = new();
			return;
		}
		if (Vitals.HP <= 0)
		{
			determinedActions = new();
			return;
		}

		//this should affect player the same way, to do in characerbase class?
		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
			.Where(x => x != null);
		if (actionOverrides.Any())
		{
			determinedActions = actionOverrides.ToList();
			return;
		}

		var game = Game.Instance;
		PursuitTarget = GetPursuitTarget();

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
			determinedActions = action.GetActions();
		}
	}

	public override List<GameAction> GetDeterminedAction()
	{
		this.Vitals.ActionsPerTurnLeft--;
		this.DisplayedVitals.ActionsPerTurnLeft--;
		return determinedActions;
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
		yield return action.ExecuteRoutine(this, !action.ShouldAnimate(this));

		action.UpdateDisplayedStats();
	}

	public override void StartTurn()
	{
		MovedThisTurn = false;
		determinedActions = null;
		Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
		Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

		SyncDisplayedStats();
	}

	#region Animation
	// Controllers use both generic states and monster-prefixed clip/state names.
	internal string[] AnimationStates(string action)
	{
		return Animator.runtimeAnimatorController.animationClips
			.SelectMany(clip => new[] { clip.name, clip.name.Substring(clip.name.LastIndexOf('_') + 1) })
			.Where(name => name.IndexOf(action, StringComparison.OrdinalIgnoreCase) >= 0 && HasAnimation(Animator, name))
			.Distinct().OrderBy(name => name.Length).ThenBy(name => name, StringComparer.Ordinal).ToArray();
	}
	internal string WalkAnimationState => AnimationStates("WalkFWD").FirstOrDefault()
		?? AnimationStates("FlyFWD").FirstOrDefault() ?? AnimationStates("Walk").FirstOrDefault()
		?? AnimationStates("IdleNormal").FirstOrDefault(); // Worm has no locomotion clip.
	private void PlayState(string state)
	{
		if (state == null) throw new InvalidOperationException($"Enemy '{name}' has no matching animation state.");
		Animator.Play(state, 0, 0f);
		Animator.Update(0f);
	}
	internal override void PlayWalkAnimation()
	{
		PlayState(WalkAnimationState);
	}
	internal bool HasAnimation(Animator animator, string animationNameToCheck)
	{
		return animator != null && animator.HasState(0, Animator.StringToHash(animationNameToCheck));
	}
	internal override void PlayIdleAnimation()
	{
		PlayState(AnimationStates("IdleNormal").FirstOrDefault());
	}
	internal override void PlayAttackAnimation()
	{
		var states = AnimationStates("Attack");
		PlayState(states.Length > 0 ? states[UnityEngine.Random.Range(0, states.Length)] : null);
	}
	internal override void PlayTakeDamageAnimation()
	{
		PlayState(AnimationStates("GetHit").FirstOrDefault());
	}
	internal override void PlayDeathAnimation()
	{
		PlayState(AnimationStates("Die").FirstOrDefault());
	}

	public override List<GameAction> GetTrapSideEffects()
	{
		//TODO adapt this when enemies set off traps
		return new();
	}
	public override List<GameAction> GetInteractableSideEffects()
	{
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
