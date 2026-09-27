using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public sealed class AutoplayControlPreferenceTests
    {
        private bool hadPreference;
        private int savedPreference;
        private bool? savedOverride;
        private GameObject owner;
        private static readonly PropertyInfo Active = typeof(AutoplayRunner).GetProperty("Active");

        [SetUp]
        public void SetUp()
        {
            Assert.That(AutoplayRunner.Active, Is.Null, "Run outside a live autoplay session.");
            hadPreference = PlayerPrefs.HasKey("Dungeon.FullControl");
            savedPreference = PlayerPrefs.GetInt("Dungeon.FullControl");
            savedOverride = DungeonPreferences.FullControlOverride;
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) Object.DestroyImmediate(owner);
            Active.SetValue(null, null);
            DungeonPreferences.FullControlOverride = savedOverride;
            if (hadPreference) PlayerPrefs.SetInt("Dungeon.FullControl", savedPreference);
            else PlayerPrefs.DeleteKey("Dungeon.FullControl");
        }

        [TestCase(0, null)] [TestCase(1, null)]
        [TestCase(0, true)] [TestCase(1, true)]
        [TestCase(0, false)] [TestCase(1, false)]
        public void OwnershipWinsWithoutChangingStoredValues(int saved, bool? controlOverride)
        {
            PlayerPrefs.SetInt("Dungeon.FullControl", saved);
            DungeonPreferences.FullControlOverride = controlOverride;
            bool expected = controlOverride ?? saved != 0;
            Assert.That(DungeonPreferences.FullControl, Is.EqualTo(expected));
            owner = new GameObject("Preference test owner");
            var runner = owner.AddComponent<AutoplayRunner>();
            Active.SetValue(null, runner);
            Assert.That(DungeonPreferences.FullControl, Is.False);
            typeof(AutoplayRunner).GetProperty("Paused").SetValue(runner, true);
            typeof(AutoplayRunner).GetProperty("ReturnPromptOpen").SetValue(runner, true);
            typeof(AutoplayRunner).GetProperty("Report").SetValue(runner, new AutoplayReport { Outcome = "Completed" });
            Assert.That(DungeonPreferences.FullControl, Is.False, "Pause, prompt and completed inspection retain ownership.");
            Assert.That(PlayerPrefs.GetInt("Dungeon.FullControl"), Is.EqualTo(saved));
            Assert.That(DungeonPreferences.FullControlOverride, Is.EqualTo(controlOverride));
            typeof(AutoplayRunner).GetProperty("PlayerControlled").SetValue(runner, true);
            Assert.That(DungeonPreferences.FullControl, Is.EqualTo(expected), "Takeover restores normal preference resolution.");
            typeof(AutoplayRunner).GetProperty("PlayerControlled").SetValue(runner, false);
            Object.DestroyImmediate(owner);
            Assert.That(DungeonPreferences.FullControl, Is.EqualTo(expected), "Destroying the owner releases the temporary block.");
        }
    }
}
