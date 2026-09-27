using System;
using System.Collections.Generic;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Eight triangles outside the playable grid; water and noise are separate TWC layers.</summary>
[Serializable, ActionName(Name = "Surrounding ocean")]
public sealed class OverworldOceanLayer : TWCBuildLayer
{
    public Material Water;
    public bool NoiseOverlay;
    public int Padding = 128;
    public override TWCBuildLayer Clone() => new OverworldOceanLayer { guid=guid, assignedGenerationLayerGuid=assignedGenerationLayerGuid,
        layerName=layerName,active=active,Water=Water,NoiseOverlay=NoiseOverlay,Padding=Padding };
    public override void Execute(TileWorldCreator creator,bool force)
    {
        try
        {
            var root=creator.AddLayerObject(layerName,guid);root.transform.SetParent(creator.worldObject.transform,false);root.transform.localRotation=Quaternion.identity;
            foreach(var child in root.transform.Cast<Transform>().ToArray()) Release(child.gameObject);
            foreach(var old in root.GetComponents<EnvironmentMeshOwner>()) Release(old);
            var owner=root.AddComponent<EnvironmentMeshOwner>();
            float size=creator.twcAsset.cellSize,w=creator.twcAsset.mapWidth*size,h=creator.twcAsset.mapHeight*size,p=Math.Max(1,Padding)*size;
            Surface(NoiseOverlay?"Ocean noise":"Ocean",Water,p,NoiseOverlay?.055f:.065f);
            void Surface(string name,Material material,float border,float depth)
            {
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
                Quad(-border,-border,w+border,0);Quad(-border,h,w+border,h+border);Quad(-border,0,0,h);Quad(w,0,w+border,h);
                var mesh=new Mesh {name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                owner.Meshes.Add(mesh);owner.TriangleCount+=8;
                var obj=new GameObject(name);obj.transform.SetParent(root.transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                if(NoiseOverlay) {var properties=new MaterialPropertyBlock();properties.SetVector("_MapSize",new Vector4(w,h,0,0));renderer.SetPropertyBlock(properties);}
                void Quad(float x0,float y0,float x1,float y1)
                {
                    int start=vertices.Count;
                    vertices.AddRange(new[]{new Vector3(x0,y0,depth),new Vector3(x1,y0,depth),new Vector3(x1,y1,depth),new Vector3(x0,y1,depth)});
                    uv.AddRange(vertices.Skip(start).Select(v=>new Vector2(v.x/8,v.y/8)));
                    // Front faces point toward the game's negative-Z camera.
                    indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
                }
            }
        }
        finally {creator.executedBuildLayersCount+=1;}
    }
    private static void Release(UnityEngine.Object o)
    {
        if(o is GameObject go) go.SetActive(false);
        if(Application.isPlaying) UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);
    }
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)
    {
        layerName=EditorGUILayout.TextField("Layer name",layerName);
        Padding=EditorGUILayout.IntField("Ocean margin (cells)",Padding);
        Water=(Material)EditorGUILayout.ObjectField("Water",Water,typeof(Material),false);
        NoiseOverlay=EditorGUILayout.Toggle("Noise overlay",NoiseOverlay);
    }
#endif
}
