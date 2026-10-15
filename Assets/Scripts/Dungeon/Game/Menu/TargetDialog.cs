using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
    public class TargetDialog : Dialog
    {
        public GameObject SelectTargetPrompt;
        public GameObject TargetIndicator;
        private Character casterCharacter;
        private Skill targetingSkill;
        private UsableItemDefinition targetingItem;
        private TileWorldDungeon targetingFloor;
        private DungeonProp selectedProp;
        private List<DungeonProp> props = new();
        private System.Func<Character, Vector3Int, GameAction> createAction;
        private int missileRange;
        private List<Vector3Int> tiles = new();
        private Vector3Int selectedTile;
        private int EffectiveMissileRange => missileRange + (targetingSkill != null && targetingSkill.UsesArrows && casterCharacter != null ? ClassPassives.MissileRangeBonus(casterCharacter) : 0);
        public string RangeLabel => missileRange > 0 ? $"Range {EffectiveMissileRange} tiles" : targetingSkill != null ? "Choose a highlighted target" : "";
        private TMPro.TMP_Text promptText;
        private string originalPrompt;
        public Vector3Int Direction { get; private set; }
        public Vector3Int MissileEndpoint { get; private set; }
        private Vector2 lastMove;
        private float nextMoveTime;
        private bool pointerReady;
        private Vector2 lastPointer;
        private bool? promptGamepad;
        private Image pointerShield;
        private readonly List<RaycastResult> pointerHits = new();
        public List<Character> Targetables { get; private set; }
        public Character CameraTarget { get; private set; }
        public Vector3Int? TerminalSelectedCell => missileRange > 0 ? MissileEndpoint :
            tiles.Count > 0 ? selectedTile : selectedProp != null ? selectedProp.Position :
            CameraTarget != null ? CameraTarget.TilemapPosition : (Vector3Int?)null;
        public IReadOnlyList<Vector3Int> TerminalValidCells => tiles.Count > 0 ? tiles :
            Targetables != null ? Targetables.Where(c => c != null && c.Vitals != null).Select(c => c.TilemapPosition)
                .Concat(props.Where(p => p != null && p.Alive).Select(p => p.Position)).ToArray() :
            System.Array.Empty<Vector3Int>();
        public IEnumerable<Vector3Int> TerminalAreaCells
        {
            get
            {
                int radius = targetingSkill != null ? targetingSkill.AreaRadius : targetingItem != null ? targetingItem.AreaRadius : 0;
                if (radius <= 0 || !TerminalSelectedCell.HasValue || Game.Instance?.CurrentDungeon == null) yield break;
                var center = TerminalSelectedCell.Value;
                for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                {
                    var cell = center + new Vector3Int(x, y, 0);
                    if (Game.Instance.CurrentDungeon.IsFloorCell(cell)) yield return cell;
                }
            }
        }
        public IEnumerable<Vector3Int> TerminalMissilePath
        {
            get
            {
                if (missileRange <= 0 || casterCharacter == null || !MissileTargeting.IsDirection(Direction)) yield break;
                int length = TileWorldDungeon.ChevDistance(casterCharacter.TilemapPosition, MissileEndpoint);
                for (int i = 1; i <= length; i++) yield return casterCharacter.TilemapPosition + Direction * i;
            }
        }
        public string TerminalLabel
        {
            get
            {
                string name = targetingSkill != null ? targetingSkill.DisplayName : targetingItem != null ? targetingItem.DisplayName : "Select target";
                var cell = TerminalSelectedCell;
                string target = cell.HasValue ? "Selected ground tile" : "No target selected";
                if (selectedProp != null) target = selectedProp.GetInteractionText();
                else if (CameraTarget != null && (CameraTarget is Ally || DungeonSight.OverlapsVisible(Game.Instance.PartyVisibleTiles,
                    Character.ToBounds(CameraTarget.FootPrint, CameraTarget.TilemapPosition))))
                    target = CameraTarget is Enemy enemy && EnemyBehavior.IsDisguised(enemy) ? "Item" : GameMessages.Name(CameraTarget);
                int radius = targetingSkill != null ? targetingSkill.AreaRadius : targetingItem != null ? targetingItem.AreaRadius : 0;
                string direction = missileRange > 0 ? $"\nAiming {DirectionName(Direction)}, up to {EffectiveMissileRange} tiles away" : "";
                return GameplayText.Format(name + "\n" + target + direction + (radius > 0 ? $"\nAffects an area within {radius} tiles" : "") +
                    $"\nPress {InputPrompts.Confirm} to confirm or {InputPrompts.Back} to cancel.");
            }
        }
        private static string DirectionName(Vector3Int direction) =>
            (direction.y > 0 ? "north" : direction.y < 0 ? "south" : "") + (direction.x > 0 ? "east" : direction.x < 0 ? "west" : "");

        private void Awake()
        {
            // Drain UI pointer events while world targeting owns the pointer. This
            // surface sits above dungeon HUD controls and below Settings.
            var canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 150;
            pointerShield = GetComponent<Image>();
            if (pointerShield == null) pointerShield = gameObject.AddComponent<Image>();
            pointerShield.color = Color.clear;
            pointerShield.raycastTarget = true;
        }

        internal void Setup(Character character, Skill skill)
        {
            targetingSkill = skill;
            props = ScenerySkillTargets.Candidates(character,skill);
            tiles = skill.Targeting == SkillTargeting.Tile ? skill.GetTargetTiles(character) : new();
            selectedTile = character.TilemapPosition;
            Setup(character, skill.GetTargetCharacters(character),
                (target, direction) => skill.Targeting == SkillTargeting.Tile ? SkillAction.ForTile(character, skill, selectedTile) : skill.Targeting == SkillTargeting.Missile ?
                    SkillAction.ForMissile(character, skill, direction) : new SkillAction(character, skill, target),
                skill.Targeting == SkillTargeting.Missile ? skill.MissileRange : 0);
        }

        internal void Setup(Character character, List<Character> targets,
            System.Func<Character, Vector3Int, GameAction> factory, int range = 0, UsableItemDefinition item = null)
        {
            casterCharacter = character;
            targetingItem = item;
            targetingFloor = Game.Instance.CurrentDungeon;
            createAction = factory;
            missileRange = range;
            Targetables = targets;
            if (missileRange == 0 && Targetables.Count == 0 && props.Count == 0 && tiles.Count == 0) { MenuManager.Close(this); return; }
            enabled = true;
            Game.Instance.PlayerController.CurrentControlMode = PlayerControlMode.TargetSelecting;
            SelectTargetPrompt.SetActive(true);
            promptText = SelectTargetPrompt.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (promptText != null)
            {
                originalPrompt = promptText.text;
                promptGamepad = null;
                RefreshPrompt();
            }
            if (tiles.Count > 0) SelectTile(tiles.Contains(selectedTile) ? selectedTile : tiles[0]);
            else if (missileRange > 0) Aim(Dungeon.GetFacingOffset(character.CurrentFacing));
            else if(Targetables.Count > 0) SelectTarget(Targetables[0]); else SelectProp(props[0]);
            lastMove = Vector2.zero;
            nextMoveTime = 0;
            pointerReady = false;
            lastPointer = MenuUIInputModule.Active?.UI.Point.ReadValue<Vector2>() ?? Vector2.zero;
        }

        private void RefreshPrompt()
        {
            if (promptText == null || promptGamepad == ControlDeviceState.Gamepad) return;
            promptGamepad = ControlDeviceState.Gamepad;
            string title = missileRange > 0 ? "Aim in a direction" : "Select a highlighted target";
            string controls = InputPrompts.Pick("Click / Enter: cast   Right-click / Esc: cancel",
                "A / Cross: cast   B / Circle: cancel");
            promptText.text = GameplayText.Format(title + "\n" + controls);
            promptText.enableAutoSizing = true;promptText.fontSizeMin = 18;promptText.fontSizeMax = 24;
        }

        private void Aim(Vector3Int direction)
        {
            Direction = direction;
            var line = targetingSkill != null ? MissileTargeting.TraceLine(casterCharacter, direction, EffectiveMissileRange, targetingSkill.AreaRadius > 0 ? 1 : ArrowSupply.Penetration(casterCharacter, targetingSkill), targetingSkill.AreaRadius > 0) : null;
            var hit = line != null ? new MissileTargeting.Hit(line.Endpoint, line.Encounters.LastOrDefault().Character) : MissileTargeting.Trace(casterCharacter, direction, missileRange);
            MissileEndpoint = hit.Cell;
            CameraTarget = hit.Character;
            TargetIndicator.SetActive(true);
            TargetIndicator.transform.position = Game.Instance.CurrentDungeon.CellToWorld(hit.Cell);
            MenuManager.Instance.TargetArrow.transform.position = Game.Instance.CurrentDungeon.CellToWorld(casterCharacter.TilemapPosition + direction);
            Game.Instance.PlayerController.CameraController.SetFollowTarget(casterCharacter.transform);
        }

        private void SelectTile(Vector3Int cell)
        {
            selectedTile = cell;
            TargetIndicator.SetActive(true);
            TargetIndicator.transform.position = Game.Instance.CurrentDungeon.CellToWorld(cell);
            MenuManager.Instance.TargetArrow.transform.position = TargetIndicator.transform.position;
        }
        private void SelectProp(DungeonProp prop, bool followCamera = true)
        {
            selectedProp = prop; CameraTarget = null;
            TargetIndicator.SetActive(true); TargetIndicator.transform.position = Game.Instance.CurrentDungeon.CellToWorld(prop.Position);
            MenuManager.Instance.TargetArrow.transform.position = TargetIndicator.transform.position;
            if (followCamera) Game.Instance.PlayerController.CameraController.SetFollowTarget(prop.transform);
        }
        private void SelectTarget(Character target, bool followCamera = true)
        {
            selectedProp = null;
            CameraTarget = target;
            TargetIndicator.SetActive(true);
            TargetIndicator.transform.position = target.transform.position;
            MenuManager.Instance.TargetArrow.transform.position = target.transform.position;
            if (followCamera) Game.Instance.PlayerController.CameraController.SetFollowTarget(target.transform);
        }

        private bool ProcessPointer()
        {
            var module = MenuUIInputModule.Active;
            if (module == null) return false;
            var ui = module.UI;
            // A held button used to open targeting must be released before it can cast.
            if (!pointerReady)
            {
                pointerReady = !ui.Click.IsPressed() && !ui.RightClick.IsPressed();
                return false;
            }
            if (ui.RightClick.WasPressedThisFrame())
            {
                CloseDialog();Common.Instance.MenuInputHandler.ClearInputThisFrame();return true;
            }
            var position = ui.Point.ReadValue<Vector2>();
            bool clicked = ui.Click.WasPressedThisFrame();
            bool moved = (position-lastPointer).sqrMagnitude > 1;
            lastPointer = position;
            if (!clicked && !moved) return false;
            if (TerminalMode.Effective || !TryPointTarget(position)) return false;
            if (clicked)
            {
                ConfirmTarget();Common.Instance.MenuInputHandler.ClearInputThisFrame();return true;
            }
            return false;
        }

        internal bool TryPointTarget(Vector2 position)
        {
            var camera = Camera.main;
            if (camera == null || casterCharacter == null || targetingFloor == null || !camera.pixelRect.Contains(position)) return false;
            if (EventSystem.current != null)
            {
                pointerHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position=position },pointerHits);
                // Ignore only our event-draining surface; HUD graphics beneath it
                // must still prevent a world cast through a control.
                if (pointerHits.Any(hit=>hit.module is GraphicRaycaster && hit.gameObject != pointerShield.gameObject)) return false;
            }
            var ray = camera.ScreenPointToRay(position);
            var ground = new Plane(Vector3.forward,new Vector3(0,0,DungeonPresentation.GroundPlaneZ));
            if (!ground.Raycast(ray,out float distance)) return false;
            var cell = targetingFloor.WorldToCell(ray.GetPoint(distance));
            if (missileRange > 0)
            {
                var delta = cell-casterCharacter.TilemapPosition;
                if (delta == Vector3Int.zero) return false;
                float angle = Mathf.Round(Mathf.Atan2(delta.y,delta.x)/(Mathf.PI*.25f))*Mathf.PI*.25f;
                Aim(new Vector3Int(Mathf.RoundToInt(Mathf.Cos(angle)),Mathf.RoundToInt(Mathf.Sin(angle)),0));return true;
            }
            if (tiles.Count > 0)
            {
                if (!tiles.Contains(cell)) return false;
                SelectTile(cell);return true;
            }
            // Elevated character meshes can project above their logical floor cell.
            // Prefer the nearest visible body over another target's projected cell.
            Character target = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in Targetables ?? new List<Character>())
            {
                if (candidate == null || !GameMessages.Visible(candidate) || candidate.VisualParent == null) continue;
                foreach (var renderer in candidate.VisualParent.GetComponentsInChildren<Renderer>())
                    if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer) &&
                        renderer.enabled && !renderer.forceRenderingOff && renderer.bounds.IntersectRay(ray,out float hit) && hit < nearest)
                    { target=candidate;nearest=hit; }
            }
            if (target != null) { SelectTarget(target,false);return true; }
            target = Targetables?.FirstOrDefault(character=>character != null &&
                GameMessages.Visible(character) && Character.ToBounds(character.FootPrint,character.TilemapPosition).Contains(cell));
            if (target != null) { SelectTarget(target,false);return true; }
            var prop = props.FirstOrDefault(candidate=>candidate != null && candidate.Alive && candidate.Position==cell);
            if (prop == null) return false;
            SelectProp(prop,false);return true;
        }

        private void Update()
        {
            if (Game.Instance.PlayerController.CurrentControlMode != PlayerControlMode.TargetSelecting ||
                Common.Instance.GlobalSettings.IsOpen || MenuUIInputModule.Active?.InputConsumed == true ||
                Owner?.Current != this || AutoplayRunner.BlocksPlayerInput) return;
            RefreshPrompt();
            if (ProcessPointer()) return;
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
            if (tiles.Count > 0)
            {
                var nextCell = selectedTile + new Vector3Int((int)direction.x, (int)direction.y);
                if (tiles.Contains(nextCell)) SelectTile(nextCell);
                nextMoveTime = Time.unscaledTime + (lastMove == Vector2.zero ? .3f : .1f); lastMove = move; return;
            }
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
            var candidates = Targetables.Where(c => c != null && c != CameraTarget).ToList();
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
            if (Game.Instance == null || Game.Instance.CurrentDungeon != targetingFloor || caster.Vitals == null || caster.Vitals.HP <= 0 ||
                !Game.Instance.AllCharacters.Contains(caster))
            { MenuManager.Close(this); return; }
            var action = selectedProp != null && targetingSkill != null ? SkillAction.ForScenery(caster,targetingSkill,selectedProp) : createAction(CameraTarget, Direction);
            if (action == null) { MenuManager.Close(this); return; }
            if (!action.ValidateCommand(caster, out var reason)) { if (reason != null) GameMessages.Post(reason, true); MenuManager.Close(this); return; }
            MenuManager.Instance.CloseAllMenus();
            caster.SetAction(action);
        }

        internal void CancelTargetSelection() => Close();
        internal void Close()
        {
            enabled = false;
            TargetIndicator.SetActive(false);
            SelectTargetPrompt.SetActive(false);
            if (promptText != null) promptText.text = GameplayText.Format(originalPrompt);
            promptText = null;
            props.Clear(); tiles.Clear(); selectedProp=null; targetingSkill=null; targetingItem=null; targetingFloor=null;
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
