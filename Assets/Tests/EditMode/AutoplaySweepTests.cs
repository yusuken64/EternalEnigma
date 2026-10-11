using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public class AutoplaySweepTests
    {
        [Test]
        public void MaterialCatalogRebuildsDestroyedPlayModeObjects()
        {
            var previous = MaterialCatalog.All.ToArray();
            foreach (var definition in previous) UnityEngine.Object.DestroyImmediate(definition);
            var current = MaterialCatalog.All;
            Assert.That(current.Count, Is.EqualTo(4));
            Assert.That(current.All(definition => definition != null), Is.True);
            Assert.That(MaterialCatalog.ForKind(EternalEnigma.Core.World.GatheringKind.Ore), Is.SameAs(current[0]));
        }

        [Test]
        public void ModesRoundTripAndLegacySettingsRemainCampaigns()
        {
            Assert.That(JsonUtility.FromJson<AutoplayOptions>("{\"Seed\":7}").Mode, Is.EqualTo(AutoplayMode.Campaign));
            var options = JsonUtility.FromJson<AutoplayOptions>(JsonUtility.ToJson(AutoplayOptions.LootSweep()));
            Assert.That(options.Mode, Is.EqualTo(AutoplayMode.FloorLootSweep));
            Assert.That(options.RunCount, Is.EqualTo(10000)); Assert.That(options.SeedForRun(9999), Is.EqualTo(10041));
            Assert.That(options.InfiniteResources || options.DebugCoverage, Is.False);
            Assert.That(options.Speed, Is.EqualTo(32)); Assert.That(options.StallSeconds, Is.EqualTo(300));
            Assert.That(options.ActionLimit, Is.EqualTo(100000)); Assert.That(options.TimeLimitMinutes, Is.EqualTo(60));
        }

        [TestCase("Passed", true, false)] [TestCase("Failed", true, false)]
        [TestCase("Victory", false, true)] [TestCase("Defeat", false, true)]
        [TestCase("GameError", false, false)] [TestCase("AutomationError", false, false)]
        [TestCase("Stopped", false, false)]
        public void ContinuationIsModeSpecific(string outcome, bool sweep, bool campaign)
        {
            Assert.That(AutoplayRunner.ContinuesAfter(outcome, AutoplayMode.FloorLootSweep), Is.EqualTo(sweep));
            Assert.That(AutoplayRunner.ContinuesAfter(outcome), Is.EqualTo(campaign));
        }

        [Test]
        public void ManifestHasExactDistinctCasesAndBalancedSortedSupportedProfiles()
        {
            var m = AutoplaySweepManifest.Create(AutoplayOptions.LootSweep(), Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog"), "test-build", "test-content");
            var clone = JsonUtility.FromJson<AutoplaySweepManifest>(JsonUtility.ToJson(m)); clone.Validate();
            Assert.That(clone.Cases.Count, Is.EqualTo(10000)); Assert.That(clone.Cases.Select(c => c.Id).Distinct().Count(), Is.EqualTo(10000));
            var profiles = clone.Cases.Select(c => c.Profile).Distinct().ToArray();
            Assert.That(profiles, Is.EqualTo(profiles.OrderBy(p => p, StringComparer.Ordinal).ToArray()));
            Assert.That(profiles.Length, Is.EqualTo(Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Themes.Length * 15 + 3));
            var counts = clone.Cases.GroupBy(c => c.Profile).Select(g => g.Count()).ToArray();
            Assert.That(counts.Max() - counts.Min(), Is.LessThanOrEqualTo(1));
            for (int i = 0; i < clone.Cases.Count; i++)
            {
                var c = clone.Cases[i]; Assert.That(c.Index, Is.EqualTo(i)); Assert.That(c.Seed, Is.EqualTo(42 + i));
                Assert.That(c.Profile, Is.EqualTo(profiles[i % profiles.Length]));
                Assert.That(c.Options().Equals(m.Cases[i].Options()), Is.True);
            }
            clone.Cases[0].Width++;
            Assert.Throws<InvalidDataException>(() => clone.Validate());
        }

        [Test]
        public void ResumeRequiresFinalizedMatchingVersionAndEveryArtifact()
        {
            var options = AutoplayOptions.LootSweep(); options.RunCount = 2;
            var m = AutoplaySweepManifest.Create(options, Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog"), "test-build", "test-content");
            string folder = Path.GetFullPath(Path.Combine("Temp", "SweepManifestTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            var report = new AutoplayReport { Options = options, ValidationOnly = true, EligibleForBalance = false, CaseId = m.Cases[0].Id,
                ManifestId = m.Id, BuildId = m.BuildId, ContentId = m.ContentId, Outcome = "Passed", ArtifactsComplete = true };
            void Save() => File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report));
            foreach (var artifact in new[] { "save.json", "actions.jsonl", "issues.jsonl", "final.png", "entrance.png", "gameplay-final.png" }) File.WriteAllText(Path.Combine(folder, artifact), "test artifact");
            Save(); Assert.That(m.FinalizedCases(folder, m.BuildId, m.ContentId), Is.EquivalentTo(new[] { report.CaseId }));
            File.Delete(Path.Combine(folder, "entrance.png")); Assert.That(m.FinalizedCases(folder, m.BuildId, m.ContentId), Is.Empty);
            File.WriteAllText(Path.Combine(folder, "entrance.png"), "test artifact");
            Assert.That(m.FinalizedCases(folder, "changed-build", m.ContentId), Is.Empty);
            Assert.That(m.FinalizedCases(folder, m.BuildId, "changed-content"), Is.Empty);
            report.ArtifactsComplete = false; Save(); Assert.That(m.FinalizedCases(folder, m.BuildId, m.ContentId), Is.Empty);
            report.ArtifactsComplete = true; report.Outcome = "Stopped"; Save(); Assert.That(m.FinalizedCases(folder, m.BuildId, m.ContentId), Is.Empty);
            report.Outcome = "Failed"; Save(); Assert.That(m.FinalizedCases(folder, m.BuildId, m.ContentId).Count, Is.EqualTo(1));
            File.Delete(Path.Combine(folder, "final.png")); Assert.That(m.FinalizedCases(folder, m.BuildId, m.ContentId), Is.Empty);
            File.WriteAllText(Path.Combine(folder, "report.json"), "{interrupted"); Assert.That(m.FinalizedCases(folder, m.BuildId, m.ContentId), Is.Empty);
        }
    }
}
