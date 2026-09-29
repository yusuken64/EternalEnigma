using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class CombatVisualSequence
{
    internal CombatEffectProfile Profile;
    internal Character Actor;
    internal Vector3 Origin, Center;
    internal int Radius;
    internal bool AreaPlayed, SingleFlight, FlightPlayed;
    internal readonly HashSet<Character> DamageRecipients = new();
}

internal sealed class CombatVisualReplay
{
    internal CombatVisualSequence Sequence;
    internal Character BoundTarget;
    internal bool Cast;
    internal readonly List<(Vector3 point, bool hit, Bounds? bounds)> Impacts = new();
    readonly Dictionary<Character, Dictionary<string, StatusVisualProfile>> before = new(), after = new();
    readonly Dictionary<Character, Vector3Int> positions = new();
    readonly HashSet<Character> changedStatuses = new();
    bool afterMovement;
    Character resolvingActor;
    internal Vector3Int OriginCell;

    internal void Begin(Character actor)
    {
        OriginCell = actor.TilemapPosition;
        resolvingActor = actor;
        var game = Game.Instance;
        if (game == null) return;
        foreach (var c in game.AllCharacters.Concat(game.DownedAllies.Cast<Character>()).Where(c => c != null).Distinct())
        { before[c] = Snapshot(c); positions[c] = c.TilemapPosition; }
    }

    internal static Dictionary<string, StatusVisualProfile> Snapshot(Character c)
    {
        var result = new Dictionary<string, StatusVisualProfile>();
        if (c.Vitals == null || c.Vitals.HP <= 0) return result;
        foreach (var status in c.StatusEffects)
            if (status != null && !status.IsExpired())
            {
                var profile = CombatVisualCatalog.Instance?.ForStatus(status);
                if (profile != null) result[status.StackKey] = profile;
            }
        return result;
    }

    internal void Configure(CombatEffectProfile profile, Character actor, Vector3Int center, int radius = 0)
    {
        if (profile == null || Game.Instance?.CurrentDungeon == null) return;
        Sequence = new CombatVisualSequence { Profile = profile, Actor = actor,
            Origin = CombatEffectPlayer.Body(actor, actor != null && actor != resolvingActor ? actor.TilemapPosition : OriginCell), Center = CombatEffectPlayer.Body(null, center), Radius = radius };
        Cast = true;
    }

    internal void End(GameAction action, Character actor, List<GameAction> children)
    {
        var game = Game.Instance;
        if (game?.CurrentDungeon == null) return;
        foreach (var c in game.AllCharacters.Concat(game.DownedAllies.Cast<Character>()).Where(c => c != null).Distinct())
        {
            var snapshot = Snapshot(c);
            after[c] = snapshot;
            if (!before.TryGetValue(c, out var old) || old.Count != snapshot.Count || snapshot.Any(p => !old.TryGetValue(p.Key, out var v) || v != p.Value)) changedStatuses.Add(c);
        }
        foreach (var c in before.Keys.Where(c => c != null && !game.AllCharacters.Contains(c) && !game.DownedAllies.Contains(c as Ally))) after[c] = new();

        Character recipient = action is TakeDamageAction damage ? damage.Target : action is TakeHealAction heal ? heal.target : BoundTarget;
        if (Sequence == null && action is TakeDamageAction standalone)
        {
            var profile = CombatVisualCatalog.Instance?.ForElement(standalone.Element);
            Configure(profile, standalone.Attacker != null ? standalone.Attacker : actor, standalone.Target != null ? standalone.Target.TilemapPosition : OriginCell);
            Cast = false; // Status ticks and reactive damage do not cast again.
        }
        if (Sequence == null && action is TakeHealAction) { Configure(CombatVisualCatalog.Instance?.Heal, actor, recipient != null ? recipient.TilemapPosition : OriginCell); Cast = false; }
        if (Sequence != null)
        {
            if (action is TakeDamageAction hit)
            {
                if (hit.Target != null) Sequence.DamageRecipients.Add(hit.Target);
                AddImpact(hit.Target, hit.Target != null ? hit.Target.TilemapPosition : OriginCell, !hit.Missed);
            }
            else if (action is TakeHealAction)
                AddImpact(recipient, recipient != null ? recipient.TilemapPosition : OriginCell, true);
            else if (action is PropDamageAction prop)
                AddImpact(null, prop.Cell, true);
            else if (action is ApplyStatusChanceAction && children.Count == 0 && recipient != null)
                AddImpact(recipient, recipient.TilemapPosition, false);
            else if (!Cast && children.Count == 0 && action is not RemoveStatusEffectAction && action is not DeathAction && action is not ApplyStatusChanceAction && action is not ScaledDamageAction && action is not RandomHitsAction)
            {
                var targets = changedStatuses.Where(c => after[c].Count > 0).Concat(action is ApplyStatusEffectAction ? Enumerable.Empty<Character>() : action.VisualTargets)
                    .Where(c => c != null).Distinct().ToList();
                foreach (var moved in positions.Where(p => p.Key != null && p.Key.TilemapPosition != p.Value))
                { afterMovement = true; if (!targets.Contains(moved.Key)) targets.Add(moved.Key); }
                if (action is ApplyStatusEffectAction applied && applied.VisualAppliedTarget != null) targets.Add(applied.VisualAppliedTarget);
                if (targets.Count == 0 && recipient != null && action is not ApplyStatusEffectAction) targets.Add(recipient);
                foreach (var target in targets.Distinct()) AddImpact(target, target.TilemapPosition, true);
            }
            if (Cast && children.Count == 0 && Impacts.Count == 0) Impacts.Add((Sequence.Center, false, null));
            foreach (var child in children.Where(c => c != null && c is not TrapResolutionAction && c is not TakeDamageAction { Environmental: true }))
            {
                child.Visuals.Sequence ??= Sequence;
                child.Visuals.BoundTarget ??= recipient;
            }
        }
    }

    internal void AddImpact(Character target, Vector3Int cell, bool hit) => Impacts.Add((CombatEffectPlayer.Body(target, cell), hit,
        Sequence?.Profile.Impact.FitToTarget == true ? CombatEffectPlayer.TargetBounds(target, cell) : null));

    internal IEnumerator Play(GameAction action, Character actor, bool skip)
    {
        var player = CombatEffectPlayer.Get();
        if (!skip && player != null && Sequence != null)
        {
            if (Cast) yield return player.Cast(Sequence);
            bool secondaryStatus = (action is ApplyStatusEffectAction || action is ApplyStatusChanceAction) && BoundTarget != null && Sequence.DamageRecipients.Contains(BoundTarget);
            if (!afterMovement && !secondaryStatus) foreach (var impact in Impacts) yield return player.Deliver(Sequence, impact.point, impact.hit, impact.bounds);
        }
        yield return action.ExecuteRoutine(actor, skip);
        if (!skip && player != null && Sequence != null && afterMovement)
            foreach (var impact in Impacts) yield return player.Deliver(Sequence, impact.point, impact.hit, impact.bounds);
        // Even skipped playback advances the aura ledger; never consult future simulation state here.
        if (player != null) foreach (var pair in after) player.SetStatuses(pair.Key, pair.Value);
    }
}
