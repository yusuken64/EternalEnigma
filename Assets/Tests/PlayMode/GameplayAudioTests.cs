#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class GameplayAudioTests
    {
        private GameTestHarness harness;
        private AudioManager audio;
        private DungeonAnimationMode? previousAnimation;
        private readonly List<(SoundCue cue, AudioClip clip, AudioMixerGroup group)> heard = new();
        private readonly List<AudioClip> played = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousAnimation = DungeonPreferences.AnimationOverride;
            harness = new GameTestHarness();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (audio != null) { audio.CuePlayed -= Heard; audio.SoundPlayed -= Played; }
            try { yield return harness.Cleanup(); }
            finally { DungeonPreferences.AnimationOverride = previousAnimation; heard.Clear(); played.Clear(); }
        }

        private void Listen()
        {
            audio = AudioManager.Instance;
            Assert.That(audio, Is.Not.Null);
            audio.CuePlayed += Heard;
            audio.SoundPlayed += Played;
        }
        private void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group) => heard.Add((cue, clip, group));
        private void Played(AudioClip clip, AudioMixerGroup _) => played.Add(clip);

        [UnityTest]
        public IEnumerator AuthoredActionCatalogIsCompleteAndRoutesToIndependentMixerGroups()
        {
            yield return harness.LoadCommon();
            Listen();
            Assert.That(audio.UIAudioMixerGroup, Is.Not.Null);
            Assert.That(audio.EffectAudioMixerGroup, Is.Not.Null);
            Assert.That(audio.MusicAudioMixerGroup, Is.Not.Null);
            Assert.That(new[] { audio.UIAudioMixerGroup, audio.EffectAudioMixerGroup, audio.MusicAudioMixerGroup }.Distinct().Count(), Is.EqualTo(3));
            var ui = new HashSet<SoundCue> { SoundCue.Navigate, SoundCue.Confirm, SoundCue.Cancel, SoundCue.Denied, SoundCue.MenuOpen, SoundCue.MenuClose };
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                yield return null; // Separate UI activations, as real input does.
                heard.Clear();
                Assert.That(audio.SoundEffects.For(cue), Is.Not.Null, cue + " has no authored clip.");
                audio.PlayCue(cue);
                Assert.That(heard.Count, Is.EqualTo(1), cue + " did not play.");
                Assert.That(heard[0].group, Is.SameAs(ui.Contains(cue) ? audio.UIAudioMixerGroup : audio.EffectAudioMixerGroup), cue + " used the wrong channel.");
            }
            Assert.That(audio.SoundEffects.Purchase, Is.Not.SameAs(audio.SoundEffects.Sell), "Trading needs distinct purchase and sale cues.");
            Assert.That(audio.SoundEffects.GateUnlock, Is.Not.SameAs(audio.SoundEffects.LevelUp), "Unlock glissandos and level-up dings need distinct cues.");
        }

        [UnityTest]
        public IEnumerator DamageMissBlockAndTrapAudioSurviveDisabledAnimations()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            yield return harness.LoadDungeon(new TestScenario());
            Listen();
            var hero = harness.Ally;
            int hp = hero.Vitals.HP;
            var hit = new TakeDamageAction(hero, hero, 1, false) { Environmental = true };
            ResolveAndPlay(hit);
            hit.PlayQueuedSounds(hero);
            yield return hit.ExecuteRoutine(hero, true);
            Assert.That(hero.Vitals.HP, Is.EqualTo(hp - 1));
            Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Damage }), "Damage must sound once even when visual playback is skipped or revisited.");

            heard.Clear();
            hp = hero.Vitals.HP;
            ResolveAndPlay(new TakeDamageAction(hero, hero, 1, false, true) { Environmental = true });
            Assert.That(hero.Vitals.HP, Is.EqualTo(hp));
            Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Miss }));
            heard.Clear();
            ResolveAndPlay(new TakeDamageAction(hero, hero, 0, false) { Environmental = true });
            Assert.That(hero.Vitals.HP, Is.EqualTo(hp));
            Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Block }));

            heard.Clear();
            var trap = Object.Instantiate(FantasyTrap.PrefabFor((int)FantasyTrapKind.Arrow), harness.Game.transform);
            trap.Position = hero.TilemapPosition;
            trap.ActivationChance = 1;
            var consequence = new TrapResolutionAction(trap, hero, Vector3Int.right);
            consequence.ExecuteImmediate(hero);
            consequence.PlayQueuedSounds(hero);
            yield return consequence.ExecuteRoutine(hero, true);
            Assert.That(hero.Vitals.HP, Is.LessThan(hp));
            Assert.That(heard.Count(e => e.cue == SoundCue.Trap), Is.EqualTo(1));
            Assert.That(heard.Count(e => e.cue == SoundCue.Damage), Is.EqualTo(1));
            Assert.That(heard.All(e => e.group == audio.EffectAudioMixerGroup), Is.True);
        }

        [UnityTest]
        public IEnumerator EnemyAudioRequiresSightAndCameraButPlayerDamageAlwaysSounds()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            yield return harness.LoadDungeon(new TestScenario());
            Listen();
            var game = harness.Game;
            var dungeon = game.CurrentDungeon;
            var remote = new Vector3Int(dungeon.dungeonWidth + 10, dungeon.dungeonHeight + 10);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab");
            var enemy = Object.Instantiate(prefab, game.transform);
            enemy.InitialzeVitalsFromStats();
            enemy.SyncDisplayedStats();
            enemy.SetPosition(remote);
            game.Enemies.Add(enemy);
            var camera = game.PlayerController.CameraController.Camera;
            var cameraPosition = camera.transform.position;
            var cameraRotation = camera.transform.rotation;
            var playbackSight = game.PlaybackVisibleTiles.ToArray();
            try
            {
                // Put the enemy in the camera, but outside every party member's sight.
                camera.transform.position = dungeon.CellToWorld(remote) + Vector3.back * 20;
                camera.transform.rotation = Quaternion.identity;
                game.PlaybackVisibleTiles.Clear();
                game.RefreshSight();
                Assert.That(game.PartyVisibleTiles.Contains(remote), Is.False);
                int hp = enemy.Vitals.HP;
                ResolveAndPlay(new TakeDamageAction(enemy, enemy, 1, false) { Environmental = true }, enemy);
                Assert.That(enemy.Vitals.HP, Is.EqualTo(hp - 1), "Hidden actions still resolve gameplay.");
                Assert.That(heard, Is.Empty, "An enemy inside fog must not reveal itself through sound.");

                // Revealing the tile alone is insufficient when it is outside the camera.
                game.PlaybackVisibleTiles.Add(remote);
                camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
                ResolveAndPlay(new TakeDamageAction(enemy, enemy, 1, false) { Environmental = true }, enemy);
                Assert.That(heard, Is.Empty, "Offscreen enemy activity must remain silent.");

                camera.transform.rotation = Quaternion.identity;
                ResolveAndPlay(new TakeDamageAction(enemy, enemy, 1, false) { Environmental = true }, enemy);
                Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Damage }));
                Assert.That(heard[0].group, Is.SameAs(audio.EffectAudioMixerGroup));

                heard.Clear();
                game.PlaybackVisibleTiles.Clear();
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                int playerHp = harness.Ally.Vitals.HP;
                ResolveAndPlay(new TakeDamageAction(enemy, harness.Ally, 1, false) { Environmental = true }, enemy);
                Assert.That(harness.Ally.Vitals.HP, Is.EqualTo(playerHp - 1));
                Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Damage }), "Damage to the controlled hero must sound even when its source is unseen.");
            }
            finally
            {
                game.PlaybackVisibleTiles.Clear();
                game.PlaybackVisibleTiles.UnionWith(playbackSight);
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                game.Enemies.Remove(enemy);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ActualLevelGainsCoalesceAndOnlyCommandedWaitingSounds()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            yield return harness.LoadDungeon(new TestScenario());
            Listen();
            var hero = harness.Ally;
            int level = hero.Vitals.Level;
            int award = LevelSystem.ExperienceAtLevel(level + 2) - hero.Vitals.Exp;
            ResolveAndPlay(new AddXPAction(hero, award));
            Assert.That(hero.Vitals.Level, Is.EqualTo(level + 2));
            Assert.That(heard.Count(e => e.cue == SoundCue.LevelUp), Is.EqualTo(1), "Simultaneous level gains should produce one ding sequence.");
            yield return new WaitForSecondsRealtime(.2f);
            heard.Clear();
            ResolveAndPlay(new AddXPAction(hero, 0));
            Assert.That(heard, Is.Empty, "Reading or refreshing an existing level must not ding.");

            var automatic = new WaitAction();
            automatic.SetPlaybackContext(new DungeonActionPlayback(false, hero, hero));
            ResolveAndPlay(automatic);
            Assert.That(heard, Is.Empty, "Automatic turn bookkeeping must not tick.");
            var commanded = new WaitAction();
            commanded.SetPlaybackContext(new DungeonActionPlayback(true, hero, hero));
            ResolveAndPlay(commanded);
            Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Wait }));
            Assert.That(heard[0].group, Is.SameAs(audio.EffectAudioMixerGroup));
        }

        [UnityTest]
        public IEnumerator WalkingIsSilentAndDoorUnlockSoundsOnlyOnce()
        {
            yield return harness.LoadDungeon(new TestScenario());
            Listen();
            var hero = harness.Ally;
            var dungeon = harness.Game.CurrentDungeon;
            var origin = hero.TilemapPosition;
            var destination = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left }
                .Select(offset => origin + offset).First(cell => dungeon.CanWalkTo(origin, cell) && dungeon.GetInteractable(cell) == null &&
                    harness.Game.AllCharacters.All(c => c == hero || c.TilemapPosition != cell));
            foreach (var mode in new[] { DungeonAnimationMode.Normal, DungeonAnimationMode.NoAnimations })
            {
                DungeonPreferences.AnimationOverride = mode;
                var from = hero.TilemapPosition;
                var to = from == origin ? destination : origin;
                var movement = new MovementAction(hero, from, to);
                Assert.That(movement.IsValid(hero), Is.True);
                ResolveAndPlay(movement);
                yield return movement.ExecuteRoutine(hero, mode == DungeonAnimationMode.NoAnimations);
                Assert.That(hero.TilemapPosition, Is.EqualTo(to));
                Assert.That(played, Is.Empty, "Ordinary walking must not play any audio, including legacy footsteps.");
            }

            var swing = new AttackAction(hero, hero.TilemapPosition, destination);
            int hp = hero.Vitals.HP;
            ResolveAndPlay(swing);
            yield return swing.ExecuteRoutine(hero, true);
            Assert.That(hero.Vitals.HP, Is.EqualTo(hp));
            Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Attack }), "A deliberate attack into empty space still needs its swing sound.");
            heard.Clear();
            var door = DungeonProp.Create(dungeon, new DungeonScenery(destination.ToGridPoint(), DungeonSceneryKind.Door, 1, 42, SceneryReward.None), null);
            harness.Game.RefreshSight();
            Assert.That(harness.Game.PartyVisibleTiles.Contains(destination), Is.True);
            door.Unlock();
            door.Unlock();
            Assert.That(door.Opened, Is.True);
            Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.DoorOpen }));
            Assert.That(heard[0].group, Is.SameAs(audio.EffectAudioMixerGroup));
        }

        private void ResolveAndPlay(GameAction action, Character actor = null)
        {
            actor ??= harness.Ally;
            var consequences = action.ExecuteImmediate(actor);
            action.RecordOutcome(actor);
            action.UpdateDisplayedStats();
            action.PlayQueuedSounds(actor);
            foreach (var consequence in consequences) ResolveAndPlay(consequence, actor);
        }
    }
}
#endif
