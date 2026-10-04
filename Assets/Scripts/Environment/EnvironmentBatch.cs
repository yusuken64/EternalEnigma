using System.Collections.Generic;
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
    public void Finish()
    {
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
            var obj = new GameObject($"Cosmetic {pair.Key.Item1},{pair.Key.Item2} {pair.Key.Item3.name}");
            obj.transform.SetParent(parent, false); obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key.Item3;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            obj.AddComponent<SilhouetteParticipant>().Role = pair.Key.Item4;
        }
    }
}
