#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public sealed class ChaosActorState
{
    public string Id, Name, Position, Status, Equipment, Skills;
    public int HP, Mana, Hunger, Level, X, Y, MaxHP, MaxMana, Strength, Defense, Arrows, CastRemaining;
}

[Serializable]
public sealed class ChaosSnapshot
{
    private static readonly Dictionary<Character, string> actorIds = new();
    public static void ResetActors() => actorIds.Clear();
    private static string ActorId(Character actor)
    {
        if (actor is Ally hero && !string.IsNullOrEmpty(hero.TownAllyId)) return hero.TownAllyId;
        if (!actorIds.TryGetValue(actor, out var id)) actorIds.Add(actor, id = actor.CharacterName + "#" + actorIds.Count);
        return id;
    }
    public string Scene, Selected, Dialog, VisibleUI, Position, ControlledHero, Inventory, Campaign, StateHash, ProgressHash;
    public bool Loading, Ready, Settings, Targeting, CanControl;
    public int Gold, Floor;
    public List<ChaosActorState> Actors = new();
    public static ChaosSnapshot Read()
    {
        var common = Common.InstanceOrNull;
        var game = Game.Instance;
        var town = UnityEngine.Object.FindFirstObjectByType<Town>();
        var world = UnityEngine.Object.FindFirstObjectByType<OverworldScene>();
        var selected = EventSystem.current?.currentSelectedGameObject;
        var result = new ChaosSnapshot { Scene = SceneManager.GetActiveScene().name,
            Selected = ChaosUI.Path(selected?.transform) + "|" + ChaosUI.Label(selected?.GetComponent<Selectable>()), Settings = common?.GlobalSettings?.IsOpen == true,
            Dialog = game != null ? MenuManager.Instance?.CurrentDialog?.GetType().Name :
                UnityEngine.Object.FindFirstObjectByType<TownMenuManager>()?.CurrentDialog?.GetType().Name,
            Loading = common == null || common.Travel.IsTransitioning || common.ScreenTransition.BlockScreen.activeSelf,
            Ready = game != null ? game.IsReady : town != null ? town.TownPlayer?.ControllingTownAlly != null :
                // MainMenu suspends its controller while Options owns input.
                world != null || UnityEngine.Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include)?.IsReady == true };
        result.Targeting = game?.PlayerController?.CurrentControlMode == PlayerControlMode.TargetSelecting;
        result.CanControl = result.Ready && !result.Loading && (game == null ||
            !game.NewFloorMessage.gameObject.activeSelf && (!game.TurnManager.IsProcessingTurn || game.TurnManager.AwaitingCommand));
        if (common?.GlobalSettings?.IsOpen == true) result.Dialog = "GlobalSettings";
        result.VisibleUI = string.Join("|", ChaosUI.Visible().Select(s => ChaosUI.Path(s.transform) + ":" + ChaosUI.Label(s)));
        if (game?.IsReady == true)
        {
            var controlled = game.PlayerController.ControlledAlly;
            result.ControlledHero = controlled?.TownAllyId;
            result.Position = controlled?.TilemapPosition.ToString();
            result.Gold = game.PlayerController.Gold; result.Floor = game.PlayerController.Floor;
            result.Inventory = JsonUtility.ToJson(new InventoryEnvelope { Items = ItemSaveData.Capture(game.PlayerController.Inventory.InventoryItems) });
            foreach (var actor in game.AllCharacters.Concat(game.DownedAllies).Where(a => a != null).Distinct())
                result.Actors.Add(new ChaosActorState { Id = ActorId(actor),
                    Name = actor.CharacterName, Position = actor.TilemapPosition.ToString(), HP = actor.Vitals.HP, Mana = actor.Vitals.Mana,
                    Hunger = actor.Vitals.Hunger, Level = actor.Vitals.Level, Status = string.Join("|", actor.StatusEffects.Where(s => s != null && !s.IsExpired()).Select(s => s.GetType().Name)),
                    X = actor.TilemapPosition.x, Y = actor.TilemapPosition.y, MaxHP = actor.FinalStats.HPMax, MaxMana = actor.FinalStats.ManaMax,
                    Strength = actor.FinalStats.Strength, Defense = actor.FinalStats.Defense, Arrows = ArrowSupply.Count(actor), CastRemaining = actor.PendingCast?.Remaining ?? 0,
                    Equipment = string.Join("|", actor.Equipment.GetEquippedItems().Select(i => i.ItemName + ":" + i.StackStock)),
                    Skills = string.Join("|", actor.Skills.Where(s => s != null).Select(s => s.SkillName + ":" + s.Rank)) });
        }
        else if (town?.TownPlayer?.ControllingTownAlly != null)
        {
            result.ControlledHero = town.TownPlayer.ControllingTownAlly.Id;
            result.Position = town.TownPlayer.ControllingTownAlly.TilemapPosition.ToString();
            result.Gold = town.TownPlayer.Gold;
            result.Inventory = JsonUtility.ToJson(new InventoryEnvelope { Items = ItemSaveData.Capture(town.TownPlayer.Inventory) });
        }
        if (common?.CampaignContext != null)
        {
            var context = common.CampaignContext;
            // Capture() updates the live serialization mirror. Copy it instead to keep inspection read-only.
            var state = ChaosJson.Clone(context.State);
            state.Keys = context.Keys.OrderBy(x => x).ToArray(); state.Completed = context.Completed.OrderBy(x => x).ToArray();
            state.Roster = context.Roster.OrderBy(x => x).ToArray(); state.Active = context.Active.ToArray();
            state.Opened = context.Opened.OrderBy(x => x).ToArray(); state.Resolved = context.Resolved.OrderBy(x => x).ToArray();
            state.Claimed = context.Claimed.OrderBy(x => x).ToArray(); state.CollectedPickups = context.CollectedPickups.OrderBy(x => x).ToArray();
            result.Campaign = JsonUtility.ToJson(state);
            if (world != null) result.Position = state.X + "," + state.Y;
        }
        result.Actors = result.Actors.OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        // No frame counters, timestamps, pointer position, or instance IDs in replay hashes.
        result.StateHash = ChaosJson.Hash(JsonUtility.ToJson(result));
        var meaningful = ChaosJson.Clone(result); meaningful.Selected = null; meaningful.StateHash = null;
        result.ProgressHash = ChaosJson.Hash(JsonUtility.ToJson(meaningful));
        return result;
    }
    [Serializable] private sealed class InventoryEnvelope { public List<ItemSaveData> Items; }
}

