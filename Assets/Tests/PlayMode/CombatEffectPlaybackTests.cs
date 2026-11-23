#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using NUnit.Framework;
using TWC;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    public sealed class CombatEffectPlaybackTests
    {
        GameObject root, particle;
        Game game;
        Ally actor;
        TileWorldCreatorAsset map;
        CombatEffectProfile profile;
        StatusVisualProfile aura;
        AudioManager previousAudio;
        [SetUp] public void Setup()
        {
            previousAudio = AudioManager.Instance;
            root = new GameObject("Combat effects fixture");
            game = root.AddComponent<Game>(); game.enabled = false;
            game.Allies = new(); game.Enemies = new(); game.DeadUnits = new(); game.DownedAllies = new(); game.StatusEffectPrefabs = new();
            var creator = root.AddComponent<TileWorldCreator>(); creator.enabled = false;
            map = ScriptableObject.CreateInstance<TileWorldCreatorAsset>(); map.cellSize = 2.5f; creator.twcAsset = map;
            game.CurrentDungeon = root.AddComponent<TileWorldDungeon>(); game.CurrentDungeon.enabled = false; game.CurrentDungeon.Interactables = new();
            game.CurrentDungeon.Setup(creator, DungeonFloorGenerator.Generate(new DungeonFloorOptions(42)));
            var go = new GameObject("Visual actor"); go.transform.SetParent(root.transform);
            actor = go.AddComponent<Ally>(); actor.enabled = false; actor.Equipment = go.AddComponent<Equipment>(); actor.Skills = new();
            actor.VisualParent = new GameObject("Visual"); actor.VisualParent.transform.SetParent(go.transform);
            actor.BaseStats = new Stats { HPMax = 100 }; actor.Vitals = new Vitals(); actor.Vitals.HP = 100;
            actor.DisplayedVitals = new Vitals(); actor.DisplayedStats.Sync(actor.FinalStats); actor.DisplayedVitals.HP = 100;
            actor.TilemapPosition = game.CurrentDungeon.GetStartPosition(); actor.transform.position = game.CurrentDungeon.CellToWorld(actor.TilemapPosition); game.Allies.Add(actor);
            particle = new GameObject("Test particles", typeof(ParticleSystem)); particle.SetActive(false);
            profile = ScriptableObject.CreateInstance<CombatEffectProfile>();
            profile.CastSeconds = .01f; profile.ImpactSeconds = .01f; profile.FlightSeconds = new Vector2(.03f, .03f);
            profile.Projectile.Prefab = particle; profile.Impact.Prefab = particle; profile.Area.Prefab = particle;
            aura = ScriptableObject.CreateInstance<StatusVisualProfile>(); aura.Aura.Prefab = particle;
            var feedObject = new GameObject("Test event feed"); feedObject.SetActive(false); feedObject.transform.SetParent(root.transform);
            feedObject.AddComponent<GameMessages>().AuthorLayout(true); feedObject.SetActive(true);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Object.Destroy(root); Object.Destroy(particle); Object.Destroy(map); Object.Destroy(profile); Object.Destroy(aura); yield return null;
            typeof(SingletonMonoBehaviour<AudioManager>).GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, previousAudio);
        }
        sealed class EmptyAction : GameAction
        {
            internal override bool IsValid(Character c) => true;
            internal override List<GameAction> ExecuteImmediate(Character c) => new();
            internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false) { yield break; }
        }

        AudioManager AudioFixture(out List<AudioClip> played, out AudioClip start, out AudioClip release)
        {
            // The editor's starting scene may already own an audio manager.
            typeof(SingletonMonoBehaviour<AudioManager>).GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, null);
            var go = new GameObject("Ability audio fixture"); go.transform.SetParent(root.transform);
            var audio = go.AddComponent<AudioManager>(); audio.enabled = false;
            audio.SoundEffects = go.AddComponent<SoundEffects>();
            audio.EffectAudioSources = new() { go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>() };
            start = AudioClip.Create("Universal cast start", 2205, 1, 22050, false);
            release = AudioClip.Create("Elemental release", 2205, 1, 22050, false);
            audio.SoundEffects.AbilityCast = start;
            var observed = new List<AudioClip>(); played = observed;
            audio.SoundPlayed += (clip, group) => observed.Add(clip);
            return audio;
        }

        Skill AudioSkill(AudioClip release, int charge = 0)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            skill.SkillName = "Audio test spell"; skill.Targeting = SkillTargeting.Self;
            skill.TargetSelector = new TargetSelector { Team = TargetTeam.Self, Area = TargetArea.Self };
            skill.CastSound = release; skill.CastTime = charge; actor.Skills.Add(skill);
            return skill;
        }

        IEnumerator ResolveAndReplay(GameAction action)
        {
            action.SetPlaybackContext(DungeonAnimationMode.NoAnimations, true, actor, actor);
            action.Visuals.Begin(actor);
            var children = action.ExecuteImmediate(actor);
            action.Visuals.End(action, actor, children);
            yield return action.Visuals.Play(action, actor, true);
        }

        [UnityTest] public IEnumerator ImpactOnlySkillsWaitForResolutionBeforeSpawningParticles()
        {
            var skill = AudioSkill(null);
            profile.Projectile.Prefab = null; profile.Area.Prefab = null;
            skill.VisualProfile = profile;
            var player = CombatEffectPlayer.Get();
            try
            {
                var action = new SkillAction(actor, skill, actor);
                action.SetPlaybackContext(DungeonAnimationMode.Normal, true, actor, actor);
                action.Visuals.Begin(actor);
                var children = action.ExecuteImmediate(actor);
                action.Visuals.End(action, actor, children);
                yield return action.Visuals.Play(action, actor, false);
                Assert.That(player.ActiveCount, Is.Zero, "A physical skill must not also emit a generic activation burst.");
                yield return player.Deliver(action.Visuals.Sequence, actor.transform.position, true);
                Assert.That(player.ActiveCount, Is.EqualTo(1), "Only the resolved hit produces attack particles.");
            }
            finally { actor.Skills.Remove(skill); Object.Destroy(skill); }
        }

        [UnityTest] public IEnumerator SlashesFaceTheCameraFromAllEightCastDirections()
        {
            profile.Projectile.Prefab = null; profile.Area.Prefab = null;
            profile.Impact.AlignWithCastDirection = true;
            profile.Impact.Rotation = new Vector3(-90, 0, 0);
            var rig = new GameObject("Angled dungeon camera");
            rig.transform.SetParent(root.transform);
            var view = rig.AddComponent<Camera>();
            view.enabled = false;
            view.transform.rotation = Quaternion.LookRotation(new Vector3(0, 12, 14), Vector3.up);
            game.PlayerController = rig.AddComponent<PlayerController>();
            game.PlayerController.enabled = false;
            var cameraController = rig.AddComponent<CameraController>();
            cameraController.enabled = false;
            cameraController.Camera = view;
            game.PlayerController.CameraController = cameraController;
            var player = CombatEffectPlayer.Get();
            foreach (var direction in new[] { Vector3.up, Vector3.right, Vector3.down, Vector3.left,
                new Vector3(1,1), new Vector3(1,-1), new Vector3(-1,-1), new Vector3(-1,1) })
            {
                var sequence = new CombatVisualSequence { Profile = profile, Direction = direction };
                actor.SetFacing(Facing.Down); // Playback uses the snapshot, not future actor facing.
                yield return player.Deliver(sequence, actor.transform.position, true);
                var effect = player.GetComponentsInChildren<ParticleSystem>().Single();
                var camera = view.transform;
                var projected = Vector3.ProjectOnPlane(direction, camera.forward).normalized;
                Assert.That(Vector3.Dot(effect.transform.forward, projected), Is.GreaterThan(.999f));
                Assert.That(Vector3.Dot(effect.transform.up, -camera.forward), Is.GreaterThan(.999f));
                player.Clear(); yield return null;
            }
        }

        [UnityTest] public IEnumerator InstantSkillHasOneUniversalStartAndOneReleaseEvenWithAnimationsSkipped()
        {
            AudioFixture(out var played, out var start, out var release);
            var skill = AudioSkill(release);
            try
            {
                var action = new SkillAction(actor, skill, actor);
                yield return ResolveAndReplay(action);
                CollectionAssert.AreEqual(new[] { start, release }, played);
                yield return action.Visuals.Play(action, actor, true);
                Assert.That(played.Count, Is.EqualTo(2), "Replaying the same action must not duplicate its audio.");
                skill.ManaCost = 1;
                yield return ResolveAndReplay(new SkillAction(actor, skill, actor));
                Assert.That(played.Count, Is.EqualTo(2), "Invalid casts do not start or release.");
            }
            finally { actor.Skills.Remove(skill); Object.Destroy(skill); Object.Destroy(start); Object.Destroy(release); }
        }

        [UnityTest] public IEnumerator ChargedSkillReleasesOnceAndInterruptedChargeNeverReleases()
        {
            AudioFixture(out var played, out var start, out var release);
            var skill = AudioSkill(release, 1);
            try
            {
                yield return ResolveAndReplay(new SkillAction(actor, skill, actor));
                CollectionAssert.AreEqual(new[] { start }, played);
                var releaseAction = new AdvanceCastAction().ExecuteImmediate(actor).Single();
                yield return ResolveAndReplay(releaseAction);
                CollectionAssert.AreEqual(new[] { start, release }, played);
                yield return ResolveAndReplay(new SkillAction(actor, skill, actor));
                PendingCast.Cancel(actor, "test interruption");
                Assert.That(new AdvanceCastAction().ExecuteImmediate(actor), Is.Empty);
                CollectionAssert.AreEqual(new[] { start, release, start }, played);
            }
            finally { actor.Skills.Remove(skill); Object.Destroy(skill); Object.Destroy(start); Object.Destroy(release); }
        }

        [UnityTest] public IEnumerator LegacyEnemySpellRetainsItsReleaseClipAndFizzleIsSilent()
        {
            AudioFixture(out var played, out var start, out var release);
            try
            {
                CastSpellAction Spell() => new() { Target = actor, CastSound = release, GetActionsFunc = () => new() };
                yield return ResolveAndReplay(Spell());
                CollectionAssert.AreEqual(new[] { start }, played);
                var released = new AdvanceCastAction().ExecuteImmediate(actor).Single();
                yield return ResolveAndReplay(released);
                CollectionAssert.AreEqual(new[] { start, release }, played);
                yield return ResolveAndReplay(Spell());
                var fizzled = new AdvanceCastAction().ExecuteImmediate(actor).Single();
                actor.Vitals.HP = 0;
                yield return ResolveAndReplay(fizzled);
                CollectionAssert.AreEqual(new[] { start, release, start }, played);
            }
            finally { Object.Destroy(start); Object.Destroy(release); }
        }

        [UnityTest] public IEnumerator ImportedEffectAudioSourcesAreDisabledBeforeActivation()
        {
            var source = particle.AddComponent<AudioSource>(); source.playOnAwake = true;
            profile.Projectile.Prefab = null; profile.Area.Prefab = null;
            yield return CombatEffectPlayer.Get().Deliver(new CombatVisualSequence { Profile = profile }, actor.transform.position, true);
            var spawned = CombatEffectPlayer.Get().GetComponentsInChildren<AudioSource>(true).Single();
            Assert.That(spawned.enabled, Is.False);
            Assert.That(spawned.playOnAwake, Is.False);
            Assert.That(spawned.isPlaying, Is.False);
        }

        [UnityTest] public IEnumerator LevelUpFacesSouthCelebratesAndRestoresFacing()
        {
            actor.SetFacing(Facing.UpRight);
            var player = CombatEffectPlayer.Get();
            var action = new LevelUpAction(actor);
            var routine = action.ExecuteRoutine(actor);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(actor.CurrentFacing, Is.EqualTo(Facing.Down));
            yield return null;
            Assert.That(player.ActiveCount, Is.EqualTo(2), "The celebration has a burst and a ground ring.");
            Assert.That(player.GetComponentsInChildren<ParticleSystem>().Any(p => p.particleCount > 0), Is.True);
            yield return routine.Current;
            yield return routine;
            Assert.That(actor.CurrentFacing, Is.EqualTo(Facing.UpRight));
            player.Clear();
            yield return action.ExecuteRoutine(actor, true);
            Assert.That(player.ActiveCount, Is.Zero);
            Assert.That(actor.CurrentFacing, Is.EqualTo(Facing.UpRight));
        }

        [UnityTest] public IEnumerator RepeatedProjectileHitsHaveSeparateVisibleImpacts()
        {
            var audio = AudioFixture(out var played, out var start, out var release);
            audio.SoundEffects.Impact_flesh = release;
            profile.Area.Prefab = null;
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(actor.VisualParent.transform, false);
            mesh.transform.localScale = Vector3.one * 5;
            var point = CombatEffectPlayer.Body(actor, actor.TilemapPosition);
            var sequence = new CombatVisualSequence { Profile = profile, Actor = actor, SingleFlight = true, ContinuousFlight = true,
                Origin = point - Vector3.right * 5, Center = point };
            var hits = new[] { new TakeDamageAction(actor, actor, 5, false), new TakeDamageAction(actor, actor, 5, false) };
            try
            {
                foreach (var hit in hits)
                {
                    hit.SetPlaybackContext(DungeonAnimationMode.Normal, true, actor, actor);
                    hit.Visuals.Begin(actor); hit.Visuals.Sequence = sequence;
                    var children = hit.ExecuteImmediate(actor); hit.Visuals.End(hit, actor, children);
                }
                Assert.That(sequence.FlightImpacts.Count, Is.EqualTo(1));
                yield return hits[0].Visuals.Play(hits[0], actor, false);
                var player = CombatEffectPlayer.Get();
                var first = player.GetComponentsInChildren<ParticleSystem>().Single();
                Assert.That(first.transform.position.z, Is.LessThan(mesh.GetComponent<Renderer>().bounds.min.z), "Contact particles must clear the model.");
                Assert.That(player.ActiveCount, Is.EqualTo(1));
                Assert.That(played.Count, Is.EqualTo(1));
                yield return hits[1].Visuals.Play(hits[1], actor, false);
                Assert.That(player.ActiveCount, Is.EqualTo(2), "The second hit bursts again without another flight.");
                Assert.That(played.Count, Is.EqualTo(2));
            }
            finally { Object.Destroy(start); Object.Destroy(release); }
        }

        [UnityTest] public IEnumerator AuthoredExplosionAndLightningParticlesRestartWhenReused()
        {
            var player = CombatEffectPlayer.Get();
            foreach (var authored in new[] { CombatVisualCatalog.Instance.Explosion, Resources.Load<CombatEffectProfile>("CombatEffects/Profiles/Lightning Flight") })
            {
                var burst = Object.Instantiate(authored);
                burst.Projectile = new(); burst.Area = new(); burst.ImpactSeconds = .12f;
                try
                {
                    GameObject previous = null;
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        yield return player.Deliver(new CombatVisualSequence { Profile = burst }, actor.transform.position, true);
                        var systems = player.GetComponentsInChildren<ParticleSystem>();
                        Assert.That(systems.Any(p => p.particleCount > 0), Is.True, authored.name + " hit " + repeat);
                        var effect = systems.First().transform;
                        while (effect.parent != player.transform) effect = effect.parent;
                        if (previous != null) Assert.That(effect.gameObject, Is.SameAs(previous), "Exercise the pooled instance.");
                        previous = effect.gameObject;
                        player.Release(previous);
                    }
                }
                finally { player.Clear(); Object.Destroy(burst); }
            }
        }

        bool[,] OpenCastingFloor()
        {
            var dungeon = game.CurrentDungeon;
            var mask = new bool[dungeon.dungeonWidth, dungeon.dungeonHeight];
            for (int x = 1; x < mask.GetLength(0) - 1; x++)
                for (int y = 1; y < mask.GetLength(1) - 1; y++) mask[x, y] = true;
            typeof(TileWorldDungeon).GetField("floorMask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(dungeon, mask);
            actor.SetPosition(new Vector3Int(8, 8));
            return mask;
        }

        Ally CastingTarget(Vector3Int cell)
        {
            var target = Object.Instantiate(actor, root.transform);
            target.BaseStats = new Stats { HPMax = 1000 }; target.Vitals = new Vitals();
            target.DisplayedVitals = new Vitals();
            target.Team = Team.Enemy; target.SetPosition(cell);
            target.BaseStats.HPMax = 1000; target.InvalidateCachedStats(); target.Vitals.HP = 1000;
            game.Allies.Add(target);
            return target;
        }

        List<GameAction> ResolveRecorded(GameAction action)
        {
            var result = new List<GameAction>();
            var queue = new Queue<GameAction>(); queue.Enqueue(action);
            while (queue.Count > 0)
            {
                var next = queue.Dequeue(); result.Add(next);
                next.Visuals.Begin(actor); var children = next.ExecuteImmediate(actor); next.Visuals.End(next, actor, children);
                foreach (var child in children) queue.Enqueue(child);
                Assert.That(result.Count, Is.LessThan(100));
            }
            return result;
        }

        [TestCase(1, 1)] [TestCase(-1, 1)] [TestCase(1, -1)] [TestCase(-1, -1)]
        [TestCase(1, 0)] [TestCase(-1, 0)] [TestCase(0, 1)] [TestCase(0, -1)]
        public void AuthoredDirectionalSpellsHitAndRetainTheirAim(int x, int y)
        {
            OpenCastingFloor();
            var direction = new Vector3Int(x, y);
            var target = CastingTarget(actor.TilemapPosition + direction * 3);
            var far = CastingTarget(actor.TilemapPosition + direction * 5);
            actor.BaseStats.ManaMax = 1000; actor.InvalidateCachedStats();
            foreach (string name in new[] { "Fire Bolt", "Ice Bolt", "Lightning Bolt", "Fireball" })
            {
                var skill = Object.Instantiate(Resources.Load<Skill>("Classes/Skills/Elementalist/" + name));
                actor.Skills.Add(skill); actor.Vitals.Mana = 1000; target.Vitals.HP = far.Vitals.HP = 1000;
                try
                {
                    var action = SkillAction.ForMissile(actor, skill, direction);
                    Assert.That(action.IsValid(actor), Is.True, name);
                    ResolveRecorded(action);
                    Assert.That(actor.CurrentFacing, Is.EqualTo(Character.GetFacing(direction)), name + " charge");
                    var all = new List<GameAction>();
                    int advances = 0;
                    while (actor.PendingCast != null)
                    {
                        Assert.That(++advances, Is.LessThan(10));
                        actor.SetFacing(Character.GetFacing(-direction));
                        all.AddRange(ResolveRecorded(new AdvanceCastAction()));
                    }
                    Assert.That(actor.CurrentFacing, Is.EqualTo(Character.GetFacing(direction)), name + " release");
                    Assert.That(target.Vitals.HP, Is.LessThan(1000), name);
                    var release = all.OfType<SkillAction>().Last();
                    bool pierces = skill.AreaRadius == 0 && skill.MissileTargets != 1;
                    Assert.That(far.Vitals.HP < 1000, Is.EqualTo(pierces), name + " penetration");
                    int distance = pierces ? 5 : 3;
                    if (pierces && (skill.MissileTargets == 0 || skill.MissileTargets > 2))
                    {
                        distance = skill.MissileRange;
                        var origin = actor.TilemapPosition; var dungeon = game.CurrentDungeon;
                        if (x != 0) distance = Mathf.Min(distance, x < 0 ? origin.x - 1 : dungeon.dungeonWidth - 2 - origin.x);
                        if (y != 0) distance = Mathf.Min(distance, y < 0 ? origin.y - 1 : dungeon.dungeonHeight - 2 - origin.y);
                    }
                    Assert.That(release.Visuals.Sequence.Center, Is.EqualTo(CombatEffectPlayer.Body(null, actor.TilemapPosition + direction * distance)), name);
                }
                finally { actor.Skills.Remove(skill); Object.DestroyImmediate(skill); }
            }
            var bolt = UnityEditor.AssetDatabase.LoadAssetAtPath<UsableItemDefinition>("Assets/Prefabs/Dungeon/Items/Spell_Bolt.asset");
            var inventory = root.AddComponent<Inventory>();
            var item = bolt.AsInventoryItem(null); inventory.Add(item); target.Vitals.HP = 1000;
            var use = new UseInventoryItemAction(inventory, actor, item).WithDirection(direction);
            ResolveRecorded(use);
            Assert.That(actor.CurrentFacing, Is.EqualTo(Character.GetFacing(direction)));
            Assert.That(target.Vitals.HP, Is.LessThan(1000));
            Assert.That(use.Visuals.Sequence.Profile, Is.SameAs(bolt.VisualProfile));
            Assert.That(use.Visuals.Sequence.Center, Is.EqualTo(CombatEffectPlayer.Body(null, target.TilemapPosition)));
        }

        [TestCase(1, 1)] [TestCase(-1, 1)] [TestCase(1, -1)] [TestCase(-1, -1)]
        public void DiagonalItemAndSkillShotsRespectWallsSceneryAndRange(int x, int y)
        {
            var mask = OpenCastingFloor(); var origin = actor.TilemapPosition;
            var direction = new Vector3Int(x, y);
            Assert.That(MissileTargeting.Trace(actor, direction, 3).Cell, Is.EqualTo(origin + direction * 3));
            Assert.That(MissileTargeting.TraceLine(actor, direction, 3).Endpoint, Is.EqualTo(origin + direction * 3));
            foreach (var side in new[] { new Vector3Int(x, 0), new Vector3Int(0, y) })
            {
                var cell = origin + side;
                mask[cell.x, cell.y] = false;
                Assert.That(MissileTargeting.Trace(actor, direction, 3).Cell, Is.EqualTo(origin));
                Assert.That(MissileTargeting.TraceLine(actor, direction, 3).Endpoint, Is.EqualTo(origin));
                mask[cell.x, cell.y] = true;
                var prop = DungeonProp.Create(game.CurrentDungeon, new EternalEnigma.Core.World.DungeonScenery(cell.ToGridPoint(),
                    EternalEnigma.Core.World.DungeonSceneryKind.Container, 1, 42, EternalEnigma.Core.World.SceneryReward.None), null);
                try
                {
                    Assert.That(MissileTargeting.Trace(actor, direction, 3).Cell, Is.EqualTo(origin));
                    Assert.That(MissileTargeting.TraceLine(actor, direction, 3).Endpoint, Is.EqualTo(origin));
                }
                finally { game.CurrentDungeon.Interactables.Remove(prop); Object.DestroyImmediate(prop.gameObject); }
            }
        }

        [UnityTest] public IEnumerator CastPlaybackUsesRecordedFacingAndSelfCastsKeepFacing()
        {
            OpenCastingFloor();
            var target = CastingTarget(actor.TilemapPosition + new Vector3Int(2, -2));
            var action = new CastSpellAction { Target = target, GetActionsFunc = () => new() };
            ResolveRecorded(action);
            actor.SetFacing(Facing.Up);
            target.SetPosition(actor.TilemapPosition + Vector3Int.left);
            yield return action.Visuals.Play(action, actor, true);
            Assert.That(actor.CurrentFacing, Is.EqualTo(Facing.DownRight));
            ResolveRecorded(new AdvanceCastAction());
            Assert.That(actor.CurrentFacing, Is.EqualTo(Facing.Left), "Release follows a selected target that moved.");
            var self = AudioSkill(null);
            try
            {
                ResolveRecorded(new SkillAction(actor, self, actor));
                Assert.That(actor.CurrentFacing, Is.EqualTo(Facing.Left));
            }
            finally { actor.Skills.Remove(self); Object.Destroy(self); }
        }

        [UnityTest] public IEnumerator AreaSoundSurvivesSkippedAnimationsAndMultipleRecipientsDoNotRepeatIt()
        {
            AudioFixture(out var played, out var start, out var release);
            profile.AreaSound = release;
            try
            {
                var action = new EmptyAction();
                action.SetPlaybackContext(DungeonAnimationMode.NoAnimations, true, actor, actor);
                action.Visuals.Begin(actor); action.Visuals.Configure(profile, actor, actor.TilemapPosition);
                action.Visuals.AddImpact(actor, actor.TilemapPosition, true);
                action.Visuals.AddImpact(actor, actor.TilemapPosition + Vector3Int.right, true);
                action.Visuals.End(action, actor, new());
                yield return action.Visuals.Play(action, actor, true);
                CollectionAssert.AreEqual(new[] { release }, played);
                yield return CombatEffectPlayer.Get().Deliver(action.Visuals.Sequence, actor.transform.position, true, audible: true);
                CollectionAssert.AreEqual(new[] { release }, played);
            }
            finally { Object.Destroy(start); Object.Destroy(release); }
        }

        [UnityTest] public IEnumerator DamageAndHealingSoundsWaitForProjectileImpact()
        {
            var audio = AudioFixture(out var played, out var start, out var release);
            audio.SoundEffects.Impact_flesh = release; audio.SoundEffects.Impact_heal = release;
            profile.FlightSeconds = new Vector2(.3f, .3f);
            profile.Area.Prefab = null;
            var times = new List<float>();
            audio.SoundPlayed += (clip, group) => times.Add(Time.unscaledTime);
            try
            {
                foreach (var action in new GameAction[] { new TakeDamageAction(actor, actor, 5, false), new TakeHealAction(actor, actor, 5, false) })
                {
                    action.SetPlaybackContext(DungeonAnimationMode.Normal, true, actor, actor);
                    action.Visuals.Begin(actor);
                    action.Visuals.Sequence = new CombatVisualSequence { Profile = profile, Actor = actor,
                        Origin = CombatEffectPlayer.Body(actor, actor.TilemapPosition) - Vector3.right * 10 };
                    var children = action.ExecuteImmediate(actor); action.Visuals.End(action, actor, children);
                    float began = Time.unscaledTime;
                    yield return action.Visuals.Play(action, actor, false);
                    Assert.That(times.Last() - began, Is.GreaterThanOrEqualTo(.25f), "Outcome audio belongs at arrival, after the flight.");
                }
                Assert.That(played.Count, Is.EqualTo(2));
            }
            finally { Object.Destroy(start); Object.Destroy(release); }
        }

        [UnityTest] public IEnumerator PiercingProjectileSoundsEachContactOnceInFlightOrder()
        {
            var audio = AudioFixture(out var played, out var start, out var release);
            audio.SoundEffects.Impact_flesh = release;
            profile.FlightSeconds = new Vector2(1.2f, 1.2f); profile.Area.Prefab = null;
            var go = new GameObject("Far audio target"); go.transform.SetParent(root.transform);
            var far = go.AddComponent<Ally>(); far.enabled = false; far.Equipment = go.AddComponent<Equipment>(); far.Skills = new();
            far.BaseStats = new Stats { HPMax = 100 }; far.Vitals = new Vitals(); far.Vitals.HP = 100;
            far.DisplayedVitals = new Vitals(); far.DisplayedStats.Sync(far.FinalStats); far.DisplayedVitals.HP = 100;
            far.TilemapPosition = actor.TilemapPosition + Vector3Int.right * 2;
            far.transform.position = game.CurrentDungeon.CellToWorld(far.TilemapPosition); game.Allies.Add(far);
            var times = new List<float>(); audio.SoundPlayed += (clip, group) => times.Add(Time.unscaledTime);
            try
            {
                var sequence = new CombatVisualSequence { Profile = profile, Actor = actor, SingleFlight = true, ContinuousFlight = true,
                    Origin = CombatEffectPlayer.Body(actor, actor.TilemapPosition) - Vector3.right * 5,
                    Center = CombatEffectPlayer.Body(far, far.TilemapPosition) + Vector3.right * 5 };
                var hits = new[] { new TakeDamageAction(actor, actor, 5, false), new TakeDamageAction(actor, far, 5, false) };
                foreach (var hit in hits)
                {
                    hit.SetPlaybackContext(DungeonAnimationMode.Normal, true, actor, actor);
                    hit.Visuals.Begin(actor); hit.Visuals.Sequence = sequence;
                    var children = hit.ExecuteImmediate(actor); hit.Visuals.End(hit, actor, children);
                }
                float began = Time.unscaledTime;
                yield return hits[0].Visuals.Play(hits[0], actor, false);
                Assert.That(times.Count, Is.EqualTo(2), "Both contacts sound while the single projectile travels.");
                Assert.That(times[0] - began, Is.GreaterThan(.2f));
                Assert.That(times[1] - times[0], Is.GreaterThan(.15f), "Distant contacts must not sound together at the end.");
                yield return hits[1].Visuals.Play(hits[1], actor, false);
                Assert.That(played.Count, Is.EqualTo(2), "Later action playback does not repeat a contact already sounded in flight.");
            }
            finally { Object.Destroy(start); Object.Destroy(release); }
        }

        [TestCase(.5f)] [TestCase(1f)] [TestCase(3f)] [TestCase(6f)]
        public void AdaptiveImpactClearsSmallAndLargeTargets(float size)
        {
            var bounds = new Bounds(new Vector3(4, 7, -2), new Vector3(2.5f, 3, 4) * size);
            var rotation = Quaternion.Euler(30, 15, 0);
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var fit = CombatEffectPlayer.FitImpact(bounds, forward, right, up, 2.5f);
            var delta = fit.point - bounds.center;
            float nearSurface = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
            Assert.That(Vector3.Dot(delta, -forward), Is.GreaterThan(nearSurface));
            Assert.That(Mathf.Abs(Vector3.Dot(delta, right)), Is.LessThan(.001f), "Remains centered horizontally on screen.");
            Assert.That(Mathf.Abs(Vector3.Dot(delta, up)), Is.LessThan(.001f), "Remains centered vertically on screen.");
            Assert.That(fit.size, Is.GreaterThanOrEqualTo(1));
            if (size >= 3) Assert.That(fit.size, Is.GreaterThan(2));
        }

        [Test] public void TargetBoundsIgnoreParticlesAndRetainTheRecordedCell()
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(actor.VisualParent.transform, false);
            mesh.transform.localScale = new Vector3(3, 4, 6);
            var effects = new GameObject("Aura", typeof(ParticleSystem));
            effects.transform.SetParent(actor.VisualParent.transform, false);
            effects.GetComponent<ParticleSystemRenderer>().bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            profile.Impact.FitToTarget = true;
            var action = new EmptyAction(); action.Visuals.Begin(actor); action.Visuals.Configure(profile, actor, actor.TilemapPosition);
            action.Visuals.AddImpact(actor, actor.TilemapPosition, true);
            var captured = action.Visuals.Impacts.Single().bounds.Value;
            Assert.That(captured.size, Is.EqualTo(new Vector3(3, 4, 6)));
            actor.transform.position += Vector3.right * 100;
            Assert.That(action.Visuals.Impacts.Single().bounds.Value.center, Is.EqualTo(captured.center));
        }

        [UnityTest] public IEnumerator SkippedReplayDoesNotSpawnTransientEffects()
        {
            var action = new EmptyAction(); action.Visuals.Begin(actor); action.Visuals.Configure(profile, actor, actor.TilemapPosition);
            action.Visuals.End(action, actor, new());
            yield return action.Visuals.Play(action, actor, true);
            Assert.That(CombatEffectPlayer.Get().ActiveCount, Is.Zero);
        }

        [UnityTest] public IEnumerator EachValidCastCreatesAHistoryEventAndInvalidCastsDoNot()
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            skill.SkillName = "Test ability";
            skill.Targeting = SkillTargeting.Self;
            skill.TargetSelector = new TargetSelector { Team = TargetTeam.Self, Area = TargetArea.Self };
            actor.CharacterName = "Tester"; actor.Skills.Add(skill);
            GameMessages feed = null;
            try
            {
                GameMessages.BeginTurn();
                new SkillAction(actor, skill, actor).ExecuteImmediate(actor);
                feed = Object.FindFirstObjectByType<GameMessages>();
                const string message = "Tester casts Test ability.";
                Assert.That(feed.History.Count(s => s == message), Is.EqualTo(1));
                GameMessages.BeginTurn();
                new SkillAction(actor, skill, actor).ExecuteImmediate(actor);
                Assert.That(feed.History.Count(s => s == message), Is.EqualTo(2));
                Assert.That(feed.TurnEvents.Count(s => s == message), Is.EqualTo(1));
                skill.ManaCost = 1; // Fixture has no mana.
                new SkillAction(actor, skill, actor).ExecuteImmediate(actor);
                Assert.That(feed.History.Count(s => s == message), Is.EqualTo(2));
            }
            finally
            {
                actor.Skills.Remove(skill); Object.Destroy(skill);
                if (feed != null) Object.Destroy(feed.gameObject);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator MissHasFlightButNoImpactAndAreaPlaysOnce()
        {
            var player = CombatEffectPlayer.Get();
            var sequence = new CombatVisualSequence { Profile = profile, Actor = actor, Origin = actor.transform.position, Center = actor.transform.position, SingleFlight = true };
            yield return player.Deliver(sequence, actor.transform.position + Vector3.right, false);
            Assert.That(player.ActiveCount, Is.EqualTo(1), "Only the area remains after a missed shot.");
            Assert.That(sequence.FlightPlayed && sequence.AreaPlayed, Is.True);
            yield return player.Deliver(sequence, actor.transform.position, true);
            Assert.That(player.ActiveCount, Is.EqualTo(2), "Second recipient adds one impact, not another area.");
            player.Clear(); Assert.That(player.ActiveCount, Is.Zero);
        }

        [UnityTest] public IEnumerator GroundImpactFitsLargeTargetsWithoutLeavingTheFloor()
        {
            profile.Projectile.Prefab = null; profile.Area.Prefab = null;
            profile.Impact.FitToTarget = true; profile.Impact.MinimumDiameterCells = 3;
            var destination = actor.transform.position + Vector3.back;
            var player = CombatEffectPlayer.Get();
            yield return player.Deliver(new CombatVisualSequence { Profile = profile, Actor = actor }, destination, true,
                new Bounds(destination, Vector3.one * 30));
            var impact = player.GetComponentsInChildren<ParticleSystem>().Single();
            Assert.That(impact.transform.position, Is.EqualTo(CombatEffectPlayer.Ground(destination)));
            Assert.That(impact.transform.localScale.x, Is.GreaterThan(profile.Impact.Scale));
        }

        [UnityTest] public IEnumerator AuraSnapshotsSurviveFutureRemovalAndRefreshWithoutDuplicates()
        {
            var status = actor.VisualParent.AddComponent<StrengthStatusEffect>(); status.TurnsLeft = 3; status.VisualProfile = aura;
            var apply = new EmptyAction(); apply.Visuals.Begin(actor); actor.StatusEffects.Add(status); apply.Visuals.End(apply, actor, new());
            var remove = new EmptyAction(); remove.Visuals.Begin(actor); actor.StatusEffects.Remove(status); remove.Visuals.End(remove, actor, new());
            yield return apply.Visuals.Play(apply, actor, true);
            var player = CombatEffectPlayer.Get(); Assert.That(player.ActiveCount, Is.EqualTo(1));
            player.SetStatuses(actor, new Dictionary<string, StatusVisualProfile> { ["Strength"] = aura, ["OtherStrength"] = aura });
            Assert.That(player.ActiveCount, Is.EqualTo(1), "Shared aura profiles do not stack particles.");
            yield return remove.Visuals.Play(remove, actor, true);
            Assert.That(player.ActiveCount, Is.Zero);
        }

        [UnityTest] public IEnumerator ExpiredAuraIsRemovedEvenWhenItExpiredBeforeRemovalAction()
        {
            var player = CombatEffectPlayer.Get(); player.SetStatuses(actor, new Dictionary<string, StatusVisualProfile> { ["Strength"] = aura });
            var status = actor.VisualParent.AddComponent<StrengthStatusEffect>(); status.TurnsLeft = 0; status.VisualProfile = aura; actor.StatusEffects.Add(status);
            var removal = new EmptyAction(); removal.Visuals.Begin(actor); actor.StatusEffects.Remove(status); removal.Visuals.End(removal, actor, new());
            yield return removal.Visuals.Play(removal, actor, true);
            Assert.That(player.ActiveCount, Is.Zero);
        }

        [Test] public void DamageRecordsActualRecipientAndPropagatesToEveryHit()
        {
            var parent = new EmptyAction(); parent.Visuals.Begin(actor); parent.Visuals.Configure(profile, actor, actor.TilemapPosition);
            var hit = new TakeDamageAction(actor, actor, 5);
            parent.Visuals.End(parent, actor, new List<GameAction> { hit });
            var cell = actor.TilemapPosition; hit.Visuals.Begin(actor); hit.Visuals.End(hit, actor, new());
            Vector3 recorded = hit.Visuals.Impacts.Single().point;
            actor.TilemapPosition += Vector3Int.right * 4;
            Assert.That(hit.Visuals.Sequence, Is.SameAs(parent.Visuals.Sequence));
            Assert.That(hit.Visuals.Impacts.Single().point, Is.EqualTo(recorded));
            Assert.That(recorded, Is.EqualTo(CombatEffectPlayer.Body(actor, cell)));
        }

        [UnityTest] public IEnumerator AuraPreservesPrefabRotationAndMinimumFootprint()
        {
            particle.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            aura.Aura.Rotation = new Vector3(-90, 0, 0);
            aura.Aura.MinimumDiameterCells = 3; aura.Aura.ReferenceDiameter = 2;
            var player = CombatEffectPlayer.Get();
            player.SetStatuses(actor, new Dictionary<string, StatusVisualProfile> { ["Buff"] = aura });
            var effect = player.GetComponentsInChildren<ParticleSystem>().Single().transform;
            Assert.That(Quaternion.Angle(effect.rotation, Quaternion.Euler(-180, 0, 0)), Is.LessThan(.01f));
            Assert.That(effect.localScale.x * aura.Aura.ReferenceDiameter, Is.GreaterThanOrEqualTo(7.5f));
            Object.Destroy(game.CurrentDungeon); yield return null; yield return null;
            Assert.That(player.ActiveCount, Is.Zero, "Destroying the floor clears its persistent effects.");
        }
    }
}
#endif
