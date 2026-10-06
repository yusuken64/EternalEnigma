using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OverworldPresentationAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Author Overworld Presentation")]
    public static void Build()
    {
        OverworldSixTerrainAuthoring.Author();
        PaintedEnvironmentAuthoring.UpdateGrasslandGround();
        const string path="Assets/Overworld/GateMesh.asset";
        var source=EnvironmentKit.Load().Mesh("Gate");
        var mesh=Object.Instantiate(source);mesh.name="Overworld gate";
        // Keep each triangle inside a single atlas swatch. The legacy gate's
        // interpolated palette UVs crossed foliage/roof cells and looked checkered.
        var triangles=source.triangles;var old=source.vertices;var oldNormals=source.normals;
        var vertices=new Vector3[triangles.Length];var normals=new Vector3[triangles.Length];var uv=new Vector2[triangles.Length];
        for(int i=0;i<triangles.Length;i+=3)
        {
            var center=(old[triangles[i]]+old[triangles[i+1]]+old[triangles[i+2]])/3;
            bool pillar=Mathf.Abs(center.x)>.32f;
            int cellX=pillar?3:2,cellY=pillar?0:2;
            for(int j=0;j<3;j++)
            {
                int k=i+j;var v=vertices[k]=old[triangles[k]];var n=normals[k]=oldNormals[triangles[k]];
                var p=Mathf.Abs(n.x)>.7f?new Vector2(v.y+.5f,-v.z):new Vector2(v.x+.55f,-v.z);
                uv[k]=new Vector2((cellX+.08f+Mathf.Clamp01(p.x)*.84f)/4,(cellY+.08f+Mathf.Clamp01(p.y)*.84f)/4);
                triangles[k]=k;
            }
        }
        mesh.Clear();mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
        const string prefabPath="Assets/Overworld/Gate.prefab";
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var model=root.GetComponentInChildren<BiomeModel>(true);
            model.GetComponent<MeshFilter>().sharedMesh=saved;
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            model.transform.localScale=Vector3.one*(1/source.bounds.size.x);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject!=model.gameObject))renderer.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("Authored continuous mountain mapping, grass without dirt, and correctly mapped gate.");
    }
}
