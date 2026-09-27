using System.Linq;
using UnityEngine;

/// <summary>A living Goopi holds the target in place; attacks and items remain available.</summary>
public sealed class GoopiRootStatusEffect : StuckStatusEffect
{
    public Enemy Source;
    public bool Holding => Source != null && Source.Vitals.HP > 0;
    internal override bool BlocksMovement => Holding;
    internal override string StackKey => GetType().FullName + ":" + (Source != null ? Source.GetInstanceID() : 0);
    public static bool IsRooted(Character target) => target != null && target.StatusEffects.OfType<GoopiRootStatusEffect>().Any(r=>r.Holding);
    public static void Hold(Enemy source,Character target)
    {
        if(target.StatusEffects.OfType<GoopiRootStatusEffect>().Any(r=>r.Source==source && r.Holding)) return;
        var root=new GameObject("Goopi grip").AddComponent<GoopiRootStatusEffect>();
        root.transform.SetParent(target.VisualParent.transform,false);root.Source=source;root.TurnsLeft=2;
        target.StatusEffects.Add(root);
        GameMessages.ForCharacter(target,$"{GameMessages.Name(target)} is rooted by {GameMessages.Name(source)}! Defeat it to break free.");
    }
    internal override void OnCharacterDied(Character owner, Character deceased)
    {
        if (deceased != Source) return;
        owner.RemoveStatusEffect(this);
        Destroy(gameObject);
        GameMessages.ForCharacter(owner,$"{GameMessages.Name(owner)} broke free.");
    }
    public override void Tick() => TurnsLeft=Holding ? 2 : 0;
    internal override string GetEffectName() => "Rooted";
}
