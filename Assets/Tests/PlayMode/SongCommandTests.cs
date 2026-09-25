#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class SongCommandTests
    {
        private GameTestHarness harness;
        private readonly List<Object> assets = new();
        private Ally caster => harness.Ally;
        private Ally friend;
        private Enemy enemy;
        private Character first;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese" } });
            friend = harness.Game.Allies.Single(a => a != caster);
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
                Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            caster.SetPosition(center);
            friend.SetPosition(center + Vector3Int.left);
            friend.AllyStrategy = AllyStrategy.HoldPosition;
            caster.AllyStrategy = AllyStrategy.HoldPosition;

            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right);
            first = harness.Game.Enemies[0];
            enemy = (Enemy)first;

            foreach (var e in harness.Game.Enemies.Cast<Enemy>()) e.Policies.Clear();
            foreach (var actor in harness.Game.AllCharacters)
            {
                actor.BaseStats.Strength = 10;
                actor.BaseStats.Defense = 5;
                actor.BaseStats.HPMax = 100;
                actor.BaseStats.SPMax = 100;
                actor.BaseStats.HPRegenAcccumlateThreshold = 10000;
                actor.BaseStats.SPRegenAcccumlateThreshold = 10000;
                actor.InvalidateCachedStats();
                actor.Vitals.HP = 60;
                actor.Vitals.SP = 20;
                actor.SyncDisplayedStats();
            }
            enemy.Vitals.HP = 500;
            enemy.SyncDisplayedStats();
            harness.Game.UpdateMiniMap();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try { yield return harness.Cleanup(); }
            finally
            {
                foreach (var asset in assets) Object.DestroyImmediate(asset);
                assets.Clear();
            }
        }

        private StatModification Str(int v) => new StatModification { Strength = v };

        private Skill MakeSkill(string name, List<GameAction> effects)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            assets.Add(skill);
            skill.SkillName = name;
            skill.Targeting = SkillTargeting.Self;
            skill.TargetSelector = new TargetSelector { Team = TargetTeam.Allies, Area = TargetArea.All };
            skill.ActionEffects = effects ?? new List<GameAction>();
            skill.SPCost = 1;
            return skill;
        }

        private Skill MakePassive(string name, List<PassiveResponse> responses)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            assets.Add(skill);
            skill.SkillName = name;
            skill.ActivationType = ActivationType.Passive;
            skill.PassiveResponses = responses ?? new List<PassiveResponse>();
            return skill;
        }

        private IEnumerator Cast(Skill skill, Character target)
        {
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skill, target));
        }

        [UnityTest]
        public IEnumerator SongBuffsAlliesInRadius()
        {
            var skill = MakeSkill("Battle Hymn", new List<GameAction>
            {
                new StartSongAction
                {
                    SongId = "hymn",
                    SongName = "Battle Hymn",
                    Modification = Str(3),
                    Radius = 3,
                    Turns = 5
                }
            });
            caster.Skills.Add(skill);
            caster.InvalidateCachedStats();

            yield return Cast(skill, caster);
            yield return harness.ExecuteAction(new WaitAction());

            Assert.That(friend.FinalStats.Strength, Is.EqualTo(13));
            Assert.That(caster.FinalStats.Strength, Is.EqualTo(13));
        }

        [UnityTest]
        public IEnumerator SongDoesNotReachOutsideRadius()
        {
            var skill = MakeSkill("Battle Hymn", new List<GameAction>
            {
                new StartSongAction
                {
                    SongId = "hymn",
                    SongName = "Battle Hymn",
                    Modification = Str(3),
                    Radius = 3,
                    Turns = 5
                }
            });
            caster.Skills.Add(skill);
            caster.InvalidateCachedStats();

            yield return Cast(skill, caster);

            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var farCell = cells.First(p => TileWorldDungeon.ChevDistance(p, caster.TilemapPosition) == 5);
            friend.SetPosition(farCell);

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new WaitAction());

            Assert.That(friend.FinalStats.Strength, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator ThirdSongEvictsOldest()
        {
            var effect = new List<GameAction>();
            caster.Skills.Add(MakeSkill("song", effect));
            caster.InvalidateCachedStats();

            var skillA = MakeSkill("a", new List<GameAction> { new StartSongAction { SongId = "a", SongName = "a", Modification = Str(1) } });
            var skillB = MakeSkill("b", new List<GameAction> { new StartSongAction { SongId = "b", SongName = "b", Modification = new StatModification { Defense = 1 } } });
            var skillC = MakeSkill("c", new List<GameAction> { new StartSongAction { SongId = "c", SongName = "c", Modification = Str(2) } });

            caster.Skills.Add(skillA);
            caster.Skills.Add(skillB);
            caster.Skills.Add(skillC);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillB, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillC, caster));

            var active = SongRules.ActiveSongs(caster);
            Assert.That(active.Count, Is.EqualTo(2));
            Assert.That(active.Select(s => s.SongId).ToList(), Is.EqualTo(new[] { "b", "c" }));
        }

        [UnityTest]
        public IEnumerator HarmonyAllowsThreeSongs()
        {
            var passive = MakePassive("Harmony", new List<PassiveResponse> { new SongSlotBonus { ExtraSlots = 1 } });
            caster.Skills.Add(passive);
            caster.InvalidateCachedStats();

            var skillA = MakeSkill("a", new List<GameAction> { new StartSongAction { SongId = "a", SongName = "a", Modification = Str(1) } });
            var skillB = MakeSkill("b", new List<GameAction> { new StartSongAction { SongId = "b", SongName = "b", Modification = new StatModification { Defense = 1 } } });
            var skillC = MakeSkill("c", new List<GameAction> { new StartSongAction { SongId = "c", SongName = "c", Modification = Str(2) } });

            caster.Skills.Add(skillA);
            caster.Skills.Add(skillB);
            caster.Skills.Add(skillC);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillB, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillC, caster));

            var active = SongRules.ActiveSongs(caster);
            Assert.That(active.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator SameSongRefreshesInsteadOfUsingASlot()
        {
            var skillA = MakeSkill("a", new List<GameAction> { new StartSongAction { SongId = "a", SongName = "a", Modification = Str(1) } });
            caster.Skills.Add(skillA);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));

            var active = SongRules.ActiveSongs(caster);
            Assert.That(active.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SongsStackPerStatStrongest()
        {
            var skillA = MakeSkill("a", new List<GameAction> { new StartSongAction { SongId = "a", SongName = "a", Modification = Str(2) } });
            var skillB = MakeSkill("b", new List<GameAction> { new StartSongAction { SongId = "b", SongName = "b", Modification = Str(5) } });

            caster.Skills.Add(skillA);
            caster.Skills.Add(skillB);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillB, caster));

            Assert.That(friend.FinalStats.Strength, Is.EqualTo(15));
        }

        [UnityTest]
        public IEnumerator EncoreExtendsSongs()
        {
            var skillA = MakeSkill("a", new List<GameAction> { new StartSongAction { SongId = "a", SongName = "a", Modification = Str(1), Turns = 5 } });
            var skillEncore = MakeSkill("encore", new List<GameAction> { new ExtendSongsAction { Turns = 3 } });

            caster.Skills.Add(skillA);
            caster.Skills.Add(skillEncore);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));

            var songs = SongRules.ActiveSongs(caster);
            var turnsBefore = songs[0].TurnsLeft;

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillEncore, caster));

            songs = SongRules.ActiveSongs(caster);
            Assert.That(songs[0].TurnsLeft, Is.EqualTo(turnsBefore + 3));
        }

        [UnityTest]
        public IEnumerator GrandFinaleEndsSongsAndDamages()
        {
            var skillA = MakeSkill("a", new List<GameAction> { new StartSongAction { SongId = "a", SongName = "a", Modification = Str(1) } });
            var skillB = MakeSkill("b", new List<GameAction> { new StartSongAction { SongId = "b", SongName = "b", Modification = Str(2) } });
            var skillGF = MakeSkill("gf", new List<GameAction> { new GrandFinaleAction { HealPerSong = 10, DamagePerSong = 8 } });

            caster.Skills.Add(skillA);
            caster.Skills.Add(skillB);
            caster.Skills.Add(skillGF);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillB, caster));

            int casterHPBefore = caster.Vitals.HP;
            int enemyHPBefore = enemy.Vitals.HP;

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillGF, caster));

            var active = SongRules.ActiveSongs(caster);
            Assert.That(active.Count, Is.EqualTo(0));
            Assert.That(enemy.Vitals.HP, Is.EqualTo(enemyHPBefore - 16));
            Assert.That(caster.Vitals.HP, Is.EqualTo(Mathf.Min(casterHPBefore + 20, 100)));
        }

        [UnityTest]
        public IEnumerator BladeDanceHitsOncePerSong()
        {
            var skillA = MakeSkill("a", new List<GameAction> { new StartSongAction { SongId = "a", SongName = "a", Modification = Str(1) } });
            var skillB = MakeSkill("b", new List<GameAction> { new StartSongAction { SongId = "b", SongName = "b", Modification = Str(2) } });

            caster.Skills.Add(skillA);
            caster.Skills.Add(skillB);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillA, caster));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillB, caster));

            var skill = MakeSkill("blade", new List<GameAction> { new SongCountStrikeAction() });
            skill.Targeting = SkillTargeting.SelectedTarget;
            skill.TargetSelector = new TargetSelector { Team = TargetTeam.Enemies, Area = TargetArea.All };
            caster.Skills.Add(skill);
            caster.InvalidateCachedStats();

            var action = new SkillAction(caster, skill, enemy);
            var effects = action.ExecuteImmediate(caster);

            int damageCount = effects.OfType<TakeDamageAction>().Count();
            Assert.That(damageCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CommandsDifferentStatsBothApply()
        {
            var skillAtk = MakeSkill("atk", new List<GameAction> { new ApplyCommandAction { CommandId = "atk", CommandName = "atk", Modification = Str(3) } });
            var skillGrd = MakeSkill("grd", new List<GameAction> { new ApplyCommandAction { CommandId = "grd", CommandName = "grd", Modification = new StatModification { Defense = 2 } } });

            caster.Skills.Add(skillAtk);
            caster.Skills.Add(skillGrd);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillAtk, friend));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillGrd, friend));

            Assert.That(friend.FinalStats.Strength, Is.EqualTo(13));
            Assert.That(friend.FinalStats.Defense, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator CommandsSameStatStrongestWins()
        {
            var skillAtk1 = MakeSkill("atk1", new List<GameAction> { new ApplyCommandAction { CommandId = "atk", CommandName = "atk1", Modification = Str(3) } });
            var skillAtk2 = MakeSkill("atk2", new List<GameAction> { new ApplyCommandAction { CommandId = "atk", CommandName = "atk2", Modification = Str(5) } });

            caster.Skills.Add(skillAtk1);
            caster.Skills.Add(skillAtk2);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillAtk1, friend));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillAtk2, friend));

            Assert.That(friend.FinalStats.Strength, Is.EqualTo(15));
        }

        [UnityTest]
        public IEnumerator ReissuedCommandRefreshes()
        {
            var skillAtk = MakeSkill("atk", new List<GameAction> { new ApplyCommandAction { CommandId = "atk", CommandName = "atk", Modification = Str(3) } });

            caster.Skills.Add(skillAtk);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillAtk, friend));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillAtk, friend));

            var commands = friend.StatusEffects.OfType<CommandStatusEffect>().Where(c => !c.IsExpired()).ToList();
            Assert.That(commands.Count, Is.EqualTo(1));
            Assert.That(commands[0].CommandId, Is.EqualTo("atk"));
        }

        [UnityTest]
        public IEnumerator ArmsAddBonusElementalHit()
        {
            var skillFire = MakeSkill("fire", new List<GameAction> { new ApplyCommandAction { CommandId = "fire", CommandName = "fire", BonusElement = DamageElement.Fire, BonusElementPercent = 50 } });

            caster.Skills.Add(skillFire);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillFire, caster));

            var command = caster.StatusEffects.OfType<CommandStatusEffect>().FirstOrDefault(c => c.CommandId == "fire" && !c.IsExpired());
            Assert.That(command, Is.Not.Null);

            var damage = new TakeDamageAction(caster, enemy, 10);
            var response = command.GetResponseTo(caster, damage).ToList();

            Assert.That(response.Count, Is.EqualTo(1));
            var bonusHit = response[0] as TakeDamageAction;
            Assert.That(bonusHit, Is.Not.Null);
            Assert.That(bonusHit.Damage, Is.EqualTo(5));
            Assert.That(bonusHit.Element, Is.EqualTo(DamageElement.Fire));
            Assert.That(bonusHit.ResponseDepth, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReinforceHealsWhenCommandEnds()
        {
            var passive = MakePassive("reinforce", new List<PassiveResponse> { new CommandExpiryHeal { HealPercent = 0.1f } });
            caster.Skills.Add(passive);
            caster.InvalidateCachedStats();

            var skillCmd = MakeSkill("cmd", new List<GameAction> { new ApplyCommandAction { CommandId = "cmd", CommandName = "cmd", Modification = Str(1), Turns = 1 } });
            caster.Skills.Add(skillCmd);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillCmd, friend));

            int friendHPBefore = friend.Vitals.HP;

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new WaitAction());
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new WaitAction());

            Assert.That(friend.Vitals.HP, Is.EqualTo(friendHPBefore + 10));
        }

        [UnityTest]
        public IEnumerator DecisiveOrderDoublesAndEnds()
        {
            var skillCmd = MakeSkill("cmd", new List<GameAction> { new ApplyCommandAction { CommandId = "cmd", CommandName = "cmd", Modification = Str(3) } });
            var skillAmp = MakeSkill("amp", new List<GameAction> { new AmplifyCommandsAction { Multiplier = 2f, Turns = 2 } });

            caster.Skills.Add(skillCmd);
            caster.Skills.Add(skillAmp);
            caster.InvalidateCachedStats();

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillCmd, friend));

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skillAmp, friend));

            Assert.That(friend.FinalStats.Strength, Is.EqualTo(16));

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new WaitAction());
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new WaitAction());

            var commands = friend.StatusEffects.OfType<CommandStatusEffect>().Where(c => !c.IsExpired()).ToList();
            Assert.That(commands.Count, Is.EqualTo(0));
        }
    }
}
#endif
