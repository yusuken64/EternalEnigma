using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Explicit fallback assignments for procedural statuses and actions without asset owners.
[CreateAssetMenu(menuName = "Game/Combat Visual Catalog")]
public sealed class CombatVisualCatalog : ScriptableObject
{
    [Serializable] public sealed class StatusEntry
    {
        public string TypeName;
        public StatusVisualProfile Profile;
    }
    public CombatEffectProfile Melee, Ranged, Confusion, Root, Steal, Heal, Utility;
    public CombatEffectProfile Fire, Ice, Lightning;
    public StatusVisualProfile BlazingArms, FrostArms, StormArms;
    public List<StatusEntry> Statuses = new();
    static CombatVisualCatalog instance;
    public static CombatVisualCatalog Instance => instance != null ? instance : instance = Resources.Load<CombatVisualCatalog>("CombatEffects/Catalog");
    public StatusVisualProfile ForStatus(StatusEffect status)
    {
        if (status is CommandStatusEffect command)
        {
            if (command.CommandId == "blazing-arms" && BlazingArms != null) return BlazingArms;
            if (command.CommandId == "frost-arms" && FrostArms != null) return FrostArms;
            if (command.CommandId == "storm-arms" && StormArms != null) return StormArms;
        }
        return status.VisualProfile != null ? status.VisualProfile :
            Statuses.FirstOrDefault(e => e.TypeName == status.GetType().FullName)?.Profile;
    }
    public CombatEffectProfile ForElement(DamageElement element) => element switch
    { DamageElement.Fire => Fire, DamageElement.Ice => Ice, DamageElement.Lightning => Lightning, _ => Melee };
}
