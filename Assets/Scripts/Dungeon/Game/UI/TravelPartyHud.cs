using UnityEngine;

/// <summary>The shared party cards bound to live town and overworld heroes.</summary>
public sealed class TravelPartyHud : MonoBehaviour
{
    public Transform PartyRoot;
    public CharacterStatsDisplay[] PartySlots;
    private Town town;
    private OverworldScene world;
    private TownMenuManager townMenus;
    private OverworldMenuManager worldMenus;
    private Canvas canvas;
    private void OnEnable() { HudRefresh.Changed += Refresh; HudRefresh.Request(HudChange.Party | HudChange.Visibility); }
    private void OnDisable() => HudRefresh.Changed -= Refresh;

    public static TravelPartyHud Ensure(MonoBehaviour owner) => AuthoredUI.Require<TravelPartyHud>(owner.transform);

    private void Awake()
    {
        town=GetComponentInParent<Town>();
        world=GetComponentInParent<OverworldScene>();
        canvas=GetComponent<Canvas>();
        canvas.enabled=false;
    }

    private void Refresh(HudChange changes)
    {
        // The cards display party state and are independent of travel position.
        if ((changes & (HudChange.Party | HudChange.Visibility)) == 0) return;
        var common=Common.InstanceOrNull;
        if(town!=null && townMenus==null)townMenus=town.GetComponentInChildren<TownMenuManager>(true);
        if(world!=null && worldMenus==null)worldMenus=world.GetComponent<OverworldMenuManager>();
        bool ready=common!=null && !common.GlobalSettings.IsOpen && !common.ScreenTransition.HasTravelPresentation &&
            (town!=null && town.IsReady && townMenus?.CoversGameplayHud!=true || world!=null && world.IsReady);
        canvas.enabled=ready;
        if(!ready)return;

        var leader=town!=null?town.TownPlayer.ControllingTownAlly:world.Player;
        var menu=town!=null?townMenus?.PartyMenu:worldMenus?.PartyMenu;
        var selected=menu?.Owner!=null?menu.Hero?.TownActor:leader;
        var followers=world!=null?world.Followers:null;
        for(int i=0;i<PartySlots.Length;i++)
        {
            var hero=town!=null
                ?i<town.TownPlayer.RecruitedAllies.Count?town.TownPlayer.RecruitedAllies[i]:null
                :i==0?world.Player:i-1<followers.Count?followers[i-1]:null;
            var card=PartySlots[i];
            card.gameObject.SetActive(hero!=null);
            if(hero==null)continue;
            card.UpdateTravelUI(hero);
            card.GetComponent<DungeonPartyCard>().ShowTravelState(hero,hero==leader,hero==selected);
        }
    }
}
