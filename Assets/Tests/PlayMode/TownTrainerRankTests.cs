#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class TownTrainerRankTests
    {
        private GameTestHarness harness;
        private readonly List<Object> assets = new();
        private Town World => Object.FindFirstObjectByType<Town>();
        private TownMenuManager Manager => Object.FindFirstObjectByType<TownMenuManager>();

        [UnitySetUp]
        public IEnumerator SetUp() { harness = new GameTestHarness(); yield return null; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return harness.Cleanup();
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private Skill MakeSkill(string name, int cost = 50)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            assets.Add(skill);
            skill.SkillName = name;
            skill.LearnCost = cost;
            return skill;
        }

        private ClassDefinition MakeClass()
        {
            var cls = ScriptableObject.CreateInstance<ClassDefinition>();
            assets.Add(cls);
            cls.Id = "test";
            cls.DisplayName = "Test";
            cls.Skills = new List<ClassSkillEntryData>
            {
                new ClassSkillEntryData { Skill = MakeSkill("T Novice"), Tier = 1, MaxRank = 1, Kind = SkillKind.Mastery },
                new ClassSkillEntryData { Skill = MakeSkill("T Strike"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Guard"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Parry"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Adept"), Tier = 2, MaxRank = 1, Kind = SkillKind.Mastery },
                new ClassSkillEntryData { Skill = MakeSkill("T Cleave"), Tier = 2, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Master"), Tier = 3, MaxRank = 1, Kind = SkillKind.Mastery },
            };
            return cls;
        }

        [UnityTest]
        public IEnumerator RankUpChargesSkillPointsPerRankAndPersists()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());

            var hero = World.TownPlayer.ControllingTownAlly;
            var cls = MakeClass();
            hero.PrimaryClass = cls;
            hero.Level = 20;

            var original = World.Configuration.LearnableSkills.ToList();
            World.Configuration.LearnableSkills.Clear();

            try
            {
                var strikeSkill = cls.Skills.First(s => s.Skill.SkillName == "T Strike").Skill;

                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                Assert.That(hero.GetRank("T Strike"), Is.EqualTo(1));
                int points = SkillLearningRules.EarnedPoints(20);
                Assert.That(TrainerOffers.AvailablePoints(hero), Is.EqualTo(points - 1));

                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                Assert.That(hero.GetRank("T Strike"), Is.EqualTo(2));
                Assert.That(TrainerOffers.AvailablePoints(hero), Is.EqualTo(points - 1 - 2));

                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                Assert.That(hero.GetRank("T Strike"), Is.EqualTo(3));
                Assert.That(TrainerOffers.AvailablePoints(hero), Is.EqualTo(points - 1 - 2 - 3));
                Assert.That(World.TownPlayer.Gold, Is.EqualTo(10000), "Skills cost points, not gold.");

                SaveSystem.SaveData(Common.Instance.GameSaveData); // Explicit checkpoint persists the captured ranks.
                var saved = SaveSystem.LoadData().TownSaveData;
                Assert.That(saved.RecruitedAlliesData[0].SkillRanks.Any(sr => sr.SkillName == "T Strike" && sr.Rank == 3), Is.True);
                Assert.That(saved.RecruitedAlliesData[0].Skills, Does.Contain("T Strike"));
            }
            finally
            {
                World.Configuration.LearnableSkills = original;
            }
        }

        [UnityTest]
        public IEnumerator LevelGateAndTierLockReturnReasons()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());

            var hero = World.TownPlayer.ControllingTownAlly;
            var cls = MakeClass();
            hero.PrimaryClass = cls;
            hero.Level = 1;

            var original = World.Configuration.LearnableSkills.ToList();
            World.Configuration.LearnableSkills.Clear();

            try
            {
                var strikeSkill = cls.Skills.First(s => s.Skill.SkillName == "T Strike").Skill;
                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                int goldAfterFirst = World.TownPlayer.Gold;

                Assert.That(World.Services.Learn(hero, strikeSkill, out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(World.TownPlayer.Gold, Is.EqualTo(goldAfterFirst));

                var cleaveSkill = cls.Skills.First(s => s.Skill.SkillName == "T Cleave").Skill;
                Assert.That(World.Services.Learn(hero, cleaveSkill, out reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
            }
            finally
            {
                World.Configuration.LearnableSkills = original;
            }
        }

        [UnityTest]
        public IEnumerator TrainerDialogShowsRanksAndRefreshesAfterPurchase()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());

            var hero = World.TownPlayer.ControllingTownAlly;
            var cls = MakeClass();
            hero.PrimaryClass = cls;

            var original = World.Configuration.LearnableSkills.ToList();
            World.Configuration.LearnableSkills.Clear();

            try
            {
                var building = World.TownBuildings.First(b => b.Definition.DialogId == "trainer");
                building.Interact(World.TownPlayer, null);
                yield return null;

                var dialog = (BallistaDialog)Manager.CurrentDialog;
                dialog.Show();

                var noviceItem = ((BallistaDialog)Manager.CurrentDialog).SkillGridItems
                    .First(item => item.GetSkill().SkillName == "T Novice");

                noviceItem.ToggleOn_Clicked();
                yield return null;
                dialog.BallistaPurchaseDialog.Purchase_Clicked();

                Assert.That(hero.GetRank("T Novice"), Is.EqualTo(1));
                Assert.That(noviceItem.SkillText.text, Does.Contain("1/1"));
            }
            finally
            {
                World.Configuration.LearnableSkills = original;
            }
        }

        [UnityTest]
        public IEnumerator TrainerLayoutFitsSupportedResolutionsWithLongText()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var hero = World.TownPlayer.ControllingTownAlly;
            hero.PrimaryClass = MakeClass();
            hero.PrimaryClass.Skills[1].Skill.SkillName = "An exceptionally long skill name for testing the training menu";
            hero.SecondaryClass = MakeClass();
            hero.SecondaryClass.Id = "capture-secondary";
            hero.SecondaryClass.DisplayName = "Secondary class with a long name";
            hero.PrimaryClass.Skills[1].Skill.Description = string.Join("\n", Enumerable.Repeat(
                "A detailed explanation of the skill, its targets, duration, and tactical effects.", 30));
            World.TownBuildings.First(b => b.Definition.DialogId == "trainer").Interact(World.TownPlayer, null);
            yield return null;
            var dialog = (BallistaDialog)Manager.CurrentDialog;
            var view = dialog.Layout;
            dialog.SkillGridItems[1].GridButton.Select();
            var parent = view.transform.parent;
            var canvas = GameUISkin.Canvas("Trainer capture", dialog.transform);
            var camera = new GameObject("Trainer capture camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            view.transform.SetParent(canvas.transform, false);
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            System.IO.Directory.CreateDirectory("Temp/TrainerValidation");
            try
            {
                foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1280,800), new Vector2Int(1920,1080) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    camera.targetTexture = target;
                    yield return null;
                    dialog.SkillGridItems[1].GridButton.Select();
                    yield return new WaitForSecondsRealtime(.15f);
                    Canvas.ForceUpdateCanvases();
                    var root = (RectTransform)canvas.transform;
                    foreach (var control in new Component[] { view.PrimaryTab, view.SecondaryTab, view.Balance, view.PreviewScroll, view.ListScroll, view.Close })
                    {
                        var corners = new Vector3[4];
                        ((RectTransform)control.transform).GetWorldCorners(corners);
                        var bounds = new Bounds(root.InverseTransformPoint(corners[0]), Vector3.zero);
                        foreach (var corner in corners) bounds.Encapsulate(root.InverseTransformPoint(corner));
                        Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(root.rect.xMin - 1), control.name);
                        Assert.That(bounds.max.x, Is.LessThanOrEqualTo(root.rect.xMax + 1), control.name);
                        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(root.rect.yMin - 1), control.name);
                        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(root.rect.yMax + 1), control.name);
                    }
                    camera.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var image = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);
                    image.Apply();
                    System.IO.File.WriteAllBytes($"Temp/TrainerValidation/trainer-{size.x}x{size.y}.png", image.EncodeToPNG());
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                    Object.Destroy(image);
                    Object.Destroy(target);
                }
            }
            finally
            {
                view.transform.SetParent(parent, false);
                Object.Destroy(camera.gameObject);
                Object.Destroy(canvas.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TrainerSelectionScrollingTabsAndModalFocus()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var hero = World.TownPlayer.ControllingTownAlly;
            hero.PrimaryClass = MakeClass();
            hero.SecondaryClass = MakeClass();
            hero.SecondaryClass.Id = "secondary";
            foreach (var e in hero.SecondaryClass.Skills) e.Skill.SkillName = "Secondary " + e.Skill.SkillName;
            hero.Level = 20;
            World.TownBuildings.First(b => b.Definition.DialogId == "trainer").Interact(World.TownPlayer, null);
            yield return null;
            var dialog = (BallistaDialog)Manager.CurrentDialog;
            var view = dialog.Layout;
            Assert.That(dialog.SkillGridItems.Count, Is.EqualTo(7));
            var es = EventSystem.current;
            var first = dialog.SkillGridItems[0];
            Canvas.ForceUpdateCanvases();
            Assert.That(view.ListScroll.content.rect.height, Is.GreaterThan(view.ListScroll.viewport.rect.height));
            dialog.SkillGridItems.Last().GridButton.Select();
            view.ListScroll.verticalNormalizedPosition = .5f;
            var wheel = new PointerEventData(es) { scrollDelta = new Vector2(0, -1) };
            ExecuteEvents.ExecuteHierarchy(first.gameObject, wheel, ExecuteEvents.scrollHandler);
            float wheelPosition = view.ListScroll.verticalNormalizedPosition;
            Assert.That(wheelPosition, Is.LessThan(.5f));
            var hover = new PointerEventData(es) { pointerId = -1 };
            ExecuteEvents.Execute(first.gameObject, hover, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(es.currentSelectedGameObject, Is.SameAs(first.gameObject));
            Assert.That(view.ListScroll.verticalNormalizedPosition, Is.EqualTo(wheelPosition).Within(.01f),
                "Hovering a skill must not pull the list away from manual scrolling.");
            dialog.SkillGridItems.Last().GridButton.Select();
            foreach (var row in dialog.SkillGridItems)
            {
                Assert.That(row.GridButton, Is.TypeOf<SelectToActivateButton>());
                Assert.That(row.GridButton.interactable, Is.True);
                row.GridButton.Select();
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(view.Preview.text, Does.Contain(row.GetSkill().SkillName));
                Assert.That(Manager.CurrentDialog, Is.SameAs(dialog), "Selection must only preview.");
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.ListScroll.viewport, row.transform);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(view.ListScroll.viewport.rect.yMin - 1));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(view.ListScroll.viewport.rect.yMax + 1));
                if (row != dialog.SkillGridItems.Last())
                {
                    ExecuteEvents.Execute(row.gameObject, new AxisEventData(es) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
                    Assert.That(es.currentSelectedGameObject, Is.SameAs(dialog.SkillGridItems[dialog.SkillGridItems.IndexOf(row) + 1].gameObject));
                }
            }
            var lastName = dialog.SkillGridItems.Last().GetSkill().SkillName;
            float primaryScroll = view.ListScroll.verticalNormalizedPosition;
            dialog.ChangeTab(ClassSource.Secondary);
            yield return null;
            Assert.That(dialog.SkillGridItems.Count, Is.EqualTo(4));
            dialog.ChangeTab(ClassSource.Primary);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(es.currentSelectedGameObject.GetComponent<SkillGridItem>().GetSkill().SkillName, Is.EqualTo(lastName));
            Assert.That(view.ListScroll.verticalNormalizedPosition, Is.EqualTo(primaryScroll).Within(.01));
            first = dialog.SkillGridItems[0];
            var pointer = new PointerEventData(es) { button = PointerEventData.InputButton.Left, pointerId = -1 };
            ExecuteEvents.Execute(first.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(first.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(Manager.CurrentDialog, Is.SameAs(dialog), "First pointer press selects without buying.");
            ExecuteEvents.Execute(first.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(first.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(Manager.CurrentDialog, Is.SameAs(dialog.BallistaPurchaseDialog));
            dialog.BallistaPurchaseDialog.Cancel_ClickeD();
            yield return null;
            Assert.That(es.currentSelectedGameObject, Is.SameAs(first.gameObject));
            Assert.That(hero.GetRank(first.GetSkill().SkillName), Is.Zero);
            yield return new WaitForSecondsRealtime(.45f);
            ExecuteEvents.Execute(first.gameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
            yield return null;
            dialog.BallistaPurchaseDialog.Purchase_Clicked();
            yield return null;
            Assert.That(es.currentSelectedGameObject, Is.SameAs(first.gameObject));
            Assert.That(view.Preview.text, Does.Contain("Rank 1/1"));
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(10000));
            first.GetSkill().Description = string.Join("\n", Enumerable.Repeat("A long description explaining this skill and its effects.", 40));
            dialog.Refresh();
            Canvas.ForceUpdateCanvases();
            view.PreviewControl.Select();
            ExecuteEvents.Execute(view.PreviewControl.gameObject, new AxisEventData(es) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
            Assert.That(view.PreviewScroll.verticalNormalizedPosition, Is.LessThan(1));
            ExecuteEvents.Execute(view.PreviewControl.gameObject, new AxisEventData(es) { moveDir = MoveDirection.Left }, ExecuteEvents.moveHandler);
            Assert.That(es.currentSelectedGameObject, Is.SameAs(first.gameObject));
            Manager.CloseAllMenus();
            World.TownBuildings.First(b => b.Definition.DialogId == "trainer").Interact(World.TownPlayer, null);
            yield return null;
            Assert.That(dialog.SkillGridItems[0].Offer.Source, Is.EqualTo(ClassSource.Primary));
        }

        [UnityTest]
        public IEnumerator ConfirmationRevalidatesPointsAndClasslessStillPaysGold()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var hero = World.TownPlayer.ControllingTownAlly;
            hero.PrimaryClass = MakeClass();
            hero.Level = 1;
            World.TownBuildings.First(b => b.Definition.DialogId == "trainer").Interact(World.TownPlayer, null);
            yield return null;
            var dialog = (BallistaDialog)Manager.CurrentDialog;
            var strike = dialog.SkillGridItems.First(i => i.GetSkill().SkillName == "T Strike");
            strike.GridButton.Select();
            strike.ToggleOn_Clicked();
            hero.SetRank("T Guard", 1);
            hero.SetRank("T Parry", 1);
            dialog.BallistaPurchaseDialog.Purchase_Clicked();
            yield return null;
            Assert.That(hero.GetRank("T Strike"), Is.Zero);
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(10000));
            Assert.That(dialog.Layout.Preview.text, Does.Contain("Needs 1 skill points"));
            hero.PrimaryClass = null;
            hero.SecondaryClass = null;
            var skill = MakeSkill("Classless gold skill", 75);
            var original = World.Configuration.LearnableSkills;
            try
            {
                World.Configuration.LearnableSkills = new List<Skill> { skill };
                Assert.That(World.Services.Learn(hero, skill, out _), Is.True);
                Assert.That(World.TownPlayer.Gold, Is.EqualTo(10000 - skill.LearnCost));
            }
            finally { World.Configuration.LearnableSkills = original; }
        }

        [UnityTest]
        public IEnumerator RestoredHeroKeepsRanksAndHighestLevel()
        {
            var save = new TestScenario { Gold = 100 }.CreateSave();
            save.TownSaveData.RecruitedAlliesData[0].Skills = new List<string> { "Healing" };
            save.TownSaveData.RecruitedAlliesData[0].SkillRanks = new List<SkillRankSaveData>
            {
                new SkillRankSaveData { SkillName = "Healing", Rank = 2 }
            };
            save.TownSaveData.RecruitedAlliesData[0].Level = 12;

            yield return harness.LoadTown(save);

            var hero = World.TownPlayer.ControllingTownAlly;
            Assert.That(hero.GetRank("Healing"), Is.EqualTo(2));
            Assert.That(hero.Level, Is.EqualTo(12));
        }
    }
}
#endif
