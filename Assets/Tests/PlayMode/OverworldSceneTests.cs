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
        private GameTestHarness harness;
        private TestInputScope inputScope;
        [UnitySetUp]
        public IEnumerator Setup() { inputScope=new TestInputScope();harness = new GameTestHarness(); yield return harness.LoadCommon(); }
        private Scene scene;
        private Keyboard keyboard;
        private Gamepad pad;

        [UnityTest]
        public IEnumerator GateUnlockAudioOnlyPlaysForANewUnlock()
        {
            var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 0));
            Common.Instance.CampaignContext = context;
            context.State.Scene = "Overworld";
            var route = context.Campaign.Routes.First(r => r.ShortcutKind == ShortcutKind.Keyed && r.IsWarp);
            context.Position = context.Grid.Locations[route.From];
            context.Keys.Add(route.KeyId);
            var audio = AudioManager.Instance;
            var heard = new List<(SoundCue cue, UnityEngine.Audio.AudioMixerGroup group)>();
            void Heard(SoundCue cue, AudioClip _, UnityEngine.Audio.AudioMixerGroup group) => heard.Add((cue, group));
            audio.CuePlayed += Heard;
            try
            {
                yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("Overworld");
                var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
                yield return harness.WaitUntil(() => world.IsReady && !Common.Instance.Travel.IsTransitioning, "gate audio world");
                Assert.That(heard.Any(e => e.cue == SoundCue.GateUnlock), Is.False, "Loading the world is not an unlock.");
                using var session = world.BeginGateInteraction(route.Id);
                Assert.That(session, Is.Not.Null);
                Assert.That(world.AttemptGate(session, new KeyLockAction("missing-key")), Is.EqualTo(LockOutcome.NoEffect));
                Assert.That(heard.Any(e => e.cue == SoundCue.GateUnlock), Is.False);
                Assert.That(world.AttemptGate(session, new KeyLockAction(route.KeyId)), Is.EqualTo(LockOutcome.Opened));
                Assert.That(heard.Count(e => e.cue == SoundCue.GateUnlock), Is.EqualTo(1));
                Assert.That(heard.Single(e => e.cue == SoundCue.GateUnlock).group, Is.SameAs(audio.EffectAudioMixerGroup));
                Assert.That(world.AttemptGate(session, new KeyLockAction(route.KeyId)), Is.EqualTo(LockOutcome.NoEffect));
                Assert.That(world.Warp(route.Id), Is.True);
                Assert.That(heard.Count(e => e.cue == SoundCue.GateUnlock), Is.EqualTo(1), "Crossing and refreshing an open gate must stay silent.");
                Assert.That(heard.Single(e => e.cue == SoundCue.Teleport).group, Is.SameAs(audio.EffectAudioMixerGroup));

                yield return SceneManager.UnloadSceneAsync(scene);
                yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("Overworld");
                world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
                yield return harness.WaitUntil(() => world.IsReady && !Common.Instance.Travel.IsTransitioning, "restored gate audio world");
                Assert.That(heard.Count(e => e.cue == SoundCue.GateUnlock), Is.EqualTo(1), "Restoring an opened gate must not replay its glissando.");

                var arrival = context.Position;
                GridPoint? emptyGround = null;
                foreach (var cell in Enumerable.Range(0, context.Grid.Width).SelectMany(x =>
                    Enumerable.Range(0, context.Grid.Height).Select(y => new GridPoint(x, y))))
                {
                    if (!context.Gates.IsWalkable(cell, context.Held) || context.Grid.Locations.Values.Contains(cell) ||
                        context.Grid.Pickups.Any(p => p.Position.Equals(cell)) || context.Gates.Nearby(cell).Any()) continue;
                    context.Position = cell;
                    if (world.NearbySign() != null) continue;
                    emptyGround = cell;
                    break;
                }
                Assert.That(emptyGround.HasValue, Is.True, "Fixture needs ordinary ground without an interaction.");
                heard.Clear();
                world.ClaimRewards(announceLocked: false);
                Assert.That(heard, Is.Empty, "Automated interaction probes must stay silent.");
                world.ClaimRewards();
                Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Denied }));
                Assert.That(heard[0].group, Is.SameAs(audio.UIAudioMixerGroup));
                context.Position = arrival;
            }
            finally { audio.CuePlayed -= Heard; }
        }

        [UnityTest]
        public IEnumerator UnlockedWarpsAreAvailableToKeyboardAndGamepadAfterRewards()
        {
            var context = Enumerable.Range(0, 32)
                .Select(seed => new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed)))
                .First(c => c.Campaign.Sources.Any(s => s.LocationId == "checkpoint-1" && s.CompanionId == null));
            Common.Instance.CampaignContext = context;
            context.State.Scene = "Overworld";
            context.Position = context.Grid.Locations["checkpoint-1"];
            var route = context.Grid.WarpsAt(context.Position).First();
            context.Resolved.Add(route.Id);
            var pending = context.Campaign.Sources.Where(s => s.LocationId == "checkpoint-1").Select(s => s.Id).ToArray();
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            yield return harness.WaitUntil(() => world.IsReady, "travel chooser world");
            var menu = world.GetComponent<OverworldMenuManager>();
            var origin = world.Position;
            yield return null;
            var interactionButton = world.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Single(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == "Interact / Travel");
            Assert.That(interactionButton.IsActive() && interactionButton.IsInteractable(), Is.True, "World interaction needs a visible mouse control.");

            interactionButton.onClick.Invoke();
            yield return null;
            Assert.That(pending.All(context.Claimed.Contains), Is.True, "Interaction claims local rewards before offering travel.");
            Assert.That(world.Position, Is.EqualTo(origin));
            yield return AcknowledgeKeyAnnouncement();

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(menu.Opened, Is.True, "Enter must expose unlocked warp destinations.");
            var picker = menu.PartyMenu.transform.parent.GetComponentsInChildren<PartyMenuPicker>().Single(p => p.Owner != null);
            var resources = world.GetComponentInChildren<ResourceHUD>();
            Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(resources.transform, resources.GoldText.transform.parent).min.y,
                Is.GreaterThan(RectTransformUtility.CalculateRelativeRectTransformBounds(resources.transform, picker.Title.transform.parent).max.y),
                "Travel choices must leave the resource strip readable.");
            var destinations = picker.Rows.GetComponentsInChildren<AuthoredButton>().Where(b => b.Label.text.StartsWith("Warp to ")).ToArray();
            Assert.That(destinations.Length, Is.EqualTo(1), "Locked routes must not be offered as usable destinations.");
            Assert.That(destinations[0].Label.text, Is.EqualTo(world.WarpLabel(route)));
            var gates = context.Gates.Nearby(origin).Where(r => context.Gates.NeedsOpening(r) && r.ShortcutKind != ShortcutKind.FarSide).ToArray();
            Assert.That(picker.Rows.GetComponentsInChildren<AuthoredButton>().Count(b => b.Label.text.StartsWith("Inspect gate to ")),
                Is.EqualTo(gates.Length), "Each nearby locked gate must be independently accessible.");
            Assert.That(world.TryMove(1, 0), Is.False, "A travel choice must block world movement.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(menu.Opened, Is.False);
            Assert.That(world.Position, Is.EqualTo(origin), "Back must leave travel unchanged.");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.That(menu.Opened, Is.True, "The gamepad interaction button must open the same destination chooser.");
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo(world.WarpLabel(route)));
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.That(menu.Opened, Is.False);
            Assert.That(world.Position, Is.EqualTo(context.Grid.Locations[route.To]));
        }

        [UnityTest]
        public IEnumerator UniqueCampaignRewardUsesTheSameNameInPopupMenuAndGateAction()
        {
            var context = Enumerable.Range(0, 32)
                .Select(seed => new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed)))
                .Where(c => c.Campaign.Sources.Any(s => s.Capability == Capability.RoyalSurveyorsMaps &&
                    c.Campaign.Locations.Single(l => l.Id == s.LocationId).Kind == LocationKind.Checkpoint))
                .OrderByDescending(c => CampaignRewardNames.Name(c, Capability.RoyalSurveyorsMaps).Length).First();
            Common.Instance.CampaignContext = context;
            var source = context.Campaign.Sources.Single(s => s.Capability == Capability.RoyalSurveyorsMaps);
            context.State.Scene = "Overworld";
            context.Position = context.Grid.Locations[source.LocationId];
            string name = CampaignRewardNames.Name(context, source.Capability);
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            yield return harness.WaitUntil(() => world.IsReady, "unique reward world");

            world.ClaimRewards(false);
            yield return null;
            Assert.That(context.Held.Contains(source.Capability), Is.True);
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain(name));
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain(CampaignRewardNames.Purpose(source.Capability)));
            Assert.That(context.Campaign.Sources.Count(s => s.Capability == source.Capability), Is.EqualTo(1));
            yield return AcknowledgeKeyAnnouncement();
            int claims = context.Claimed.Count;
            world.ClaimRewards(false);
            yield return null;
            Assert.That(Common.Instance.Travel.IsTransitioning, Is.False, "Revisiting a provider must not repeat its reward popup.");
            Assert.That(context.Claimed.Count, Is.EqualTo(claims));

            var menu = world.GetComponent<OverworldMenuManager>();
            menu.OpenPartyMenu(PartyMenuTab.Capabilities);
            yield return null;
            Assert.That(menu.PartyMenu.EntryButtons.SelectMany(b => b.GetComponentsInChildren<TMPro.TMP_Text>())
                .Count(t => t.text.Contains(name)), Is.EqualTo(1));
            menu.PartyMenu.CloseDialog();
            yield return null;

            var route = context.Campaign.Routes.First(r => r.Requirement.Alternatives.Count == 1 &&
                r.Requirement.Alternatives[0].Equals(CapabilitySet.Of(source.Capability)) &&
                context.Grid.Locks.Any(g => g.RouteId == r.Id));
            var gate = context.Grid.Locks.Single(g => g.RouteId == route.Id);
            context.Position = gate.Cells.SelectMany(OverworldMovement.Neighbors).First(p =>
                context.Gates.IsWalkable(p, context.Held) && context.Gates.Nearby(p).Any(r => r.Id == route.Id));
            Assert.That(menu.ChooseGate(route), Is.True);
            yield return null;
            var picker = menu.PartyMenu.transform.parent.GetComponentsInChildren<PartyMenuPicker>().Single(p => p.Owner != null);
            var button = picker.Rows.GetComponentsInChildren<AuthoredButton>()
                .Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == name);
            Assert.That(button.Button.interactable, Is.True);
            Directory.CreateDirectory("Temp/CampaignKeys");
            ScreenCapture.CaptureScreenshot("Temp/CampaignKeys/named-gate-action.png");
            yield return new WaitForEndOfFrame();
            Assert.That(button.Label.isTextOverflowing, Is.False, "The long generated reward name must fit its action button.");
            button.Button.onClick.Invoke();
            yield return null;
            Assert.That(context.Resolved.Contains(route.Id), Is.True);
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain(name));
            yield return AcknowledgeKeyAnnouncement();
        }

        [UnityTest] public IEnumerator RoadSignOpensTownAndLandmarkDirectionsWithoutChangingProgression()
        {
            var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 42));
            Common.Instance.CampaignContext = context;
            var sign = BiomeRoadSigns.Plan(context.Grid).First(s => s.Priority == 0);
            context.Position = new GridPoint(sign.Verge.x, sign.Verge.y);
            var position = context.Position; int collected = context.Keys.Count, completed = context.Completed.Count;
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            yield return harness.WaitUntil(() => world.IsReady, "road sign world");
            Assert.That(world.NearbySign(), Is.Not.Null);
            world.ClaimRewards(); yield return null;
            var menu = world.GetComponent<OverworldMenuManager>();
            var dialog = Object.FindFirstObjectByType<MessageDialog>();
            Assert.That(menu.Opened, Is.True); Assert.That(dialog, Is.Not.Null);
            Assert.That(dialog.PromptText.text, Does.StartWith("Road sign"));
            Assert.That(dialog.PromptText.text, Does.Contain("road steps"));
            Assert.That(context.Campaign.Locations.Where(l => l.Kind == LocationKind.Town).Any(l => dialog.PromptText.text.Contains(context.GetTownDisplayName(l.Id))), Is.True);
            Assert.That(dialog.PromptText.text.Split('\n').Count(s => s.Contains("road steps")), Is.GreaterThan(2), "Landmarks appear alongside at most two towns.");
            Directory.CreateDirectory("Temp/TodoVerification"); ScreenCapture.CaptureScreenshot("Temp/TodoVerification/road-sign.png");
            yield return new WaitForEndOfFrame();
            Assert.That(context.Position, Is.EqualTo(position)); Assert.That(context.Keys.Count, Is.EqualTo(collected));
            Assert.That(context.Completed.Count, Is.EqualTo(completed)); dialog.CloseDialog();
        }

        [UnityTest]
        public IEnumerator CampaignHintChangesWithDeviceWhileMessageIsStored()
        {
            Common.Instance.CampaignContext = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 42));
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            yield return harness.WaitUntil(() => world.IsReady, "overworld control prompts");
            var feed = AuthoredUI.Require<GameMessages>(world.transform);
            var message = feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content.GetComponent<TMPro.TMP_Text>();
            Assert.That(world.Message, Does.Contain("{Interact}"));
            yield return harness.WaitUntil(() => message.text.Contains("Enter"), "first campaign event log entry");
            Assert.That(message.text, Does.Contain("Enter"));
            Assert.That(feed.History.Count(entry => entry == world.Message), Is.EqualTo(1));
            int eventCount = feed.History.Count;

            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftStick));
            yield return null;
            Assert.That(message.text, Does.Contain("A / Cross"));
            Assert.That(message.text, Does.Not.Contain("Enter"));
            yield return null; // CursorManager may update before the input module in this frame.
            Assert.That(Cursor.visible, Is.False);

            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
            yield return null;
            Assert.That(message.text, Does.Contain("Enter"));
            Assert.That(message.text, Does.Not.Contain("A / Cross"));
            yield return null;
            Assert.That(Cursor.visible, Is.True);
            Assert.That(feed.History.Count, Is.EqualTo(eventCount), "Changing controls must not duplicate campaign messages.");
            GameMessages.ShowHistory();
            yield return null;
            var history = feed.GetComponentInChildren<EventHistoryDialog>();
            Assert.That(history.Entries.text, Does.Contain("Enter"));
            Assert.That(history.Entries.text, Does.Not.Contain("{Interact}"));
            history.CloseDialog();
        }

        [UnityTest]
        public IEnumerator LockChooserOffersEveryAcquiredOptionAndCancelsWithoutOpening()
        {
            Common.Instance.BeginSandbox(42);
            var context=Common.Instance.CampaignContext;
            context.Roster.UnionWith(context.Campaign.Companions.Select(c=>c.Id));
            var route=context.Campaign.Routes.First(r=>r.ShortcutKind==ShortcutKind.Keyed && context.Grid.Locks.Any(g=>g.RouteId==r.Id));
            var gate=context.Grid.Locks.First(g=>g.RouteId==route.Id);
            context.Position=gate.Cells.SelectMany(OverworldMovement.Neighbors).First(p=>context.Gates.IsWalkable(p,context.Held) && context.Gates.Nearby(p).Any(r=>r.Id==route.Id));
            context.Keys.Add("wrong key"); context.Keys.Add(route.KeyId);
            yield return SceneManager.LoadSceneAsync("Overworld",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("Overworld");
            var world=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<OverworldScene>()).Single();
            yield return harness.WaitUntil(()=>world.IsReady,"lock chooser world");
            Assert.That(world.OpenGate(route.Id),Is.True);
            yield return null;
            var menu=world.GetComponent<OverworldMenuManager>();
            var picker=menu.PartyMenu.transform.parent.GetComponentsInChildren<PartyMenuPicker>().Single(p=>p.Owner!=null);
            Assert.That(context.Gates.NeedsOpening(route),Is.True);
            Assert.That(world.TryMove(1,0),Is.False);
            Assert.That(world.OpenGate(route.Id),Is.False);
            var buttons=picker.Rows.GetComponentsInChildren<AuthoredButton>();
            Assert.That(buttons.Length,Is.EqualTo(CampaignGuidance.Acquired(context).Count()+2));
            Assert.That(buttons.All(b=>b.Button.interactable),Is.True,"Benched capabilities remain selectable and explain their requirements.");
            Assert.That(picker.Rows.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains(route.LockText)),Is.True);
            var benched=CampaignGuidance.Acquired(context).First(capability=>!world.Held.Contains(capability));
            var unavailable=buttons.Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith(CampaignRewardNames.Name(context,benched)+" requires "));
            unavailable.Button.Select();
            unavailable.Button.onClick.Invoke();
            Assert.That(picker.Title.text,Does.Contain("active party"));
            Assert.That(context.Gates.NeedsOpening(route),Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,Is.SameAs(unavailable.Button.gameObject));
            yield return null;
            var wrong=buttons.Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text=="wrong key");
            wrong.Button.Select();
            wrong.Button.onClick.Invoke();
            Assert.That(picker.Title.text,Does.Contain("does not fit").And.Contain(context.Campaign.KeyLabel(route.KeyId)));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,Is.SameAs(wrong.Button.gameObject));
            Assert.That(context.Gates.NeedsOpening(route),Is.True);
            var correct=buttons.Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text==context.Campaign.KeyLabel(route.KeyId));
            correct.Button.onClick.Invoke();
            Assert.That(context.Gates.NeedsOpening(route),Is.True,"A second submission in the same frame must be ignored.");
            Directory.CreateDirectory("Temp/TravelVerification");
            ScreenCapture.CaptureScreenshot("Temp/TravelVerification/lock-chooser.png");
            yield return new WaitForEndOfFrame();
            var read=picker.Rows.GetComponentInChildren<TrainerPreviewScroll>();
            var move=new UnityEngine.EventSystems.AxisEventData(UnityEngine.EventSystems.EventSystem.current){moveDir=UnityEngine.EventSystems.MoveDirection.Down};
            read.OnMove(move);
            Assert.That(picker.scrollView.verticalNormalizedPosition,Is.LessThan(1));
            Assert.That(read.navigation.selectOnRight,Is.Not.Null);
            var cancelled=menu.Interaction;
            picker.BackButton.onClick.Invoke();
            Assert.That(cancelled.Closed,Is.True);
            Assert.That(menu.Interaction,Is.Null);
            Assert.That(context.Gates.NeedsOpening(route),Is.True);
            yield return null;
            context.Roster.Clear();context.Keys.Clear();
            Assert.That(world.OpenGate(route.Id),Is.True);
            yield return null;
            picker=menu.PartyMenu.transform.parent.GetComponentsInChildren<PartyMenuPicker>().Single(p=>p.Owner!=null);
            var missing=picker.Rows.GetComponentsInChildren<AuthoredButton>().Single();
            Assert.That(missing.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo(context.Campaign.KeyLabel(route.KeyId)));
            Assert.That(missing.Button.interactable,Is.True);
            missing.Button.Select();
            missing.Button.onClick.Invoke();
            Assert.That(picker.Title.text,Does.Contain("you do not have it").And.Contain(context.Campaign.KeyLabel(route.KeyId)));
            Assert.That(context.Gates.NeedsOpening(route),Is.True);
            Assert.That(context.Keys,Is.Empty);
            Assert.That(menu.Interaction.Closed,Is.False);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,Is.SameAs(missing.Button.gameObject));
            var description=picker.Rows.GetComponentInChildren<TrainerPreviewScroll>().GetComponent<TMPro.TMP_Text>();
            description.text=string.Join("\n\n",Enumerable.Repeat(route.LockText,12));
            picker.SetFirstSelect();
            Assert.That(picker.scrollView.content.rect.height,Is.GreaterThan(picker.scrollView.viewport.rect.height));
            Assert.That(description.isTextOverflowing,Is.False,"Long obstacle descriptions must remain readable in the scroll area.");
            picker.BackButton.onClick.Invoke();
            context.Keys.Add(route.KeyId);
            Assert.That(world.OpenGate(route.Id),Is.True);
            yield return null;
            picker=menu.PartyMenu.transform.parent.GetComponentsInChildren<PartyMenuPicker>().Single(p=>p.Owner!=null);
            correct=picker.Rows.GetComponentsInChildren<AuthoredButton>().Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text==context.Campaign.KeyLabel(route.KeyId));
            correct.Button.onClick.Invoke();
            Assert.That(context.Gates.NeedsOpening(route),Is.False);
            Assert.That(menu.Opened,Is.False);
            Assert.That(context.Keys,Does.Contain(route.KeyId));
            correct.Button.onClick.Invoke();
            Assert.That(context.Opened.Count(id=>id==route.Id),Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CompanionWithoutAnimatorCanWalkAfterCachedTerrainReload()
        {
            Common.Instance.BeginSandbox(42);
            var context = Common.Instance.CampaignContext;
            var companion = context.Campaign.Companions.First();
            context.Roster.Add(companion.Id);
            Assert.That(context.SetParty(new[] { companion.Id }), Is.True);
            for (int visit = 0; visit < 2; visit++)
            {
                yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("Overworld");
                var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
                float deadline = Time.realtimeSinceStartup + 90;
                while (!world.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(world.IsReady, Is.True, "Overworld with static companion did not become ready.");
                var follower = world.Followers.Single();
                // Synthetic missing-component regression; authored hero prefabs must all be wired.
                follower.HeroAnimator = null;
                if (visit == 0) Assert.That(world.SimulateDungeonVictory(), Is.True);
                var start = world.Position;
                var next = OverworldMovement.Neighbors(start).First(p => world.CanStep(start, p));
                yield return WalkTo(world, next);
                yield return WalkTo(world, start); // The follower moves on this step too.
                Assert.That(world.IsMoving, Is.False);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

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
            Assert.That(biomeRenderer.Biomes.Select(b => b.Material.mainTexture).Distinct().Count(), Is.EqualTo(8),"Painted biomes differ by artwork, not material tint.");
            var ground=world.Map.GetComponent<TWC.TileWorldCreator>().worldObject.GetComponentsInChildren<PaintedGroundOutput>();
            Assert.That(ground.Length,Is.EqualTo(1),"TWC is the sole broad ground owner.");
            Assert.That(ground[0].SurfaceCells[(int)GroundSurface.Water],Is.GreaterThan(0));
            Assert.That(ground[0].GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled&&r.sharedMaterial==PaintedGroundStyle.Load().Water),Is.True);
            var grid = world.Map.CurrentGrid;
            var cosmetic = world.Map.GetComponent<TWC.TileWorldCreator>().worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o => o.name == SmartEnvironmentMasks.Walls + "_layer");
            Assert.That(cosmetic.PropCount, Is.EqualTo(grid.TownFootprints.Sum(t => t.Walls.Count())));
            Assert.That(cosmetic.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(cosmetic.GetComponentsInChildren<MeshRenderer>().All(r => r.enabled && r.gameObject.activeInHierarchy), Is.True);
            var water = Enumerable.Range(0, grid.Width * grid.Height).Select(i => new GridPoint(i % grid.Width, i / grid.Width))
                .First(p => grid.RequiresBoat(p));
            Assert.That(grid.IsWalkable(water, CapabilitySet.Empty), Is.False);
            Assert.That(grid.IsWalkable(water, CapabilitySet.Of(Capability.Boat)), Is.True);
            foreach (var gate in world.Map.CurrentGrid.Locks)
                Assert.That(world.Map.CurrentGrid.IsWalkable(gate.Cells[0], world.Held), Is.False);
            var start = world.Position;
            Assert.That(world.TryMove(1, 0), Is.False, "The interior dungeon must unlock town departure first.");
            Assert.That(world.SimulateDungeonVictory(), Is.True);
            var next = OverworldMovement.Neighbors(start).First(p => world.CanStep(start, p));
            Assert.That(world.TryMove(next.X - start.X, next.Y - start.Y), Is.True);
            yield return new WaitForSeconds(.25f);
            Assert.That(world.Position, Is.EqualTo(next));
            Assert.That(Vector3.Distance(world.Player.transform.position, world.CellToWorld(next)), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(world.ViewCamera.transform.position, world.CellCenterToWorld(next) + world.CameraOffset), Is.LessThan(.01f));
            Assert.That(world.TryMove(2, 0), Is.False);
            Assert.That(world.TryMove(start.X - next.X, start.Y - next.Y), Is.True);
            yield return new WaitForSeconds(.25f);
            keyboard = InputSystem.AddDevice<Keyboard>();
            var gateway = grid.TownFootprints.Single(t => t.Entrance.Equals(start));
            var gatewayApproach = gateway.Approach;
            int stepX = gatewayApproach.X - start.X, stepY = gatewayApproach.Y - start.Y;
            var outwardKey = stepX > 0 ? Key.RightArrow : stepX < 0 ? Key.LeftArrow : stepY > 0 ? Key.UpArrow : Key.DownArrow;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(outwardKey));
            Assert.That(world.CanStep(start, gatewayApproach), Is.True);
            float inputDeadline = Time.realtimeSinceStartup + 2;
            while (world.Position.Equals(start) && Time.realtimeSinceStartup < inputDeadline) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.25f);
            Assert.That(world.Position, Is.EqualTo(gatewayApproach), "Keyboard should leave through the single gateway.");
            pad = InputSystem.AddDevice<Gamepad>();
            pad.MakeCurrent();
            var inwardButton = stepX > 0 ? GamepadButton.DpadLeft : stepX < 0 ? GamepadButton.DpadRight : stepY > 0 ? GamepadButton.DpadDown : GamepadButton.DpadUp;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(inwardButton));
            inputDeadline = Time.realtimeSinceStartup + 2;
            while (!world.Position.Equals(start) && Time.realtimeSinceStartup < inputDeadline) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return new WaitForSeconds(.25f);
            Assert.That(world.Position, Is.EqualTo(start), $"Gamepad should return to the start town. Current pad: {Gamepad.current==pad}; enabled: {pad.enabled}; dialog: {MenuUIInputModule.Active?.HasDialog}; consumed: {MenuUIInputModule.Active?.InputConsumed}; transitioning: {Common.Instance.Travel.IsTransitioning}");
            world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
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
                    if (parents.ContainsKey(cell) || !world.CanStep(at, cell)) continue;
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
            // Generated gates present as fiction; hand-built ones still fall back to "Requires:".
            Assert.That(world.Message, blockedRoute.ShortcutKind == ShortcutKind.FarSide ? Does.Contain("Open shortcut") : Does.Contain(blockedRoute.GateHint));
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
            IEnumerator ClearBridge(CampaignRoute boundary)
            {
                if (boundary.CompletionLocationId == null || world.Context.Completed.Contains(boundary.CompletionLocationId)) yield break;
                yield return WalkTo(world, grid.Locations[boundary.CompletionLocationId]);
                Assert.That(world.SimulateDungeonVictory(), Is.True, "The bridge encounter must be cleared before crossing.");
            }
            yield return WalkTo(world, grid.Locations["story-0"]);
            world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
            Assert.That(world.CollectedKeys, Is.Empty, "An ordinary claim cannot award the completion key.");
            Assert.That(world.SimulateDungeonVictory(), Is.True);
            yield return WalkTo(world, grid.Locations["repeatable-0"]);
            Assert.That(world.SimulateDungeonVictory(), Is.True);
            var exit = grid.Locks.Single(g => g.RouteId == "starter-exit");
            var exitApproach = exit.Cells.SelectMany(OverworldMovement.Neighbors).First(p =>
                exit.Cells.Any(g => System.Math.Abs(g.X - p.X) + System.Math.Abs(g.Y - p.Y) == 1) && Path(world, p) != null);
            yield return WalkTo(world, exitApproach);
            Assert.That(AutoplayRunner.TryGateActions(world, "starter-exit"), Is.True);
            yield return AcknowledgeKeyAnnouncement();
            var objective = c.ReturnObjectives.Single(o => o.Required);
            var gate = grid.Locks.Single(g => g.RouteId == objective.GateIds[0]);
            var approach = gate.Cells.SelectMany(OverworldMovement.Neighbors).Distinct()
                .Where(p => gate.Cells.Any(g => System.Math.Abs(g.X - p.X) + System.Math.Abs(g.Y - p.Y) == 1))
                .First(p => Path(world, p) != null);
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 15;
                yield return WalkTo(world, approach, gate.RouteId);
                var blocked = gate.Cells.First(g => System.Math.Abs(g.X - approach.X) + System.Math.Abs(g.Y - approach.Y) == 1);
                Assert.That(world.TryMove(blocked.X - approach.X, blocked.Y - approach.Y), Is.False);
                Assert.That(world.Message, Does.Contain(c.Routes.Single(r=>r.Id==gate.RouteId).LockText));
                var engineering = c.Sources.First(source => source.Capability == Capability.Engineering);
                yield return WalkTo(world, grid.Locations[engineering.LocationId]); world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
                Assert.That(world.Held.Contains(Capability.Engineering), Is.True);
                var enabling = c.Sources.First(source => source.Capability == objective.EnablingCapability);
                Assert.That(c.Locations.Single(l => l.Id == enabling.LocationId).RegionId, Is.Not.EqualTo(objective.RegionId));
                yield return WalkTo(world, grid.Locations[enabling.LocationId]); world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
                yield return WalkTo(world, grid.PlayerStart);
                if (enabling.CompanionId != null)
                {
                    Assert.That(world.ToggleCompanion(enabling.CompanionId), Is.True);
                    var follower = world.Followers.Single(a => a.Id == enabling.CompanionId);
                    Assert.That(follower.CirlcleRenderer.color, Is.EqualTo(follower.AllyColor));
                    Assert.That(world.ToggleCompanion(enabling.CompanionId), Is.True);
                    Assert.That(follower.gameObject.activeSelf, Is.False);
                    Assert.That(world.Followers.Any(a => a.Id == enabling.CompanionId), Is.False);
                    Assert.That(world.ToggleCompanion(enabling.CompanionId), Is.True);
                    Assert.That(world.Followers.Count(a => a.Id == enabling.CompanionId), Is.EqualTo(1));
                }
                Assert.That(world.Held.Contains(objective.EnablingCapability), Is.True);
                var gateMarkers = world.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "Gate " + gate.RouteId).Select(t => t.gameObject).ToArray();
                Assert.That(gateMarkers, Is.Not.Empty);
                Assert.That(gateMarkers.All(m => m.activeSelf), Is.True, "Acquiring the requirement must not hide the gate.");
                Assert.That(AutoplayRunner.TryGateActions(world, gate.RouteId), Is.False, "Cannot open a gate remotely.");
                yield return WalkTo(world, approach, gate.RouteId);
                Assert.That(world.TryMove(blocked.X - approach.X, blocked.Y - approach.Y), Is.False);
                Assert.That(AutoplayRunner.TryGateActions(world, gate.RouteId), Is.True);
                Assert.That(world.Message, Does.Contain("The way is now open!"));
                Assert.That(gateMarkers.All(m => !m.activeSelf), Is.True);
                Assert.That(world.CanStep(approach, blocked), Is.True);
                var reward = c.Sources.First(source => source.Capability == objective.RewardCapability);
                yield return WalkTo(world, grid.Locations[reward.LocationId]); world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
                yield return WalkTo(world, grid.PlayerStart);
                if (reward.CompanionId != null) Assert.That(world.ToggleCompanion(reward.CompanionId), Is.True);
                Assert.That(world.Held.Contains(objective.RewardCapability.Value), Is.True);
                yield return ClearBridge(c.Routes.Single(r => r.IsProgressionBoundary && r.From == "checkpoint-1"));
                yield return ClearBridge(c.Routes.Single(r => r.IsProgressionBoundary && r.From == "checkpoint-2"));
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
                        yield return WalkTo(world, grid.Locations[source.LocationId]); world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
                        if (source.CompanionId != null && !world.Held.Contains(capability))
                        {
                            var town = c.Locations.Where(l => l.Kind == LocationKind.Town).Select(l => grid.Locations[l.Id])
                                .Select(p => new { point = p, path = Path(world, p) }).Where(p => p.path != null).OrderBy(p => p.path.Count).First().point;
                            yield return WalkTo(world, town); Assert.That(world.ToggleCompanion(source.CompanionId), Is.True);
                        }
                        yield return ClearBridge(boundary);
                        yield return WalkTo(world, grid.Locations["checkpoint-" + later]);
                    }
                    var shortcut = c.Routes.Single(r => r.ShortcutKind == ShortcutKind.Keyed && r.To == "checkpoint-" + later);
                    Assert.That(shortcut.IsWarp, Is.True);
                    Assert.That(grid.Locks.Any(g => g.RouteId == shortcut.Id), Is.False);
                    Assert.That(world.Warp(shortcut.Id), Is.False);
                    Assert.That(world.Message, Does.Contain(shortcut.KeyLabel));
                    Assert.That(world.OpenShortcut(), Is.False);
                    yield return WalkTo(world, grid.Locations[shortcut.KeyLocationId]);
                    if (shortcut.KeyCondition == KeyAcquisition.DungeonCompletion)
                    {
                        world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
                        Assert.That(world.CollectedKeys, Does.Not.Contain(shortcut.KeyId), "The shortcut key requires encounter completion.");
                        Assert.That(world.SimulateDungeonVictory(), Is.True);
                    }
                    world.ClaimRewards(announceLocked: false); yield return AcknowledgeKeyAnnouncement();
                    Assert.That(world.CollectedKeys, Does.Contain(shortcut.KeyId));
                    yield return WalkTo(world, grid.Locations[shortcut.To]);
                    Assert.That(world.Warp(shortcut.Id), Is.False, "Collecting a key must leave the gate locked.");
                    Assert.That(AutoplayRunner.TryGateActions(world, shortcut.Id), Is.True);
                    yield return AcknowledgeKeyAnnouncement();
                    Assert.That(world.Message, Does.Contain("The way is now open!"));
                    Assert.That(world.Warp(shortcut.Id), Is.True);
                    Assert.That(world.Position, Is.EqualTo(grid.Locations[shortcut.From]));
                    foreach (var follower in world.Followers)
                    {
                        Assert.That(follower.TilemapPosition, Is.EqualTo(world.Player.TilemapPosition));
                        Assert.That(Vector3.Distance(follower.transform.position, world.Player.transform.position), Is.LessThan(.01f));
                    }
                    Assert.That(Vector3.Distance(world.Player.transform.position, world.CellToWorld(world.Position)), Is.LessThan(.01f));
                    Assert.That(world.Warp(shortcut.Id), Is.True);
                    Assert.That(world.Position, Is.EqualTo(grid.Locations[shortcut.To]));
                }
                CaptureOverview(world, "Temp/OverworldScene/biome-" + seed + ".png");
                CaptureOverview(world, "Temp/OverworldScene/countryside-" + seed + ".png", world.Position);
                var crossing = grid.Locks.First(g => c.Routes.Single(r => r.Id == g.RouteId).IsProgressionBoundary);
                CaptureOverview(world, "Temp/OverworldScene/crossing-" + seed + ".png", crossing.Cells[0]);
                CaptureOverview(world, "Temp/OverworldScene/destination-" + seed + ".png", grid.Locations[objective.DestinationIds[0]]);
            }
            finally { Time.timeScale = previousTimeScale; }
        }

        private static Stack<GridPoint> Path(OverworldScene world, GridPoint target, string blockedRoute = null)
        {
            var parents = new Dictionary<GridPoint, GridPoint> { [world.Position] = world.Position };
            var queue = new Queue<GridPoint>(); queue.Enqueue(world.Position);
            while (queue.Count > 0 && !parents.ContainsKey(target))
            {
                var at = queue.Dequeue();
                foreach (var next in OverworldMovement.Neighbors(at))
                    if ((blockedRoute == null || world.Map.CurrentGrid.LockAt(next)?.RouteId != blockedRoute) && !parents.ContainsKey(next) && (world.CanStep(at, next) || (at.X == next.X || at.Y == next.Y) && world.Map.CurrentGrid.CanStep(at, next, world.Held))) { parents[next] = at; queue.Enqueue(next); }
            }
            if (!parents.ContainsKey(target)) return null;
            var path = new Stack<GridPoint>();
            for (var at = target; !at.Equals(world.Position); at = parents[at]) path.Push(at);
            return path;
        }

        private static IEnumerator AcknowledgeKeyAnnouncement()
        {
            var common = Common.Instance;
            if (!common.Travel.IsTransitioning) yield break;
            yield return null;
            yield return null;
            Assert.That(common.MessageDialog.gameObject.activeSelf, Is.True);
            Assert.That(common.MessageDialog.PromptText.text, Does.Contain("GATE UNLOCKED!").Or.Contain("KEY ACQUIRED!").Or.Contain("KEYS ACQUIRED!").Or.Contain("CAPABILITY ACQUIRED!").Or.Contain("CAPABILITY USED!").Or.Contain("REWARDS ACQUIRED!"));
            common.MessageDialog.Ok_Clicked();
            yield return null;
            yield return null;
            Assert.That(common.Travel.IsTransitioning, Is.False);
        }

        private static IEnumerator WalkTo(OverworldScene world, GridPoint target, string blockedRoute = null)
        {
            var path = Path(world, target, blockedRoute);
            Assert.That(path, Is.Not.Null, "No walkable path to " + target + " with " + world.Held);
            while (path.Count > 0)
            {
                var next = path.Pop();
                var previousPartyCells = new[] { world.Player }.Concat(world.Followers)
                    .Select(a => a.TilemapPosition).ToArray();
                if (!world.CanStep(world.Position, next))
                    Assert.That(AutoplayRunner.TryGateActions(world), Is.True, "Eligible gates must be opened locally before crossing.");
                yield return AcknowledgeKeyAnnouncement();
                Assert.That(world.TryMove(next.X - world.Position.X, next.Y - world.Position.Y), Is.True);
                float deadline = Time.realtimeSinceStartup + 3;
                while (world.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(world.IsMoving, Is.False);
                for (int i = 0; i < world.Followers.Count; i++)
                {
                    var follower = world.Followers[i];
                    var expected = previousPartyCells[i];
                    Assert.That(follower.TilemapPosition, Is.EqualTo(expected), "Each ally follows its leader's previous tile.");
                    Assert.That(Vector3.Distance(follower.transform.position,
                        world.CellToWorld(new GridPoint(expected.x, expected.y))), Is.LessThan(.01f));
                }
            }
        }

        private static void CaptureOverview(OverworldScene world, string path, GridPoint? detail = null)
        {
            var camera = world.ViewCamera; var grid = world.Map.CurrentGrid;
            var target = new RenderTexture(1024, 1024, 24); var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            var position = camera.transform.position; var rotation = camera.transform.rotation; float size = camera.orthographicSize;
            try
            {
                Vector3 center = world.CellToWorld(new GridPoint(grid.Width / 2, grid.Height / 2));
                camera.transform.position = center + Vector3.back * 100; camera.transform.LookAt(center, Vector3.up);
                camera.orthographicSize = grid.Height * world.Map.Template.cellSize * .53f;
                if (detail.HasValue)
                {
                    camera.transform.position = world.CellCenterToWorld(detail.Value) + world.CameraOffset;
                    camera.transform.rotation = rotation;
                    camera.orthographicSize = size;
                }
                camera.targetTexture = target; camera.Render();
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
            yield return harness.Cleanup();
            inputScope.Dispose();
        }
    }
}
