#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class FinaleGameplayTests
    {
        GameTestHarness harness;
        DungeonAnimationMode? oldAnimations;
        [UnitySetUp] public IEnumerator Setup()
        { harness = new GameTestHarness { TimeoutSeconds = 240 }; oldAnimations = DungeonPreferences.AnimationOverride; DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        { yield return harness.Cleanup(); DungeonPreferences.AnimationOverride = oldAnimations; }
        IEnumerator LoadFinale()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = EndgameDebugStart.Create(TownSceneLoader.Default, common.ItemManager, EndgameDebugDestination.Dungeon);
            common.GameSaveData.IsSandbox = false;
            SaveSystem.SaveData(common.GameSaveData);
            common.Travel.Continue();
            int coveredPlanningFrames = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + harness.TimeoutSeconds;
            while (harness.Game?.IsReady != true)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Finale initialization timed out.");
                AssertCovered(common.ScreenTransition);
                var title = common.ScreenTransition.ShutterScreen.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (harness.Game != null && harness.Game.Finale == null && title.text.Contains("Floor 1</size>"))
                {
                    coveredPlanningFrames++;
                    if (coveredPlanningFrames == 1)
                    {
                        System.IO.Directory.CreateDirectory("Temp/FinaleDebug");
                        ScreenCapture.CaptureScreenshot("Temp/FinaleDebug/loading-floor-1.png");
                    }
                }
                yield return null;
            }
            Assert.That(coveredPlanningFrames, Is.GreaterThan(0),
                "The labeled loading screen must get a frame before synchronous two-map planning begins.");
            yield return harness.WaitUntil(() => !common.ScreenTransition.BlockScreen.activeSelf, "finale reveal");
            yield return harness.WaitForIdle();
        }
        static void AssertCovered(ScreenTransition transition, int floor = 0)
        {
            Assert.That(transition.BlockScreen.activeInHierarchy, Is.True, "Loading must block game input.");
            Assert.That(transition.ShutterScreen.gameObject.activeInHierarchy, Is.True);
            Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1).Within(.001f));
            var title = transition.ShutterScreen.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.That(title, Is.Not.Null);
            Assert.That(title.gameObject.activeInHierarchy && title.alpha > .99f, Is.True);
            Assert.That(title.text, Does.StartWith("Loading..."));
            if (floor > 0) Assert.That(title.text, Is.EqualTo($"Loading...\n<size=70%>The reverse world - Floor {floor}</size>"));
        }
        static void Resolve(Character actor, GameAction action)
        {
            var queue = new Queue<GameAction>(); queue.Enqueue(action);
            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                foreach (var child in next.ExecuteImmediate(actor) ?? new()) queue.Enqueue(child);
                next.UpdateDisplayedStats();
            }
        }
        Enemy Reveal(Enemy enemy)
        {
            var game = harness.Game;
            var identity = enemy.GetComponent<FinaleActor>();
            string id = identity.Id, region = identity.RegionId;
            bool lastBuilding = game.Enemies.Count(e => e.GetComponent<FinaleActor>() is { Hidden: true } a && a.RegionId == region) == 1;
            var position = enemy.TilemapPosition;
            int population = game.Enemies.Count, defeated = game.Finale.Session.Defeated.Count;
            var cell = game.CurrentDungeon.GetWalkableNeighborhoodTiles(enemy.TilemapPosition)
                .First(p => game.Finale.Session.CanReveal(enemy.GetComponent<FinaleActor>().Id, p.ToGridPoint()) &&
                    game.CurrentDungeon.OverlapsAnyOtherCharacter(harness.Ally, Character.ToBounds(p)) == null);
            harness.Ally.SetPosition(cell);
            Resolve(harness.Ally, new RevealMimicAction(enemy));
            var revealed = game.Enemies.OfType<Enemy>().Single(e => e.GetComponent<FinaleActor>().Id == id);
            Assert.That(revealed.TilemapPosition, Is.EqualTo(position));
            Assert.That(revealed.GetComponent<FinaleActor>().Family, Is.EqualTo(game.Finale.Session.MimicFamily(id)));
            Assert.That(revealed.GetComponent<FinaleActor>().Hidden, Is.False);
            Assert.That(game.Finale.CanAct(revealed), Is.False);
            Assert.That(game.Enemies.Count, Is.EqualTo(population)); Assert.That(game.Finale.Session.Defeated.Count, Is.EqualTo(defeated));
            if (lastBuilding)
            {
                Assert.That(revealed.IsBoss, Is.True, "The last building in each biome must reveal a boss.");
                Assert.That(revealed.GetComponent<LargeBossBehavior>(), Is.Not.Null);
            }
            return revealed;
        }
        void Kill(Enemy enemy)
        {
            Resolve(harness.Ally, new TakeDamageAction(harness.Ally, enemy, int.MaxValue / 8) { Environmental = true, AwardExperience = false });
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator MimicsIgnoreDamageUntilAdjacentEntryAndSkipTheirRevealTurn()
        {
            yield return LoadFinale();
            var game = harness.Game; var session = game.Finale.Session;
            Assert.That(session.CurrentFloor, Is.EqualTo(1)); Assert.That(session.Unlocked.Count, Is.EqualTo(1));
            Assert.That(session.Defeated, Is.Empty); Assert.That(session.Revealed, Is.Empty);
            Assert.That(game.CurrentDungeon.Floor.Overworld, Is.SameAs(Common.Instance.CampaignContext.Grid));
            var mimic = game.Enemies.OfType<Enemy>().First(e => e.GetComponent<FinaleActor>().Hidden);
            Assert.That(mimic.FootPrint, Is.EqualTo(FootPrint.Size1x1));
            int hp = mimic.Vitals.HP;
            Kill(mimic); Assert.That(mimic.Vitals.HP, Is.EqualTo(hp)); Assert.That(session.Defeated, Is.Empty);
            Assert.That(game.Finale.CanAct(mimic), Is.False);
            mimic = Reveal(mimic);
            Assert.That(mimic.GetComponent<FinaleActor>().Hidden, Is.False);
            Assert.That(game.Finale.CanAct(mimic), Is.False);
            game.Finale.EndTurn(); Assert.That(game.Finale.CanAct(mimic), Is.True);
            Assert.That(session.Revealed, Does.Contain(mimic.GetComponent<FinaleActor>().Id));
            var closed = session.Floor.Regions[1].Enemies[0].Position.ToCell();
            Assert.That(SkillMovement.CanOccupy(harness.Ally, closed), Is.False);
            var before = harness.Ally.TilemapPosition; SkillMovement.Place(harness.Ally, closed);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(before));
            Assert.That(game.Finale.RequestExit(), Is.False);
            Assert.That(Common.Instance.Travel.FinishDungeon(true, game.PlayerController), Is.False);
            for (int roll = 0; roll < 10000; roll++)
                Assert.That(FinaleRoster.IsReserved(game.EnemyManager.GetEnemyPrefab(38, roll).name), Is.False);
            var source = game.Enemies.OfType<Enemy>().First(e => !session.Floor.Enemies.Any(p => p.IsMimic && p.Id == e.GetComponent<FinaleActor>().Id));
            var sourceIdentity = source.GetComponent<FinaleActor>();
            Assert.That(EncounterActor.Required(source), Is.True, "Required enemies cannot be converted.");
            var spawnCell = game.CurrentDungeon.GetWalkableNeighborhoodTiles(source.TilemapPosition)
                .First(p => FinaleActor.AllowsPosition(source, p) && SkillMovement.CanOccupy(source, p));
            var spawn = new SpawnEnemyAction(source, spawnCell, source.VisualParent.transform.position, source);
            Resolve(harness.Ally, spawn); yield return spawn.ExecuteRoutine(harness.Ally, true);
            var child = spawn.SpawnedEnemy;
            Assert.That(child, Is.Not.Null);
            var childIdentity = child.GetComponent<FinaleActor>();
            Assert.That(childIdentity.Id, Is.EqualTo(sourceIdentity.Id + "/summon-1"));
            Assert.That(childIdentity.RegionId, Is.EqualTo(sourceIdentity.RegionId));
            Assert.That(session.EnemyRegions[childIdentity.Id], Is.EqualTo(session.EnemyRegions[sourceIdentity.Id]));
            foreach (var enemy in game.Enemies.OfType<Enemy>().Where(e => e != child).ToArray())
            {
                if (enemy.Vitals.HP > 0) Kill(enemy.GetComponent<FinaleActor>().Hidden ? Reveal(enemy) : enemy);
            }
            game.Finale.EndTurn();
            Assert.That(session.Unlocked.Count, Is.EqualTo(1), "A living summon still owns the region.");
            Kill(child); game.Finale.EndTurn(); Assert.That(session.Unlocked.Count, Is.EqualTo(2));
            game.Finale.EndTurn(); Assert.That(session.Unlocked.Count, Is.EqualTo(2), "The boundary opens once.");
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator BothStairsPreservePartyAndWakeAtHomeWithoutSaving()
        {
            yield return LoadFinale();
            var common = Common.Instance; var context = common.CampaignContext; var game = harness.Game;
            var session = game.Finale.Session; var originalGrid = context.Grid;
            int writes = harness.Store.Writes;
            string checkpoint = harness.Store.Json;
            var roster = common.GameSaveData.Roster.Select(a => a.AllyId).ToArray();
            var inventory = game.PlayerController.Inventory.InventoryItems.ToArray();
            var party = PartyRules.PartyMembers(game).Where(a => !a.IsSummon).ToArray();
            Assert.That(party.Length, Is.GreaterThanOrEqualTo(3));
            var expectedWeapons = party.ToDictionary(a => a.TownAllyId, a => a.Equipment.EquippedWeapon.ItemName);
            // Cover both representations of an unarmed hero while retaining real weapons in the summary.
            party[0].Equipment.EquippedWeapon = null;
            JsonUtility.FromJsonOverwrite("{\"EquippedWeapon\":{\"ItemDefinition\":null}}", party[1].Equipment);
            Assert.That(party[1].Equipment.EquippedWeapon, Is.Not.Null);
            Assert.That(party[1].Equipment.EquippedWeapon.ItemDefinition, Is.Null);
            expectedWeapons[party[0].TownAllyId] = expectedWeapons[party[1].TownAllyId] = "Unarmed";
            game.PlayerController.Gold = 4321;
            for (int floor = 1; floor <= 2; floor++)
            {
                yield return harness.WaitForIdle(); // The floor title also owns input after the scene cover opens.
                Assert.That(game.Finale.Title, Is.EqualTo($"The reverse world - Floor {floor}"));
                var hud = Object.FindFirstObjectByType<DungeonHud>();
                var header = (TMPro.TMP_Text)typeof(DungeonHud).GetField("header",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(hud);
                Assert.That(header.text, Is.EqualTo(GameplayText.Format($"Finale {floor} · {session.Remaining} left")));
                var oldTerrain = game.CurrentDungeon;
                for (int sweep = 0; !session.FloorCleared; sweep++)
                {
                    Assert.That(sweep, Is.LessThan(100), "Finale clearance must make progress.");
                    // Deliberately visit boss buildings early to exercise deferred boss reveals.
                    foreach (var enemy in game.Enemies.OfType<Enemy>().OrderByDescending(e => e.IsBoss).ToArray())
                    {
                        if (enemy.Vitals.HP > 0) Kill(enemy.GetComponent<FinaleActor>().Hidden ? Reveal(enemy) : enemy);
                    }
                    game.Finale.EndTurn();
                }
                Assert.That(session.CurrentFloor, Is.EqualTo(floor));
                Assert.That(context.State.Finished, Is.False);
                Assert.That(session.Plan.Floors.Count, Is.EqualTo(2));
                var stairs = game.CurrentDungeon.Interactables.OfType<Stairs>().Single();
                Assert.That(stairs.GetInteractionText(), Is.EqualTo(floor == 2 ? "Ascend and return home" : "Ascend to the next floor"));
                harness.Ally.SetPosition(session.Floor.Stairs.ToCell());
                harness.Ally.Vitals.Mana = 1; harness.Ally.SyncDisplayedStats();
                DungeonPreferences.AnimationOverride = floor % 2 == 1
                    ? DungeonAnimationMode.Normal : DungeonAnimationMode.NoAnimations;
                Assert.That(game.Finale.RequestExit(), Is.True);
                if (floor < 2)
                {
                    int loadingFrames = 0;
                    double deadline = Time.realtimeSinceStartupAsDouble + harness.TimeoutSeconds;
                    while (!game.IsReady || game.Finale.IsTransitioning)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Next finale floor timed out.");
                        AssertCovered(common.ScreenTransition, floor + 1);
                        if (loadingFrames++ == 0)
                            ScreenCapture.CaptureScreenshot($"Temp/FinaleDebug/loading-floor-{floor + 1}.png");
                        yield return null;
                    }
                    Assert.That(loadingFrames, Is.GreaterThan(0), "Each floor must load behind a rendered cover.");
                    yield return harness.WaitUntil(() => !common.ScreenTransition.BlockScreen.activeSelf, "next floor reveal");
                    Assert.That(oldTerrain == null, Is.True, "Previous terrain must be released.");
                    Assert.That(game.PlayerController.Gold, Is.EqualTo(4321));
                    Assert.That(game.PlayerController.Inventory.InventoryItems, Is.EquivalentTo(inventory));
                    Assert.That(harness.Ally.Vitals.Mana, Is.LessThan(harness.Ally.FinalStats.ManaMax), "Ascending must not give a free rest.");
                }
            }
            yield return harness.WaitUntil(() => common.Travel.HomecomingCompleted, "homecoming wake and restored control");
            Assert.That(session.StairTransitions, Is.EqualTo(new[] { 1, 2 }));
            var town = Object.FindFirstObjectByType<Town>(); var bed = town.GetComponentInChildren<HomeBed>();
            Assert.That(town.Configuration.Id, Is.EqualTo(context.Campaign.StartLocationId));
            Assert.That(town.TownPlayer.ControllingTownAlly.Id, Is.EqualTo(common.GameSaveData.ProtagonistId));
            Assert.That(town.TownPlayer.ControllingTownAlly.TilemapPosition, Is.EqualTo(bed.Tile - Vector3Int.up));
            Assert.That(town.TownPlayer.CutsceneLocked || bed.IsSleeping || common.ScreenTransition.BlockScreen.activeSelf, Is.False);
            Assert.That(town.TownPlayer.CameraController.enabled, Is.True);
            Assert.That(town.TownPlayer.RecruitedAllies.All(a => a.Hp == -1 && a.Mana == -1), Is.True);
            Assert.That(context.Grid, Is.SameAs(originalGrid)); Assert.That(context.State.Finished, Is.True);
            Assert.That(common.GameSaveData.Roster.Select(a => a.AllyId), Is.EquivalentTo(roster));
            Assert.That(town.TownPlayer.Gold, Is.EqualTo(4321));
            Assert.That(harness.Store.Writes, Is.EqualTo(writes)); Assert.That(harness.Store.Json, Is.EqualTo(checkpoint));
            Assert.That(Object.FindFirstObjectByType<GameOverScreen>(), Is.Null);
            var summary = Object.FindFirstObjectByType<CampaignSummaryDialog>();
            Assert.That(summary, Is.Not.Null); Assert.That(common.GameSaveData.Completion.Gold, Is.EqualTo(4321));
            Assert.That(common.GameSaveData.Completion.Party.Length, Is.EqualTo(roster.Length));
            Assert.That(common.GameSaveData.Completion.Party.ToDictionary(h => h.Id, h => h.Weapon), Is.EqualTo(expectedWeapons));
            Assert.That(summary.Heroes.Where(h => h.Root.gameObject.activeSelf).All(h => h.Portrait.sprite != null), Is.True);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(summary.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.isTextOverflowing), Is.False);
            System.IO.Directory.CreateDirectory("Temp/TodoVerification");
            ScreenCapture.CaptureScreenshot("Temp/TodoVerification/hall-of-fame.png"); yield return new WaitForEndOfFrame();
            summary.SaveButton.onClick.Invoke();
            Assert.That(harness.Store.Writes, Is.EqualTo(writes + 1)); Assert.That(summary.SaveButton.interactable, Is.False);
            Assert.That(SaveSystem.LoadData().Completion.Party.Select(h => h.Id), Is.EqualTo(common.GameSaveData.Completion.Party.Select(h => h.Id)));
            Assert.That(SaveSystem.LoadData().Completion.Party.ToDictionary(h => h.Id, h => h.Weapon), Is.EqualTo(expectedWeapons));
            summary.ContinueButton.onClick.Invoke(); yield return null;
            common.Travel.Continue();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true && !common.Travel.IsTransitioning, "completed save continues");
            Assert.That(common.CampaignContext.State.Finished, Is.True);
            common.CampaignContext.Position = common.CampaignContext.Grid.Locations[context.Campaign.FinalLocationId];
            Assert.That(common.CampaignContext.BeginDungeon(), Is.False);
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator RetreatResetsFinaleButRetainsLootAndDoesNotOverwriteCheckpoint()
        {
            yield return LoadFinale();
            var common = Common.Instance; var session = harness.Game.Finale.Session;
            string checkpoint = harness.Store.Json; int writes = harness.Store.Writes;
            var enemy = harness.Game.Enemies.OfType<Enemy>().First(e => !e.GetComponent<FinaleActor>().Hidden);
            Kill(enemy); harness.Game.Finale.EndTurn(); Assert.That(session.Defeated, Is.Not.Empty);
            harness.Game.PlayerController.Gold = 8765;
            GameOverScreen.Retreat(harness.Game.PlayerController);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "retreat town");
            Assert.That(common.CampaignContext.Finale, Is.Null); Assert.That(common.CampaignContext.State.Finished, Is.False);
            Assert.That(common.GameSaveData.TownSaveData.Gold, Is.EqualTo(8765));
            Assert.That(harness.Store.Json, Is.EqualTo(checkpoint)); Assert.That(harness.Store.Writes, Is.EqualTo(writes));
        }
    }
}
#endif
