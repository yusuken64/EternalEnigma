using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public sealed class ChaosTests
    {
        GameObject host;
        ItemManager manager;
        [SetUp] public void Setup()
        {
            host = new GameObject("chaos test item catalog"); manager = host.AddComponent<ItemManager>();
            var text = File.ReadAllText("Assets/Scenes/Common.unity");
            var block = Regex.Match(text, @"  ItemDefinitions:[\s\S]*?  StartingItems:[\s\S]*?(?=\n---)").Value;
            manager.ItemDefinitions = Regex.Matches(block, @"guid: ([a-f0-9]{32})").Cast<Match>()
                .Select(m => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value))).Where(i => i != null).Distinct().ToList();
            manager.StartingItems = new();
            Assert.That(manager.ItemDefinitions.Count, Is.GreaterThan(10));
        }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(host); }
        [Test] public void FixtureIsReproducibleAndExactReplayDoesNotRegenerate()
        {
            var a = ChaosFixtures.Create(42, TownSceneLoader.Default, manager);
            var b = ChaosFixtures.Create(42, TownSceneLoader.Default, manager);
            Assert.That(JsonUtility.ToJson(a), Is.EqualTo(JsonUtility.ToJson(b)));
            var replay = ChaosJson.Clone(a); replay.Validate();
            Assert.That(replay.SaveJson, Is.EqualTo(a.SaveJson));
            replay.Party[0].AllyName = "tampered";
            Assert.Throws<InvalidDataException>(() => replay.Validate());
        }
        [Test] public void RandomPartiesAndItemsAreLegalAndVaryAcrossSeeds()
        {
            string previous = null; var compositions = new System.Collections.Generic.HashSet<string>();
            var inventories = new System.Collections.Generic.HashSet<string>();
            var items = ChaosFixtures.Items(manager);
            for (int seed = 1; seed <= 16; seed++)
            {
                var fixture = ChaosFixtures.Create(seed, TownSceneLoader.Default, manager, previousComposition: previous);
                fixture.Validate();
                Assert.That(fixture.Party.Select(h => h.AllyId).Distinct().Count(), Is.EqualTo(fixture.Party.Count));
                Assert.That(fixture.Party.Count, Is.InRange(1, TownSceneLoader.Default.MaxPartySize));
                Assert.That(fixture.ControlledHeroId, Is.EqualTo(fixture.Party[0].AllyId));
                Assert.That(fixture.Composition, Is.Not.EqualTo(previous));
                Assert.That(fixture.Inventory.Count, Is.LessThanOrEqualTo(Inventory.DefaultCapacity));
                foreach (var h in fixture.Party)
                {
                    var prefab = TownSceneLoader.Default.AllyCatalog.Single(p => p.Id == h.AllyId);
                    Assert.That(h.PrimaryClassId, Is.EqualTo(prefab.PrimaryClass?.Id ?? ""));
                    Assert.That(h.SecondaryClassId, Is.EqualTo(prefab.SecondaryClass?.Id ?? ""));
                    foreach (var e in h.Equipment) {
                        ChaosFixtures.ValidateItem(e, items);
                        Assert.That(HeroClass.AllowsItem(prefab.PrimaryClass, prefab.SecondaryClass, (EquipableInventoryItem)e.Restore(manager)), Is.True);
                    }
                }
                foreach (var item in fixture.Inventory) ChaosFixtures.ValidateItem(item, items);
                compositions.Add(fixture.Composition); inventories.Add(string.Join("|", fixture.Inventory.Select(i => i.ItemName)));
                previous = fixture.Composition;
            }
            Assert.That(compositions.Count, Is.GreaterThan(8)); Assert.That(inventories.Count, Is.GreaterThan(8));
        }
        [Test] public void RequiredHeroSkillRankAndResourcesSurviveRandomization()
        {
            var hero = TownSceneLoader.Default.AllyCatalog.First(h => h.PrimaryClass?.Skills.Any(e => e.Skill != null) == true);
            var skill = hero.PrimaryClass.Skills.First(e => e.Skill != null);
            var item = ChaosFixtures.Items(manager).First(i => i.StackMax > 0);
            var required = new ChaosPrerequisites { HeroId = hero.Id, ClassId = hero.PrimaryClass.Id, SkillName = skill.Skill.SkillName,
                Rank = skill.MaxRank, Level = 20, HP = 12, SP = 0, Inventory = new() { new ItemSaveData { ItemName = item.ItemName, HasStock = true, Stock = item.StackMax } } };
            var fixture = ChaosFixtures.Create(72, TownSceneLoader.Default, manager, required: required);
            var data = fixture.Party.Single(h => h.AllyId == hero.Id);
            Assert.That(data.SkillRanks.Single(s => s.SkillName == skill.Skill.SkillName).Rank, Is.EqualTo(skill.MaxRank));
            Assert.That(data.Sp, Is.Zero); Assert.That(data.Hp, Is.EqualTo(12));
            Assert.That(fixture.Inventory.Single(i => i.ItemName == item.ItemName).Stock, Is.EqualTo(item.StackMax));
            Assert.That(fixture.Guaranteed, Has.Count.EqualTo(1)); fixture.Validate();
        }
        [Test] public void UnresolvablePrerequisitesNeverSilentlyBecomeRandom()
        {
            Assert.Throws<InvalidDataException>(() => ChaosFixtures.Create(1, TownSceneLoader.Default, manager,
                required: new ChaosPrerequisites { HeroId = "missing hero" }));
        }
        [Test] public void RandomStreamsAreIndependent()
        {
            Assert.That(ChaosRandom.Derive(42, "heroes"), Is.Not.EqualTo(ChaosRandom.Derive(42, "inventory")));
            var a = new ChaosRandom(ChaosRandom.Derive(42, "input")); var b = new ChaosRandom(ChaosRandom.Derive(42, "input"));
            ChaosFixtures.Create(999, TownSceneLoader.Default, manager);
            Assert.That(a.Next(), Is.EqualTo(b.Next()));
        }
        [Test] public void PreferenceIsolationNeverWritesPersonalKeys()
        {
            const string key = "Dungeon.FullControl";
            bool existed = PlayerPrefs.HasKey(key); int before = PlayerPrefs.GetInt(key, -19);
            using (GamePreferences.Isolate()) { GamePreferences.SetInt(key, 127); GamePreferences.Save(); Assert.That(GamePreferences.GetInt(key), Is.EqualTo(127));
                Assert.That(PlayerPrefs.HasKey(key), Is.EqualTo(existed)); Assert.That(PlayerPrefs.GetInt(key, -19), Is.EqualTo(before)); }
            Assert.That(GamePreferences.GetInt(key, -19), Is.EqualTo(before));
        }
        [Test] public void SaveScopeRestoresStoreAndSlot()
        {
            var old = new ChaosSaveStore(); using var outer = SaveSystem.UseStore(old); SaveSystem.ActiveSlot = 2;
            old.Write(2, "personal");
            using (SaveSystem.UseStore(new ChaosSaveStore())) { SaveSystem.SaveData(new GameSaveData()); Assert.That(SaveSystem.ActiveSlot, Is.Zero); }
            Assert.That(SaveSystem.ActiveSlot, Is.EqualTo(2)); Assert.That(old.Read(2), Is.EqualTo("personal"));
        }
        [Test] public void SelectionReleaseAndPassiveOwnershipAreNotCoverage()
        {
            var row = new ChaosCase { Kind = "Active" };
            var evidence = new ChaosEvidence { MenuOpened = true, Invoked = true, BeforeHash = "a", AfterHash = "b", Oracle = "test", ControlRestored = true, CostsVerified = true, TurnsVerified = true };
            Assert.That(evidence.Satisfies(row), Is.False);
            evidence.EffectVerified = true; Assert.That(evidence.Satisfies(row), Is.True);
            row.Kind = "Passive"; Assert.That(evidence.Satisfies(row), Is.False);
            evidence.Triggered = true; Assert.That(evidence.Satisfies(row), Is.False);
            evidence.PairedComparison = true; Assert.That(evidence.Satisfies(row), Is.True);
        }
        [Test] public void MeaningfulChangesCountButRepeatedStateDoesNot()
        {
            var progress = new ChaosProgress(); progress.Reset(0); progress.Observe("a", 1); progress.Observe("b", 2);
            progress.Observe("b", 8); progress.Observe("b", 11);
            Assert.That(progress.Expired(12, 10), Is.True);
            progress.Observe("a", 12); Assert.That(progress.Expired(12, 10), Is.False);
        }
        [Test] public void DeveloperAndQuitControlsCannotBeActivatedByChaos()
        {
            foreach (string label in new[] { "Exit", "Quit", "Developer", "Endgame Town", "Test Dungeon", "Debug Autoplay" })
                Assert.That(ChaosUI.Forbidden(label), Is.True, label);
            Assert.That(ChaosUI.Forbidden("Continue"), Is.False);
            Assert.That(ChaosUI.Forbidden("Skills"), Is.False);
        }
        [Test] public void ChangedVersionsAndIncompleteCoverageRejectFullPass()
        {
            var report = new ChaosReport { BuildId = "a", ContentId = "b" };
            Assert.Throws<InvalidDataException>(() => ChaosJson.RequireIdentity(report, "changed", "b"));
            var coverage = new ChaosCoverage { Cases = new() { new ChaosCase { Passed = true } }, Participation = new() { new ChaosParticipation() } };
            Assert.That(coverage.FullPass, Is.False); coverage.Participation[0].Exercised = 1; Assert.That(coverage.FullPass, Is.True);
        }
        [Test] public void InvalidDurationsAndSeedOverflowAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new ChaosOptions { DurationSeconds = float.NaN }.Validate());
            Assert.Throws<ArgumentException>(() => new ChaosOptions { Seed = int.MaxValue, RunCount = 2 }.Validate());
        }
        [Test] public void MissingAndChangedArtifactsPreventResume()
        {
            string directory = Path.GetFullPath("Temp/Chaos/ArtifactTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "evidence.json");
            try
            {
                ChaosJson.Write(file, new ChaosEvidence { EffectVerified = true });
                var report = new ChaosReport { ArtifactsComplete = true, Artifacts = new() {
                    new ChaosArtifact { Path = "evidence.json", Bytes = new FileInfo(file).Length, Sha256 = ChaosJson.FileHash(file) } } };
                ChaosJson.VerifyArtifacts(directory, report);
                File.WriteAllText(file, "changed"); Assert.Throws<InvalidDataException>(() => ChaosJson.VerifyArtifacts(directory, report));
                File.Delete(file); Assert.Throws<InvalidDataException>(() => ChaosJson.VerifyArtifacts(directory, report));
                Assert.Catch<IOException>(() => ChaosJson.Write(Path.Combine(directory, "absent", "report.json"), report));
            }
            finally { if (File.Exists(file)) File.Delete(file); Directory.Delete(directory); }
        }
    }
}
