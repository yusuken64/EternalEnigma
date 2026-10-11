using System;
using System.Diagnostics;

// Read-only observation. Calls are omitted from non-development players.
public static class GameInputDiagnostics
{
    public static event Action<string, Character, Skill> Observed;
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Record(string kind, Character actor = null, Skill skill = null) => Observed?.Invoke(kind, actor, skill);
}
