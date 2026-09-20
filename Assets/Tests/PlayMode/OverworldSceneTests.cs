using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class OverworldSceneTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private Gamepad pad;

        [UnityTest]
        public IEnumerator AuthoredSceneBuildsCampaignAndMovesHeroWithSealedGates()
        {
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            float deadline = Time.realtimeSinceStartup + 90;
            while (!world.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.IsReady, Is.True, "Terrain build did not complete.");
            Assert.That(world.Player, Is.Not.Null);
            Assert.That(world.Position, Is.EqualTo(world.Map.CurrentGrid.PlayerStart));
            Assert.That(world.Map.GetComponent<TWC.TileWorldCreator>().worldObject.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(0));
            Assert.That(world.Map.Template.mapWidth, Is.EqualTo(256));
            Assert.That(world.Map.GetComponent<TWC.TileWorldCreator>().twcAsset, Is.Not.SameAs(world.Map.Template));
            var biomeRenderer = world.Map.GetComponent<OverworldBiomeRenderer>();
            Assert.That(biomeRenderer, Is.Not.Null);
            Assert.That(biomeRenderer.Biomes.Length, Is.EqualTo(8));
            Assert.That(biomeRenderer.BarrierMaterial, Is.Not.Null);
            Assert.That(biomeRenderer.Biomes.All(b => b.Material != null && b.Material.mainTexture != null), Is.True);
            Assert.That(biomeRenderer.Biomes.Select(b => b.Material.color).Distinct().Count(), Is.EqualTo(8));
            Assert.That(biomeRenderer.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled && r.sharedMaterial == biomeRenderer.Biomes.First(b => b.Biome == OverworldBiome.Water).Material), Is.True);
            var grid = world.Map.CurrentGrid;
            var water = Enumerable.Range(0, grid.Width * grid.Height).Select(i => new GridPoint(i % grid.Width, i / grid.Width))
                .First(p => grid.RequiresBoat(p));
            Assert.That(grid.IsWalkable(water, CapabilitySet.Empty), Is.False);
            Assert.That(grid.IsWalkable(water, CapabilitySet.Of(Capability.Boat)), Is.True);
            foreach (var gate in world.Map.CurrentGrid.Locks)
                Assert.That(world.Map.CurrentGrid.IsWalkable(gate.Cells[0], world.Held), Is.False);
            var start = world.Position;
            var next = OverworldMovement.Neighbors(start).First(p => world.Map.CurrentGrid.CanStep(start, p, world.Held));
            Assert.That(world.TryMove(next.X - start.X, next.Y - start.Y), Is.True);
            yield return new WaitForSeconds(.25f);
            Assert.That(world.Position, Is.EqualTo(next));
            Assert.That(Vector3.Distance(world.Player.transform.position, world.CellToWorld(next)), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(world.ViewCamera.transform.position, world.CellCenterToWorld(next) + world.CameraOffset), Is.LessThan(.01f));
            Assert.That(world.TryMove(2, 0), Is.False);
            Assert.That(world.TryMove(start.X - next.X, start.Y - next.Y), Is.True);
            yield return new WaitForSeconds(.25f);
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            Assert.That(world.CanStep(start, new GridPoint(start.X + 1, start.Y)), Is.True);
            float inputDeadline = Time.realtimeSinceStartup + 2;
            while (world.Position.Equals(start) && Time.realtimeSinceStartup < inputDeadline) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.25f);
            Assert.That(world.Position, Is.EqualTo(new GridPoint(start.X + 1, start.Y)), "Keyboard should move one cell.");
            pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadLeft));
            inputDeadline = Time.realtimeSinceStartup + 2;
            while (!world.Position.Equals(start) && Time.realtimeSinceStartup < inputDeadline) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return new WaitForSeconds(.25f);
            Assert.That(world.Position, Is.EqualTo(start), "Gamepad should return to the start town.");
            world.ClaimRewards();
            Assert.That(world.Message, Does.Not.Contain("Generating"));

            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            var previousTarget = world.ViewCamera.targetTexture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                world.ViewCamera.targetTexture = target;
                world.ViewCamera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Temp/OverworldScene");
                File.WriteAllBytes("Temp/OverworldScene/play-preview.png", image.EncodeToPNG());
                var cameraPosition = world.ViewCamera.transform.position;
                var cameraRotation = world.ViewCamera.transform.rotation;
                float cameraSize = world.ViewCamera.orthographicSize;
                try
                {
                    Vector3 center = world.CellToWorld(new GridPoint(grid.Width / 2, grid.Height / 2));
                    world.ViewCamera.transform.position = center + Vector3.back * 100;
                    world.ViewCamera.transform.LookAt(center, Vector3.up);
                    world.ViewCamera.orthographicSize = grid.Height * world.Map.Template.cellSize * .53f;
                    world.ViewCamera.Render();
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    File.WriteAllBytes("Temp/OverworldScene/biome-overview.png", image.EncodeToPNG());
                }
                finally
                {
                    world.ViewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                    world.ViewCamera.orthographicSize = cameraSize;
                }
            }
            finally
            {
                world.ViewCamera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.Destroy(image);
                Object.Destroy(target);
            }

            // Walk through actual scene movement to the nearest currently closed gate.
            var parents = new Dictionary<GridPoint, GridPoint>();
            var pending = new Queue<GridPoint>();
            pending.Enqueue(world.Position);
            parents[world.Position] = world.Position;
            GridPoint? approach = null, blockedGate = null;
            while (pending.Count > 0 && approach == null)
            {
                var at = pending.Dequeue();
                foreach (var cell in OverworldMovement.Neighbors(at))
                {
                    if ((cell.X == at.X || cell.Y == at.Y) && world.Map.CurrentGrid.LockAt(cell) != null && !world.Map.CurrentGrid.IsWalkable(cell, world.Held))
                    { approach = at; blockedGate = cell; break; }
                    if (parents.ContainsKey(cell) || !world.Map.CurrentGrid.CanStep(at, cell, world.Held)) continue;
                    parents[cell] = at;
                    pending.Enqueue(cell);
                }
            }
            Assert.That(approach.HasValue, Is.True);
            var path = new Stack<GridPoint>();
            for (var at = approach.Value; !at.Equals(world.Position); at = parents[at]) path.Push(at);
            while (path.Count > 0)
            {
                var cell = path.Pop();
                Assert.That(world.TryMove(cell.X - world.Position.X, cell.Y - world.Position.Y), Is.True);
                float moveDeadline = Time.realtimeSinceStartup + 3;
                while (world.IsMoving && Time.realtimeSinceStartup < moveDeadline) yield return null;
                Assert.That(world.IsMoving, Is.False, "Hero movement did not finish.");
            }
            Assert.That(world.TryMove(blockedGate.Value.X - world.Position.X, blockedGate.Value.Y - world.Position.Y), Is.False);
            Assert.That(world.Position, Is.EqualTo(approach.Value));
            var blockedRoute = world.Campaign.Routes.Single(r => r.Id == world.Map.CurrentGrid.LockAt(blockedGate.Value).RouteId);
            Assert.That(world.Message, blockedRoute.ShortcutKind == ShortcutKind.FarSide ? Does.Contain("Open shortcut") : Does.Contain("Requires:"));
        }

        [UnityTest] public IEnumerator RequiredReturnTripSeedZero() => RequiredReturnTrip(0);
        [UnityTest] public IEnumerator RequiredReturnTripSeed42() => RequiredReturnTrip(42);
        [UnityTest] public IEnumerator RequiredReturnTripSeedNegativeOne() => RequiredReturnTrip(-1);

        private IEnumerator RequiredReturnTrip(int seed)
        {
            void Configure(Scene loaded, LoadSceneMode mode)
            {
                if (loaded.name != "Overworld") return;
                foreach (var map in loaded.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignOverworld>())) map.Seed = seed;
            }
            SceneManager.sceneLoaded += Configure;
            try { yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive); }
            finally { SceneManager.sceneLoaded -= Configure; }
            scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            float deadline = Time.realtimeSinceStartup + 90;
            while (!world.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.IsReady, Is.True);
            Assert.That(world.Campaign.Seed, Is.EqualTo(seed));
            var c = world.Campaign; var grid = world.Map.CurrentGrid;
            var objective = c.ReturnObjectives.Single(o => o.Required);
            var gate = grid.Locks.Single(g => g.RouteId == objective.GateIds[0]);
            var approach = gate.Cells.SelectMany(OverworldMovement.Neighbors).Distinct()
                .Where(p => gate.Cells.Any(g => System.Math.Abs(g.X - p.X) + System.Math.Abs(g.Y - p.Y) == 1))
                .First(p => Path(world, p) != null);
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 15;
                yield return WalkTo(world, approach);
                var blocked = gate.Cells.First(g => System.Math.Abs(g.X - approach.X) + System.Math.Abs(g.Y - approach.Y) == 1);
                Assert.That(world.TryMove(blocked.X - approach.X, blocked.Y - approach.Y), Is.False);
                Assert.That(world.Message, Does.Contain(objective.EnablingCapability.ToString()));
                var engineering = c.Sources.First(source => source.Capability == Capability.Engineering);
                yield return WalkTo(world, grid.Locations[engineering.LocationId]); world.ClaimRewards();
                Assert.That(world.Held.Contains(Capability.Engineering), Is.True);
                var enabling = c.Sources.First(source => source.Capability == objective.EnablingCapability);
                Assert.That(c.Locations.Single(l => l.Id == enabling.LocationId).RegionId, Is.Not.EqualTo(objective.RegionId));
                yield return WalkTo(world, grid.Locations[enabling.LocationId]); world.ClaimRewards();
                yield return WalkTo(world, grid.PlayerStart);
                if (enabling.CompanionId != null) Assert.That(world.ToggleCompanion(enabling.CompanionId), Is.True);
                Assert.That(world.Held.Contains(objective.EnablingCapability), Is.True);
                var reward = c.Sources.First(source => source.Capability == objective.RewardCapability);
                yield return WalkTo(world, grid.Locations[reward.LocationId]); world.ClaimRewards();
                yield return WalkTo(world, grid.PlayerStart);
                if (reward.CompanionId != null) Assert.That(world.ToggleCompanion(reward.CompanionId), Is.True);
                Assert.That(world.Held.Contains(objective.RewardCapability.Value), Is.True);
                yield return WalkTo(world, grid.Locations["checkpoint-3"]);
                Assert.That(world.Position, Is.EqualTo(grid.Locations["checkpoint-3"]));
                // Reach each later biome normally, collect its key, and walk the passage both ways.
                for (int later = 3; later <= 5; later++)
                {
                    if (later > 3)
                    {
                        var boundary = c.Routes.Single(r => r.IsProgressionBoundary && r.From == "checkpoint-" + (later - 1));
                        var critical = c.Manifest.Where(m => m.Role == CapabilityRole.Critical).Select(m => m.Id).ToArray();
                        var capability = boundary.Requirement.Alternatives.SelectMany(a => a.Values).First(critical.Contains);
                        var source = c.Sources.First(s => s.Capability == capability);
                        yield return WalkTo(world, grid.Locations[source.LocationId]); world.ClaimRewards();
                        if (source.CompanionId != null && !world.Held.Contains(capability))
                        {
                            var town = c.Locations.Where(l => l.Kind == LocationKind.Town).Select(l => grid.Locations[l.Id])
                                .Select(p => new { point = p, path = Path(world, p) }).Where(p => p.path != null).OrderBy(p => p.path.Count).First().point;
                            yield return WalkTo(world, town); Assert.That(world.ToggleCompanion(source.CompanionId), Is.True);
                        }
                        yield return WalkTo(world, grid.Locations["checkpoint-" + later]);
                    }
                    var shortcut = c.Routes.Single(r => r.ShortcutKind == ShortcutKind.Keyed && r.To == "checkpoint-" + later);
                    Assert.That(shortcut.IsWarp, Is.True);
                    Assert.That(grid.Locks.Any(g => g.RouteId == shortcut.Id), Is.False);
                    Assert.That(world.Warp(shortcut.Id), Is.False);
                    Assert.That(world.Message, Does.Contain(shortcut.KeyId));
                    Assert.That(world.OpenShortcut(), Is.False);
                    yield return WalkTo(world, grid.Locations[shortcut.KeyLocationId]); world.ClaimRewards();
                    Assert.That(world.CollectedKeys, Does.Contain(shortcut.KeyId));
                    yield return WalkTo(world, grid.Locations[shortcut.To]);
                    Assert.That(world.Warp(shortcut.Id), Is.True);
                    Assert.That(world.Position, Is.EqualTo(grid.Locations[shortcut.From]));
                    Assert.That(Vector3.Distance(world.Player.transform.position, world.CellToWorld(world.Position)), Is.LessThan(.01f));
                    Assert.That(world.Warp(shortcut.Id), Is.True);
                    Assert.That(world.Position, Is.EqualTo(grid.Locations[shortcut.To]));
                }
                CaptureOverview(world, "Temp/OverworldScene/biome-" + seed + ".png");
            }
            finally { Time.timeScale = previousTimeScale; }
        }

        private static Stack<GridPoint> Path(OverworldScene world, GridPoint target)
        {
            var parents = new Dictionary<GridPoint, GridPoint> { [world.Position] = world.Position };
            var queue = new Queue<GridPoint>(); queue.Enqueue(world.Position);
            while (queue.Count > 0 && !parents.ContainsKey(target))
            {
                var at = queue.Dequeue();
                foreach (var next in OverworldMovement.Neighbors(at))
                    if (!parents.ContainsKey(next) && world.CanStep(at, next)) { parents[next] = at; queue.Enqueue(next); }
            }
            if (!parents.ContainsKey(target)) return null;
            var path = new Stack<GridPoint>();
            for (var at = target; !at.Equals(world.Position); at = parents[at]) path.Push(at);
            return path;
        }

        private static IEnumerator WalkTo(OverworldScene world, GridPoint target)
        {
            var path = Path(world, target);
            Assert.That(path, Is.Not.Null, "No walkable path to " + target + " with " + world.Held);
            while (path.Count > 0)
            {
                var next = path.Pop();
                Assert.That(world.TryMove(next.X - world.Position.X, next.Y - world.Position.Y), Is.True);
                float deadline = Time.realtimeSinceStartup + 3;
                while (world.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(world.IsMoving, Is.False);
            }
        }

        private static void CaptureOverview(OverworldScene world, string path)
        {
            var camera = world.ViewCamera; var grid = world.Map.CurrentGrid;
            var target = new RenderTexture(1024, 1024, 24); var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            var position = camera.transform.position; var rotation = camera.transform.rotation; float size = camera.orthographicSize;
            try
            {
                Vector3 center = world.CellToWorld(new GridPoint(grid.Width / 2, grid.Height / 2));
                camera.transform.position = center + Vector3.back * 100; camera.transform.LookAt(center, Vector3.up);
                camera.orthographicSize = grid.Height * world.Map.Template.cellSize * .53f; camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); image.Apply();
                Directory.CreateDirectory("Temp/OverworldScene"); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previous;
                camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size;
                Object.Destroy(target); Object.Destroy(image);
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
