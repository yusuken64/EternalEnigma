using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>The first home's book, capability chest and door use ordinary facing interactions.</summary>
public sealed class OpeningHomeLesson : MonoBehaviour
{
    private Town town;
    private OpeningHomeProps visuals;
    private bool lastKey, lastUnlocked, lastRead;
    private readonly HashSet<Vector3Int> floor = new();
    public Vector3Int BookCell { get; private set; }
    public Vector3Int ChestCell { get; private set; }
    public Vector3Int DoorCell { get; private set; }
    private CampaignContext Context => Common.InstanceOrNull?.CampaignContext;
    public bool IsLocked => Context?.HasHomeLesson == true && !Context.State.HomeDoorUnlocked;

    public static OpeningHomeLesson Create(Town town)
    {
        var lesson = town.GetComponent<OpeningHomeLesson>();
        var interior = town.Plan.Interiors.SingleOrDefault(i => i.Spec.Kind == TownInteriorKind.Home);
        if (town.Configuration.Id != "town-0" || interior == null)
        {
            if (lesson != null)
            {
                lesson.enabled = false;
                if (lesson.visuals != null) Destroy(lesson.visuals.gameObject);
                Destroy(lesson);
            }
            return null;
        }
        var room = town.Plan.ShopRoomAt(interior.Door);
        lesson ??= town.gameObject.AddComponent<OpeningHomeLesson>();
        lesson.enabled = true;
        lesson.town = town;
        lesson.BookCell = interior.Props.Single(p => p.Asset == "Lectern").Cell.ToCell();
        lesson.ChestCell = interior.Props.Single(p => p.Asset == "Chest").Cell.ToCell();
        lesson.DoorCell = room.Door.ToCell() + Vector3Int.up;
        lesson.floor.Clear();
        lesson.floor.UnionWith(room.Floor.Select(c => c.ToCell()));
        if (lesson.visuals != null) { lesson.visuals.gameObject.SetActive(false); Destroy(lesson.visuals.gameObject); }
        lesson.visuals = OpeningHomeProps.Create(town, lesson.BookCell, lesson.ChestCell, lesson.DoorCell);
        lesson.RefreshVisuals(true);
        return lesson;
    }

    private void Update() => RefreshVisuals();
    private void RefreshVisuals(bool force = false)
    {
        if (visuals == null) return;
        var context = Context;
        bool bypass = context?.HasHomeLesson != true;
        bool key = bypass || context.HasHomeKey, unlocked = bypass || context.State.HomeDoorUnlocked,
            read = bypass || context.State.ReadHomeBook;
        if (!force && key == lastKey && unlocked == lastUnlocked && read == lastRead) return;
        visuals.Refresh(key, unlocked, read);
        lastKey = key; lastUnlocked = unlocked; lastRead = read;
    }

    // Restrict the whole home boundary while locked. This also rejects diagonal
    // corner steps, party followers and direct TownMovement actions around the door.
    public bool AllowsEntry(Vector3Int cell) => cell != BookCell && cell != ChestCell &&
        (!IsLocked || cell != DoorCell && floor.Contains(cell));

    public string InteractionName(Vector3Int cell) => cell == BookCell ? "book" :
        cell == ChestCell ? "chest" : cell == DoorCell ? "front door" : "object";

