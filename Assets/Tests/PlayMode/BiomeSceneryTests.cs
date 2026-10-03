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
        [UnitySetUp] public IEnumerator Setup() { harness=new GameTestHarness();yield return harness.LoadDungeon(new TestScenario()); }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

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
