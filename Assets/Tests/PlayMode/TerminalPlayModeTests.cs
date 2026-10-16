#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Linq;
using EternalEnigma.Core.Terminal;
using EternalEnigma.Core.World;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TerminalPlayModeTests
    {
        private GameTestHarness harness;
        private System.IDisposable preferences;
        private TestInputScope inputScope;
        [UnitySetUp] public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            inputScope = new TestInputScope();
            preferences = GamePreferences.Isolate();
            TerminalMode.SetRequested(false);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            var result = TestContext.CurrentContext.Result;
            System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalPlayModeResults.txt"),
                TestContext.CurrentContext.Test.Name + ": " + result.Outcome.Status + " " + (result.Message ?? "").Split('\n')[0] + "\n");
            TerminalMode.SetRequested(false);
            yield return harness.Cleanup();
            inputScope.Dispose();
            preferences.Dispose();
        }
        [UnityTest] public IEnumerator MainMenuToggleRestylesAndRestoresMenu()
        {
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var button = menu.StartButton.GetComponent<UnityEngine.UI.Button>();
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var originalText = label.text;
            var originalPosition = ((RectTransform)button.transform.parent).anchorMin;
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var view = menu.GetComponent<TerminalMainMenuView>();
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(view, Is.Not.Null);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(label.font, Is.SameAs(screen.FontAsset));
            Assert.That(label.text, Does.Contain("START"));
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("#####"));
            Assert.That(((RectTransform)button.transform.parent).anchorMin.x, Is.EqualTo(.31f).Within(.001f));
            yield return new WaitForSecondsRealtime(1.35f);
            int completeTitleLength = screen.GetComponentInChildren<TextMeshProUGUI>().text.Length;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalMainMenu.png"));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(.1f);
            var pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left, pointerId = -1 };
            var menuButton = (SelectToActivateButton)button;
            menuButton.OnPointerDown(pointer);
            menuButton.OnPointerClick(pointer);
            yield return null;
            var slots = Object.FindFirstObjectByType<CampaignSlots>(FindObjectsInactive.Include);
            Assert.That(slots.gameObject.activeSelf, Is.True, "One click opens the selected menu action.");
            slots.BackButton.onClick.Invoke();
            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(label.text, Is.EqualTo(originalText));
            Assert.That(((RectTransform)button.transform.parent).anchorMin, Is.EqualTo(originalPosition));
            TerminalMode.SetRequested(true);
            yield return null;
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text.Length, Is.LessThan(completeTitleLength));
            TerminalMode.SetRequested(false);
        }
        [UnityTest] public IEnumerator TownToggleKeepsControlledCellAndShowsAscii()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town = Object.FindFirstObjectByType<Town>();
            var before = town.TownPlayer.ControllingTownAlly.TilemapPosition;
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(screen, Is.Not.Null);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalTown.png"));
            yield return null;
            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(town.TownPlayer.ControllingTownAlly.TilemapPosition, Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DungeonTogglePreservesAnimationPreferenceAndKeepsTurn()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var before = harness.Ally.TilemapPosition;
            var savedOverride = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            try
            {
                TerminalMode.SetRequested(true);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(TerminalMode.Effective, Is.True);
                Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
                var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
                Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalDungeon.png"));
                yield return null;
                TerminalMode.SetRequested(false);
                yield return null;
                Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(before));
            }
            finally { DungeonPreferences.AnimationOverride = savedOverride; }
        }
        [UnityTest] public IEnumerator DungeonTerminalHidesDuplicateUiButKeepsMapAndHistory()
        {
            yield return harness.LoadDungeon(new TestScenario());
            GameMessages.Post("Before terminal mode");
            yield return null;
            var minimap = Object.FindFirstObjectByType<Minimap>();
            var messages = Object.FindFirstObjectByType<GameMessages>();
            Assert.That(minimap, Is.Not.Null);
            Assert.That(messages, Is.Not.Null);
            var messageCanvas = messages.GetComponentInChildren<Canvas>();
            Assert.That(messageCanvas, Is.Not.Null);
            Assert.That(minimap.minimapImage, Is.Not.Null);
            Assert.That(minimap.minimapImage.gameObject.activeSelf, Is.True);
            Assert.That(messageCanvas.enabled, Is.True);

            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(minimap.minimapImage.gameObject.activeSelf, Is.False);
            if (minimap.Frame != null) Assert.That(minimap.Frame.enabled, Is.False);
            Assert.That(minimap.dungeonMap, Is.Not.Null);
            Assert.That(messageCanvas.enabled, Is.False);
            GameMessages.Post("Terminal history remains live");
            Assert.That(messages.History, Does.Contain("Terminal history remains live"));

            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(minimap.minimapImage.gameObject.activeSelf, Is.True);
            if (minimap.Frame != null) Assert.That(minimap.Frame.enabled, Is.True);
            Assert.That(messageCanvas.enabled, Is.True);
        }
        [UnityTest] public IEnumerator OverworldToggleUsesLiveCampaignPosition()
        {
            yield return harness.LoadCommon();
            Common.Instance.BeginSandbox(42);
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            yield return harness.WaitUntil(() => world.IsReady, "terminal overworld");
            var before = world.Position;
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalOverworld.png"));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(.1f);
            TerminalMode.SetRequested(false);
            Assert.That(world.Position, Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DungeonFloorReplacementRefreshesTerminal()
        {
            yield return harness.LoadDungeon(new TestScenario());
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var oldFloor = harness.Game.CurrentDungeon;
            int number = harness.Game.PlayerController.Floor;
            harness.Game.AdvanceFloor();
            yield return harness.WaitUntil(() => harness.Game.IsReady && harness.Game.CurrentDungeon != oldFloor,
                "terminal next floor");
            yield return new WaitForSecondsRealtime(.25f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(number + 1));
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("Dungeon floor " + (number + 1)));
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
        }

        [UnityTest] public IEnumerator TerminalShopUsesProductionTransactionAndRestoresFocusAfterCancel()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var town = Object.FindFirstObjectByType<Town>();
            var bakery = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Bakery");
            var shop = (ShopMenuDialog)Object.FindFirstObjectByType<TownMenu>().OpenBuilding(bakery, town.TownPlayer, null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard };
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var button = shop.ShopItems[0].BuyButton;
            button.Select();
            yield return null;
            yield return Press(keyboard, Key.Enter);
            Assert.That(shop.BuyConfirmationDialog.gameObject.activeSelf, Is.True);
            Assert.That(MenuUIInputModule.Active.OwnsFocus(shop.BuyConfirmationDialog), Is.True);
            var bridge = MenuUIInputModule.Active.GetComponent<TerminalMenuBridge>();
            Assert.That(bridge.Model, Is.Not.Null);
            yield return Press(keyboard, Key.Escape);
            Assert.That(shop.BuyConfirmationDialog.gameObject.activeSelf, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(button.gameObject));
            Assert.That(town.TownPlayer.Gold, Is.EqualTo(10000));

            yield return Press(keyboard, Key.Enter);
            int stock = town.Services.Shop(bakery).Stock[0].Remaining;
            town.TownPlayer.Gold = 0; // The confirmation's displayed offer is now stale.
            yield return Press(keyboard, Key.Enter);
            Assert.That(town.TownPlayer.Gold, Is.Zero);
            Assert.That(town.TownPlayer.Inventory, Is.Empty);
            Assert.That(town.Services.Shop(bakery).Stock[0].Remaining, Is.EqualTo(stock));
            Assert.That(bridge.Model, Is.Not.Null, "Failure message remains an owned terminal dialog.");
        }

        [UnityTest] public IEnumerator DungeonKnowledgeMatchesRevealRulesAndLargeMimics()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var game = harness.Game;
            var dungeon = game.CurrentDungeon;
            var minimap = Object.FindFirstObjectByType<Minimap>();
            var cells = Enumerable.Range(1, dungeon.dungeonWidth - 2).SelectMany(x =>
                Enumerable.Range(1, dungeon.dungeonHeight - 2).Select(y => new Vector3Int(x, y)))
                .Where(p => dungeon.IsWalkable(p) && !game.AllCharacters.Any(c => c.TilemapPosition == p)).Take(6).ToArray();
            yield return harness.SpawnEnemy("Enemy_Slime", cells[5]);
            var enemy = (Enemy)game.Enemies.Last();
            enemy.FootPrint = FootPrint.Size3x3;
            var behavior = enemy.GetComponent<EnemyBehavior>() ?? enemy.gameObject.AddComponent<EnemyBehavior>();
            behavior.Mimic = true;

            T Add<T>(Vector3Int cell) where T : Interactable
            {
                var go = new GameObject("Terminal knowledge " + typeof(T).Name);
                go.transform.SetParent(dungeon.transform);
                var value = go.AddComponent<T>(); value.Position = cell; dungeon.Interactables.Add(value); return value;
            }
            var gold = Add<Gold>(cells[0]);
            var dropped = Add<DroppedItem>(cells[1]);
            var trap = Add<DamageTrap>(cells[2]);
            trap.VisualObject = new GameObject("Trap revealed visual");
            trap.VisualObject.transform.SetParent(trap.transform); trap.VisualObject.SetActive(false);
            var visual = new GameObject("Terminal scenery test visual");
            var container = DungeonProp.Create(dungeon, new DungeonScenery(new GridPoint(cells[3].x, cells[3].y),
                DungeonSceneryKind.Container, 10, 1, SceneryReward.Item), null, visual);
            var hazard = DungeonProp.Create(dungeon, new DungeonScenery(new GridPoint(cells[4].x, cells[4].y),
                DungeonSceneryKind.Hazard, 0, 1, SceneryReward.None), null, visual);
            Object.Destroy(visual);
            game.PartyVisibleTiles.Clear();
            for (int y = 0; y < dungeon.dungeonHeight; y++) for (int x = 0; x < dungeon.dungeonWidth; x++)
                minimap.dungeonMap[x, y].visibility = Minimap.MinimapTileVisibility.Unseen;
            game.FloorReveal.TreasureRevealed = false;
            game.FloorReveal.TrapsRevealed = false;
            game.FloorReveal.EnemiesRevealedTurns = 0;
            bool Contains(DungeonTerminalSnapshot snapshot, Object value) => snapshot.Actors.Any(a => a.Id == value.GetInstanceID().ToString());
            var unseen = TerminalSceneSnapshots.Dungeon(game, minimap);
            Assert.That(Contains(unseen, gold), Is.False);
            Assert.That(Contains(unseen, dropped), Is.False);
            Assert.That(Contains(unseen, container), Is.False);
            Assert.That(Contains(unseen, hazard), Is.False);
            Assert.That(Contains(unseen, trap), Is.False);
            Assert.That(Contains(unseen, enemy), Is.False);
            game.FloorReveal.TreasureRevealed = true;
            var treasure = TerminalSceneSnapshots.Dungeon(game, minimap);
            Assert.That(Contains(treasure, gold), Is.True);
            Assert.That(Contains(treasure, dropped), Is.True);
            Assert.That(Contains(treasure, container), Is.True);
            Assert.That(Contains(treasure, trap), Is.False, "Treasure sense must not disclose traps.");
            Assert.That(Contains(treasure, enemy), Is.False, "A disguised mimic is still an actor, not revealed loot.");
            game.FloorReveal.TreasureRevealed = false;
            minimap.dungeonMap[cells[4].x, cells[4].y].visibility = Minimap.MinimapTileVisibility.Explored;
            game.PartyVisibleTiles.UnionWith(new[] { cells[1], cells[2], cells[3], cells[5] + Vector3Int.left });
            var known = TerminalSceneSnapshots.Dungeon(game, minimap);
            Assert.That(Contains(known, dropped), Is.True);
            Assert.That(Contains(known, container), Is.True);
            Assert.That(known.Actors.Single(a => a.Id == hazard.GetInstanceID().ToString()).VisibleInFog, Is.True);
            Assert.That(Contains(known, trap), Is.False, "A trap in sight still needs to be discovered.");
            var footprint = known.Actors.Where(a => a.Id == enemy.GetInstanceID().ToString()).ToArray();
            Assert.That(footprint.Length, Is.EqualTo(9));
            Assert.That(footprint.All(a => a.Glyph == 'i' && a.Kind == TerminalActorKind.Item && a.VisibleInFog), Is.True);
            behavior.Mimic = false;
            enemy.CharacterName = "Ice Golem";
            var undisguised = TerminalSceneSnapshots.Dungeon(game, minimap);
            var enemyFootprint = undisguised.Actors.Where(a => a.Id == enemy.GetInstanceID().ToString()).ToArray();
            Assert.That(enemyFootprint.Length, Is.EqualTo(9));
            Assert.That(enemyFootprint.All(a => a.Glyph == 'i' && a.Kind == TerminalActorKind.Enemy), Is.True,
                "An enemy's initial can match the item glyph without disguising it as loot.");
            var renderer = new DungeonTerminalRenderer();
            var enemyCell = new GridPoint(enemy.TilemapPosition.x, enemy.TilemapPosition.y);
            renderer.Render(undisguised, 40, 20, enemyCell);
            Assert.That(renderer.TryFrameCell(enemyCell, out int enemyColumn, out int enemyRow), Is.True);
            Assert.That(renderer.Frame[enemyColumn, enemyRow].Color, Is.EqualTo(TerminalPalette.Enemy));
            trap.VisualObject.SetActive(true);
            Assert.That(Contains(TerminalSceneSnapshots.Dungeon(game, minimap), trap), Is.True);
            game.PartyVisibleTiles.Remove(cells[2]);
            Assert.That(Contains(TerminalSceneSnapshots.Dungeon(game, minimap), trap), Is.False);
            game.FloorReveal.TrapsRevealed = true;
            Assert.That(Contains(TerminalSceneSnapshots.Dungeon(game, minimap), trap), Is.True);
            Assert.That(Contains(unseen, hazard), Is.False, "Earlier captures do not mutate when knowledge changes.");
        }

        [UnityTest] public IEnumerator TargetPreviewSharesMissileTraceAndCancelOrStaleConfirmSpendsNothing()
        {
            yield return harness.LoadDungeon(new TestScenario { SP = 20 });
            var game = harness.Game;
            var caster = harness.Ally;
            var dungeon = game.CurrentDungeon;
            var center = Enumerable.Range(1, dungeon.dungeonWidth - 2).SelectMany(x =>
                Enumerable.Range(1, dungeon.dungeonHeight - 2).Select(y => new Vector3Int(x, y)))
                .First(p => Enumerable.Range(-1, 3).All(dx => Enumerable.Range(-1, 3).All(dy => dungeon.IsWalkable(p + new Vector3Int(dx, dy)))));
            caster.SetPosition(center);
            caster.SetFacingByTargetPosition(center + Vector3Int.right);
            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right);
            var enemy = (Enemy)game.Enemies.Last();
            enemy.Policies.Clear();
            game.UpdateMiniMap();
            var skill = Common.Instance.SkillManager.GetSkillInstanceByName("Damage");
            try
            {
                skill.Targeting = SkillTargeting.Missile; skill.MissileRange = 4; skill.AreaRadius = 1;
                skill.TargetSelector.Area = TargetArea.All; caster.Skills.Add(skill);
                int sp = caster.Vitals.SP, gold = game.PlayerController.Gold;
                MenuManager.Instance.OpenTargetingMenu(caster, skill);
                var dialog = MenuManager.Instance.TargetDialog;
                var expected = MissileTargeting.TraceLine(caster, Vector3Int.right, 4, 1, true);
                Assert.That(dialog.MissileEndpoint, Is.EqualTo(expected.Endpoint));
                Assert.That(dialog.TerminalMissilePath.Last(), Is.EqualTo(expected.Endpoint));
                Assert.That(dialog.TerminalLabel, Does.Contain("Aiming east"));
                Assert.That(dialog.TerminalAreaCells.Count(), Is.GreaterThan(1));
                var snapshot = TerminalSceneSnapshots.Dungeon(game, Object.FindFirstObjectByType<Minimap>(), targetDialog: dialog);
                Assert.That(snapshot.Target.Path.Count, Is.GreaterThan(0));
                Assert.That(snapshot.Target.Area.Count, Is.GreaterThan(1));
                MenuManager.Close(dialog);
                Assert.That(caster.Vitals.SP, Is.EqualTo(sp));
                Assert.That(game.PlayerController.Gold, Is.EqualTo(gold));

                skill.Targeting = SkillTargeting.SelectedTarget;
                MenuManager.Instance.OpenTargetingMenu(caster, skill);
                enemy.Vitals.HP = 0;
                dialog.ConfirmTarget();
                Assert.That(dialog.enabled, Is.False);
                Assert.That(caster.Vitals.SP, Is.EqualTo(sp));
                Assert.That(game.TurnManager.IsProcessingTurn, Is.False);
            }
            finally { caster.Skills.Remove(skill); Object.Destroy(skill); }
        }

        [UnityTest] public IEnumerator SharedSaveContinuesBothDirectionsWithMatchingPartyInventoryGoldAndProgression()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            var menu = Object.FindFirstObjectByType<MainMenu>();
            yield return harness.WaitUntil(() => menu.IsReady, "terminal new campaign menu");
            TerminalMode.SetRequested(true);
            common.GameSaveData = menu.CreateNewSave(42);
            common.Travel.NewCampaign(42);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !common.ScreenTransition.BlockScreen.activeSelf, "terminal campaign town");
            var town = Object.FindFirstObjectByType<Town>();
            town.TownPlayer.Gold = 10000;
            // A progressed fixture verifies these collections survive the presentation boundary.
            common.CampaignContext.Completed.Add("story-0");
            common.CampaignContext.Keys.Add("Town gate key");
            foreach (var hero in town.TownPlayer.RecruitedAllies) hero.EnsureStartingSkills();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var bakery = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Bakery");
            foreach (bool sourceTerminal in new[] { true, false })
            {
                TerminalMode.SetRequested(sourceTerminal);
                MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard };
                var townMenu = Object.FindFirstObjectByType<TownMenu>();
                var shop = (ShopMenuDialog)townMenu.OpenBuilding(bakery, town.TownPlayer, null);
                yield return null;
                int beforeGold = town.TownPlayer.Gold;
                int price = shop.ShopItemDatas[0].Cost;
                shop.ShopItems[0].BuyButton.Select();
                yield return null;
                yield return Press(keyboard, Key.Enter);
                yield return Press(keyboard, Key.Enter);
                Assert.That(town.TownPlayer.Gold, Is.EqualTo(beforeGold - price));
                shop.CloseDialog();
                var inn = town.TownBuildings.Single(b => b.Definition.Id == "inn");
                var saveDialog = (InnDialog)townMenu.OpenBuilding(inn.Definition, town.TownPlayer, null);
                yield return null;
                saveDialog.SaveButton.Select();
                yield return null;
                yield return Press(keyboard, Key.Enter);
                Assert.That(common.MessageDialog.PromptText.text, Does.Contain("Game saved"));
                common.MessageDialog.Ok_Clicked();
                var saved = SaveSystem.LoadData();
                string savedJson = harness.Store.Json;
                int writes = harness.Store.Writes;
                Assert.That(saved.SavePointId, Does.EndWith("/inn"));
                TerminalMode.SetRequested(!sourceTerminal);
                yield return harness.WaitUntil(() => TerminalMode.Effective == !sourceTerminal,
                    "selected presentation before returning to the menu");
                Assert.That(town.TownPlayer.Gold, Is.EqualTo(saved.TownSaveData.Gold));
                Assert.That(harness.Store.Json, Is.EqualTo(savedJson), "Changing presentation cannot write a save.");
                Assert.That(common.Travel.ReturnToMenu(), Is.True);
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true &&
                    !common.ScreenTransition.BlockScreen.activeSelf, "continue in the other presentation");
                menu = Object.FindFirstObjectByType<MainMenu>();
                MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard };
                menu.Continue_Clicked();
                yield return null;
                yield return Press(keyboard, Key.Enter);
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>() != town &&
                    Object.FindFirstObjectByType<Town>()?.IsReady == true, "restored terminal/3D town under its cover");
                town = Object.FindFirstObjectByType<Town>();
                Assert.That(common.ScreenTransition.BlockScreen.activeSelf, Is.True,
                    "Inspect the restored presentation before the destination is revealed.");
                Assert.That(TerminalMode.Effective, Is.EqualTo(!sourceTerminal),
                    "A scene replacement must retain the selected presentation beneath its cover.");
                Assert.That(Object.FindFirstObjectByType<GameMessages>().GetComponentInChildren<Canvas>().enabled,
                    Is.EqualTo(sourceTerminal), "The destination binds its event feed while travel is still covered.");
                yield return harness.WaitUntil(() => !common.ScreenTransition.BlocksInput, "restored town reveal");
                Assert.That(TerminalMode.Effective, Is.EqualTo(!sourceTerminal));
                Assert.That(town.TownPlayer.Gold, Is.EqualTo(saved.TownSaveData.Gold));
                Assert.That(ItemSaveData.Capture(town.TownPlayer.Inventory).Select(i => JsonUtility.ToJson(i)),
                    Is.EqualTo(saved.TownSaveData.InventoryItems.Select(i => JsonUtility.ToJson(i))));
                town.WriteSaveData();
                Assert.That(common.GameSaveData.TownSaveData.RecruitedAlliesData.Select(a => JsonUtility.ToJson(a)),
                    Is.EqualTo(saved.TownSaveData.RecruitedAlliesData.Select(a => JsonUtility.ToJson(a))));
                var restored = common.CampaignContext.Capture();
                Assert.That(restored.Completed, Is.EquivalentTo(saved.Campaign.Completed));
                Assert.That(restored.Keys, Is.EquivalentTo(saved.Campaign.Keys));
                Assert.That(restored.Claimed, Is.EquivalentTo(saved.Campaign.Claimed));
                Assert.That(restored.Resolved, Is.EquivalentTo(saved.Campaign.Resolved));
                Assert.That(restored.Active, Is.EqualTo(saved.Campaign.Active));
                Assert.That(restored.Roster, Is.EquivalentTo(saved.Campaign.Roster));
                Assert.That(restored.LocationId, Is.EqualTo(saved.Campaign.LocationId));
                Assert.That(harness.Store.Json, Is.EqualTo(savedJson));
                Assert.That(harness.Store.Writes, Is.EqualTo(writes), "Continue uses the explicit save without rewriting it.");
            }
        }

        private static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
#endif
