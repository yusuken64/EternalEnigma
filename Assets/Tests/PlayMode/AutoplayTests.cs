#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    // Explicit: these session checks are opt-in, just like the full playthrough. No CI launch.
    [Explicit("Run manually with Tools > Eternal Enigma > Tests > Run Autoplay")]
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class AutoplayTests
    {
        private GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup()
        {
            if (!UnityEditor.SessionState.GetBool("EternalEnigma.Autoplay.ManualChecks",false))
                Assert.Ignore("Autoplay session checks require the manual Run Autoplay command.");
            harness = new GameTestHarness(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (harness == null) yield break;
            if (AutoplayRunner.Active != null)
            {
                var run = AutoplayRunner.Active; var report = run.Report; var path = Path.Combine(run.DirectoryPath,"report.json");
                report.ValidationOnly = true; report.EligibleForBalance = false;
                run.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null,"demo exit");
                File.WriteAllText(path,JsonUtility.ToJson(report,true));
            }
            yield return harness.Cleanup();
        }

        [UnityTest]
        public IEnumerator InputPromptCancelAndReturnPreserveOriginalSave()
        {
            yield return harness.LoadMainMenu(new TestScenario { Gold = 321 }.CreateSave());
            var original = Common.Instance.GameSaveData; string json = harness.Store.Json;
            Assert.That(Object.FindFirstObjectByType<WatchDemoMenu>().WatchButton, Is.Not.Null);
            Directory.CreateDirectory("Temp/AutoplayValidation");
            ScreenCapture.CaptureScreenshot("Temp/AutoplayValidation/main-menu.png");
            yield return null;
            var demoMenu = Object.FindFirstObjectByType<WatchDemoMenu>();
            demoMenu.WatchButton.onClick.Invoke();
            Assert.That(demoMenu.IsConfiguring,Is.True);
            ScreenCapture.CaptureScreenshot("Temp/AutoplayValidation/demo-options.png");
            yield return null;
            demoMenu.StartDemo();
            var run = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true,"demo's separate campaign");
            run.Report.ValidationOnly = true;
            Assert.That(Common.Instance.GameSaveData,Is.Not.SameAs(original));
            Assert.That(harness.Store.Json,Is.EqualTo(json));
            run.SetPaused(true);
            run.SetSpeed(4);
            Assert.That(Time.timeScale, Is.Zero, "Changing speed keeps the demo paused.");
            run.SetPaused(false);
            Assert.That(Time.timeScale, Is.EqualTo(4));
            run.SetSpeed(.5f);
            Assert.That(Time.timeScale, Is.EqualTo(.5f));
            run.SetPaused(true);
            using (var input = new TestInputScope())
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                var mouse = InputSystem.AddDevice<Mouse>();
                var pad = InputSystem.AddDevice<Gamepad>();
                var touch = InputSystem.AddDevice<Touchscreen>();
                try
                {
                    yield return new WaitForSecondsRealtime(.85f);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True);
                    ScreenCapture.CaptureScreenshot("Temp/AutoplayValidation/return-prompt.png");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    yield return null;
                    run.ConfirmReturn(false);
                    Assert.That(run.ReturnPromptOpen,Is.False);
                    Assert.That(run.Paused,Is.True,"Cancel restores the previous pause state.");
                    yield return new WaitForSecondsRealtime(.55f);
                    InputSystem.QueueStateEvent(mouse,new MouseState { delta = new Vector2(20,0) });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.False,"Pointer movement can reach playback controls.");
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = new Vector2(Screen.width-30, Screen.height-40), buttons = 1 });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.False,"Clicking the playback panel does not interrupt playback.");
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = new Vector2(50,50) });
                    yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = new Vector2(50,50), buttons = 1 });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True,"Clicking outside controls opens the return prompt.");
                    InputSystem.QueueStateEvent(mouse,new MouseState());
                    run.ConfirmReturn(false);
                    yield return new WaitForSecondsRealtime(.55f);
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True,"Controller input opens the return prompt.");
                    InputSystem.QueueStateEvent(pad,new GamepadState());
                    run.ConfirmReturn(false);
                    yield return new WaitForSecondsRealtime(.55f);
                    InputSystem.QueueStateEvent(touch,new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(50,50) });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True,"Touch opens the return prompt.");
                    InputSystem.QueueStateEvent(touch,new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended });
                    run.ConfirmReturn(false);
                    run.SetPaused(false); run.RequestReturn(); run.ConfirmReturn(false);
                    Assert.That(run.Paused,Is.False,"Cancel resumes a running demo.");
                    run.RequestReturn(); run.ConfirmReturn(true);
                    yield return harness.WaitUntil(() => AutoplayRunner.Active == null,"restore original session");
                    Assert.That(Common.Instance.GameSaveData,Is.SameAs(original));
                    Assert.That(harness.Store.Json,Is.EqualTo(json));
                    Assert.That(Common.Instance.GameSaveData.TownSaveData.Gold,Is.EqualTo(321));
                }
                finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(touch); }
            }
        }

        [UnityTest]
        public IEnumerator NormalModeAllowsDefeatAndWritesTuningReport()
        {
            yield return harness.LoadMainMenu(null);
            AutoplayRunner.WatchDemo(new AutoplayOptions { DebugPlaythrough = false, Godmode = true, InfiniteResources = true });
            var run = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Game>()?.IsReady == true,"normal demo dungeon");
            Assert.That(run.Report.EligibleForBalance,Is.True,"Unmodified normal runs are eligible for tuning.");
            run.Report.ValidationOnly = true;
            run.SetPaused(true);
            int before = harness.Ally.Vitals.HP;
            harness.Ally.Vitals.HP = 0;
            Assert.That(harness.Ally.Vitals.HP,Is.Zero);
            Assert.That(run.Report.DamageTaken,Is.EqualTo(before));
            Assert.That(AutoplayRunner.InfiniteResourcesFor(harness.Ally),Is.False);
            run.SetPaused(false);
            yield return harness.WaitUntil(() => !run.Running,"automatic defeat detection");
            var report = JsonUtility.FromJson<AutoplayReport>(File.ReadAllText(Path.Combine(run.DirectoryPath,"report.json")));
            Assert.That(report.Outcome,Is.EqualTo("Defeat"));
            Assert.That(report.EligibleForBalance,Is.False,"Synthetic lethal damage must be excluded from tuning data.");
            Assert.That(report.Party[0].HP,Is.Zero);
        }

        [UnityTest]
        public IEnumerator DebugProtectsOnlyPartyAndRestoresResourceRulesOnExit()
        {
            yield return harness.LoadDungeon(new TestScenario());
            string original = harness.Store.Json;
            AutoplayRunner.WatchDemo(new AutoplayOptions { DebugPlaythrough = true });
            var run = AutoplayRunner.Active; run.SetPaused(true);
            run.Report.ValidationOnly = true;
            harness.Ally.Vitals.HP = 0; harness.Ally.Vitals.SP = 0; harness.Ally.Vitals.Hunger = 0;
            Assert.That(harness.Ally.Vitals.HP,Is.EqualTo(harness.Ally.FinalStats.HPMax));
            Assert.That(harness.Ally.Vitals.SP,Is.EqualTo(harness.Ally.FinalStats.SPMax));
            Assert.That(harness.Ally.Vitals.Hunger,Is.EqualTo(harness.Ally.FinalStats.HungerMax));
            Assert.That(run.Report.EligibleForBalance,Is.False);
            int strength = harness.Ally.FinalStats.Strength;
            var dungeon = harness.Game.CurrentDungeon;
            var free = Enumerable.Range(0,dungeon.dungeonWidth).SelectMany(x => Enumerable.Range(0,dungeon.dungeonHeight).Select(y => new Vector3Int(x,y)))
                .First(p => dungeon.IsWalkable(p) && !harness.Game.AllCharacters.Any(c => c.ToBounds().Contains(p)));
            yield return harness.SpawnEnemy("Enemy_Slime",free);
            var enemy = harness.Game.Enemies.Last();
            AttackAction.GetAttackDamage(harness.Ally,enemy,out bool hit,out int damage);
            Assert.That(hit,Is.True); Assert.That(damage,Is.EqualTo(enemy.Vitals.HP));
            var ranged = new RangedAttackAction(harness.Ally,enemy,1,null).ExecuteImmediate(harness.Ally).OfType<TakeDamageAction>().Single();
            Assert.That(ranged.damage,Is.EqualTo(enemy.Vitals.HP));
            Assert.That(harness.Ally.FinalStats.Strength,Is.EqualTo(strength),"Infinite strength must not alter saved/base stats.");
            var stats = new Stats { HPMax = 10, SPMax = 4, HungerMax = 10 };
            var unrelated = new Vitals { LinkedStats = () => stats }; unrelated.HP = 0;
            Assert.That(unrelated.HP,Is.Zero,"Godmode cannot protect enemy/unrelated vitals.");
            run.Options.Godmode = false; run.Options.InfiniteResources = false;
            harness.Ally.Vitals.HP = 3; harness.Ally.Vitals.SP = 0;
            Assert.That(harness.Ally.Vitals.HP,Is.EqualTo(3));
            Assert.That(harness.Ally.Vitals.SP,Is.Zero);
            run.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null,"debug cleanup");
            Assert.That(harness.Store.Json,Is.EqualTo(original));
        }
    }
}
#endif
