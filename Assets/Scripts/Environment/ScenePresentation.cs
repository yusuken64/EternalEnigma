using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Session-only daylight phase; no clocks or catch-up on resume.</summary>
public sealed class ScenePresentation : MonoBehaviour
{
    public static float Phase { get; private set; }
    private static int advancedFrame=-1;
    private Light sun;
    private SilhouetteRenderer silhouettes;
    private Game game;
    private Town town;
    private OverworldScene world;
    private Camera viewCamera;
    private Color originalBackground;
    private static readonly Color GrasslandSky = new Color32(0x65, 0xBD, 0xF2, 0xFF);
    public static Color FillAmbient(Color ambient) => Color.Lerp(ambient,new Color(.62f,.64f,.67f),.65f);
    public static Vector3 SunDirection(bool outdoor, float phase)
    {
        float angle=(outdoor?phase*360:35)*Mathf.Deg2Rad;
        float elevation=(outdoor?55+10*Mathf.Sin(phase*Mathf.PI*2):55)*Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle)*Mathf.Cos(elevation),Mathf.Sin(angle)*Mathf.Cos(elevation),Mathf.Sin(elevation));
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPhase(){Phase=0;advancedFrame=-1;SilhouetteParticipant.Active.Clear();}
    public static void Ensure(MonoBehaviour owner)
    {
        if(owner.GetComponent<ScenePresentation>()!=null)return;
        var presentation=owner.gameObject.AddComponent<ScenePresentation>();
        presentation.game=owner as Game;presentation.town=owner as Town;presentation.world=owner as OverworldScene;
    }
    public static void RegisterWorld(Transform root)
    {
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(renderer.GetComponent<SilhouetteParticipant>()!=null || renderer.GetComponentInParent<Canvas>()!=null ||
                renderer.GetComponentInParent<ParticleSystem>()!=null || renderer.GetComponent<BiomeDecorationEffect>()!=null)continue;
            var material=renderer.sharedMaterial;
            if(material==null)continue;
            string name=renderer.name.ToLowerInvariant();
            string shader=material.shader.name.ToLowerInvariant();
            if(shader.Contains("water") || shader.Contains("ocean") || shader.Contains("fog") || shader.Contains("shore") ||
                material.renderQueue>=3000 || name.Contains("marker") || name.Contains("warp") || name.Contains("pool") || name.Contains("water"))continue;
            var participant=renderer.gameObject.AddComponent<SilhouetteParticipant>();
            participant.Role=name.Contains("bridge") || renderer.bounds.size.z<.12f ? SilhouetteRole.Receiver :
                renderer.bounds.size.z>.4f?SilhouetteRole.Caster:SilhouetteRole.None;
        }
    }
    private void Start()
    {
        viewCamera=world!=null && world.ViewCamera!=null?world.ViewCamera:Camera.main;
        if(viewCamera!=null)
        {
            originalBackground=viewCamera.backgroundColor;
            silhouettes=viewCamera.GetComponent<SilhouetteRenderer>()??viewCamera.gameObject.AddComponent<SilhouetteRenderer>();
        }
        sun=RenderSettings.sun;
        if(sun==null)foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){sun=light;break;}
        if(sun!=null)sun.shadows=LightShadows.None;
        RenderSettings.ambientLight=FillAmbient(RenderSettings.ambientLight);
    }
    private void Update()
    {
        bool outdoor=game==null || game.DungeonGenerator!=null &&
            game.DungeonGenerator.CurrentVisuals.Environment==DungeonEnvironmentKind.Outdoor;
        if(town?.Plan!=null && town.TownPlayer?.ControllingTownAlly!=null)
        {
            var cell=town.TownPlayer.ControllingTownAlly.TilemapPosition;
            var interior=town.Plan.Layers[EternalEnigma.Core.World.TownLayers.ShopFloor];
            if(cell.x>=0 && cell.y>=0 && cell.x<interior.Width && cell.y<interior.Height && interior[cell.x,cell.y])outdoor=false;
        }
        if(viewCamera!=null)
        {
            bool grassland=game!=null
                ? game.DungeonGenerator!=null && game.DungeonGenerator.CurrentVisuals.Biome==OverworldBiome.Grassland
                : town!=null
                    ? town.WalkableMap!=null &&
                      (town.WalkableMap.TileWorldCreator?.GetComponent<TownBiomeStyle>()?.Current ?? OverworldBiome.Grassland)==OverworldBiome.Grassland
                    : world!=null && world.IsReady && world.Map.CurrentGrid.BiomeAt(world.Position)==OverworldBiome.Grassland;
            viewCamera.backgroundColor=outdoor && grassland?GrasslandSky:originalBackground;
        }
        var common=Common.Instance;
        bool ready=game!=null?game.IsReady:town!=null?town.IsReady:world!=null&&world.IsReady;
        bool running=ready && Application.isFocused && Time.timeScale>0 && common!=null && !common.Travel.IsTransitioning && !common.GlobalSettings.IsOpen;
        if(outdoor && running && advancedFrame!=Time.frameCount)
        {
            advancedFrame=Time.frameCount;
            Phase=Mathf.Repeat(Phase+Time.deltaTime/Mathf.Max(1,GamePresentationProfile.Current?.SunPeriodSeconds??1200),1);
        }
        var direction=SunDirection(outdoor,Phase);
        if(silhouettes!=null)silhouettes.Direction=direction;
        if(sun!=null)sun.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
    }
    private void OnDestroy()
    {
        if(viewCamera!=null)viewCamera.backgroundColor=originalBackground;
    }
}
