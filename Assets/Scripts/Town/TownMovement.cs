using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EternalEnigma.Core.World;

internal class TownMovement : TownAction
{
	private const float WalkDuration = 0.1f;
	private TownPlayer townPlayer;
	private Vector3Int originalPosition;
	private Vector3Int newMapPosition;
	private readonly bool heldMovement;

	public TownMovement(TownPlayer townPlayer, Vector3Int originalPosition, Vector3Int newMapPosition, bool heldMovement = false)
	{
		this.heldMovement = heldMovement;
		this.townPlayer = townPlayer;
		this.originalPosition = originalPosition;
		this.newMapPosition = newMapPosition;
	}

	internal override List<TownAction> ExecuteImmediate()
	{
		var town=UnityEngine.Object.FindFirstObjectByType<Town>();
        if(town!=null && !town.CanEnter(newMapPosition))return new();
        // Move the currently controlled ally
		townPlayer.ControllingTownAlly.TilemapPosition = newMapPosition;
		townPlayer.RecordWalkPosition();
		town?.RefreshRoofs();

		int trailIndex = 1;
		foreach (var ally in townPlayer.RecruitedAllies)
		{
			if (ally == townPlayer.ControllingTownAlly)
				continue;

			// Move following ally to previous position of their leader
			var target=townPlayer.GetNthFromLastPosition(trailIndex);
            if(town==null||town.CanEnter(target))ally.TilemapPosition = target;
			trailIndex++;
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine()
	{
		// Reorder list so the controlling ally is first
		List<TownAlly> orderedAllies = new();
		orderedAllies.Add(townPlayer.ControllingTownAlly);
		orderedAllies.AddRange(townPlayer.RecruitedAllies.Where(a => a != townPlayer.ControllingTownAlly));

		List<Tweener> tweens = new();

		for (int i = 0; i < orderedAllies.Count; i++)
		{
			var ally = orderedAllies[i];
			var targetTile = ally.TilemapPosition;
			Vector3 targetWorld = townPlayer.WalkableMap.CellToWorld(targetTile);

			// Calculate facing based on current world position (not TilemapPosition)
			Vector3 offsetWorld = targetWorld - ally.transform.position;
			if (offsetWorld.sqrMagnitude < .0001f)
			{
				ally.HeroAnimator?.StopWalkContinuation();
				continue;
			}
			var direction = new Vector3Int((int)Mathf.Clamp(offsetWorld.x, -1, 1),
					  (int)Mathf.Clamp(offsetWorld.y, -1, 1),
					  (int)offsetWorld.z);
			ally.SetFacing(GetFacing(direction));
			if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
			{
				ally.transform.position = targetWorld;
				ally.HeroAnimator?.PlayIdleAnimation();
				continue;
			}

			ally.HeroAnimator?.BeginWalk(heldMovement && townPlayer.CanContinueHeldWalk);
			var tween = ally.transform.DOMove(targetWorld, WalkDuration);
			if (heldMovement) tween.SetEase(Ease.Linear);
			tweens.Add(tween);
		}
		if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
		{
			townPlayer.CameraController?.SnapToFollowTarget();
			yield break;
		}
		var plan=UnityEngine.Object.FindFirstObjectByType<Town>()?.Plan;
		if (plan != null && newMapPosition != originalPosition)
		{
			var cell=newMapPosition.ToGridPoint();
			var effects=AudioManager.Instance?.SoundEffects;
			var clip=plan.Layers[TownLayers.ShopFloor].At(cell) ? effects?.StepWood :
				plan.Layers[TownLayers.Parks].At(cell) ? effects?.StepGrass : effects?.StepRock;
			AudioManager.Instance?.PlayFootstep(clip,plan.Seed,newMapPosition.x,newMapPosition.y);
		}

		// All party tweens start together. Yield once per frame for the group;
		// yielding each completed/killed tween can add a frame per follower.
		while (tweens.Any(tween => tween.IsActive() && !tween.IsComplete()))
			yield return null;

		// Retain only the walk cycles started by this manual movement sequence.
		foreach (var ally in orderedAllies)
		{
			ally.HeroAnimator?.CompleteWalk(heldMovement && townPlayer.CanContinueHeldWalk);
		}
		townPlayer.CameraController?.SnapToFollowTarget();
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

	internal TownMovement GetReverse()
	{
		return new TownMovement(this.townPlayer, newMapPosition, originalPosition);
	}
}

public abstract class TownAction
{
	internal abstract List<TownAction> ExecuteImmediate();
	internal abstract IEnumerator ExecuteRoutine();
}
