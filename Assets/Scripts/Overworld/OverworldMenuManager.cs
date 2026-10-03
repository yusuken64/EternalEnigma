using JuicyChickenGames.Menu;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public sealed class OverworldMenuManager : MonoBehaviour
{
    private readonly DialogController dialogs = new();
    private OverworldScene world;
    public PartyMenu PartyMenu { get; private set; }
    public bool Opened=>dialogs.Opened;
    private void Awake()=>world=GetComponent<OverworldScene>();
    private void Start()=>PartyMenuLauncher.Create(transform,()=>world.IsReady&&!world.IsMoving&&!world.Context.IsSandbox,OpenPartyMenu);
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
        if(!world.IsReady || world.IsMoving || world.Context.IsSandbox)return;
        if(PartyMenu==null)PartyMenu=global::PartyMenu.Create(transform);
        PartyMenu.Setup(new OverworldPartyMenuContext(world),tab,Common.Instance.GameSaveData.ProtagonistId);
        dialogs.Open(PartyMenu);
    }
    private void LateUpdate()=>dialogs.Tick();
    private void OnDestroy(){if(dialogs.Opened)dialogs.CloseAll();}
}
