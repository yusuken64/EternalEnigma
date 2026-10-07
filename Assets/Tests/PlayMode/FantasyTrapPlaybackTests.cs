#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.Generation;
using NUnit.Framework;
using TWC;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class FantasyTrapPlaybackTests
    {
        GameObject root;
        Game game;
        Ally ally;
        Camera camera;
        TileWorldCreatorAsset asset;
        [SetUp] public void Setup()
        {
            root = new GameObject("Trap playback fixture");
            game = root.AddComponent<Game>(); game.enabled = false;
            game.Allies = new(); game.Enemies = new(); game.DeadUnits = new(); game.StatusEffectPrefabs = new();
            game.PlayerController = root.AddComponent<PlayerController>(); game.PlayerController.enabled = false;
            game.PlayerController.Inventory = root.AddComponent<Inventory>();
            var creator = root.AddComponent<TileWorldCreator>(); creator.enabled = false;
            asset = ScriptableObject.CreateInstance<TileWorldCreatorAsset>(); asset.cellSize = 2.5f; creator.twcAsset = asset;
            game.CurrentDungeon = root.AddComponent<TileWorldDungeon>(); game.CurrentDungeon.enabled = false; game.CurrentDungeon.Interactables = new();
            game.CurrentDungeon.Setup(creator, DungeonFloorGenerator.Generate(new DungeonFloorOptions(42)));
            var actor = new GameObject("Test hero"); actor.transform.SetParent(root.transform);
            ally = actor.AddComponent<Ally>(); ally.enabled = false; ally.Equipment = actor.AddComponent<Equipment>(); ally.Skills = new();
            ally.VisualParent = new GameObject("Hero visual"); ally.VisualParent.transform.SetParent(actor.transform);
            ally.BaseStats = new Stats { HPMax = 100, Strength = 10 }; ally.Vitals = new Vitals(); ally.Vitals.HP = 100;
            ally.DisplayedVitals = new Vitals(); ally.DisplayedStats.Sync(ally.FinalStats); ally.DisplayedVitals.HP = 100;
            ally.TilemapPosition = game.CurrentDungeon.GetStartPosition(); actor.transform.position = game.CurrentDungeon.CellToWorld(ally.TilemapPosition);
            game.Allies.Add(ally);
            var cameraObject = new GameObject("Trap test camera"); cameraObject.transform.SetParent(root.transform);
            camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 8;
            camera.transform.position = actor.transform.position + new Vector3(1.25f,1.25f,-30); camera.transform.rotation = Quaternion.identity;
            game.PlayerController.CameraController = root.AddComponent<CameraController>(); game.PlayerController.CameraController.Camera = camera;
            var messages = new GameObject("Trap test messages"); messages.transform.SetParent(root.transform);
            messages.SetActive(false); messages.AddComponent<GameMessages>().AuthorLayout(true); messages.SetActive(true);
            var canvas = GameUISkin.Canvas("Combat callouts", game.transform, 0);
            var callouts = canvas.gameObject.AddComponent<CombatCalloutView>(); callouts.Root = (RectTransform)canvas.transform;
            callouts.Normal = Resources.Load<DungeonFloatingText>("UI/Authored/Callout");
            callouts.Prominent = Resources.Load<DungeonFloatingText>("UI/Authored/ProminentCallout");
            typeof(Game).GetProperty("IsReady").SetValue(game, true);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var messages = Object.FindFirstObjectByType<GameMessages>(); if (messages != null) Object.Destroy(messages.gameObject);
            Object.Destroy(root); Object.Destroy(asset); yield return null;
        }

        [UnityTest] public IEnumerator ProjectilesEnterFromOffscreenAndHistoryExplainsHitAndEvade()
        {
            foreach (var id in new[] { "LogProjectile", "ArrowProjectile", "IronArrowProjectile" })
            {
                var action = new TrapProjectileAction(ally, id); action.ExecuteImmediate(ally);
                var routine = action.ExecuteRoutine(ally);
                Assert.That(routine.MoveNext(), Is.True);
                var projectile = GameObject.Find(id + " flight"); Assert.That(projectile, Is.Not.Null);
                var bounds = projectile.GetComponent<Renderer>().bounds;
                Assert.That(camera.WorldToViewportPoint(bounds.max).x, Is.LessThan(0), "The whole projectile must start offscreen.");
                yield return routine.Current;
                var center = projectile.GetComponent<Renderer>().bounds.center;
                Assert.That(camera.WorldToViewportPoint(center).x, Is.InRange(.45f,.55f));
                Assert.That(routine.MoveNext(), Is.False); yield return null;
                Assert.That(projectile == null, Is.True);
                var skipped = action.ExecuteRoutine(ally, true); Assert.That(skipped.MoveNext(), Is.False);
            }
            var trap = Object.Instantiate(FantasyTrap.PrefabFor(0), root.transform); trap.Position = ally.TilemapPosition;
            trap.ActivationChance = 0;
            var evade = new TrapResolutionAction(trap, ally, Vector3Int.right); evade.ExecuteImmediate(ally);
            yield return evade.ExecuteRoutine(ally);
            Assert.That(Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Any(t => t.text == "Evaded"), Is.True);
            var messages = Object.FindFirstObjectByType<GameMessages>();
            Assert.That(messages.History.Any(m => m.Contains("evaded the Arrow Trap") && m.Contains("no trap effects")), Is.True);
            trap.ActivationChance = 1;
            new TrapResolutionAction(trap, ally, Vector3Int.right).ExecuteImmediate(ally);
            Assert.That(messages.History.Any(m => m.Contains("triggered the Arrow Trap")), Is.True);
            Assert.That(messages.History.Any(m => m.Contains("took 10 damage")), Is.True);
            Assert.That(ally.Vitals.HP, Is.EqualTo(90));
        }

        [UnityTest] public IEnumerator CombatTextCentersOnVisualParent()
        {
            ally.VisualParent.transform.localPosition = new Vector3(1.25f, 1.25f, 0);
            DungeonFloatingText.Show(game, "Centered", Color.white, ally);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var label = root.GetComponentsInChildren<TMPro.TMP_Text>().Single(t => t.text == "Centered");
            var canvas = label.GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
                camera.WorldToScreenPoint(ally.VisualParent.transform.position), null, out var expected);
            Assert.That(label.rectTransform.anchoredPosition.x, Is.EqualTo(expected.x).Within(.01f));
            Assert.That(label.rectTransform.pivot, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(label.alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Center));
        }
    }
}
#endif