public static class ChaosUI
{
    public static string Path(Transform transform) => transform == null ? "" :
        (transform.parent == null ? "" : Path(transform.parent) + "/") + transform.name;
    public static string Label(Selectable selectable) => selectable == null ? "" :
        selectable.GetComponentInChildren<TMP_Text>()?.text ?? selectable.GetComponentInChildren<Text>()?.text ?? selectable.name;
    public static Selectable[] Visible() => Selectable.allSelectablesArray.Where(s => s != null && s.IsActive() && s.IsInteractable() &&
        (MenuUIInputModule.Active == null || MenuUIInputModule.Active.Allows(s.gameObject))).OrderBy(s => Path(s.transform), StringComparer.Ordinal).ToArray();
    public static Vector2 Center(Selectable selectable)
    {
        var rect = (RectTransform)selectable.transform;
        var canvas = selectable.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, rect.TransformPoint(rect.rect.center));
    }
    public static bool Forbidden(string label)
    {
        // TerminalMainMenuView prefixes the selected action with "> ".
        string action = label.Trim();
        if (action.StartsWith("> ", StringComparison.Ordinal)) action = action.Substring(2).TrimStart();
        return action.Equals("Exit", StringComparison.OrdinalIgnoreCase) ||
            new[] { "Quit", "Exit game", "autoplay", "test dungeon", "endgame", "developer" }.Any(s => action.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
    }
    public static bool ForbiddenPoint(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        return hits.Any(hit => Forbidden(Label(hit.gameObject.GetComponentInParent<Selectable>())));
    }
    // A graph search observes authored navigation; it never moves focus itself.
    public static string NextDirection(Selectable current, Selectable target)
    {
        if (current == null || target == null) return "DownArrow";
        var pending = new Queue<(Selectable node, string first)>();
        var seen = new HashSet<Selectable> { current };
        pending.Enqueue((current, null));
        while (pending.Count > 0)
        {
            var (node, first) = pending.Dequeue();
            foreach (var edge in new[] { (node.FindSelectableOnUp(), "UpArrow"), (node.FindSelectableOnDown(), "DownArrow"),
                (node.FindSelectableOnLeft(), "LeftArrow"), (node.FindSelectableOnRight(), "RightArrow") })
            {
                if (edge.Item1 == null || !seen.Add(edge.Item1)) continue;
                if (edge.Item1 == target) return first ?? edge.Item2;
                pending.Enqueue((edge.Item1, first ?? edge.Item2));
            }
        }
        return "DownArrow";
    }
}

// Focus alone is excluded from ProgressHash. Revisiting a room is still valid player progress.
public sealed class ChaosProgress
{
    private string previous;
    public double LastProgress { get; private set; }
    public void Reset(double now) { previous = null; LastProgress = now; }
    public bool Observe(string signature, double now) { if (signature == previous) return false; previous = signature; LastProgress = now; return true; }
    public bool Expired(double now, double limit) => now - LastProgress >= limit;
}
#endif
