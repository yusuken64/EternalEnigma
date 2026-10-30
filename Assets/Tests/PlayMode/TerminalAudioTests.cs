#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TerminalAudioTests
    {
        private GameTestHarness harness;
        private IDisposable preferences;
        private TestInputScope input;
        private AudioClip probe;
        private bool listenerPaused;
        private bool editorMuted;
        private float listenerVolume;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            preferences = GamePreferences.Isolate();
            input = new TestInputScope();
            listenerPaused = AudioListener.pause;
            listenerVolume = AudioListener.volume;
            editorMuted = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = false;
            AudioListener.pause = false;
            AudioListener.volume = 1;
            TerminalMode.SetRequested(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                TerminalMode.SetRequested(false);
                yield return harness.Cleanup();
            }
            finally
            {
                if (probe != null) Object.DestroyImmediate(probe);
                AudioListener.pause = listenerPaused;
                AudioListener.volume = listenerVolume;
                EditorUtility.audioMasterMute = editorMuted;
                input.Dispose();
                preferences.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AsciiSwitchChangesLiveAudioOnlyWhenTheViewSwitchesAndRestoresDryPlayback()
        {
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
            var audio = Common.Instance.AudioManager;
            AssertAllSourcesHaveOneFilter(audio);
            audio.StopAllCoroutines(); // The probe owns this disposable scene's music source.
            foreach (var source in Sources(audio)) source.Stop();
            var music = audio.MusicAudioSource;
            music.Stop();
            music.outputAudioMixerGroup = null;
            music.volume = 1;
            music.mute = false;
            music.pitch = 1;
            music.loop = true;
            int rate = AudioSettings.outputSampleRate;
            probe = AudioClip.Create("Terminal audio probe", rate * 8, 1, rate, false);
            var tone = Enumerable.Range(0, probe.samples)
                .Select(i => .5f * (float)Math.Sin(2 * Math.PI * 3001 * i / rate)).ToArray();
            Assert.That(probe.SetData(tone, 0), Is.True);
            music.clip = probe;
            music.Play();
            // Unity starts maintaining the output history on its first read.
            var initialOutput = new float[2048];
            float deadline = Time.realtimeSinceStartup + 5;
            while (Time.realtimeSinceStartup < deadline)
            {
                AudioListener.GetOutputData(initialOutput, 0);
                if (initialOutput.Any(s => Math.Abs(s) > .05f)) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(HeldFraction(), Is.LessThan(.1f), "Normal mode must pass the original signal.");
            float before = music.time;

            // The requested preference can change while presentation is covered.
            var cover = Common.Instance.ScreenTransition.BlockScreen;
            cover.SetActive(true);
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(TerminalMode.Effective, Is.False);
            Assert.That(HeldFraction(), Is.LessThan(.1f), "Audio must wait for the visible mode.");
            cover.SetActive(false);
            yield return harness.WaitUntil(() => TerminalMode.Effective, "ASCII audio mode");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(HeldFraction(), Is.GreaterThan(.5f), "The live DSP output must contain the 11 kHz sample hold.");
            Assert.That(music.clip, Is.SameAs(probe));
            Assert.That(music.time, Is.GreaterThan(before), "Switching presentation must not restart music.");
            before = music.time;

            TerminalMode.SetRequested(false);
            yield return harness.WaitUntil(() => !TerminalMode.Effective, "normal audio mode");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(HeldFraction(), Is.LessThan(.1f), "Disabling ASCII must restore the original output.");
            Assert.That(music.time, Is.GreaterThan(before));
            Assert.That(music.volume, Is.EqualTo(1));
            Assert.That(music.pitch, Is.EqualTo(1));
            AssertAllSourcesHaveOneFilter(audio);
        }

        [UnityTest]
        public IEnumerator SavedAsciiPreferenceAndSceneReloadRetainFiltersAndMixerRouting()
        {
            TerminalMode.SetRequested(true);
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
            var audio = Common.Instance.AudioManager;
            Assert.That(TerminalMode.Effective, Is.True);
            AssertAllSourcesHaveOneFilter(audio);
            var sources = Sources(audio);
            var filters = sources.Select(s => s.GetComponent<TerminalAudioFilter>()).ToArray();
            var groups = sources.Select(s => s.outputAudioMixerGroup).ToArray();
            var parameters = new[] { "MasterVolume", "MusicVolume", "EffectVolume", "UIVolume" };
            var mixer = audio.MusicAudioMixerGroup.audioMixer;
            var volumes = parameters.Select(p => { mixer.GetFloat(p, out float value); return value; }).ToArray();

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlocksInput, "reloaded main menu");
            Assert.That(Common.Instance.AudioManager, Is.SameAs(audio));
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(Sources(audio).Select(s => s.GetComponent<TerminalAudioFilter>()), Is.EqualTo(filters));
            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(TerminalMode.Effective, Is.False);
            Assert.That(sources.Select(s => s.outputAudioMixerGroup), Is.EqualTo(groups));
            Assert.That(parameters.Select(p => { mixer.GetFloat(p, out float value); return value; }), Is.EqualTo(volumes));
            AssertAllSourcesHaveOneFilter(audio);
        }

        private static AudioSource[] Sources(AudioManager audio) =>
            new[] { audio.MusicAudioSource }.Concat(audio.EffectAudioSources).Concat(audio.UIAudioSources).ToArray();

        private static void AssertAllSourcesHaveOneFilter(AudioManager audio)
        {
            foreach (var source in Sources(audio))
            {
                Assert.That(source, Is.Not.Null);
                Assert.That(source.GetComponent<TerminalAudioFilter>(), Is.Not.Null, source.name);
                TerminalAudioFilter.Ensure(source);
                Assert.That(source.GetComponents<TerminalAudioFilter>().Length, Is.EqualTo(1), source.name);
                Assert.That(source.GetComponent<TerminalAudioFilter>().enabled, Is.True);
            }
        }

        private static float HeldFraction()
        {
            var output = new float[2048];
            // AudioSource.GetOutputData observes the signal before source filters.
            AudioListener.GetOutputData(output, 0);
            Assert.That(output.Max(Math.Abs), Is.GreaterThan(.05f), "The probe must produce actual audio.");
            int held = 0;
            for (int i = 1; i < output.Length; i++)
                if (Math.Abs(output[i] - output[i - 1]) < .00001f) held++;
            return (float)held / (output.Length - 1);
        }
    }
}
#endif
