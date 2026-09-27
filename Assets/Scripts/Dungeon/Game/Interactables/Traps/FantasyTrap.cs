using System.Collections.Generic;
using UnityEngine;

// Explicit values are the seeded generation catalogue. Never reorder or recycle them.
public enum FantasyTrapKind
{
    Arrow = 0, IronArrow = 1, RollingLog = 2, FallingRock = 3, Bomb = 4,
    Warp = 5, Pitfall = 6, Updraft = 7, IceSlick = 8, Disarm = 9,
    ShadowBind = 10, Hallucination = 11, Summoning = 12, Transformation = 13,
    WitheringHex = 14, ClumsyJinx = 15, Blight = 16, Scorch = 17
}

public sealed class FantasyTrap : Trap
{
    public const int KindCount = 18;
    public FantasyTrapKind Kind;
    [Range(0, 1)] public float ActivationChance = .5f;
    [Range(0, 100)] public int DamagePercent = 10;
    public int Distance = 3;
    public StatusEffect[] Effects;
    internal override string GetInteractionText() => System.Text.RegularExpressions.Regex.Replace(Kind.ToString(), "(?<=[a-z])([A-Z])", " $1") + " Trap";
    internal override List<GameAction> GetTrapSideEffects(Character character)
        => CanTrigger(character) ? new() { new TrapResolutionAction(this, character, TileWorldDungeon.GetFacingOffset(character.CurrentFacing)) } : new();

    internal static FantasyTrap PrefabFor(int roll)
    {
        var kind = (FantasyTrapKind)((uint)roll % KindCount);
        var prefab = Resources.Load<FantasyTrap>("FantasyTraps/" + kind);
        if (prefab == null) throw new System.InvalidOperationException("Missing fantasy trap prefab: " + kind);
        return prefab;
    }
}
