using JuicyChickenGames.Menu;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Capabilities;

[DefaultExecutionOrder(-50)]
public sealed class OverworldMenuManager : MonoBehaviour
{
    private readonly DialogController dialogs = new();
    private OverworldScene world;
    public PartyMenu PartyMenu { get; private set; }
    public bool Opened=>dialogs.Opened;
    internal void Open(Dialog dialog)=>dialogs.Open(dialog);
    public LockInteractionSession Interaction { get; private set; }
    public TargetDialog TargetDialog { get; private set; }
    private CampaignRoute interactionRoute;
    private readonly HashSet<Capability> applied = new();
    public string CompanionName(string id) => GameplayText.Name(CampaignParty.Resolve(id, TownSceneLoader.Default)?.Name ?? "a companion");

    private void EnsurePickers()
    {
        if(PartyMenu==null)PartyMenu=global::PartyMenu.Create(transform);
    }

    public bool ChooseTravel()
    {
        if (Opened || world.IsMoving || !world.IsReady || Common.Instance.Travel.IsTransitioning ||
            Common.Instance.GlobalSettings.IsOpen || MenuUIInputModule.Active?.HasDialog == true) return false;
        var context = world.Context;
        var position = world.Position;
        var routes = world.Map.CurrentGrid.WarpsAt(position).Where(r => r.CanTraverse(world.Held, context.Resolved)).ToArray();
        var gates = context.Gates.Nearby(position).Where(r => context.Gates.NeedsOpening(r) && r.ShortcutKind != ShortcutKind.FarSide).ToArray();
        if (routes.Length == 0) return gates.Length > 0 && ChooseGate(gates[0]);
        EnsurePickers();
        bool StillHere() => Common.Instance.CampaignContext == context && context.Position.Equals(position);
        var choices = routes.Select(route => (world.WarpLabel(route), (Action)(() =>
        {
            if (StillHere()) world.Warp(route.Id);
        }))).ToList();
        foreach (var gate in gates)
        {
            string destination = world.Map.CurrentGrid.Locations[gate.From].Equals(position) ? gate.To : gate.From;
            choices.Add(("Inspect gate to " + CampaignGuidance.LocationName(context, destination),
                () => { if (StillHere()) world.OpenGate(gate.Id); }));
        }
        dialogs.Open(PartyMenuPicker.Build(PartyMenu.transform.parent, "Choose a destination", choices));
        return true;
    }

    public bool ChooseGate(CampaignRoute route)
    {
        if(Opened || world.IsMoving || !world.IsReady || Common.Instance.Travel.IsTransitioning ||
            Common.Instance.GlobalSettings.IsOpen || MenuUIInputModule.Active?.HasDialog == true)return false;
        var session=world.BeginGateInteraction(route.Id);
        if(session==null)return false;
        Interaction=session;
        interactionRoute=route;
        applied.Clear();
        GameMessages.Post(route.GateHint, coalesce:true);
        ShowPartyMenu(PartyMenuTab.Capabilities);
        return true;
    }

    internal string GateActionRestriction(LockAction action)
    {
        if (action is CapabilityLockAction use && !world.Held.Contains(use.Capability)) return MissingCapability(use.Capability);
        if (action is KeyLockAction key && !world.CollectedKeys.Contains(key.KeyId))
            return "You don't have the " + world.Campaign.KeyLabel(key.KeyId) + ".";
        return null;
    }

    private string MissingCapability(Capability capability)
    {
        string name = CampaignRewardNames.Name(world.Context, capability);
        string source = CampaignGuidance.CapabilitySource(world.Context, capability, CompanionName);
        return string.IsNullOrEmpty(source) ? "You don't have " + name + " yet." :
            "To use " + name + ", bring " + source + " in your travelling party.";
    }

    private string RequirementText(Requirement requirement) =>
        requirement.Describe(c => CampaignRewardNames.Name(world.Context, c));

