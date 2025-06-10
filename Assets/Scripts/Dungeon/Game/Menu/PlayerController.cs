using UnityEngine;
using System.Collections.Generic;
using JuicyChickenGames.Menu;
using System.Linq;
using System;

public class PlayerController : MonoBehaviour
{
	private float holdTime = 0f;
	private float repeatTime = 0.1f;
	private GameAction pickedAction;
	public Camera Camera;
	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);
	private CheatConsole _cheatConsole;
	private float menuCooldown;
	public Ally ControlledAlly { get; private set; }
    public bool IsWaitingForPlayerInput { get; private set; }

    public CameraMode CurrentCameraMode;
	public Character FollowTarget;
	public List<Character> Targetables;

	public Inventory Inventory;
	public Vector3Int TilemapPosition => ControlledAlly.TilemapPosition;

    public Stats FinalStats => ControlledAlly.FinalStats;
	public Stats DisplayedStats => ControlledAlly.DisplayedStats;
	public Vitals Vitals => ControlledAlly.Vitals;
	public Vitals DisplayedVitals => ControlledAlly.DisplayedVitals;
	public Action<Ally, Skill, Vector3Int> TargetSelected { get; private set; }
	public Skill TargetingSkill;

	private void Start()
	{
		_cheatConsole = FindFirstObjectByType<CheatConsole>();
		if (Game.Instance.Allies.Count > 0)
        {
            TakeControl(Game.Instance.Allies[0]);
        }
    }

	private void Update()
	{
		if (_cheatConsole.ScreenObject.activeSelf) { return; }

		menuCooldown += Time.deltaTime;

		if (PlayerInputHandler.Instance.isMoving)
		{
			holdTime += Time.deltaTime;
		}

		if (CurrentCameraMode == CameraMode.TargetSelect)
		{
			if (PlayerInputHandler.Instance.isMoving)
			{
				if (Mathf.Abs(PlayerInputHandler.Instance.moveInput.x) > Mathf.Abs(PlayerInputHandler.Instance.moveInput.y))
				{
					if (PlayerInputHandler.Instance.moveInput.x > 0.5f)
						SelectTargetable(Facing.Right);
					else if (PlayerInputHandler.Instance.moveInput.x < -0.5f)
						SelectTargetable(Facing.Left);
				}
				else
				{
					if (PlayerInputHandler.Instance.moveInput.y > 0.5f)
						SelectTargetable(Facing.Up);
					else if (PlayerInputHandler.Instance.moveInput.y < -0.5f)
						SelectTargetable(Facing.Down);
				}
			}
			return;
		}

		if (Game.Instance.InventoryMenu.isActiveAndEnabled ||
			Game.Instance.AllyMenu.isActiveAndEnabled ||
			Game.Instance.SkillDialog.isActiveAndEnabled)
		{
			menuCooldown = 0;
			holdTime = 0;
			return;
		}

		if (IsWaitingForPlayerInput && menuCooldown > 0.2f)
		{
			DeterminePlayerAction();
		}
	}

	private void DeterminePlayerAction()
	{
		var originalPosition = new Vector3Int(ControlledAlly.TilemapPosition.x, ControlledAlly.TilemapPosition.y);
		var newMapPosition = new Vector3Int(ControlledAlly.TilemapPosition.x, ControlledAlly.TilemapPosition.y);

		Vector2 move = PlayerInputHandler.Instance.moveInput;

		if (move.sqrMagnitude >= 0.01f)
		{
			// Normalize the input so diagonal directions are consistent
			move.Normalize();

			if (move.x < -0.5f && move.y > 0.5f)
				ControlledAlly.SetFacing(Facing.UpLeft);
			else if (move.x > 0.5f && move.y > 0.5f)
				ControlledAlly.SetFacing(Facing.UpRight);
			else if (move.x < -0.5f && move.y < -0.5f)
				ControlledAlly.SetFacing(Facing.DownLeft);
			else if (move.x > 0.5f && move.y < -0.5f)
				ControlledAlly.SetFacing(Facing.DownRight);
			else if (move.y > 0.5f)
				ControlledAlly.SetFacing(Facing.Up);
			else if (move.x < -0.5f)
				ControlledAlly.SetFacing(Facing.Left);
			else if (move.y < -0.5f)
				ControlledAlly.SetFacing(Facing.Down);
			else if (move.x > 0.5f)
				ControlledAlly.SetFacing(Facing.Right);
		}

		if (!PlayerInputHandler.Instance.holdPosition)
		{
			if (holdTime > repeatTime)
			{
				holdTime = 0f;
				var offset = Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing);
				if (Game.Instance.CurrentDungeon.CanWalkTo(newMapPosition, newMapPosition + offset))
				{
					newMapPosition += offset;
					ControlledAlly.SetAction(new MovementAction(ControlledAlly, originalPosition, newMapPosition));
					return;
				}
			}
		}

		if (PlayerInputHandler.Instance.attackPressed)
		{
			var offset = Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing);
			newMapPosition += offset;
			ControlledAlly.SetAction(new AttackAction(ControlledAlly, originalPosition, newMapPosition));
			return;
		}
		else if (PlayerInputHandler.Instance.interactPressed)
		{
			if (ControlledAlly.currentInteractable != null)
			{
				ControlledAlly.SetAction(new InteractAction(ControlledAlly.currentInteractable));
				return;
			}
		}
	}

	public void TakeControl(Ally newAlly)
    {
        if (newAlly != null)
        {
            ControlledAlly = newAlly;
        }
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

	internal bool CanOpenMenu()
	{
		return !ControlledAlly.StatusEffects.Any(x => x.PreventsMenu());
	}

	private void SelectTargetable(Facing facing)
	{
		var dir = Dungeon.GetFacingOffset(facing);
		var next = GetNextSelectableWithWrap(FollowTarget, Targetables, dir);

		if (next == null) { return; }
		FollowTarget = next;
		MenuManager.Instance.TargetArrow.transform.position = FollowTarget.transform.position;
	}

	internal void InvokeTargetSelection(Skill skill, List<Character> possibleTargets, Action<Ally, Skill, Vector3Int> targetSelected)
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
		TargetSelected?.Invoke(ControlledAlly, TargetingSkill, FollowTarget.TilemapPosition);
		Targetables = null;
		FollowTarget = null;
		TargetingSkill = null;
		TargetSelected = null;
	}
}
