#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonFramePerformanceTests
    {
        [Serializable] private sealed class Counter { public string name, unit; public double average, maximum; }
        [Serializable] private sealed class Sample { public string condition; public int frames; public double fps, averageFrameMs, maximumFrameMs; public Counter[] counters; public FinalePerformance.Sample[] functions; }
        [Serializable] private sealed class Report { public string unity, gpu, api; public int width, height; public Sample[] samples; }
        private GameTestHarness harness;
        private TestInputScope inputs;
        private bool? oldControl;
        private DungeonAnimationMode? oldAnimation;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope(); oldControl = DungeonPreferences.FullControlOverride; oldAnimation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.FullControlOverride = false; DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Avery" } });
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { yield return harness.Cleanup(); }
            finally { inputs?.Dispose(); DungeonPreferences.FullControlOverride = oldControl; DungeonPreferences.AnimationOverride = oldAnimation; }
        }

        [UnityTest] public IEnumerator HudTextRefreshesOnChangesAndDoesNoIdleFormatting()
        {
            yield return new WaitForSecondsRealtime(.3f);
            var ally = harness.Ally;
            var card = harness.Game.CharacterStatsDisplays.Single(c => c.Character == ally);
            var resources = harness.Game.GetComponentInChildren<ResourceHUD>();
            var feed = harness.Game.GetComponentInChildren<GameMessages>();
            var feedText = feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content.GetComponent<TMPro.TMP_Text>();
            FinalePerformance.Sample[] samples;
            FinalePerformance.Begin();
            try
            {
                for (int frame = 0; frame < 30; frame++) yield return null;
                // Empty action boundaries must not rebuild the existing event history.
                GameMessages.BeginTurn(); GameMessages.FinishAction();
                yield return null; yield return null;
            }
            finally { samples = FinalePerformance.Finish(); }
            Assert.That(samples.Where(s => s.Name is "Gameplay text formatting" or "Event feed layout").Sum(s => s.Calls),
                Is.Zero, "An idle HUD must not format strings or rebuild the event feed.");

            string previous = card.HpDisplay.ValueText.text;
            ally.Vitals.HP -= 5;
            yield return null; yield return null;
            Assert.That(card.HpDisplay.ValueText.text, Is.EqualTo(previous), "The HUD follows playback's displayed state, not future simulation results.");
            ally.SyncDisplayedStats();
            yield return null; yield return null;
            Assert.That(card.HpDisplay.ValueText.text, Does.Contain($"{ally.Vitals.HP}/{ally.FinalStats.HPMax}"));
            ally.DisplayedVitals.Mana = 0; ally.DisplayedVitals.Hunger = 3; ally.DisplayedVitals.Level++;
            yield return null; yield return null;
            Assert.That(card.ManaDisplay.ValueText.text, Does.Contain($"0/{ally.DisplayedStats.ManaMax}"));
            Assert.That(card.HungerDisplay.ValueText.text, Does.Contain($"3/{ally.DisplayedStats.HungerMax}"));
            Assert.That(card.NameText.text, Does.Contain($"Lv {ally.DisplayedVitals.Level}"));

            var player = harness.Game.PlayerController;
            player.Gold += 7;
            var item = Common.Instance.ItemManager.StartingItems.First().AsInventoryItem(null);
            player.Inventory.Add(item);
            SmallKeys.Add();
            yield return null; yield return null;
            Assert.That(resources.GoldText.text, Does.Contain(player.Gold.ToString("N0")).And.Contain("Keys"));
            Assert.That(resources.BagText.text, Does.Contain($"{player.Inventory.Count()}/{player.Inventory.MaxItems}"));
            player.Inventory.Remove(item); Assert.That(SmallKeys.TrySpend(), Is.True);
            yield return null; yield return null;
            Assert.That(resources.BagText.text, Does.Contain($"{player.Inventory.Count()}/{player.Inventory.MaxItems}"));
            Assert.That(resources.GoldText.text, Does.Not.Contain("Keys"));

            FinalePerformance.Begin();
            try
            {
                GameMessages.Post("HUD change one"); GameMessages.Post("HUD change two"); GameMessages.FinishAction();
                yield return null; yield return null;
            }
            finally { samples = FinalePerformance.Finish(); }
            Assert.That(feedText.text, Does.Contain("HUD change two"));
            Assert.That(samples.Single(s => s.Name == "Event feed layout").Calls, Is.EqualTo(1), "Messages arriving together need only one layout.");
            Common.Instance.GlobalSettings.ShowDialog(); yield return null; yield return null;
            Assert.That(resources.GetComponent<Canvas>().enabled, Is.False);
            Common.Instance.GlobalSettings.Exit_Clicked(); yield return null; yield return null;
            Assert.That(resources.GetComponent<Canvas>().enabled, Is.True);
        }

        [UnityTest, Timeout(120000)] public IEnumerator IdentifyFrameBottleneckWithRenderingAblations()
        {
            int target = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled).ToArray();
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l => (light: l, shadows: l.shadows)).ToArray();
            var shadows = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                .Select(r => (renderer: r, mode: r.shadowCastingMode)).ToArray();
            var recorders = new List<(ProfilerRecorder recorder, string name, string unit)>();
            var samples = new List<Sample>();
            Directory.CreateDirectory("Temp/MovementAudit");
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions = handles.Select(ProfilerRecorderHandle.GetDescription).ToArray();
            File.WriteAllLines("Temp/MovementAudit/available-profiler-counters.txt", descriptions.Select(d => $"{d.Category.Name}: {d.Name} [{d.UnitType}]"));
            var names = new HashSet<string> { "Main Thread", "Render Thread", "PlayerLoop", "EditorLoop", "CPU Total Frame Time", "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "GPU Frame Time", "Gfx.WaitForPresentOnGfxThread", "Gfx.PresentFrame", "Gfx.WaitForCommands", "Gfx.WaitForGfxCommandsFromMainThread", "Gfx.WaitForRenderThread", "GfxDevice.WaitForRenderThread", "WaitForTargetFPS", "Camera.Render", "RenderPipelineManager.DoRenderLoop_Internal", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "Canvas.BuildBatch", "Canvas.SendWillRenderCanvases", "GC.Collect", "Draw Calls Count", "Batches Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count", "Visible Skinned Meshes Count", "Animators.Update" };
            try
            {
                Application.targetFrameRate = 120; QualitySettings.vSyncCount = 0;
                foreach (var description in descriptions.Where(d => names.Contains(d.Name)))
                {
                    var recorder = ProfilerRecorder.StartNew(description.Category, description.Name, 240);
                    if (recorder.Valid) recorders.Add((recorder, description.Name, description.UnitType.ToString()));
                    else recorder.Dispose();
                }
                foreach (string condition in new[] { "baseline", "no_world_camera", "no_ui", "no_shadows", "no_world_camera_or_ui", "restored_baseline" })
                {
                    camera.enabled = condition is not "no_world_camera" and not "no_world_camera_or_ui";
                    foreach (var canvas in canvases) if (canvas != null) canvas.enabled = condition is not "no_ui" and not "no_world_camera_or_ui";
                    foreach (var pair in lights) if (pair.light != null) pair.light.shadows = condition == "no_shadows" ? LightShadows.None : pair.shadows;
                    foreach (var pair in shadows) if (pair.renderer != null) pair.renderer.shadowCastingMode = condition == "no_shadows" ? UnityEngine.Rendering.ShadowCastingMode.Off : pair.mode;
                    yield return new WaitForSecondsRealtime(.5f);
                    foreach (var entry in recorders) { var recorder = entry.recorder; recorder.Reset(); recorder.Start(); }
                    FinalePerformance.Begin();
                    int frames = 0; double maxFrame = 0, start = Time.realtimeSinceStartupAsDouble;
                    while (Time.realtimeSinceStartupAsDouble - start < 2.5)
                    {
                        yield return null; frames++; maxFrame = Math.Max(maxFrame, Time.unscaledDeltaTime * 1000);
                    }
                    double seconds = Time.realtimeSinceStartupAsDouble - start;
                    samples.Add(new Sample { condition = condition, frames = frames, fps = frames / seconds,
                        functions = FinalePerformance.Finish(),
                        averageFrameMs = seconds * 1000 / frames, maximumFrameMs = maxFrame,
                        counters = recorders.Select(entry => {
                            var values = entry.recorder.ToArray().Select(s => (double)s.Value).ToArray();
                            return new Counter { name = entry.name, unit = entry.unit, average = values.Length == 0 ? 0 : values.Average(), maximum = values.Length == 0 ? 0 : values.Max() };
                        }).ToArray() });
                }
                var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                    api = SystemInfo.graphicsDeviceType.ToString(), width = Screen.width, height = Screen.height, samples = samples.ToArray() };
                File.WriteAllText("Temp/MovementAudit/frame-profile.json", JsonUtility.ToJson(report, true));
                Debug.Log("FPS AUDIT: " + string.Join("; ", samples.Select(s => $"{s.condition}: {s.fps:F1}fps")));
                Assert.That(recorders.Count, Is.GreaterThan(0));
            }
            finally
            {
                FinalePerformance.Enabled = false;
                foreach (var entry in recorders) entry.recorder.Dispose();
                camera.enabled = true;
                foreach (var canvas in canvases) if (canvas != null) canvas.enabled = true;
                foreach (var pair in lights) if (pair.light != null) pair.light.shadows = pair.shadows;
                foreach (var pair in shadows) if (pair.renderer != null) pair.renderer.shadowCastingMode = pair.mode;
                Application.targetFrameRate = target; QualitySettings.vSyncCount = vsync;
            }
        }
    }
}
#endif