    internal void UseGateAction(LockAction action, string label)
    {
        if (!Opened || PartyMenu.Owner != dialogs || dialogs.Current is TargetDialog) return;
        string restriction = GateActionRestriction(action);
        if (restriction != null) { PartyMenu.Reject(restriction); return; }
        var targets = world.Context.Gates.Nearby(world.Position).Where(route => route.HasGate &&
            world.Context.Gates.NeedsOpening(route) && route.ShortcutKind != ShortcutKind.FarSide &&
            (route.CompletionLocationId == null || world.Context.Completed.Contains(route.CompletionLocationId))).ToList();
        if (targets.Count == 0) { PartyMenu.Reject("There are no nearby gates to target."); return; }
        if (TargetDialog == null)
            TargetDialog = Instantiate(GameContent.Load<TargetDialog>("UI/OverworldTargetDialog"), transform);
        dialogs.Open(TargetDialog);
        TargetDialog.Setup(world, label, targets, interactionRoute?.Id, route => AttemptGate(route, action));
        TargetDialog.CloseAction = TargetDialog.Close;
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }

    private void AttemptGate(CampaignRoute route, LockAction action)
    {
        if (dialogs.Current != TargetDialog) return;
        if (interactionRoute != route || Interaction == null || Interaction.Closed)
        {
            EndGateInteraction();
            Interaction = world.BeginGateInteraction(route.Id);
            interactionRoute = route;
        }
        if (Interaction == null) { PartyMenu.Reject("Return to this gate before using that capability or key."); return; }
        var outcome = world.AttemptGate(Interaction, action, false);
        if (outcome == LockOutcome.Opened) { dialogs.CloseAll(); return; }
        if (Interaction.Closed)
        {
            EndGateInteraction();
            PartyMenu.Complete(null);
            PartyMenu.Reject("Return to this gate before using that capability or key.");
            return;
        }
        if (outcome == LockOutcome.Progress)
        {
            if (action is CapabilityLockAction progress) applied.Add(progress.Capability);
            AudioManager.Instance?.PlayCue(SoundCue.Status);
            PartyMenu.Complete("Something changed, but the way is still blocked.");
        }
        else PartyMenu.Reject(GateRejection(route, action, applied));
    }

    private void EndGateInteraction()
    {
        Interaction?.Dispose(); Interaction=null; interactionRoute=null; applied.Clear();
    }

    private string GateRejection(CampaignRoute route, LockAction action, HashSet<Capability> applied)
    {
        if(action is KeyLockAction key)
        {
            if(!world.CollectedKeys.Contains(key.KeyId))return "You don't have the " + world.Campaign.KeyLabel(key.KeyId) + ".";
            return route.ShortcutKind==ShortcutKind.Keyed
                ? "The " + world.Campaign.KeyLabel(key.KeyId) + " doesn't fit. This lock takes the " + world.Campaign.KeyLabel(route.KeyId) + "."
                : "No key opens this way. " + route.GateHint;
        }
        if(action is CapabilityLockAction use)
        {
            string name=CampaignRewardNames.Name(world.Context,use.Capability);
            if(!world.Held.Contains(use.Capability))return MissingCapability(use.Capability);
            if(route.ShortcutKind==ShortcutKind.Keyed)return "This gate needs the " + world.Campaign.KeyLabel(route.KeyId) + ".";
            if(applied.Contains(use.Capability))return "You've already used " + name + " here. Something else is needed. " + route.GateHint;
            return name + " won't open this way. " + route.GateHint;
        }
        return "Choose an ability or key for this gate.";
    }

