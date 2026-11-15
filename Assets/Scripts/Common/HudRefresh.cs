using System;
using UnityEngine;

[Flags]
internal enum HudChange
{
    Party = 1, Resources = 2, Dungeon = 4, Visibility = 8, Travel = 16, Events = 32,
    All = Party | Resources | Dungeon | Visibility | Travel | Events
}

/// <summary>Coalesces model notifications into one presentation update before drawing.</summary>
internal static class HudRefresh
{
    internal static event Action<HudChange> Changed;
    private static HudChange pending;
    private static bool scheduled, dispatching;

    internal static void Request(HudChange changes)
    {
        if (!Application.isPlaying) return;
        pending |= changes;
        if (scheduled) return;
        scheduled = true;
        Canvas.willRenderCanvases += Flush;
    }

    internal static void PartyChanged() => Request(HudChange.Party | HudChange.Dungeon);
    private static void PromptsChanged() => Request(HudChange.All);

    private static void Flush()
    {
        // Fast walking retains all model changes and presents the final state together.
        if (dispatching || DungeonFastWalk.DeferPresentation) return;
        Canvas.willRenderCanvases -= Flush;
        scheduled = false;
        var changes = pending;
        pending = 0;
        dispatching = true;
        try { Changed?.Invoke(changes); }
        finally { dispatching = false; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Canvas.willRenderCanvases -= Flush;
        pending = 0;
        scheduled = dispatching = false;
        Changed = null;
        ControlDeviceState.Changed -= PromptsChanged;
        ControlDeviceState.Changed += PromptsChanged;
        ControlBindings.Changed -= PromptsChanged;
        ControlBindings.Changed += PromptsChanged;
    }
}
