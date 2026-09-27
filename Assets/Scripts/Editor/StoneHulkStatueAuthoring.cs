#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StoneHulkStatueAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Art/Build Stone Hulk Statue")]
    public static void Build()
    {
        const string folder="Assets/Resources/DungeonProps";
        if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources","DungeonProps");
        var enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_BlackKnight.prefab"));
        var root=new GameObject("StoneHulkStatue");
        var owned=new List<Mesh>();
        try
        {
            enemy.Animator.Rebind(); enemy.Animator.Update(0);
            var idle=enemy.Animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.Contains("IdleNormal"));
            if(idle!=null) idle.SampleAnimation(enemy.Animator.gameObject,0);
            var parts=new List<CombineInstance>();var materials=new List<Material>();
            foreach(var renderer in enemy.Animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh(); renderer.BakeMesh(mesh);owned.Add(mesh);
                for(int i=0;i<mesh.subMeshCount;i++)
                {
                    parts.Add(new CombineInstance {mesh=mesh,subMeshIndex=i,transform=enemy.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix});
                    materials.Add(renderer.sharedMaterials[Mathf.Min(i,renderer.sharedMaterials.Length-1)]);
                }
            }
            var combined=new Mesh {name="Stone Hulk statue"};combined.CombineMeshes(parts.ToArray(),false,true);
            var bounds=combined.bounds;
            float scale=Mathf.Min(1.7f/Mathf.Max(bounds.size.x,bounds.size.y),2.5f/Mathf.Max(.01f,bounds.size.z));
            var offset=new Vector3(bounds.center.x,bounds.center.y,bounds.max.z);
            combined.vertices=combined.vertices.Select(v=>(v-offset)*scale).ToArray();combined.RecalculateBounds();
            string meshPath=folder+"/StoneHulkStatue.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existing==null) AssetDatabase.CreateAsset(combined,meshPath);
            else {EditorUtility.CopySerialized(combined,existing);Object.DestroyImmediate(combined);combined=existing;}
            root.AddComponent<MeshFilter>().sharedMesh=combined;
            root.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,folder+"/StoneHulkStatue.prefab");
            AssetDatabase.SaveAssets();
        }
        finally {foreach(var mesh in owned) Object.DestroyImmediate(mesh); Object.DestroyImmediate(enemy.gameObject);Object.DestroyImmediate(root);}
    }
}
#endif
