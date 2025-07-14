using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JuicyChickenGames.Menu
{
	public class TargetDialog : Dialog
	{
		public GameObject SelectTargetPrompt;
		private Character casterCharacter;
		private Skill targetingSkill;
		private EventSystem _eventSystem;
		private float menuCooldown = 0f;

		// === Targeting State ===
		private Skill TargetingSkill;
		private Action<Ally, Skill, Vector3Int> TargetSelectedAction;
		private Character cameraTarget;

		public List<Character> Targetables { get; private set; }
		public Character CameraTarget
		{
			get => cameraTarget;
			set
			{
				cameraTarget = value;
				UpdateCameraFollow();
			}
		}

		internal void Setup(Character character, Skill skill)
		{
			casterCharacter = character;
			targetingSkill = skill;

			var player = FindFirstObjectByType<PlayerController>();
			SelectTargetPrompt.SetActive(true);
			var possibleTargets = skill.GetTargets(character)
				.Select(x => Game.Instance.AllCharacters.First(y => y.TilemapPosition == x))
				.ToList(); //TODO adapt possible targets to positions instead of characters
			InvokeTargetSelection(skill, possibleTargets, TargetSelected);
			_eventSystem = EventSystem.current;
			_eventSystem.enabled = false;
		}

		private void Update()
		{
			if (Game.Instance.PlayerController.CurrentControlMode == PlayerControlMode.TargetSelecting)
			{
				menuCooldown += Time.deltaTime;
				if (menuCooldown > 0.2f)
				{
					HandleTargetInput();
				}
			}
		}

		private void TargetSelected(Ally caster, Skill skill, Vector3Int target)
		{
			caster.SetAction(new SkillAction(caster, skill, target));
			SelectTargetPrompt.SetActive(false);
			CloseAction?.Invoke();
			this.Close();
		}

		private void UpdateCameraFollow()
		{
			if (CameraTarget != null)
			{
				Game.Instance.PlayerController.CameraController.SetFollowTarget(CameraTarget.transform);
			}
		}

		internal override void SetFirstSelect()
		{
			//EventSystem.current?.firstSelectedGameObject = null;
		}

		internal void Close()
		{
			_eventSystem.enabled = true;
			Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.FollowAlly;
		}

		internal void SetNavigation()
		{
		}

		private void SelectTargetable(Facing facing)
		{
			var dir = Dungeon.GetFacingOffset(facing);
			var next = GetNextSelectableWithWrap(CameraTarget, Targetables, dir);

			if (next == null) { return; }
			CameraTarget = next;
			MenuManager.Instance.TargetArrow.transform.position = CameraTarget.transform.position;
		}

		internal void InvokeTargetSelection(Skill skill, List<Character> possibleTargets, Action<Ally, Skill, Vector3Int> targetSelected)
		{
			Targetables = possibleTargets;
			TargetSelectedAction = targetSelected;
			TargetingSkill = skill;

			MenuInputHandler.Instance.SubmitMenuInput = false;
			Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.TargetSelecting;
			CameraTarget = possibleTargets.First();
			MenuManager.Instance.TargetArrow.transform.position = CameraTarget.transform.position;
		}

		internal void CancelTargetSelection()
		{
			Targetables = null;
			CameraTarget = null;
			TargetingSkill = null;
			TargetSelectedAction = null;
			Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.FollowAlly;
		}

		internal void ConfirmTarget()
		{
			TargetSelectedAction?.Invoke(Game.Instance.PlayerController.ControlledAlly, TargetingSkill, CameraTarget.TilemapPosition);
			Targetables = null;
			CameraTarget = null;
			TargetingSkill = null;
			TargetSelectedAction = null;
			Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.FollowAlly;
		}

		Character GetNextSelectableWithWrap(Character current, List<Character> allEntities, Vector3Int dir)
		{
			Character best = FindInDirection(current, allEntities, dir);
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

		private void HandleTargetInput()
		{
			Vector2 move = MenuInputHandler.Instance.MoveInput;
			if (!MenuInputHandler.Instance.IsMoving || move.magnitude < 0.5f)
				return;

			Facing facing = Facing.Down;

			if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
				facing = move.x > 0 ? Facing.Right : Facing.Left;
			else
				facing = move.y > 0 ? Facing.Up : Facing.Down;

			SelectTargetable(facing);
			menuCooldown = 0;
		}

	}
}