    public bool TryInteract(Vector3Int targetCell)
    {
        var context = Context;
        var player = town?.TownPlayer;
        var hero = player?.ControllingTownAlly;
        if (!enabled || context?.HasHomeLesson != true || !town.IsReady || hero == null || player.IsBusy ||
            context.State.Scene != "Town" || context.State.LocationId != "town-0" ||
            player.CutsceneLocked || Common.Instance.Travel.IsTransitioning || Common.Instance.GlobalSettings.IsOpen ||
            FindFirstObjectByType<TownMenuManager>()?.DialogStack.Count > 0 ||
            hero.TilemapPosition + Dungeon.GetFacingOffset(hero.CurrentFacing) != targetCell) return false;
        if (targetCell != BookCell && targetCell != ChestCell && targetCell != DoorCell) return false;
        player.StopHeldWalk();
        context.State.InteractedInOpening = true;
        if (targetCell == BookCell)
        {
            bool firstRead = context.ReadOpeningBook();
            town.SaveProgress();
            AudioManager.Instance?.PlayCue(SoundCue.Confirm);
            TownMenu.ShowMessage(context.State.HomeDoorUnlocked
                ? "Your Home Key unlocked the front door."
                : context.HasHomeKey
                ? "<b>Before you set out</b>\n\nYour Home Key opens the front door. Face the door and press " + InputPrompts.Interact + " to use it."
                : firstRead
                    ? "<b>Before you set out</b>\n\nOpen the chest to find your Home Key.\nIt is your first capability. Use it at the front door to unlock the way."
                    : "Open the chest to find your Home Key, then use it at the front door.");
        }
        else if (targetCell == ChestCell)
        {
            bool acquired = context.ClaimHomeKey();
            town.SaveProgress();
            AudioManager.Instance?.PlayCue(acquired ? SoundCue.ContainerOpen : SoundCue.Confirm);
            TownMenu.ShowMessage(acquired
                ? "<b>Home Key acquired!</b>\n\nYour first capability opens the front door.\nFace the door and press " + InputPrompts.Interact + " to use it."
                : "The chest is empty. You already have its Home Key.");
        }
        else if (!context.State.HomeDoorUnlocked)
        {
            if (context.UnlockHomeDoor())
            {
                town.SaveProgress();
                AudioManager.Instance?.PlayCue(SoundCue.GateUnlock);
                GameMessages.Post("You used the Home Key. The front door is open!");
            }
            else
            {
                AudioManager.Instance?.PlayCue(SoundCue.Denied);
                TownMenu.ShowMessage("The front door is locked. Your Home Key is in the chest.\nThe book beside your bed explains how to use it.");
            }
        }
        else GameMessages.Post("The front door is open.");
        RefreshVisuals();
        return true;
    }

    public string Objective
    {
        get
        {
            var context = Context;
            var hero = town?.TownPlayer?.ControllingTownAlly;
            if (!IsLocked || hero == null) return "";
            var ahead = hero.TilemapPosition + Dungeon.GetFacingOffset(hero.CurrentFacing);
            if (ahead == BookCell) return "Book: press " + InputPrompts.Interact + " to read.";
            if (ahead == ChestCell) return context.HasHomeKey ? "Chest empty. Use your Home Key at the front door." :
                "Chest: press " + InputPrompts.Interact + " to open.";
            if (ahead == DoorCell) return context.HasHomeKey ? "Front door: press " + InputPrompts.Interact + " to unlock." :
                "Front door locked. Press " + InputPrompts.Interact + " to inspect.";
            if (context.HasHomeKey) return "Use your Home Key at the front door. Face it and press " + InputPrompts.Interact + ".";
            if (!context.State.MovedInOpening) return "Use " + InputPrompts.Move + " to walk to the book or chest.";
            return context.State.ReadHomeBook ? "Open the chest to find your Home Key." :
                "Read the book beside your bed, or explore the chest.";
        }
    }

    /// <summary>Autoplay follows the same optional book discovery and real interactions as a player.</summary>
    public bool TryGetNextInteraction(out Vector3Int standCell, out Vector3Int targetCell)
    {
        standCell = default;
        var context = Context;
        targetCell = context?.HasHomeKey == true ? DoorCell : context?.State.ReadHomeBook == true ? ChestCell : BookCell;
        return IsLocked && TryGetInteractionStand(targetCell, out standCell);
    }

    public bool TryGetInteractionStand(Vector3Int targetCell, out Vector3Int standCell)
    {
        standCell = default;
        var hero = town?.TownPlayer?.ControllingTownAlly;
        if (hero == null) return false;
        var candidates = new[] { Vector3Int.down, Vector3Int.right, Vector3Int.left, Vector3Int.up }
            .Select(direction => targetCell + direction)
            .Where(cell => cell == hero.TilemapPosition || town.CanEnter(cell))
            .OrderBy(cell => (cell - hero.TilemapPosition).sqrMagnitude).ToArray();
        if (candidates.Length == 0) return false;
        standCell = candidates[0];
        return true;
    }
}
