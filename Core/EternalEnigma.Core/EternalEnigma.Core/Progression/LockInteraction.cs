using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Progression;

// New action types may be added without teaching the UI about solution evaluation.
public abstract class LockAction { }
public sealed class CapabilityLockAction : LockAction
{
    public Capability Capability { get; }
    public CapabilityLockAction(Capability capability) { Capability = capability; }
}
public sealed class KeyLockAction : LockAction
{
    public string KeyId { get; }
    public KeyLockAction(string keyId) { KeyId = keyId; }
}
public enum LockOutcome { NoEffect, Progress, Opened }

/// <summary>Pure, temporary solution progress. Never mutates campaign state or consumes resources.</summary>
public sealed class LockSolutionEvaluator
{
    private readonly CampaignRoute route;
    private readonly CapabilitySet[] progress;
    private bool complete;
    public LockSolutionEvaluator(CampaignRoute route)
    { this.route = route; progress = new CapabilitySet[route.Requirement.Alternatives.Count]; }

    public LockOutcome Apply(LockAction action)
    {
        if (complete || route.ShortcutKind == ShortcutKind.FarSide) return LockOutcome.NoEffect;
        if (route.ShortcutKind == ShortcutKind.Keyed)
        {
            if (action is not KeyLockAction key || key.KeyId != route.KeyId) return LockOutcome.NoEffect;
            complete = true;
            return LockOutcome.Opened;
        }
        if (action is not CapabilityLockAction use) return LockOutcome.NoEffect;
        bool changed = false;
        for (int i = 0; i < progress.Length; i++)
        {
            var solution = route.Requirement.Alternatives[i];
            if (!solution.Contains(use.Capability) || progress[i].Contains(use.Capability)) continue;
            progress[i] = progress[i].Union(CapabilitySet.Of(use.Capability));
            changed = true;
            complete |= progress[i].ContainsAll(solution);
        }
        return complete ? LockOutcome.Opened : changed ? LockOutcome.Progress : LockOutcome.NoEffect;
    }
}

/// <summary>Owns one chooser's lifetime and revalidates every action before evaluating it.</summary>
public sealed class LockInteractionSession : IDisposable
{
    private readonly LockSolutionEvaluator evaluator;
    private readonly Func<bool> valid;
    private readonly Func<LockAction, bool> available;
    private readonly Func<bool> commit;
    private bool submitting;
    public bool Closed { get; private set; }
    public LockInteractionSession(CampaignRoute route, Func<bool> valid,
        Func<LockAction, bool> available, Func<bool> commit)
    { evaluator = new(route); this.valid = valid; this.available = available; this.commit = commit; }

    public LockOutcome Attempt(LockAction action)
    {
        if (Closed || submitting) return LockOutcome.NoEffect;
        submitting = true;
        try
        {
            if (!valid()) { Dispose(); return LockOutcome.NoEffect; }
            if (!available(action)) return LockOutcome.NoEffect;
            var outcome = evaluator.Apply(action);
            if (outcome != LockOutcome.Opened) return outcome;
            Closed = true;
            return commit() ? LockOutcome.Opened : LockOutcome.NoEffect;
        }
        finally { submitting = false; }
    }
    public void Dispose() => Closed = true;
}
