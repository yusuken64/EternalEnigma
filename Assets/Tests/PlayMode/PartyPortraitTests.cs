#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class PartyPortraitTests
    {
        private GameTestHarness harness;
        private TestInputScope inputs;
        private System.IDisposable preferences;
        private UnityEditor.EditorWindow view;
        private int originalSize;
        private const System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs=new TestInputScope();preferences=GamePreferences.Isolate();TerminalMode.SetRequested(false);
            harness=new GameTestHarness();
            view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            originalSize=(int)view.GetType().GetProperty("selectedSizeIndex",Flags).GetValue(view);
            Directory.CreateDirectory("Temp/PartyPortraitLayout");
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            TerminalMode.SetRequested(false);
            yield return harness.Cleanup();inputs.Dispose();preferences.Dispose();
            view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,originalSize);
        }

        [UnityTest] public IEnumerator TownCardsTrackVitalsSelectionAndRoster()
        {
            yield return harness.LoadTown(new TestScenario{AllyName="Avery"}.CreateSave());
            var town=Object.FindFirstObjectByType<Town>();
            var heroes=town.TownPlayer.RecruitedAllies;
            for(int i=1;i<4;i++)
            {
                var hero=Object.Instantiate(heroes[0],town.transform);hero.Id="portrait-"+i;hero.Name="Companion "+i;
                heroes.Add(hero);
            }
            yield return new WaitForSecondsRealtime(.2f);
            var hud=town.GetComponentInChildren<TravelPartyHud>(true);
            AssertCards(hud,heroes.ToArray());
            yield return AssertVitals(hud.PartySlots[1],heroes[1]);
            var manager=Object.FindFirstObjectByType<TownMenuManager>();
            manager.OpenPartyMenu(PartyMenuTab.Stats);yield return null;
            manager.PartyMenu.BrowseHero(1);yield return null;
            AssertSelected(hud,manager.PartyMenu.Hero.TownActor);
            manager.PartyMenu.CloseDialog();yield return null;
            town.TownPlayer.SelectAlly(heroes[2].Id);yield return null;
            AssertSelected(hud,heroes[2]);
            yield return Capture("town",hud);
            yield return AssertTerminalRestores(hud);
            heroes.RemoveRange(2,2);town.TownPlayer.EnsureControlledAlly();yield return null;
            AssertCards(hud,heroes.ToArray());
        }

        [UnityTest] public IEnumerator OverworldCardsTrackLivePartyAndRebuilds()
        {
            yield return harness.LoadCommon();
            Common.Instance.BeginSandbox(42);
            var context=Common.Instance.CampaignContext;
            var ids=context.Campaign.Companions.Take(3).Select(c=>c.Id).ToArray();
            context.Roster.UnionWith(ids);Assert.That(context.SetParty(ids),Is.True);
            yield return SceneManager.LoadSceneAsync("Overworld",LoadSceneMode.Additive);
            var world=Object.FindFirstObjectByType<OverworldScene>();
            yield return harness.WaitUntil(()=>world.IsReady,"portrait overworld ready");
            yield return null;
            var hud=world.GetComponentInChildren<TravelPartyHud>(true);
            AssertCards(hud,new[]{world.Player}.Concat(world.Followers).ToArray());
            AssertSelected(hud,world.Player);
            yield return AssertVitals(hud.PartySlots[1],world.Followers[0]);
            yield return Capture("sandbox-overworld",hud);
            yield return AssertTerminalRestores(hud);
            Assert.That(world.ToggleCompanion(ids[2]),Is.True);
            yield return null;
            AssertCards(hud,new[]{world.Player}.Concat(world.Followers).ToArray());
        }

        [UnityTest] public IEnumerator CampaignOverworldShowsAllPartyCards()
        {
            yield return harness.LoadCommon();
            var context=new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign,42));
            Common.Instance.CampaignContext=context;
            var ids=context.Campaign.Companions.Take(3).Select(c=>c.Id).ToArray();
            context.Roster.UnionWith(ids);Assert.That(context.SetParty(ids),Is.True);
            yield return SceneManager.LoadSceneAsync("Overworld",LoadSceneMode.Additive);
            var world=Object.FindFirstObjectByType<OverworldScene>();
            yield return harness.WaitUntil(()=>world.IsReady,"campaign portraits ready");
            yield return null;
            var hud=world.GetComponentInChildren<TravelPartyHud>(true);
            AssertCards(hud,new[]{world.Player}.Concat(world.Followers).ToArray());
            yield return Capture("overworld",hud);
        }

        private static void AssertCards(TravelPartyHud hud,TownAlly[] heroes)
        {
            Assert.That(hud,Is.Not.Null);
            Assert.That(hud.GetComponent<Canvas>().enabled,Is.True);
            for(int i=0;i<hud.PartySlots.Length;i++)
            {
                var card=hud.PartySlots[i];
                Assert.That(card.gameObject.activeSelf,Is.EqualTo(i<heroes.Length));
                if(i>=heroes.Length)continue;
                Assert.That(card.TownActor,Is.SameAs(heroes[i]));
                Assert.That(card.NameText.text,Does.Contain(heroes[i].Name));
                Assert.That(card.PortraitImage.sprite,Is.SameAs(heroes[i].Portrait));
            }
        }

        private static IEnumerator AssertVitals(CharacterStatsDisplay card,TownAlly hero)
        {
            var stats=TownUtilityService.StatsFor(hero);
            int hp=Mathf.Max(1,stats.HPMax/2),mana=stats.ManaMax/2,food=stats.HungerMax/2;
            hero.Hp=hp;hero.Mana=mana;hero.HasHunger=true;hero.Hunger=food;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(card.HpDisplay.ValueText.text,Is.EqualTo($"HP {hp}/{stats.HPMax}"));
            Assert.That(card.ManaDisplay.ValueText.text,Is.EqualTo($"Mana {mana}/{stats.ManaMax}"));
            Assert.That(card.HungerDisplay.ValueText.text,Is.EqualTo($"Food {food}/{stats.HungerMax}"));
            Assert.That(card.HpDisplay.StatValueSlider.value,Is.EqualTo((float)hp/stats.HPMax).Within(.001f));
            hero.Hp=0;yield return new WaitForSecondsRealtime(.2f);
            Assert.That(card.NameText.text,Does.Contain("Downed"));
            hero.Hp=-1;hero.Mana=-1;hero.HasHunger=false;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(card.HpDisplay.StatValueSlider.value,Is.EqualTo(1));
            Assert.That(card.ManaDisplay.StatValueSlider.value,Is.EqualTo(1));
            Assert.That(card.HungerDisplay.StatValueSlider.value,Is.EqualTo(1));
        }

        private static void AssertSelected(TravelPartyHud hud,TownAlly selected)
        {
            foreach(var card in hud.PartySlots.Where(c=>c.gameObject.activeSelf))
                Assert.That(card.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Turn highlight").enabled,
                    Is.EqualTo(card.TownActor==selected));
        }

        private static IEnumerator AssertTerminalRestores(TravelPartyHud hud)
        {
            var font=hud.PartySlots[0].NameText.font;
            TerminalMode.SetRequested(true);yield return new WaitForSecondsRealtime(.25f);
            Assert.That(hud.GetComponent<Canvas>().enabled,Is.True);
            Assert.That(hud.PartySlots[0].NameText.font,Is.Not.SameAs(font));
            TerminalMode.SetRequested(false);yield return new WaitForSecondsRealtime(.25f);
            Assert.That(hud.PartySlots[0].NameText.font,Is.SameAs(font));
        }

        private IEnumerator Capture(string scene,TravelPartyHud hud)
        {
            foreach(var resolution in new[]{new Vector2(960,600),new Vector2(1280,720)})
            {
                view.GetType().GetMethod("SetCustomResolution",Flags).Invoke(view,new object[]{resolution,"Party portraits"});view.Repaint();
                yield return new WaitForSecondsRealtime(.2f);Canvas.ForceUpdateCanvases();
                foreach(var card in hud.PartySlots.Where(c=>c.gameObject.activeSelf))
                {
                    var corners=new Vector3[4];((RectTransform)card.transform).GetWorldCorners(corners);
                    Assert.That(corners[0].x,Is.GreaterThanOrEqualTo(0));
                    Assert.That(corners[2].x,Is.LessThanOrEqualTo(Screen.width));
                    Assert.That(corners[0].y,Is.GreaterThanOrEqualTo(0));
                    Assert.That(corners[2].y,Is.LessThanOrEqualTo(Screen.height));
                }
                ScreenCapture.CaptureScreenshot($"Temp/PartyPortraitLayout/{scene}-{resolution.x}x{resolution.y}.png");
                yield return new WaitForSecondsRealtime(.2f);
            }
        }
    }
}
#endif
