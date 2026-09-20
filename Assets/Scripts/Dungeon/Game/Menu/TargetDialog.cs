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
        private System.Func<Character, Vector3Int, GameAction> createAction;
        private int missileRange;
        private TMPro.TMP_Text promptText;
        private string originalPrompt;
        public Vector3Int Direction { get; private set; }
        public Vector3Int MissileEndpoint { get; private set; }
        private Vector2 lastMove;
        private float nextMoveTime;
        public List<Character> Targetables { get; private set; }
        public Character CameraTarget { get; private set; }

        internal void Setup(Character character, Skill skill)
        {
            Setup(character, skill.GetTargetCharacters(character),
                (target, direction) => skill.Targeting == SkillTargeting.Missile ?
                    SkillAction.ForMissile(character, skill, direction) : new SkillAction(character, skill, target),
                skill.Targeting == SkillTargeting.Missile ? skill.MissileRange : 0);
        }

        internal void Setup(Character character, List<Character> targets,
            System.Func<Character, Vector3Int, GameAction> factory, int range = 0)
        {
            casterCharacter = character;
            createAction = factory;
            missileRange = range;
            Targetables = targets;
            if (missileRange == 0 && Targetables.Count == 0) { MenuManager.Close(this); return; }
            enabled = true;
            Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.TargetSelecting;
            SelectTargetPrompt.SetActive(true);
            promptText = SelectTargetPrompt.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (promptText != null)
            {
                originalPrompt = promptText.text;
                if (missileRange > 0) promptText.text = "Aim in a direction, then confirm";
            }
            if (missileRange > 0) Aim(Dungeon.GetFacingOffset(character.CurrentFacing));
            else SelectTarget(Targetables[0]);
            lastMove = Vector2.zero;
            nextMoveTime = 0;
        }

        private void Aim(Vector3Int direction)
        {
            Direction = direction;
            var hit = MissileTargeting.Trace(casterCharacter, direction, missileRange);
            MissileEndpoint = hit.Cell;
            CameraTarget = hit.Character;
            TargetIndicator.SetActive(true);
            TargetIndicator.transform.position = Game.Instance.CurrentDungeon.CellToWorld(hit.Cell);
            MenuManager.Instance.TargetArrow.transform.position = Game.Instance.CurrentDungeon.CellToWorld(casterCharacter.TilemapPosition + direction);
            Game.Instance.PlayerController.CameraController.SetFollowTarget(casterCharacter.transform);
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
            if (missileRange > 0)
            {
                Aim(new Vector3Int(Mathf.Abs(move.x) > 0.25f ? (int)Mathf.Sign(move.x) : 0,
                    Mathf.Abs(move.y) > 0.25f ? (int)Mathf.Sign(move.y) : 0));
                return;
            }
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
            if (createAction == null || casterCharacter == null) return;
            var caster = casterCharacter;
            var action = createAction(CameraTarget, Direction);
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
            if (promptText != null) promptText.text = originalPrompt;
            promptText = null;
            Targetables = null;
            CameraTarget = null;
            casterCharacter = null;
            createAction = null;
            missileRange = 0;
            Direction = Vector3Int.zero;
            Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.FollowAlly;
        }
        internal override void SetFirstSelect() { }
        internal void SetNavigation() { }
    }
}
