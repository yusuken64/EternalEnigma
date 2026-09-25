using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EternalEnigma.Core.Classes;
using Object = UnityEngine.Object;

public class TrainerOfferTests
{
    private List<Object> createdObjects;

    [SetUp]
    public void SetUp()
    {
        createdObjects = new List<Object>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in createdObjects)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }
        createdObjects.Clear();
    }

    private Skill MakeSkill(string name, int cost = 50)
    {
        var skill = ScriptableObject.CreateInstance<Skill>();
        createdObjects.Add(skill);
        skill.SkillName = name;
        skill.LearnCost = cost;
        return skill;
    }

    private ClassDefinition MakeClass()
    {
        var cls = ScriptableObject.CreateInstance<ClassDefinition>();
        createdObjects.Add(cls);
        cls.Id = "test";
        cls.DisplayName = "Test";
        cls.Skills = new List<ClassSkillEntryData>
        {
            new ClassSkillEntryData { Skill = MakeSkill("T Novice"), Tier = 1, MaxRank = 1, Kind = SkillKind.Mastery },
            new ClassSkillEntryData { Skill = MakeSkill("T Strike"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
            new ClassSkillEntryData { Skill = MakeSkill("T Guard"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
            new ClassSkillEntryData { Skill = MakeSkill("T Parry"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
            new ClassSkillEntryData { Skill = MakeSkill("T Adept"), Tier = 2, MaxRank = 1, Kind = SkillKind.Mastery },
            new ClassSkillEntryData { Skill = MakeSkill("T Cleave"), Tier = 2, MaxRank = 5, Kind = SkillKind.Normal },
            new ClassSkillEntryData { Skill = MakeSkill("T Master"), Tier = 3, MaxRank = 1, Kind = SkillKind.Mastery },
        };
        return cls;
    }

    private TownAlly MakeAlly()
    {
        var go = new GameObject("ally");
        createdObjects.Add(go);
        var ally = go.AddComponent<TownAlly>();
        ally.Skills = new List<string>();
        ally.SkillRanks = new List<SkillRankSaveData>();
        ally.HighestLevel = 1;
        return ally;
    }

    private TownConfiguration MakeTown(params Skill[] learnable)
    {
        var town = ScriptableObject.CreateInstance<TownConfiguration>();
        createdObjects.Add(town);
        town.LearnableSkills = learnable.ToList();
        return town;
    }

    [Test]
    public void RankHelpersTrackLearnedSkills()
    {
        var ally = MakeAlly();

        // GetRank("x") == 0
        Assert.AreEqual(0, ally.GetRank("x"));

        // SetRank("x", 3) → Skills contains "x", GetRank == 3
        ally.SetRank("x", 3);
        Assert.Contains("x", ally.Skills);
        Assert.AreEqual(3, ally.GetRank("x"));

        // Skills.Add("y") → GetRank("y") == 1
        ally.Skills.Add("y");
        Assert.AreEqual(1, ally.GetRank("y"));

        // ToLearnedSkills() has ranks 3 and 1
        var learned = ally.ToLearnedSkills();
        Assert.AreEqual(2, learned.Count);
        Assert.AreEqual(3, learned.First(l => l.SkillId == "x").Rank);
        Assert.AreEqual(1, learned.First(l => l.SkillId == "y").Rank);

        // SetRank("x", 0) → not in Skills, GetRank == 0
        ally.SetRank("x", 0);
        Assert.IsFalse(ally.Skills.Contains("x"));
        Assert.AreEqual(0, ally.GetRank("x"));
    }

    [Test]
    public void EnsureStartingSkillsGrantsTierOneMasteryOnce()
    {
        var ally = MakeAlly();
        ally.PrimaryClass = MakeClass();

        // Call EnsureStartingSkills() twice
        ally.EnsureStartingSkills();
        ally.EnsureStartingSkills();

        // Skills.Count(s => s == "T Novice") == 1
        Assert.AreEqual(1, ally.Skills.Count(s => s == "T Novice"));

        // GetRank("T Novice") == 1
        Assert.AreEqual(1, ally.GetRank("T Novice"));

        // Classless ally → Skills stays empty
        var classlessAlly = MakeAlly();
        classlessAlly.EnsureStartingSkills();
        Assert.AreEqual(0, classlessAlly.Skills.Count);
    }

    [Test]
    public void FallbackOffersFollowTownListWithOneRank()
    {
        var classlessAlly = MakeAlly();
        var a = MakeSkill("A");
        var b = MakeSkill("B", 70);
        classlessAlly.Skills.Add("A");

        var offers = TrainerOffers.Build(classlessAlly, MakeTown(a, b));

        // 2 offers in order A, B
        Assert.AreEqual(2, offers.Count);
        Assert.AreEqual("A", offers[0].Skill.SkillName);
        Assert.AreEqual("B", offers[1].Skill.SkillName);

        // A: IsMaxed, HasClass == false
        Assert.IsTrue(offers[0].IsMaxed);
        Assert.IsFalse(offers[0].HasClass);

        // B: CanLearn, NextCost == 70, MaxRank == 1, Label == "B"
        Assert.IsTrue(offers[1].CanLearn);
        Assert.AreEqual(70, offers[1].NextCost);
        Assert.AreEqual(1, offers[1].MaxRank);
        Assert.AreEqual("B", offers[1].Label);
    }

    [Test]
    public void ClassOffersAreGroupedByTierWithLocks()
    {
        var ally = MakeAlly();
        var cls = MakeClass();
        ally.PrimaryClass = cls;
        ally.HighestLevel = 1;

        var offers = TrainerOffers.Build(ally, MakeTown());

        // Offer skill names in order: T Novice, T Strike, T Guard, T Parry, T Adept, T Cleave, T Master
        var skillNames = offers.Select(o => o.Skill.SkillName).ToList();
        Assert.AreEqual(new[] { "T Novice", "T Strike", "T Guard", "T Parry", "T Adept", "T Cleave", "T Master" }, skillNames);

        // T Novice.CanLearn
        Assert.IsTrue(offers[0].CanLearn);

        // T Strike.CanLearn && NextCost == 50 && Label == "T Strike 0/5"
        Assert.IsTrue(offers[1].CanLearn);
        Assert.AreEqual(50, offers[1].NextCost);
        Assert.AreEqual("T Strike 0/5", offers[1].Label);

        // T Cleave.CanLearn == false, LockReason non-empty, Label == "T Cleave 0/5 (T2)"
        var cleaveOffer = offers.First(o => o.Skill.SkillName == "T Cleave");
        Assert.IsFalse(cleaveOffer.CanLearn);
        Assert.IsNotEmpty(cleaveOffer.LockReason);
        Assert.AreEqual("T Cleave 0/5 (T2)", cleaveOffer.Label);
    }

    [Test]
    public void NextRankCostAndLevelGate()
    {
        var ally = MakeAlly();
        ally.PrimaryClass = MakeClass();
        ally.SetRank("T Strike", 1);
        ally.HighestLevel = 1;

        var offers = TrainerOffers.Build(ally, MakeTown());
        var strikeOffer = offers.First(o => o.Skill.SkillName == "T Strike");

        // Strike offer: CurrentRank == 1, NextCost == 100, CanLearn == false (rank 2 needs level 4)
        Assert.AreEqual(1, strikeOffer.CurrentRank);
        Assert.AreEqual(100, strikeOffer.NextCost);
        Assert.IsFalse(strikeOffer.CanLearn);

        // Set HighestLevel = 4, rebuild → CanLearn == true
        ally.HighestLevel = 4;
        offers = TrainerOffers.Build(ally, MakeTown());
        strikeOffer = offers.First(o => o.Skill.SkillName == "T Strike");
        Assert.IsTrue(strikeOffer.CanLearn);
    }

    [Test]
    public void AdeptTrainingNeedsLevelTenAndThreeTierOneSkills()
    {
        var ally = MakeAlly();
        ally.PrimaryClass = MakeClass();
        ally.HighestLevel = 10;
        ally.SetRank("T Novice", 1);
        ally.SetRank("T Strike", 1);
        ally.SetRank("T Guard", 1);

        var offers = TrainerOffers.Build(ally, MakeTown());
        var adeptOffer = offers.First(o => o.Skill.SkillName == "T Adept");

        // T Adept.CanLearn == false
        Assert.IsFalse(adeptOffer.CanLearn);

        // Add T Parry → true
        ally.SetRank("T Parry", 1);
        offers = TrainerOffers.Build(ally, MakeTown());
        adeptOffer = offers.First(o => o.Skill.SkillName == "T Adept");
        Assert.IsTrue(adeptOffer.CanLearn);

        // With 4 skills but HighestLevel = 9 → false
        ally.HighestLevel = 9;
        offers = TrainerOffers.Build(ally, MakeTown());
        adeptOffer = offers.First(o => o.Skill.SkillName == "T Adept");
        Assert.IsFalse(adeptOffer.CanLearn);
    }

    [Test]
    public void AllowlistFiltersClassOffers()
    {
        var ally = MakeAlly();
        var cls = MakeClass();
        ally.PrimaryClass = cls;

        // Town list contains only the class's T Strike skill object
        var strikeSkill = cls.Skills.First(s => s.Skill.SkillName == "T Strike").Skill;
        var offers = TrainerOffers.Build(ally, MakeTown(strikeSkill));

        // Build returns exactly one offer, T Strike
        Assert.AreEqual(1, offers.Count);
        Assert.AreEqual("T Strike", offers[0].Skill.SkillName);
    }

    [Test]
    public void FindMatchesBySkillName()
    {
        var ally = MakeAlly();
        var cls = MakeClass();
        ally.PrimaryClass = cls;

        var strikeSkill = cls.Skills.First(s => s.Skill.SkillName == "T Strike").Skill;
        var town = MakeTown(strikeSkill);

        // Find(ally, town, strikeSkill) returns the Strike offer
        var offer = TrainerOffers.Find(ally, town, strikeSkill);
        Assert.IsNotNull(offer);
        Assert.AreEqual("T Strike", offer.Skill.SkillName);

        // Find(..., null) is null
        offer = TrainerOffers.Find(ally, town, null);
        Assert.IsNull(offer);
    }
}
