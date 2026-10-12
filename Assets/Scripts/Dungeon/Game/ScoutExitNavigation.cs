using System.Linq;
using JuicyChickenGames.Menu;
using EternalEnigma.Core.World;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScoutExitNavigation : MonoBehaviour
{
    private Ally scout;
    private TileWorldDungeon floor;
    private bool inputReleased;
    public bool Active { get; private set; }

    internal static bool Threatened(Ally ally) => Game.Instance.Enemies.Any(e => e != null && e.Team != ally.Team &&
        e.Vitals.HP > 0 && !EnemyBehavior.IsDisguised(e) && Game.Instance.CurrentDungeon.CanSee(ally, e));

    internal static System.Collections.Generic.IReadOnlyList<GridPoint> Route(Ally ally)
    {
        var dungeon = Game.Instance.CurrentDungeon;
        bool Open(GridPoint p) => SkillMovement.CanOccupy(ally, p.ToCell()) && !dungeon.IsHazard(p.ToCell()) &&
            !dungeon.Interactables.OfType<Trap>().Any(t => t.Position == p.ToCell() && t.VisualObject != null && t.VisualObject.activeSelf);
        return GridSearch.Path(ally.TilemapPosition.ToGridPoint(), dungeon.Floor.Stairs,
            p => dungeon.Floor.Neighbors(p).Where(q => Open(q) && dungeon.CanWalkTo(p.ToCell(), q.ToCell())));
    }

    internal static bool CanBegin(Ally ally) => ally != null && ally.Team == Team.Player && ally.Vitals.HP > 0 &&
        Game.Instance?.CurrentDungeon?.Floor != null && !ally.IsMovementBlocked && !Threatened(ally) && Route(ally).Count > 1;

    internal void Begin(Ally ally)
    {
        scout = ally; floor = Game.Instance.CurrentDungeon; Active = true; inputReleased = false;
        GameMessages.ForCharacter(scout, $"{GameMessages.Name(scout)} is finding the exit. Move, attack or open a menu to stop.");
    }

    internal void Stop(string reason)
    {
        if (!Active) return;
        Active = false;
        GameMessages.ForCharacter(scout, $"{GameMessages.Name(scout)} stopped guiding: {reason}.");
    }

    private void Update()
    {
        if (!Active || Game.Instance == null) return;
        var input = PlayerInputHandler.Instance;
        bool pressed = Game.Instance.PlayerController.ControlledAlly == scout && input != null &&
            (input.isMoving || input.attackPressed || input.waitPressed || input.menuPressed || input.skillsPressed || input.optionsPressed || input.holdPosition);
        if (!pressed) inputReleased = true;
        if (inputReleased && (pressed || MenuManager.Instance?.Opened == true || Common.Instance?.GlobalSettings?.IsOpen == true)) Stop("manual control");
    }

    internal GameAction NextAction()
    {
        if (!Active) return null;
        if (scout == null || scout.Vitals.HP <= 0 || Game.Instance.CurrentDungeon != floor) { Stop("journey interrupted"); return null; }
        if (scout.TilemapPosition == floor.Floor.Stairs.ToCell()) { Stop("exit reached"); return null; }
        if (scout.IsMovementBlocked || Threatened(scout)) { Stop("danger ahead"); return null; }
        var route = Route(scout);
        if (route.Count < 2) { Stop("path blocked"); return null; }
        var move = new MovementAction(scout, scout.TilemapPosition, route[1].ToCell());
        if (!move.IsValid(scout)) { Stop("path blocked"); return null; }
        scout.SetFacingByTargetPosition(route[1].ToCell());
        return move;
    }
}
