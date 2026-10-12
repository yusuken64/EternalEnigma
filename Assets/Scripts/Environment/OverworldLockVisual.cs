using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Route fiction selects a closed obstacle silhouette. Logical locks remain in Core.</summary>
public sealed class OverworldLockVisual : MonoBehaviour
{
    public string SkinId { get; private set; }
    public string ModelId { get; private set; }
    public static (string Model,string Detail) Models(CampaignRoute route)
    {
        var skin=LockSkinCatalog.Skins.FirstOrDefault(s=>s.Id==route.SkinId);
        if(skin==null)return ("CastleGate",null);
        return skin.Capability switch
        {
            Capability.Climb=>("CliffStraight",null),
            Capability.Grapple=>("CornerPost","Fence"),
            Capability.Icewalk or Capability.Icebreaker=>("BoulderSmall","RockCap"),
            Capability.DeepDive=>("Reeds","RockCap"),
            Capability.Breach or Capability.Tunneler=>("BoulderLarge","BoulderSmall"),
            Capability.WaystoneStep=>("StoneBase","CastleFlag"),
            Capability.PhaseShift=>("FacadeDoor","CastleFlag"),
            Capability.HazardWard=>("DeadTree","MushroomRing"),
            Capability.Boat=>("Fence","Reeds"),
            Capability.Airship=>("CliffEnd","CastleFlag"),
            Capability.Engineering=>("Fence","Chimney"),
            Capability.Translation=>("Signboard","FacadeWall"),
            Capability.RoyalAuthority=>("CastleGate","CastleFlag"),
            Capability.AncientAttunement=>("RockCap","StoneBase"),
            Capability.BeastSpeech=>("PackTree03","BoulderSmall"),
            Capability.GuildStanding=>("ServiceShop","Fence"),
            Capability.Remedy=>("ServiceInn","FlowerBox"),
            Capability.Diplomacy=>("CastleFlag","Signboard"),
            Capability.SacredRite=>("StoneBase","FlowerBox"),
            Capability.MillwrightTraining=>("Chimney","Fence"),
            Capability.ForestersWrit=>("PackTree03","Signboard"),
            Capability.FerrymansFavor=>("Reeds","CornerPost"),
            Capability.GravediggersCharge=>("StoneBase","FacadeDoor"),
            Capability.SmugglersMark=>("FacadeDoor","Fence"),
            Capability.BellkeepersOffice=>("CastleFlag","Chimney"),
            Capability.FuneraryRites=>("RockCap","DeadTree"),
            Capability.RoyalSurveyorsMaps=>("Signboard","StoneBase"),
            _=>("CastleGate",null)
        };
    }
    public static void Build(GameObject marker,CampaignRoute route,OverworldBiome biome)
    {
        var catalog=DioramaCatalog.Load();
        var selected=Models(route);
        if(catalog?.Get(selected.Model)?.Prefab==null)return;
        foreach(var renderer in marker.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
        var view=marker.AddComponent<OverworldLockVisual>();view.SkinId=route.SkinId;view.ModelId=selected.Model;
        var root=new GameObject("Lock obstacle "+selected.Model);root.transform.SetParent(marker.transform,false);
        var batch=new EnvironmentBatch(root.transform);
        catalog.Add(batch,selected.Model,biome,Vector3.zero,variation:.75f);
        if(selected.Detail!=null && catalog.Get(selected.Detail)?.Prefab!=null)
            catalog.Add(batch,selected.Detail,biome,new Vector3(.45f,.15f,0),variation:.45f);
        batch.Finish();
    }
}
