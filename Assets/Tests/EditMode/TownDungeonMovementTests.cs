using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    public sealed class TownDungeonMovementTests
    {
        const int Size = 16;
        static readonly Vector3Int Start = new(5, 5), Goal = new(6, 6);
        GameObject root;
        Game game;
        TileWorldDungeon dungeon;
        TileWorldCreator creator;
        TileWorldCreatorAsset asset;
        Ally ally;
        Dictionary<string, bool[,]> layers;

        [SetUp] public void Setup()
        {
            root = new GameObject("Town movement fixture");
            game = root.AddComponent<Game>();
            game.Allies = new(); game.Enemies = new(); game.DeadUnits = new(); game.StatusEffectPrefabs = new();
            game.PlayerController = root.AddComponent<PlayerController>();
            creator = root.AddComponent<TileWorldCreator>();
            asset = ScriptableObject.CreateInstance<TileWorldCreatorAsset>(); asset.cellSize = 2.5f; creator.twcAsset = asset;
            dungeon = root.AddComponent<TileWorldDungeon>(); dungeon.Interactables = new(); game.CurrentDungeon = dungeon;
            layers = TownLayers.All.Concat(TownLayers.InteriorLayers).Append(TownLayers.Boundary)
                .ToDictionary(name => name, _ => new bool[Size, Size]);
            for (int i = 0; i < Size; i++)
                layers[TownLayers.Boundary][0,i] = layers[TownLayers.Boundary][i,0] =
                    layers[TownLayers.Boundary][Size-1,i] = layers[TownLayers.Boundary][i,Size-1] = true;
            Build();
            ally = Actor<Ally>(Start); game.Allies.Add(ally);
        }

        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }

        // Build disposable data through the Core constructors; no cached generated floor or asset is mutated.
        static T Construct<T>(Dictionary<string, object> arguments)
        {
            var constructor = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (T)constructor.Invoke(constructor.GetParameters()
                .Select(p => arguments.TryGetValue(p.Name, out var value) ? value : p.DefaultValue).ToArray());
        }

        void Paint(string layer, Vector3Int cell) => layers[layer][cell.x,cell.y] = true;

        void Build(bool town = true)
        {
            var walkable = layers[TownLayers.Walkable];
            var blocked = new bool[Size,Size];
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++)
            {
                blocked[x,y] = new[] { TownLayers.Houses, TownLayers.Trees, TownLayers.ShopWalls, TownLayers.Furniture, TownLayers.Boundary }
                    .Any(name => layers[name][x,y]);
                walkable[x,y] = !blocked[x,y];
            }
            var doors = new List<GridPoint>();
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                if (layers[TownLayers.Buildings][x,y]) doors.Add(new GridPoint(x,y));
            var plan = Construct<TownPlan>(new()
            {
                ["seed"] = 42, ["layers"] = layers.ToDictionary(p => p.Key, p => new GridLayer(p.Value)),
                ["buildingSlots"] = doors, ["allySlots"] = Array.Empty<Placement>(), ["shopRooms"] = Array.Empty<ShopRoom>(),
                ["partySpawn"] = new GridPoint(2,2), ["exit"] = new GridPoint(2,3), ["dungeonEntrance"] = new GridPoint(2,4)
            });
            var floor = Construct<DungeonFloor>(new()
            {
                ["seed"] = 42, ["isThroneFloor"] = false,
                ["layers"] = new Dictionary<string, GridLayer> { [DungeonLayers.Floor] = new(walkable), [DungeonLayers.Dungeon] = new(blocked) },
                ["rooms"] = Array.Empty<GridRect>(), ["start"] = new GridPoint(2,2), ["stairs"] = new GridPoint(2,3),
                ["enemies"] = Array.Empty<Placement>(), ["gold"] = Array.Empty<Placement>(),
                ["items"] = Array.Empty<Placement>(), ["traps"] = Array.Empty<Placement>(), ["townPlan"] = town ? plan : null
            });
            dungeon.Setup(creator, floor); dungeon.InitializeCache(); dungeon.DoorsChanged();
        }

        T Actor<T>(Vector3Int position) where T : Character
        {
            var go = new GameObject(typeof(T).Name); go.transform.SetParent(root.transform);
            var actor = go.AddComponent<T>(); actor.Equipment = go.AddComponent<Equipment>(); actor.Skills = new();
            actor.BaseStats = new Stats { HPMax = 100, ActionsPerTurnMax = 1, HungerMax = 100 };
            actor.Vitals = new Vitals(); actor.Vitals.HP = 100; actor.TilemapPosition = position;
            actor.Team = actor is Ally ? Team.Player : Team.Enemy;
            return actor;
        }

        [TestCase(1, 1, false)] [TestCase(1, 1, true)]
        [TestCase(-1, 1, false)] [TestCase(-1, 1, true)]
        [TestCase(1, -1, false)] [TestCase(1, -1, true)]
        [TestCase(-1, -1, false)] [TestCase(-1, -1, true)]
        public void OutdoorTreesAllowOneOrTwoBlockedSidesInEveryDirection(int dx, int dy, bool twoTrees)
        {
            Paint(TownLayers.Trees, Start + new Vector3Int(dx,0));
            if (twoTrees) Paint(TownLayers.Trees, Start + new Vector3Int(0,dy));
            Build();
            var destination = Start + new Vector3Int(dx,dy);
            Assert.That(dungeon.CanWalkTo(Start,destination), Is.True);
            Assert.That(dungeon.CanWalkTo(destination,Start), Is.True);
            Assert.That(MovementAction.FootprintCanStep(ally,Start,destination), Is.True);
            Assert.That(DungeonPlacement.CanStep(dungeon,ally,Start,destination), Is.True);
            Assert.That(dungeon.GetValidWalkDirections(Start), Does.Contain(Character.GetFacing(destination-Start)));
            Assert.That(dungeon.CanReachAdjacent(Start,destination), Is.False, "Attack and interaction reach stays strict.");
            Assert.That(dungeon.GetValidAttackDirections(Start), Has.No.Member(Character.GetFacing(destination-Start)));
            var action = new MovementAction(ally,Start,destination);
            Assert.That(action.IsValid(ally), Is.True);
            action.ExecuteImmediate(ally);
            Assert.That(ally.TilemapPosition, Is.EqualTo(destination));
        }

        [TestCase(TownLayers.Houses)] [TestCase(TownLayers.ShopWalls)] [TestCase(TownLayers.ShopFloor)]
        [TestCase(TownLayers.Roofs)] [TestCase(TownLayers.Furniture)] [TestCase(TownLayers.Counters)]
        [TestCase(TownLayers.Carpet)] [TestCase(TownLayers.Boundary)] [TestCase(TownLayers.Buildings)]
        public void TreeOverlapNeverOverridesStructuralOrInteriorLayers(string layer)
        {
            var side = new Vector3Int(6,5);
            Paint(TownLayers.Trees,side); Paint(layer,side); Build();
            Assert.That(dungeon.IsOutdoorTreeCorner(side), Is.False);
            Assert.That(dungeon.CanWalkTo(Start,Goal), Is.False);
            Assert.That(MovementAction.FootprintCanStep(ally,Start,Goal), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void TreeAndWallOrHallwayCornersStayBlocked(bool hallway)
        {
            Paint(TownLayers.Trees,new Vector3Int(6,5));
            Paint(TownLayers.ShopWalls,new Vector3Int(5,6));
            if (hallway) Paint(TownLayers.ShopWalls,new Vector3Int(4,5));
            Build();
            Assert.That(dungeon.CanWalkTo(Start,Goal), Is.False);
            Assert.That(DungeonPlacement.CanStep(dungeon,ally,Start,Goal), Is.False);
            new MovementAction(ally,Start,Goal).ExecuteImmediate(ally);
            Assert.That(ally.TilemapPosition, Is.EqualTo(Start));
        }

        [Test] public void DoorApproachAndOpeningOnlyAllowStraightSteps()
        {
            var door = new Vector3Int(6,6);
            Paint(TownLayers.Buildings,door);
            Paint(TownLayers.Trees,new Vector3Int(5,6));
            Paint(TownLayers.Trees,new Vector3Int(7,6)); Build();
            foreach (var doorway in new[] { door, door + Vector3Int.up })
            {
                foreach (var d in new[] { new Vector3Int(1,1), new Vector3Int(-1,1), new Vector3Int(1,-1), new Vector3Int(-1,-1) })
                {
                    Assert.That(dungeon.CanWalkTo(doorway,doorway+d), Is.False);
                    Assert.That(dungeon.CanWalkTo(doorway+d,doorway), Is.False);
                }
                Assert.That(dungeon.CanWalkTo(doorway+Vector3Int.down,doorway), Is.True);
                Assert.That(dungeon.CanWalkTo(doorway,doorway+Vector3Int.up), Is.True);
            }
            Paint(TownLayers.Trees,door + Vector3Int.up); Build();
            Assert.That(dungeon.IsOutdoorTreeCorner(door + Vector3Int.up), Is.False);
        }

        [Test] public void DestinationTreesOccupantsAndMapEdgesRemainBlocked()
        {
            Paint(TownLayers.Trees,new Vector3Int(6,5)); Paint(TownLayers.Trees,Goal); Build();
            Assert.That(dungeon.CanWalkTo(Start,Goal), Is.False);
            layers[TownLayers.Trees][Goal.x,Goal.y] = false; Build();
            var enemy = Actor<Enemy>(Goal); game.Enemies.Add(enemy);
            Assert.That(DungeonPlacement.CanStep(dungeon,ally,Start,Goal), Is.False);
            var action = new MovementAction(ally,Start,Goal);
            Assert.That(action.IsValid(ally), Is.False); action.ExecuteImmediate(ally);
            Assert.That(ally.TilemapPosition, Is.EqualTo(Start));
            foreach (var cell in new[] { new Vector3Int(-1,5), new Vector3Int(Size,5), new Vector3Int(5,-1), new Vector3Int(5,Size), new Vector3Int(5,5,1) })
                Assert.That(dungeon.CanWalkTo(cell-Vector3Int.right,cell), Is.False);
            Paint(TownLayers.Trees,new Vector3Int(0,5)); Build();
            Assert.That(dungeon.IsOutdoorTreeCorner(new Vector3Int(0,5)), Is.False);
        }

        [Test] public void LargeActorsCheckEverySideAndDestinationCell()
        {
            ally.FootPrint = FootPrint.Size3x3;
            var side = new Vector3Int(7,4); // Outside both origin and destination, inside the horizontal side footprint.
            Paint(TownLayers.Trees,side); Build();
            Assert.That(MovementAction.FootprintCanStep(ally,Start,Goal), Is.True);
            Assert.That(DungeonPlacement.CanStep(dungeon,ally,Start,Goal), Is.True);
            Paint(TownLayers.ShopWalls,side); Build();
            Assert.That(MovementAction.FootprintCanStep(ally,Start,Goal), Is.False);
            layers[TownLayers.ShopWalls][side.x,side.y] = false;
            var edge = new Vector3Int(7,7); Paint(TownLayers.Trees,edge); Build();
            Assert.That(MovementAction.FootprintCanStep(ally,Start,Goal), Is.False, "Clear center is insufficient.");
            layers[TownLayers.Trees][edge.x,edge.y] = false; Build();
            var enemy = Actor<Enemy>(edge); game.Enemies.Add(enemy);
            Assert.That(DungeonPlacement.CanStep(dungeon,ally,Start,Goal), Is.False);
            new MovementAction(ally,Start,Goal).ExecuteImmediate(ally);
            Assert.That(ally.TilemapPosition, Is.EqualTo(Start));
            enemy.TilemapPosition = Start + new Vector3Int(-1,2);
            Assert.That(DungeonPlacement.CanStep(dungeon,ally,Start,Goal), Is.False, "A tree cannot hide a different occupied footprint cell.");
        }

        [Test] public void PursuitFollowerAndAutoplayRoutesAgreeAndReplanAroundWalls()
        {
            Paint(TownLayers.Trees,new Vector3Int(6,5)); Paint(TownLayers.Trees,new Vector3Int(5,6)); Build();
            var enemy = Actor<Enemy>(Start); game.Enemies.Add(enemy); enemy.PursuitPosition = Goal;
            ally.TilemapPosition = new Vector3Int(2,2);
            var pursuit = enemy.CalculatePursuitPath();
            Assert.That(pursuit.Count, Is.EqualTo(1));
            Assert.That(new Vector3Int(pursuit[0].X,pursuit[0].Y), Is.EqualTo(Goal));
            enemy.TilemapPosition = new Vector3Int(13,13); ally.TilemapPosition = Start;
            Assert.That(new AllyNavigation.Routes(game,ally).Next(Goal), Is.EqualTo(Goal));
            bool Step(Vector3Int a, Vector3Int b) => DungeonPlacement.CanStep(dungeon,ally,a,b);
            var route = new AutoplayRoute();
            Assert.That(route.Next(Start,Goal,Character.GetAStarGrid,Step), Is.EqualTo(Goal));
            Paint(TownLayers.ShopWalls,new Vector3Int(6,5)); Build();
            var next = route.Next(Start,Goal,Character.GetAStarGrid,Step);
            Assert.That(next.HasValue, Is.True); Assert.That(next, Is.Not.EqualTo(Goal));
            Assert.That(Step(Start,next.Value), Is.True);
            Assert.That(new AllyNavigation.Routes(game,ally).Next(Goal), Is.Not.EqualTo(Goal));
            ally.TilemapPosition = new Vector3Int(2,2); enemy.TilemapPosition = Start;
            Assert.That(enemy.CalculatePursuitPath().Count, Is.GreaterThan(1));
        }

        [Test] public void StandardDungeonsKeepStrictCornersWithTheSameTerrain()
        {
            Paint(TownLayers.Trees,new Vector3Int(6,5)); Build(town:false);
            Assert.That(dungeon.CanWalkTo(Start,Goal), Is.False);
            Assert.That(MovementAction.FootprintCanStep(ally,Start,Goal), Is.False);
            Assert.That(dungeon.GetValidWalkDirections(Start), Has.No.Member(Facing.UpRight));
        }
    }
}
