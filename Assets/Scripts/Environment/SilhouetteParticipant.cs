using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum SilhouetteRole { None, Caster, Receiver }

/// <summary>Explicit roles survive batching; visibility remains owned by the source renderer.</summary>
public sealed class SilhouetteParticipant : MonoBehaviour
{
    internal static readonly HashSet<SilhouetteParticipant> Active = new();
    public SilhouetteRole Role = SilhouetteRole.Caster;
    public bool Dynamic;
    internal Renderer Source;
    internal int Submeshes;
    private void OnEnable()
    {
        Source=GetComponent<Renderer>();
        if(Source==null)return;
        var mesh=Source is SkinnedMeshRenderer skinned?skinned.sharedMesh:GetComponent<MeshFilter>()?.sharedMesh;
        Submeshes=mesh!=null?mesh.subMeshCount:0;
        Source.shadowCastingMode=ShadowCastingMode.Off;Source.receiveShadows=false;
        Active.Add(this);
    }
    private void OnDisable()=>Active.Remove(this);
    public static void Register(Transform root,SilhouetteRole role,bool dynamic=false)
    {
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if(renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)continue;
            if(renderer.GetComponentInParent<Canvas>()!=null || renderer.GetComponentInParent<ParticleSystem>()!=null)continue;
            var participant=renderer.GetComponent<SilhouetteParticipant>()??renderer.gameObject.AddComponent<SilhouetteParticipant>();
            participant.Role=role;participant.Dynamic=dynamic;
        }
    }
}
