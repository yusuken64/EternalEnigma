using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StatusPresentationTests
{
    readonly List<GameObject> objects = new();
    T Create<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name); objects.Add(go);
        return go.AddComponent<T>();
    }
    [TearDown] public void Cleanup()
    {
        foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
        objects.Clear();
    }

    [Test] public void FrailtyExplainsItsLivePenaltyAndDoesNotClaimToExpireWithTurns()
    {
        var frailty = Create<FrailtyStatusEffect>(); frailty.TurnsLeft = 10; frailty.Apply();
        frailty.ReApply(frailty); frailty.ReApply(frailty);
        for (int turn = 0; turn < 12; turn++) frailty.Tick();
        Assert.That(frailty.GetStatModification().Strength, Is.EqualTo(-3));
        Assert.That(StatusPresentation.Description(frailty), Does.Contain("Attack -3").And.Contain("Until cleansed").And.Not.Contain("turns left"));
        Assert.That(StatusPresentation.Badge(frailty), Is.EqualTo("x3"));
        Assert.That(StatusPresentation.Summary(frailty), Does.Contain("x3").And.Contain("Until cleansed"));
    }

    [Test] public void FrailtyNeverExpiresOnItsOwnEvenAtOneTurnLeft()
    {
        // Production prefab ships TurnsLeft = 1, which turn-limited statuses treat as their final turn.
        var frailty = Create<FrailtyStatusEffect>(); frailty.TurnsLeft = 1; frailty.Apply();
        for (int turn = 0; turn < 12; turn++)
        {
            Assert.That(frailty.IsExpired(), Is.False);
            Assert.That(frailty.IsOnFinalTurn(), Is.False);
            frailty.Tick();
        }
        Assert.That(frailty.TurnsLeft, Is.EqualTo(1));
    }

    [Test] public void TurnLimitedStatusStillCountsDownAndExpires()
    {
        var strength = Create<StrengthStatusEffect>(); strength.TurnsLeft = 2;
        Assert.That(strength.IsOnFinalTurn(), Is.False);
        strength.Tick();
        Assert.That(strength.IsOnFinalTurn(), Is.True);
        strength.Tick();
        Assert.That(strength.IsExpired(), Is.True);
    }

    [Test] public void ShieldDescriptionTracksDamageAbsorbedAndRefresh()
    {
        var owner = Create<Ally>();
        var shield = Create<DamageShieldStatusEffect>(); shield.Absorb = 20; shield.TurnsLeft = 4;
        shield.ModifyIncomingDamage(owner, new DamageContext(null, owner, 13, DamageElement.Physical, false, 0));
        Assert.That(StatusPresentation.Description(shield), Does.Contain("7 more damage").And.Contain("4 turns left"));
        shield.ReApply(shield);
        Assert.That(StatusPresentation.Description(shield), Does.Contain("20 more damage"));
    }

    [Test] public void CommandDescriptionUsesScaledStatsHealingAndElementalBonus()
    {
        var command = Create<CommandStatusEffect>();
        command.Modification = new StatModification { Strength = 4, Defense = 2 };
        command.HealPerTurn = 8; command.BonusElement = DamageElement.Fire; command.BonusElementPercent = 20;
        command.Multiplier = 1.5f; command.TurnsLeft = 1;
        Assert.That(StatusPresentation.Description(command), Does.Contain("Attack +6").And.Contain("+3 Defense")
            .And.Contain("12 HP").And.Contain("Fire hit for 30%").And.Contain("1 turn left"));
    }

    [TestCase("Root", "Cannot move")]
    [TestCase("Sleep", "Skips actions")]
    [TestCase("Confusion", "random direction")]
    [TestCase("Silence", "Cannot cast")]
    [TestCase("Poison", "one twelfth")]
    [TestCase("Bleed", "one twelfth")]
    [TestCase("Guard", "Counters direct hits")]
    [TestCase("Armored", "one quarter")]
    [TestCase("Exposed", "double damage")]
    [TestCase("Empowered", "Attack +12")]
    public void BossVariantsDescribeTheirDistinctMechanics(string kind, string detail)
    {
        var status = Create<BossEffectStatus>(); status.Kind = kind;
        Assert.That(StatusPresentation.Effect(status), Does.Contain(detail));
        Assert.That(CombatVisualCatalog.Instance.ForStatus(status)?.Icon, Is.Not.Null);
    }

    static IEnumerable<Type> StatusTypes => TypeCache.GetTypesDerivedFrom<StatusEffect>().Where(type => !type.IsAbstract);
    [TestCaseSource(nameof(StatusTypes))]
    public void EveryStatusTypeHasAnExplanationAndAnIcon(Type type)
    {
        var go = new GameObject(type.Name); objects.Add(go);
        var status = (StatusEffect)go.AddComponent(type);
        status.TurnsLeft = 5;
        if (status is BossEffectStatus boss) boss.Kind = "Guard";
        Assert.That(StatusPresentation.Effect(status), Is.Not.Empty.And.Not.EqualTo("No additional effect."), type.Name);
        Assert.That(StatusPresentation.Description(status), Does.Contain("Duration:"), type.Name);
        Assert.That(CombatVisualCatalog.Instance.ForStatus(status)?.Icon, Is.Not.Null, type.Name);
        if (status is GoopiRootStatusEffect)
            Assert.That(StatusPresentation.Duration(status), Is.EqualTo("Until the holder is defeated"));
    }
}
