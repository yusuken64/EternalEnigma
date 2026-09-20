using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JuicyChickenGames.Menu
{
    public class TargetDialog : Dialog
    {
        public GameObject SelectTargetPrompt;
        public GameObject TargetIndicator;
        private Character casterCharacter;
        private Skill targetingSkill;
        private Vector2 lastMove;
        private float nextMoveTime;
        public List<Character> Targetables { get; private set; }
        public Character CameraTarget { get; private set; }

        internal void Setup(Character character, Skill skill)
        {
            casterCharacter = character;
            targetingSkill = skill;
            Targetables = skill.GetTargetCharacters(character);
            if (Targetables.Count == 0) { MenuManager.Close(this); return; }
            enabled = true;
            Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.TargetSelecting;
            SelectTargetPrompt.SetActive(true);
            SelectTarget(Targetables[0]);
            lastMove = Vector2.zero;
            nextMoveTime = 0;
        }

        private void SelectTarget(Character target)
        {
            CameraTarget = target;
            TargetIndicator.SetActive(true);
            TargetIndicator.transform.position = target.transform.position;
            MenuManager.Instance.TargetArrow.transform.position = target.transform.position;
            Game.Instance.PlayerController.CameraController.SetFollowTarget(target.transform);
        }

        private void Update()
        {
            if (Game.Instance.PlayerController.CurrentControlMode != PlayerControlMode.TargetSelecting ||
                Common.Instance.GlobalSettings.IsOpen || MenuUIInputModule.Active?.InputConsumed == true) return;
            var move = Common.Instance.MenuInputHandler.MoveInput;
            if (move.sqrMagnitude < 0.25f) { lastMove = Vector2.zero; return; }
            if (lastMove != Vector2.zero && Time.unscaledTime < nextMoveTime) return;
            var direction = Mathf.Abs(move.x) > Mathf.Abs(move.y)
                ? new Vector2(Mathf.Sign(move.x), 0) : new Vector2(0, Mathf.Sign(move.y));
            var candidates = Targetables.Where(c => c != null && c.Vitals.HP > 0 && c != CameraTarget).ToList();
            var next = candidates.Where(c => Vector2.Dot(((Vector2)(Vector3)(c.TilemapPosition - CameraTarget.TilemapPosition)).normalized, direction) > 0.7f)
                .OrderBy(c => TileWorldDungeon.ChevDistance(c.TilemapPosition, CameraTarget.TilemapPosition)).FirstOrDefault();
            next ??= candidates.OrderBy(c => Vector2.Dot((Vector2)(Vector3)c.TilemapPosition, direction)).FirstOrDefault();
            if (next != null) SelectTarget(next);
            nextMoveTime = Time.unscaledTime + (lastMove == Vector2.zero ? 0.3f : 0.1f);
            lastMove = move;
        }

        internal void ConfirmTarget()
        {
            var caster = casterCharacter;
            var action = new SkillAction(caster, targetingSkill, CameraTarget);
            if (!action.IsValid(caster)) { MenuManager.Close(this); return; }
            MenuManager.Instance.CloseAllMenus();
            caster.SetAction(action);
        }

        internal void CancelTargetSelection() => Close();
        internal void Close()
        {
            enabled = false;
            TargetIndicator.SetActive(false);
            SelectTargetPrompt.SetActive(false);
            Targetables = null;
            CameraTarget = null;
            casterCharacter = null;
            targetingSkill = null;
            Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.FollowAlly;
        }
        internal override void SetFirstSelect() { }
        internal void SetNavigation() { }
    }
}