    public bool ConfirmEntry()
    {
        if(Opened || world.IsMoving || !world.IsReady || Common.Instance.Travel.IsTransitioning)return false;
        var context=world.Context;
        var location=context.Location;
        if(location==null)return false;
        if(location.Kind==LocationKind.Town)return Common.Instance.Travel.EnterLocation();
        if(location.Encounter==null && location.Kind!=LocationKind.StoryDungeon && location.Kind!=LocationKind.RepeatableDungeon && location.Kind!=LocationKind.FinalDungeon)return false;
        EnsurePickers();
        var position=world.Position;
        string BlockedReason() => !context.CanEnterDungeon(location.Id) ? context.OpeningPreparationHint :
            context.State.Finished && location.Id == context.Campaign.FinalLocationId ? "This final dungeon has already been completed." :
            location.Encounter?.RequiresResetFloor == true && !context.ResetFloorUnlocked
                ? "Learn Reset Floor by clearing the first repeatable dungeon before entering." :
            location.Encounter != null && !location.Encounter.EntryRequirement.IsSatisfiedBy(context.Held)
                ? "You need " + RequirementText(location.Encounter.EntryRequirement) + " to enter here." : null;
        var blockedReason=BlockedReason();
        PartyMenuPicker picker=null;
        Action enter = () =>
        {
            string reason=BlockedReason();
            if(Common.Instance.CampaignContext!=context || !context.Position.Equals(position) || context.Location!=location)
                reason="Return to this entrance before entering.";
            if(reason==null && Common.Instance.Travel.EnterLocation()) {picker.CloseDialog();return;}
            reason??="This dungeon cannot be entered right now.";
            picker.Title.text=GameplayText.Format(reason);
            GameMessages.Reject(reason);
        };
        picker=PartyMenuPicker.Build(PartyMenu.transform.parent,location.Encounter==null?"Enter dungeon?":"Enter encounter?",
            new List<(string,Action)>{("Enter", enter)},false);
        string placeName=CampaignGuidance.LocationName(context, location.Id);
        picker.Description(char.ToUpperInvariant(placeName[0])+placeName.Substring(1)+"\n"+(context.Completed.Contains(location.Id)?"Cleared":"Not cleared")+
            "\n" + location.DungeonDescription + (location.Variant == EternalEnigma.Core.World.DungeonVariant.Town ? ". Hostile territory" : "") +
            (location.Encounter?.Kind==EncounterKind.Prison?"\nYour carried and equipped possessions will be confiscated temporarily. Recover them from the guarded stash before escaping. Gold and learned skills remain available.":"")+
            (blockedReason == null ? "" : "\n" + blockedReason), showControlHints:false);
        dialogs.Open(picker);
        return true;
    }
    private void Awake()=>world=GetComponent<OverworldScene>();
    private void Start()=>PartyMenuLauncher.Create(transform,
        ()=>world.IsReady && !world.IsMoving && (!world.Context.IsSandbox || !world.ShowSandboxControls) && !Opened && Interaction==null,
        OpenPartyMenu,includeCapabilities:true);
    private void Update()
    {
        var common=Common.Instance;
        if(common==null || !world.IsReady || common.Travel.IsTransitioning || common.GlobalSettings.IsOpen ||
            AutoplayRunner.BlocksPlayerInput || MenuUIInputModule.Active?.InputConsumed==true)return;
        var input=common.MenuInputHandler;
        if (input.OptionInput && (!Opened || !input.CancelMenuInput)) { common.GlobalSettings.ShowDialog(); return; }
        if (world.IsMoving) return;
        if (TargetDialog != null && dialogs.Current == TargetDialog)
        {
            if (input.SubmitMenuInput) TargetDialog.ConfirmTarget();
            else if (input.CancelMenuInput)
            {
                AudioManager.Instance?.PlayCue(SoundCue.Cancel);
                TargetDialog.CloseDialog();
            }
            return;
        }
        if(!input.MenuOpenClosedInput && !input.OpenSkillMenuInput)return;
        var tab=input.MenuOpenClosedInput?PartyMenuTab.Inventory:PartyMenuTab.Skills;
        if(PartyMenu!=null && dialogs.Current==PartyMenu)PartyMenu.Shortcut(tab);
        else if(!Opened && MenuUIInputModule.Active?.HasDialog!=true)OpenPartyMenu(tab);
    }
    public void OpenPartyMenu(PartyMenuTab tab)
    {
        if(!world.IsReady || world.IsMoving || world.Context.IsSandbox && world.ShowSandboxControls || Interaction!=null ||
            Common.Instance.Travel.IsTransitioning || Common.Instance.GlobalSettings.IsOpen || AutoplayRunner.BlocksPlayerInput ||
            Opened && dialogs.Current != PartyMenu || !Opened && MenuUIInputModule.Active?.HasDialog == true)return;
        ShowPartyMenu(tab);
    }
    private void ShowPartyMenu(PartyMenuTab tab)
    {
        EnsurePickers();
        PartyMenu.Setup(new OverworldPartyMenuContext(world),tab,Common.Instance.GameSaveData.ProtagonistId);
        PartyMenu.CloseAction=EndGateInteraction;
        dialogs.Open(PartyMenu);
    }
    private void LateUpdate()=>dialogs.Tick();
    private void OnDisable(){if(dialogs.Opened)dialogs.CloseAll();EndGateInteraction();}
}
