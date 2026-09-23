using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkillRankTests
{
    [Test]
    public void ScalePowerAddsFifteenPercentPerRankRoundingAwayFromZero()
    {
        var scaling = new SkillRankScaling();
        Assert.That(scaling.ScalePower(10, 1), Is.EqualTo(10));
        Assert.That(scaling.ScalePower(5, 2), Is.EqualTo(6));
        Assert.That(scaling.ScalePower(5, 3), Is.EqualTo(7));
        Assert.That(scaling.ScalePower(20, 5), Is.EqualTo(32));
    }

    [Test]
    public void ScaleBuffAddsOneStepPerRankKeepingSign()
    {
        var scaling = new SkillRankScaling();
        Assert.That(scaling.ScaleBuff(2, 3), Is.EqualTo(4));
        Assert.That(scaling.ScaleBuff(-2, 3), Is.EqualTo(-4));
        Assert.That(scaling.ScaleBuff(0, 5), Is.EqualTo(0));
        Assert.That(scaling.ScaleBuff(10, 1), Is.EqualTo(10));
    }

    [Test]
    public void ScaleDurationAddsTurnsAtRanksThreeAndFive()
    {
        var scaling = new SkillRankScaling { DurationBonusAtRanks3And5 = true };
        Assert.That(scaling.ScaleDuration(3, 1), Is.EqualTo(3));
        Assert.That(scaling.ScaleDuration(3, 2), Is.EqualTo(3));
        Assert.That(scaling.ScaleDuration(3, 3), Is.EqualTo(4));
        Assert.That(scaling.ScaleDuration(3, 4), Is.EqualTo(4));
        Assert.That(scaling.ScaleDuration(3, 5), Is.EqualTo(5));

        var scalingNoBonus = new SkillRankScaling { DurationBonusAtRanks3And5 = false };
        Assert.That(scalingNoBonus.ScaleDuration(3, 5), Is.EqualTo(3));
    }

    [Test]
    public void ScaleChanceAddsPointsAndClamps()
    {
        var scaling = new SkillRankScaling();
        Assert.That(scaling.ScaleChance(0.5f, 3), Is.EqualTo(0.6f).Within(1e-5));
        Assert.That(scaling.ScaleChance(0.98f, 3), Is.EqualTo(1f));
    }

    [Test]
    public void RankContextClampsRankAndDefaultsScaling()
    {
        var context = new SkillRankContext(0, null);
        Assert.That(context.Rank, Is.EqualTo(1));
        Assert.That(context.Scaling, Is.Not.Null);

        Assert.That(SkillRankContext.Unranked.Rank, Is.EqualTo(1));
    }

    [Test]
    public void RankedSkillScalesDamageAndHealingEffects()
    {
        var skill = ScriptableObject.CreateInstance<Skill>();
        try
        {
            skill.ActionEffects = new List<GameAction>
            {
                new TakeDamageAction(null, null, 5),
                new TakeHealAction(null, null, 10)
            };

            skill.Rank = 2;
            var effects = skill.GetEffects(null, null);
            Assert.That(effects[0], Is.TypeOf<TakeDamageAction>());
            Assert.That(((TakeDamageAction)effects[0]).damage, Is.EqualTo(6));
            Assert.That(effects[1], Is.TypeOf<TakeHealAction>());
            Assert.That(((TakeHealAction)effects[1]).healing, Is.EqualTo(12));

            skill.Rank = 1;
            effects = skill.GetEffects(null, null);
            Assert.That(((TakeDamageAction)effects[0]).damage, Is.EqualTo(5));
            Assert.That(((TakeHealAction)effects[1]).healing, Is.EqualTo(10));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void PassiveModificationScalesIntegerStatsByRank()
    {
        var skill = ScriptableObject.CreateInstance<Skill>();
        try
        {
            skill.PassiveStatModification = new StatModification
            {
                HPMax = 10,
                Strength = 2,
                DropRate = 0.5f
            };

            skill.Rank = 3;
            var scaled = skill.GetScaledPassiveModification();
            Assert.That(scaled.HPMax, Is.EqualTo(12));
            Assert.That(scaled.Strength, Is.EqualTo(4));
            Assert.That(scaled.DropRate, Is.EqualTo(0.5f));

            skill.Rank = 1;
            scaled = skill.GetScaledPassiveModification();
            Assert.That(scaled.HPMax, Is.EqualTo(10));
            Assert.That(scaled.Strength, Is.EqualTo(2));

            skill.PassiveStatModification = null;
            scaled = skill.GetScaledPassiveModification();
            Assert.That(scaled, Is.Not.Null);
            Assert.That(scaled.HPMax, Is.EqualTo(0));
            Assert.That(scaled.Strength, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void SkillRanksAndHighestLevelRoundTripThroughSave()
    {
        var store = new Store();
        using (SaveSystem.UseStore(store))
        {
            var saveData = new GameSaveData();
            saveData.TownSaveData.RecruitedAlliesData = new List<TownAllyData>
            {
                new TownAllyData
                {
                    AllyName = "Rowan",
                    Skills = new List<string> { "Healing" },
                    SkillRanks = new List<SkillRankSaveData>
                    {
                        new SkillRankSaveData { SkillName = "Healing", Rank = 3 }
                    },
                    HighestLevel = 14
                }
            };

            SaveSystem.SaveData(saveData);
            var loaded = SaveSystem.LoadData();

            Assert.That(loaded.TownSaveData.RecruitedAlliesData[0].SkillRanks[0].SkillName, Is.EqualTo("Healing"));
            Assert.That(loaded.TownSaveData.RecruitedAlliesData[0].SkillRanks[0].Rank, Is.EqualTo(3));
            Assert.That(loaded.TownSaveData.RecruitedAlliesData[0].HighestLevel, Is.EqualTo(14));

            var freshAlly = new TownAllyData();
            Assert.That(freshAlly.HighestLevel, Is.EqualTo(1));
            Assert.That(freshAlly.SkillRanks, Is.Not.Null);
            Assert.That(freshAlly.SkillRanks.Count, Is.EqualTo(0));
        }
    }

    private sealed class Store : ISaveStore
    {
        public string Json;
        public string Read() => Json;
        public void Write(string json) => Json = json;
        public void Clear() => Json = null;
    }
}
