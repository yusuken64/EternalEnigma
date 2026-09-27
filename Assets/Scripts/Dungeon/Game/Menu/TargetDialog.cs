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
        private DungeonProp selectedProp;
        private List<DungeonProp> props = new();
        private System.Func<Character, Vector3Int, GameAction> createAction;
        private int missileRange;
        public string RangeLabel => missileRange > 0 ? $"Range: {missileRange} tiles" : targetingSkill != null ? "Choose a highlighted valid target" : "";
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
            targetingSkill = skill;
            props = ScenerySkillTargets.Candidates(character,skill);
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
            if (missileRange == 0 && Targetables.Count == 0 && props.Count == 0) { MenuManager.Close(this); return; }
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
            else if(Targetables.Count > 0) SelectTarget(Targetables[0]); else SelectProp(props[0]);
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

        private void SelectProp(DungeonProp prop)
        {
            selectedProp = prop; CameraTarget = null;
            TargetIndicator.SetActive(true); TargetIndicator.transform.position = Game.Instance.CurrentDungeon.CellToWorld(prop.Position);
            MenuManager.Instance.TargetArrow.transform.position = TargetIndicator.transform.position;
            Game.Instance.PlayerController.CameraController.SetFollowTarget(prop.transform);
        }
        private void SelectTarget(Character target)
        {
            selectedProp = null;
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
            if (props.Count > 0)
            {
                var cells = Targetables.Where(c=>c!=null && c.Vitals.HP>0).Select(c=>c.TilemapPosition).Concat(props.Where(p=>p!=null && p.Alive).Select(p=>p.Position)).ToList();
                var current = selectedProp != null ? selectedProp.Position : CameraTarget.TilemapPosition;
                var choices = cells.Where(p=>p!=current).ToList();
                if(choices.Count > 0)
                {
                    var forward = choices.Where(p=>Vector2.Dot(((Vector2)(Vector3)(p-current)).normalized,direction)>.7f).OrderBy(p=>TileWorldDungeon.ChevDistance(p,current)).ToList();
                    var cell = forward.Count > 0 ? forward[0] : choices.OrderBy(p=>Vector2.Dot((Vector2)(Vector3)p,direction)).First();
                    var prop = props.FirstOrDefault(p=>p!=null && p.Position==cell);
                    if(prop!=null) SelectProp(prop); else SelectTarget(Targetables.First(c=>c.TilemapPosition==cell));
                }
                nextMoveTime=Time.unscaledTime+(lastMove==Vector2.zero?.3f:.1f);lastMove=move;return;
            }
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
            var action = selectedProp != null && targetingSkill != null ? SkillAction.ForScenery(caster,targetingSkill,selectedProp) : createAction(CameraTarget, Direction);
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
            props.Clear(); selectedProp=null; targetingSkill=null;
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
