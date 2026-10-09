#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class BiomeSceneryTests
    {
        GameTestHarness harness;
        bool? previousControl;
        [UnitySetUp] public IEnumerator Setup() { previousControl=DungeonPreferences.FullControlOverride;DungeonPreferences.FullControlOverride=false;harness=new GameTestHarness();yield return harness.LoadDungeon(new TestScenario()); }
        [UnityTearDown] public IEnumerator Cleanup() {yield return harness.Cleanup();DungeonPreferences.FullControlOverride=previousControl;}

        [UnityTest]
        public IEnumerator GeneratedFloorSpawnsVisibleLooseItems()
        {
            var save=Common.Instance.GameSaveData.DungeonSaveData;
            save.UseBiomeLayout=true;save.LayoutTier=0;save.LayoutBiome=OverworldBiome.Forest;
            harness.Game.AdvanceFloor();yield return harness.WaitForIdle();
            var dungeon=harness.Game.CurrentDungeon;
            Assert.That(dungeon.Floor.Items,Is.Not.Empty,"Scenery must leave some items on the floor.");
            var pickups=dungeon.Interactables.OfType<DroppedItem>().ToArray();
            Assert.That(pickups.Length,Is.EqualTo(dungeon.Floor.Items.Count));
            Assert.That(dungeon.Interactables.OfType<Gold>().Count(),Is.EqualTo(dungeon.Floor.Gold.Count));
            var terrain=harness.Game.DungeonGenerator.TileWorldCreator.worldObject;
            var walls=terrain.transform.Find("Dungeon_layer").GetComponentsInChildren<MeshFilter>()
                .Select(f=>{var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;return c;}).ToArray();
            Physics.SyncTransforms();
            foreach(var interactable in dungeon.Interactables.Where(p=>p is DroppedItem or Gold or DungeonProp))
            {
                var bounds=interactable.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                var expected=dungeon.CellToWorld(interactable.Position)+new Vector3(1,1,0);
                Assert.That(bounds.center.x,Is.EqualTo(expected.x).Within(.02f),interactable.name+" X placement");
                Assert.That(bounds.center.y,Is.EqualTo(expected.y).Within(.02f),interactable.name+" Y placement");
                var ray=new Ray(new Vector3(bounds.center.x,bounds.center.y,-8),Vector3.forward);
                Assert.That(walls.Any(c=>c.Raycast(ray,out var hit,8)),Is.False,interactable.name+" is inside rendered terrain at "+interactable.Position);
                if(interactable is not DungeonProp {IsDoor:true})
                {
                    var view=new Vector3(0,12,14).normalized;
                    var foot=new Vector3(bounds.center.x,bounds.center.y,-.01f);
                    Assert.That(walls.Any(c=>c.Raycast(new Ray(foot-view*8,view),out var hit,7.99f)),Is.False,
                        interactable.name+" is covered by raised terrain at "+interactable.Position);
                }
            }
            foreach(var wall in walls) Object.Destroy(wall);
            foreach(var pickup in pickups)
            {
                Assert.That(pickup.InventoryItem,Is.Not.Null);
                harness.PlaceAlly(pickup.Position);harness.Game.UpdateMiniMap();
                yield return null;yield return null;
                var renderers=pickup.GetComponentsInChildren<Renderer>();
                Assert.That(renderers,Is.Not.Empty);
                Assert.That(renderers.All(r=>r.enabled && !r.forceRenderingOff),Is.True,"Nearby floor loot must be visible.");
                Assert.That(pickup.GetComponentInChildren<DungeonPickupFootprint>().GroundZ,Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.003f));
            }
            // Walk from the entrance through the actual turn pipeline. Remove combat
            // actors so this regression measures terrain and props, not random enemy AI.
            foreach(var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();yield return null;
            harness.PlaceAlly(dungeon.GetStartPosition());
            IEnumerable<GridPoint> Neighbors(GridPoint p)=>GridMovement.GetNeighbors(p.ToCell(),
                cell=>dungeon.IsWalkable(cell)&&!dungeon.IsHazard(cell)).Select(cell=>cell.ToGridPoint());
            var distances=GridSearch.Distances(dungeon.Floor.Start,Neighbors);
            var last=pickups.Where(p=>distances.ContainsKey(p.Position.ToGridPoint())).OrderBy(p=>distances[p.Position.ToGridPoint()]).First();
            var item=last.InventoryItem;
            var path=GridSearch.Path(dungeon.Floor.Start,last.Position.ToGridPoint(),Neighbors);
            Assert.That(path.Count,Is.GreaterThan(1));
            foreach(var cell in path.Skip(1))
            {
                var move=new MovementAction(harness.Ally,harness.Ally.TilemapPosition,cell.ToCell());
                Assert.That(move.IsValid(harness.Ally),Is.True);
                yield return harness.ExecuteAction(move);
                Assert.That(harness.Ally.TilemapPosition,Is.EqualTo(cell.ToCell()));
            }
            System.IO.Directory.CreateDirectory("Temp/DungeonLootValidation");
            ScreenCapture.CaptureScreenshot("Temp/DungeonLootValidation/loose-items.png");
            yield return new WaitForSecondsRealtime(.3f);
            yield return harness.ExecuteAction(new PickUpItemAction(last));
            Assert.That(harness.Game.PlayerController.Inventory.InventoryItems.Contains(item),Is.True);
            Assert.That(dungeon.Interactables.Contains(last),Is.False);
        }

        [UnityTest]
        public IEnumerator DamageOpeningHazardsAndTransitions()
        {
            var dungeon=harness.Game.CurrentDungeon;var ally=harness.Ally;
            var origin=dungeon.GetStartPosition();ally.SetPosition(origin);
            var cell=origin+Vector3Int.right;
            var theme=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Get(new DungeonVisualSelection {Biome=OverworldBiome.Forest});
            DungeonProp Prop(DungeonSceneryKind kind,int hp,SceneryReward reward=SceneryReward.None) => DungeonProp.Create(dungeon,new DungeonScenery(cell.ToGridPoint(),kind,hp,42,reward),theme);
            var prop=Prop(DungeonSceneryKind.Destructible,10000);
            Assert.That(dungeon.IsWalkable(cell),Is.False);Assert.That(dungeon.IsFloorCell(cell),Is.True);
            Assert.That(dungeon.GetVisibleTiles(ally,origin).Contains(cell),Is.True);
            int characterCount=harness.Game.AllCharacters.Count();
            void Resolve(GameAction action)
            {
                var pending=new Queue<GameAction>();pending.Enqueue(action);
                while(pending.Count>0) foreach(var child in ally.ExecuteActionImmediate(pending.Dequeue())) pending.Enqueue(child);
            }
            Resolve(new AttackAction(ally,origin,cell));Assert.That(prop.HitPoints,Is.LessThan(10000));
            int hp=prop.HitPoints;ally.SetFacingByTargetPosition(cell);
            Resolve(new RangedAttackAction(ally,null,12,null));Assert.That(prop.HitPoints,Is.EqualTo(hp-12));
            var skill=ScriptableObject.CreateInstance<Skill>();ally.Skills.Add(skill);
            try
            {
                skill.Targeting=SkillTargeting.SelectedTarget;skill.TargetSelector=new TargetSelector {Team=TargetTeam.Enemies,Area=TargetArea.Visible};
                skill.ActionEffects=new List<GameAction> {new TakeDamageAction {damage=7},new TakeHealAction {healing=500}};
                hp=prop.HitPoints;Resolve(SkillAction.ForScenery(ally,skill,prop));Assert.That(prop.HitPoints,Is.EqualTo(hp-7));
                skill.Targeting=SkillTargeting.Self;skill.AreaRadius=2;
                hp=prop.HitPoints;Resolve(new SkillAction(ally,skill,ally));Assert.That(prop.HitPoints,Is.EqualTo(hp-7));
            }
            finally {ally.Skills.Remove(skill);Object.Destroy(skill);}
            var kill=prop.Damage(ally,10000);Resolve(kill);Resolve(kill);
            Assert.That(dungeon.IsWalkable(cell),Is.True);Assert.That(harness.Game.AllCharacters.Count(),Is.EqualTo(characterCount));
            yield return null;
            var container=Prop(DungeonSceneryKind.Container,0,SceneryReward.Item);
            int drops=dungeon.Interactables.OfType<DroppedItem>().Count();
            int capacity=harness.Game.PlayerController.Inventory.MaxItems;
            harness.Game.PlayerController.Inventory.MaxItems=0;
            Resolve(new InteractAction(container));Resolve(new InteractAction(container));
            Assert.That(dungeon.Interactables.OfType<DroppedItem>().Count(),Is.EqualTo(drops+1));
            var reward=dungeon.Interactables.OfType<DroppedItem>().Single(d=>d.Position==cell);
            Resolve(new PickUpItemAction(reward));Assert.That(dungeon.Interactables.Contains(reward),Is.True);
            harness.Game.PlayerController.Inventory.MaxItems=capacity;
            foreach(var drop in dungeon.Interactables.OfType<DroppedItem>().Where(d=>d.Position==cell).ToArray()) dungeon.RemoveInteractable(drop);
            yield return null;
            var hazard=Prop(DungeonSceneryKind.Hazard,0);
            Assert.That(dungeon.EntryEffects(ally,cell,cell),Is.Empty);
            var effect=dungeon.EntryEffects(ally,origin,cell).OfType<TakeDamageAction>().Single();
            Assert.That(effect.damage,Is.EqualTo(Mathf.CeilToInt(ally.FinalStats.HPMax*.1f)));
            Assert.That(effect.Environmental,Is.True);
            var save=Common.Instance.GameSaveData.DungeonSaveData;
            save.UseBiomeLayout=true;save.LayoutTier=4;save.LayoutBiome=OverworldBiome.Volcanic;
            harness.Game.AdvanceFloor();yield return harness.WaitForIdle();
            Assert.That(harness.Game.CurrentDungeon.Floor.Width,Is.InRange(52,56));
            Assert.That(harness.Game.CurrentDungeon.Interactables.OfType<DungeonProp>().Any(),Is.True);
            Assert.That(hazard==null,Is.True);
        }
    }
}
#endif
