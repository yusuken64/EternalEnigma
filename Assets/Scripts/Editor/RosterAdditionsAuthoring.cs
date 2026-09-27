#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RosterAdditionsAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Enemies/Author Metal Slime and Goopi")]
    public static void Build()
    {
        const string enemies="Assets/Prefabs/Dungeon/Enemies/";
        var source=AssetDatabase.LoadAssetAtPath<Enemy>(enemies+"Enemy_MushroomSmile.prefab");
        var slime=PrefabUtility.LoadPrefabContents(enemies+"Enemy_Slime.prefab");
        try
        {
            slime.name="Enemy_MetalSlime";
            var enemy=slime.GetComponent<Enemy>();
            enemy.StartingStats=JsonUtility.FromJson<StartingStats>(JsonUtility.ToJson(source.StartingStats));
            enemy.StartingStats.ActionsPerTurnMax=2;
            enemy.StartingStats.AttacksPerTurnMax=1;
            var behavior=slime.GetComponent<EnemyBehavior>() ?? slime.AddComponent<EnemyBehavior>();
            behavior.AlwaysFlees=true;
            const string materialPath="Assets/Resources/DungeonProps/MetalSlime.mat";
            if(!AssetDatabase.IsValidFolder("Assets/Resources/DungeonProps")) AssetDatabase.CreateFolder("Assets/Resources","DungeonProps");
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                material.name="Metal Slime"; material.color=new Color(.72f,.78f,.84f);
                material.SetFloat("_Metallic",.7f);material.SetFloat("_Smoothness",.75f);
                AssetDatabase.CreateAsset(material,materialPath);
            }
            material.shader=Shader.Find("EternalEnigma/MetalSlime");
            material.SetTexture("_BaseMap",enemy.Animator.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.mainTexture);
            EditorUtility.SetDirty(material);
            foreach(var renderer in enemy.Animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials=renderer.sharedMaterials;
                // Desaturate the original atlas in the shader so facial details survive recoloring.
                for(int i=0;i<materials.Length;i++) materials[i]=material;
                renderer.sharedMaterials=materials;
            }
            PrefabUtility.SaveAsPrefabAsset(slime,enemies+"Enemy_MetalSlime.prefab");
        }
        finally {PrefabUtility.UnloadPrefabContents(slime);}
        var goopi=PrefabUtility.LoadPrefabContents(enemies+"Enemy_Skeleton.prefab");
        try
        {
            var behavior=goopi.GetComponent<EnemyBehavior>() ?? goopi.AddComponent<EnemyBehavior>();
            behavior.RootsAdjacentTarget=true;behavior.Stationary=true;
            PrefabUtility.SaveAsPrefabAsset(goopi,enemies+"Enemy_Skeleton.prefab");
        }
        finally {PrefabUtility.UnloadPrefabContents(goopi);}
        AssetDatabase.SaveAssets();
    }
}
#endif
