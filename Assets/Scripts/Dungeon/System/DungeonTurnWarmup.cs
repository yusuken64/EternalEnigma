#if UNITY_STANDALONE_WIN && ENABLE_MONO && !UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

internal static class DungeonTurnWarmup
{
    const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;
    static bool attempted;

    // The first accepted command JITs hundreds of methods on the main thread. Compile the
    // shared turn paths behind the first dungeon entrance cover, without executing a turn.
    internal static void Prepare()
    {
        if (attempted) return;
        attempted = true;
        try
        {
            var turnTypes = new[] {
                typeof(TurnManager), typeof(Character), typeof(Ally), typeof(Enemy), typeof(AllyNavigation),
                typeof(AllyCombat), typeof(TileWorldDungeon), typeof(GameAction), typeof(EncounterRuntime),
                typeof(AllySkillContext), typeof(SkillCastOptions), typeof(LargeBossBehavior), typeof(EnemyTargeting),
                typeof(MissileTargeting), typeof(SkillEstimates), typeof(AStar), typeof(CombatVisualReplay),
                typeof(CombatEffectPlayer), typeof(CombatVisualSequence), typeof(DungeonActionPlayback)
            };
            foreach (var type in turnTypes) PrepareType(type);
            foreach (var type in typeof(Game).Assembly.GetTypes())
                if (!type.IsAbstract && (type.IsSubclassOf(typeof(GameAction)) || type.IsSubclassOf(typeof(PolicyBase)) ||
                    typeof(IAllySkillEvaluator).IsAssignableFrom(type)))
                    PrepareType(type);
        }
        catch (Exception error)
        {
            // Normal first-use compilation remains available on a runtime that can't prepare a method.
            Debug.LogWarning("Dungeon turn warmup ended early: " + error);
        }
    }

    static void PrepareType(Type type)
    {
        if (type.ContainsGenericParameters) return;
        foreach (var method in type.GetMethods(Declared)) PrepareMethod(method);
        foreach (var ctor in type.GetConstructors(Declared))
            if (!ctor.IsStatic) PrepareMethod(ctor);
        // Includes the compiler-generated state machines used for turn and playback coroutines.
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) PrepareType(nested);
    }

    static void PrepareMethod(MethodBase method)
    {
        if (!method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null)
            method.MethodHandle.GetFunctionPointer();
    }
}
#endif
