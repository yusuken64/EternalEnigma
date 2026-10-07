using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

public sealed class EnvironmentBatch
{
    private readonly Transform parent;
    private readonly EnvironmentMeshOwner owner;
    public EnvironmentMeshOwner Owner => owner;
    private readonly Dictionary<(int, int, Material, SilhouetteRole), List<CombineInstance>> groups = new();
    private readonly Dictionary<(Material,int),Material> variants = new();
    public EnvironmentBatch(Transform parent)
    {
        this.parent = parent;
        owner = parent.gameObject.AddComponent<EnvironmentMeshOwner>();
    }
    public void AddPrefab(GameObject prefab,Vector3 position,Vector3 scale,Quaternion rotation,int lod=0,Func<Material,Material> materialMap=null)
    {
        var groupsInPrefab=prefab.GetComponentsInChildren<LODGroup>(true);
        var lodRenderers=new HashSet<Renderer>(groupsInPrefab.SelectMany(g=>g.GetLODs()).SelectMany(l=>l.renderers));
        var chosen=new HashSet<Renderer>(groupsInPrefab.SelectMany(g=>g.GetLODs()[Mathf.Clamp(lod,0,g.lodCount-1)].renderers));
        var root=Matrix4x4.TRS(position,rotation,scale)*prefab.transform.worldToLocalMatrix;
        foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null)continue;
            if(lodRenderers.Contains(renderer)&&!chosen.Contains(renderer))continue;
            var mesh=filter.sharedMesh;
            var materials=renderer.sharedMaterials;
            var keyPosition=position;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var material=materials[Mathf.Min(sub,materials.Length-1)];if(materialMap!=null)material=materialMap(material);
                if(material==null)continue;
                var key=(Mathf.FloorToInt(keyPosition.x/64),Mathf.FloorToInt(keyPosition.y/64),material,SilhouetteRole.Caster);
                if(!groups.TryGetValue(key,out var list))groups[key]=list=new List<CombineInstance>();
                list.Add(new CombineInstance {mesh=mesh,subMeshIndex=sub,transform=root*filter.transform.localToWorldMatrix});
                owner.TriangleCount+=(int)mesh.GetIndexCount(sub)/3;
            }
        }
        owner.PropCount++;
    }
    public void Add(Mesh mesh, Material material, Vector3 position, Vector3 scale, float rotation = 0, SilhouetteRole? role = null)
        => Add(mesh,material,position,scale,Quaternion.Euler(0,0,rotation),role);
    public void Add(Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation, SilhouetteRole? role = null)
    {
        var resolved = role ?? (mesh.bounds.size.z * scale.z < .12f ? SilhouetteRole.Receiver : SilhouetteRole.Caster);
        if(resolved==SilhouetteRole.Receiver && material.HasProperty("_MainTex") && material.GetTag("EnvironmentProjection",false,"")=="Planar")
        {
            uint hash=DungeonPresentation.Hash(15401,Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.y));
            int variant=(int)(hash%3);
            if(!variants.TryGetValue((material,variant),out var surface))
            {
                surface=new Material(material){name=material.name+" surface "+variant,hideFlags=HideFlags.DontSave};
                // Keep the painted road/floor pattern continuous across module joins.
                surface.mainTextureOffset=material.mainTextureOffset;
                if(surface.HasProperty("_Color"))surface.color=material.color*(variant==0?1:variant==1?.975f:1.015f);
                variants.Add((material,variant),surface);owner.Materials.Add(surface);
            }
            material=surface;
        }
        var key = (Mathf.FloorToInt(position.x / 64), Mathf.FloorToInt(position.y / 64), material, resolved);
        if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
        list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
        owner.PropCount++; owner.TriangleCount += (int)mesh.GetIndexCount(0) / 3;
    }
    public void Finish(bool smoothTerrain = false)
    {
        var terrainMeshes = new List<Mesh>();
        foreach (var pair in groups)
        {
            var mesh = new Mesh { name = "Environment chunk", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(pair.Value.ToArray(), true, true, false); mesh.RecalculateBounds();
            // Project after combining so adjacent/rotated modules share a continuous surface.
            // Atlas props retain authored UVs; only explicitly tagged painted surfaces opt in.
            string projection = pair.Key.Item3.GetTag("EnvironmentProjection", false, "");
            if (projection.Length != 0)
            {
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = vertices[i] / 2.5f; var n = normals[i];
                    uv[i] = projection == "Planar" || Mathf.Abs(n.z) >= Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.y))
                        ? new Vector2(p.x, p.y) : Mathf.Abs(n.x) > Mathf.Abs(n.y) ? new Vector2(p.y, -p.z) : new Vector2(p.x, -p.z);
                }
                mesh.uv = uv;
            }
            owner.Meshes.Add(mesh);
            if(smoothTerrain)terrainMeshes.Add(mesh);
            var obj = new GameObject($"Cosmetic {pair.Key.Item1},{pair.Key.Item2} {pair.Key.Item3.name}");
            obj.transform.SetParent(parent, false); obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key.Item3;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            obj.AddComponent<SilhouetteParticipant>().Role = pair.Key.Item4;
        }
        if(smoothTerrain)SmoothTerrain(terrainMeshes);
    }

    // Adjacent six-piece height-field quads must share lighting normals, including
    // chunk edges. Buildings and other deliberately hard-edged props never opt in.
    public static void SmoothTerrain(List<Mesh> meshes)
    {
        var sums=new Dictionary<Vector3Int,Vector3>();
        Vector3Int Key(Vector3 p)=>Vector3Int.RoundToInt(p*10000);
        foreach(var mesh in meshes)
        {
            var v=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var normal=Vector3.Cross(v[triangles[i+1]]-v[triangles[i]],v[triangles[i+2]]-v[triangles[i]]);
                for(int j=0;j<3;j++){var key=Key(v[triangles[i+j]]);sums.TryGetValue(key,out var sum);sums[key]=sum+normal;}
            }
        }
        foreach(var mesh in meshes)
        {
            var v=mesh.vertices;var normals=new Vector3[v.Length];
            for(int i=0;i<v.Length;i++)normals[i]=sums[Key(v[i])].normalized;
            mesh.normals=normals;
        }
    }
}
