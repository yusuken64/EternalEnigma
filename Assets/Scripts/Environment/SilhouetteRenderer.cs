using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One directional geometry mask, composited once. Built-in pipeline, no native shadows.</summary>
[RequireComponent(typeof(Camera))]
public sealed class SilhouetteRenderer : MonoBehaviour
{
    private Camera view,projection;
    private Material material;
    private RenderTexture depth;
    private CommandBuffer commands;
    internal RenderTexture InspectionMask;
    private readonly Plane[] planes=new Plane[6];
    private static readonly int Mask=Shader.PropertyToID("_SilhouetteMask");
    private static readonly int MaskTexture=Shader.PropertyToID("_SilhouetteMaskTexture");
    public Vector3 Direction=new Vector3(.45f,.4f,.8f).normalized;
    private void OnEnable()
    {
        view=GetComponent<Camera>();
        var shader=Resources.Load<Shader>("Presentation/Silhouette");
        if(shader==null || !shader.isSupported){enabled=false;return;}
        material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        depth=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
            {name="Silhouette depth",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};depth.Create();
        var go=new GameObject("Silhouette projection",typeof(Camera)){hideFlags=HideFlags.HideAndDontSave};
        go.transform.SetParent(transform,false);projection=go.GetComponent<Camera>();projection.enabled=false;
        projection.orthographic=true;projection.aspect=1;projection.nearClipPlane=.1f;projection.farClipPlane=240;
        commands=new CommandBuffer{name="Ground silhouettes"};view.AddCommandBuffer(CameraEvent.AfterForwardOpaque,commands);
    }
    private void OnPreCull()
    {
        if(commands==null)return;
        commands.Clear();
        GeometryUtility.CalculateFrustumPlanes(view,planes);
        float extent=view.orthographic?view.orthographicSize*Mathf.Max(1,view.aspect)+16:60;
        var ray=view.ViewportPointToRay(new Vector3(.5f,.5f));
        var center=ray.origin;
        if(Mathf.Abs(ray.direction.z)>.01f)center+=ray.direction*(-ray.origin.z/ray.direction.z);
        projection.orthographicSize=extent;
        projection.transform.position=center-Direction*110;
        projection.transform.rotation=Quaternion.LookRotation(Direction,Vector3.up);
        material.SetMatrix("_SilhouetteVP",GL.GetGPUProjectionMatrix(projection.projectionMatrix,true)*projection.worldToCameraMatrix);
        material.SetVector("_SilhouetteOrigin",projection.transform.position);
        material.SetVector("_SilhouetteDirection",Direction);
        material.SetTexture("_SilhouetteDepth",depth);
        material.SetColor("_SilhouetteColor",GamePresentationProfile.Current!=null?GamePresentationProfile.Current.Silhouette:new Color(.12f,.17f,.21f,.24f));
        var fog=FogOverlay.Instance;
        material.SetTexture("_SilhouetteFog",fog!=null?fog.VisibilityTexture:Texture2D.whiteTexture);
        material.SetVector("_SilhouetteFogBounds",fog!=null?fog.ShadowBounds:Vector4.zero);
        commands.SetRenderTarget(depth);commands.ClearRenderTarget(true,true,Color.white);
        foreach(var participant in SilhouetteParticipant.Active)
        {
            if(!Eligible(participant,SilhouetteRole.Caster))continue;
            var bounds=participant.Source.bounds;
            // Projected extents, not just the owner, decide camera inclusion.
            float distance=Mathf.Max(0,-bounds.min.z)+2;
            bounds.Expand(new Vector3(distance*2,distance*2,0));
            if(!GeometryUtility.TestPlanesAABB(planes,bounds))continue;
            commands.SetGlobalFloat("_SilhouetteDynamic",participant.Dynamic?1:0);
            for(int i=0;i<participant.Submeshes;i++)commands.DrawRenderer(participant.Source,material,i,0);
        }
        int samples=view.targetTexture!=null?view.targetTexture.antiAliasing:
            view.allowMSAA && view.actualRenderingPath!=RenderingPath.DeferredShading?Mathf.Max(1,QualitySettings.antiAliasing):1;
        commands.GetTemporaryRT(Mask,view.pixelWidth,view.pixelHeight,0,FilterMode.Point,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear,samples);
        commands.SetRenderTarget(new RenderTargetIdentifier(Mask),BuiltinRenderTextureType.CameraTarget);
        commands.ClearRenderTarget(false,true,Color.clear);
        foreach(var participant in SilhouetteParticipant.Active)
        {
            if(!Eligible(participant,SilhouetteRole.Receiver) || !GeometryUtility.TestPlanesAABB(planes,participant.Source.bounds))continue;
            for(int i=0;i<participant.Submeshes;i++)commands.DrawRenderer(participant.Source,material,i,1);
        }
        if(InspectionMask!=null)commands.Blit(Mask,InspectionMask);
        commands.SetGlobalTexture(MaskTexture,new RenderTargetIdentifier(Mask));
        commands.Blit(Mask,BuiltinRenderTextureType.CameraTarget,material,2);
        commands.ReleaseTemporaryRT(Mask);
    }
    private static bool Eligible(SilhouetteParticipant p,SilhouetteRole role)=>p!=null && p.Role==role && p.Source!=null &&
        p.Source.enabled && !p.Source.forceRenderingOff && p.Source.gameObject.activeInHierarchy;
    private void OnDisable()
    {
        if(commands!=null){if(view!=null)view.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque,commands);commands.Release();commands=null;}
        if(depth!=null){depth.Release();Destroy(depth);depth=null;}
        if(material!=null)Destroy(material);
        if(projection!=null)Destroy(projection.gameObject);
    }
}
