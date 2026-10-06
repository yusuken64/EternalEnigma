using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill effect that opens a locked door without spending a small key. Author it on a skill with
/// <see cref="SkillTargeting.LockedDoor"/>; ScenerySkillTargets binds a copy to the chosen door.
/// </summary>
[Serializable]
public class UnlockDoorAction : GameAction
{
    [Range(0f, 1f)] public float SuccessChance = 1f;
    [NonSerialized] private DungeonProp door;

    internal UnlockDoorAction For(DungeonProp target) => new() { SuccessChance = SuccessChance, door = target };

    internal override bool IsValid(Character character) => door != null && door.IsClosedDoor;

    internal override List<GameAction> ExecuteImmediate(Character character)
    {
        if (!IsValid(character)) return new();
        if (UnityEngine.Random.value >= SuccessChance) { GameMessages.Post("The lock held."); return new(); }
        GameMessages.Post("The lock clicked open.");
        door.Unlock();
        return new();
    }

    internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false) { yield break; }
}
