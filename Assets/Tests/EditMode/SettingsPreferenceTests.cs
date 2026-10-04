using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public class SettingsPreferenceTests
    {
        private readonly string[] keys = { DisplayPreferences.ModeKey, DisplayPreferences.WidthKey, DisplayPreferences.HeightKey };
        private Dictionary<string, int> saved;
        private sealed class Backend : IDisplayBackend
        {
            public Vector2Int Size { get; set; } = new(1366, 768);
            public Vector2Int Desktop => new(1920, 1080);
            public FullScreenMode Mode { get; set; } = FullScreenMode.Windowed;
            public IEnumerable<Vector2Int> Resolutions { get; set; } = new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1920,1080) };
            public int Calls;
            public void Apply(Vector2Int size, FullScreenMode mode) { Size = size; Mode = mode; Calls++; }
        }
        [SetUp] public void SetUp()
        {
            saved = keys.Where(PlayerPrefs.HasKey).ToDictionary(k => k, PlayerPrefs.GetInt);
            foreach (var key in keys) PlayerPrefs.DeleteKey(key);
        }
        [TearDown] public void TearDown()
        {
            foreach (var key in keys) { if (saved.TryGetValue(key, out var value)) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key); }
            PlayerPrefs.Save();
        }
        [Test] public void NoPreferencesLeaveCurrentConfigurationAlone()
        {
            var backend = new Backend(); new DisplayPreferences(backend).Restore(); Assert.That(backend.Calls, Is.Zero);
        }
        [Test] public void ChoicesDeduplicateSortAndIncludeCurrentWindowSize()
        {
            var preferences = new DisplayPreferences(new Backend());
            Assert.That(preferences.Choices(FullScreenMode.Windowed), Is.EqualTo(new[] { new Vector2Int(1280,720), new Vector2Int(1366,768), new Vector2Int(1920,1080) }));
            Assert.That(preferences.Choices(FullScreenMode.ExclusiveFullScreen).Count, Is.EqualTo(2));
        }
        [Test] public void EmptyEnumerationUsesCurrentDimensions()
        {
            var backend = new Backend { Resolutions = new Vector2Int[0] };
            Assert.That(new DisplayPreferences(backend).Choices(FullScreenMode.ExclusiveFullScreen), Is.EqualTo(new[] { backend.Size }));
        }
        [TestCase(FullScreenMode.ExclusiveFullScreen)]
        [TestCase(FullScreenMode.FullScreenWindow)]
        public void UnavailableSavedFullscreenFallsBackToDesktop(FullScreenMode mode)
        {
            var backend = new Backend(); var preferences = new DisplayPreferences(backend);
            preferences.Commit(new Vector2Int(1024,768), mode);
            preferences.Restore(); Assert.That(backend.Size, Is.EqualTo(backend.Desktop)); Assert.That(backend.Mode, Is.EqualTo(mode));
        }
        [Test] public void WindowedPreferencesSurviveANewService()
        {
            new DisplayPreferences(new Backend()).Commit(new Vector2Int(1024,768), FullScreenMode.Windowed);
            var backend = new Backend(); new DisplayPreferences(backend).Restore();
            Assert.That(backend.Size, Is.EqualTo(new Vector2Int(1024,768))); Assert.That(backend.Mode, Is.EqualTo(FullScreenMode.Windowed));
        }
        [Test] public void VolumeZeroUsesFiniteMuteFloorAndPercentagesRoundTrip()
        {
            Assert.That(AudioPreferences.ToDecibels(0), Is.EqualTo(-80));
            Assert.That(AudioPreferences.ToLevel(-80), Is.Zero);
            Assert.That(AudioPreferences.ToLevel(AudioPreferences.ToDecibels(.37f)), Is.EqualTo(.37f).Within(.0001f));
        }
    }
}
