using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Enemy : Character
{
	public EnemyState CurrentEnemyState;
	public Animator Animator;
	public Vector3Int? PursuitPosition;

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
		this.actionsPerTurnLeft--;
		bool canSeePlayer = CanSeePlayer();

		if (canSeePlayer)
		{
			PursuitPosition = game.PlayerCharacter.TilemapPosition;
		}

		if (PursuitPosition == this.TilemapPosition)
		{
			PursuitPosition = null;
		}

		if (!canSeePlayer ||
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

	private bool CanSeePlayer()
	{
		var game = Game.Instance;

		//determine if the monster can see player
		BoundsInt visionBounds;
		//TODO fix vision
		//if (game.DungeonGenerator.IsRoom(this.TilemapPosition))
		//{
		//	var tileGameData = game.DungeonGenerator.TileGameDataLookup[this.TilemapPosition];
		//	visionBounds = new BoundsInt()
		//	{
		//		xMin = tileGameData.node.room.X - 1,
		//		xMax = tileGameData.node.room.X + tileGameData.node.room.Width + 1,
		//		yMin = tileGameData.node.room.Y - 1,
		//		yMax = tileGameData.node.room.Y + tileGameData.node.room.Height + 1
		//	};
		//}
		//else
		//{
			visionBounds = new BoundsInt()
			{
				xMin = TilemapPosition.x - 1,
				xMax = TilemapPosition.x + 1,
				yMin = TilemapPosition.y - 1,
				yMax = TilemapPosition.y + 1
			};
		//}

		Vector3Int tilemapPosition = game.PlayerCharacter.TilemapPosition;
		var canSeePlayer = Contains2D(visionBounds, tilemapPosition);

		return canSeePlayer;
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
		return action.ExecuteImmediate(this);
	}

	public override IEnumerator ExecuteActionRoutine(GameAction action)
	{
		if (this == null) { yield break; }
		yield return StartCoroutine(action.ExecuteRoutine(this));
		action.UpdateDisplayedStats();
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

	public override void StartTurn()
	{
		actionsPerTurnLeft = ActionsPerTurnMax;
		attacksPerTurnLeft = AttacksPerTurnMax;
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
	#endregion
}

public enum EnemyState
{
	Idle,
	Sleep,
	Wander,
	Pursuit
}