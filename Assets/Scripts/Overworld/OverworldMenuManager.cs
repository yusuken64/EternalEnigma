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
    public string CompanionName(string id) => CampaignParty.Resolve(id, TownSceneLoader.Default)?.Name ?? id;

    private void EnsurePickers()
    {
        if(PartyMenu==null)PartyMenu=global::PartyMenu.Create(transform);
    }

    public bool ChooseGate(CampaignRoute route)
    {
        if(Opened || world.IsMoving || !world.IsReady || Common.Instance.Travel.IsTransitioning)return false;
        var session=world.BeginGateInteraction(route.Id);
        if(session==null)return false;
        Interaction=session;
        EnsurePickers();
        PartyMenuPicker picker=null;
        var options=new List<(string Label,Action Execute)>();
        void Attempt(LockAction action)
        {
            var outcome=world.AttemptGate(session,action);
            if(session.Closed){picker.CloseDialog();return;}
            picker.Title.text=outcome==LockOutcome.Progress?"Something changed, but the way is still blocked.":"This had no effect";
        }
        foreach(var capability in CampaignGuidance.Acquired(world.Context))
        {
            var action=new CapabilityLockAction(capability);
            bool available=world.Held.Contains(capability);
            options.Add((capability.DisplayName()+(available?"":" — Requires "+CampaignGuidance.CapabilitySource(world.Context,capability,CompanionName)),
                available?(Action)(()=>Attempt(action)):null));
        }
        foreach(var key in world.CollectedKeys)options.Add((world.Campaign.KeyLabel(key),()=>Attempt(new KeyLockAction(key))));
        picker=PartyMenuPicker.Build(PartyMenu.transform.parent,"Choose an action",options,false);
        picker.Description((route.LockText??route.GateHint)+(options.Count==0?"\n\nNo acquired capabilities or held keys.":""));
        picker.CloseAction=()=>{session.Dispose();Interaction=null;};
        dialogs.Open(picker);
        return true;
    }

    public bool ConfirmEntry()
    {
        if(Opened || world.IsMoving || !world.IsReady || Common.Instance.Travel.IsTransitioning)return false;
        var context=world.Context;
        var location=context.Location;
        if(location==null)return false;
        if(location.Kind==LocationKind.Town)return Common.Instance.Travel.EnterLocation();
        if(location.Kind!=LocationKind.StoryDungeon && location.Kind!=LocationKind.RepeatableDungeon && location.Kind!=LocationKind.FinalDungeon)return false;
        EnsurePickers();
        var position=world.Position;
        var picker=PartyMenuPicker.Build(PartyMenu.transform.parent,"Enter dungeon?",new List<(string,Action)>{("Enter",()=>
        {
            if(Common.Instance.CampaignContext==context && context.Position.Equals(position) && context.Location==location)
                Common.Instance.Travel.EnterLocation();
        })});
        picker.Description(location.Id+"\n"+(context.Completed.Contains(location.Id)?"Cleared":"Not cleared"));
        dialogs.Open(picker);
        return true;
    }
    private void Awake()=>world=GetComponent<OverworldScene>();
    private void Start()=>PartyMenuLauncher.Create(transform,()=>world.IsReady&&!world.IsMoving&&!world.Context.IsSandbox,OpenPartyMenu,includeCapabilities:true);
    private void Update()
    {
        var common=Common.Instance;
        if(common==null || !world.IsReady || world.IsMoving || common.Travel.IsTransitioning || common.GlobalSettings.IsOpen ||
            AutoplayRunner.BlocksPlayerInput || MenuUIInputModule.Active?.InputConsumed==true)return;
        var input=common.MenuInputHandler;
        if(Opened && input.OptionInput){common.GlobalSettings.ShowDialog();return;}
        if(!input.MenuOpenClosedInput && !input.OpenSkillMenuInput)return;
        var tab=input.MenuOpenClosedInput?PartyMenuTab.Inventory:PartyMenuTab.Skills;
        if(PartyMenu!=null && dialogs.Current==PartyMenu)PartyMenu.Shortcut(tab);
        else if(!Opened && MenuUIInputModule.Active?.HasDialog!=true)OpenPartyMenu(tab);
    }
    public void OpenPartyMenu(PartyMenuTab tab)
    {
        if(!world.IsReady || world.IsMoving || world.Context.IsSandbox || Interaction!=null)return;
        if(PartyMenu==null)PartyMenu=global::PartyMenu.Create(transform);
        PartyMenu.Setup(new OverworldPartyMenuContext(world),tab,Common.Instance.GameSaveData.ProtagonistId);
        dialogs.Open(PartyMenu);
    }
    private void LateUpdate()=>dialogs.Tick();
    private void OnDisable(){if(dialogs.Opened)dialogs.CloseAll();Interaction?.Dispose();Interaction=null;}
}
