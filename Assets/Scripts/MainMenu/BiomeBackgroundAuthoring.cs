using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BiomeBackgroundAuthoring : MonoBehaviour
{
    [Serializable] public sealed class Set { public OverworldBiome Biome; public GameObject Root; public Camera Camera; }
    public Set[] Sets = Array.Empty<Set>();
    public int Selected;
    public BiomeBackgroundCatalog Catalog;
#if UNITY_EDITOR
    public void Preview(int index)
    {
        Selected = index;
        for(int i=0;i<Sets.Length;i++) Sets[i].Root.SetActive(i==index);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
    public void Bake(bool all)
    {
        if (Sets.Length != 8 || Sets.Any(s=>s.Root==null || s.Camera==null)) throw new InvalidOperationException("All eight editable biome sets require a root and camera.");
        var activation = Sets.Select(s=>s.Root.activeSelf).ToArray();
        var active = RenderTexture.active;
        var previousScene = SceneManager.GetActiveScene();
        var otherRoots = Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Where(s=>s.isLoaded).SelectMany(s=>s.GetRootGameObjects()).Where(r=>r!=transform.root.gameObject).ToArray();
        var otherActivation = otherRoots.Select(r=>r.activeSelf).ToArray();
        try
        {
            SceneManager.SetActiveScene(gameObject.scene);
            foreach(var root in otherRoots) root.SetActive(false);
            foreach(var set in all ? Sets : new[]{Sets[Selected]})
            {
                foreach(var item in Sets) item.Root.SetActive(item==set);
                var camera = set.Camera; var previous = camera.targetTexture; float aspect = camera.aspect;
                var render = RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32);
                Texture2D texture = null;
                try
                {
                    camera.targetTexture=render; camera.aspect=1920f/1080;
                    camera.Render(); RenderTexture.active=render;
                    texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
                    string path="Assets/Art/CampaignBackgrounds/"+set.Biome+".png";
                    File.WriteAllBytes(path,texture.EncodeToPNG()); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.mipmapEnabled=false; importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                    var entry=Catalog.Backgrounds.Single(e=>e.Biome==set.Biome); entry.Texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                finally { camera.targetTexture=previous; camera.aspect=aspect; RenderTexture.active=active; RenderTexture.ReleaseTemporary(render); if(texture!=null) DestroyImmediate(texture); }
            }
            EditorUtility.SetDirty(Catalog); AssetDatabase.SaveAssets();
        }
        finally { for(int i=0;i<Sets.Length;i++) Sets[i].Root.SetActive(activation[i]); for(int i=0;i<otherRoots.Length;i++) otherRoots[i].SetActive(otherActivation[i]); SceneManager.SetActiveScene(previousScene); RenderTexture.active=active; }
    }
    [MenuItem("Tools/Eternal Enigma/Campaign Backgrounds/Open Authoring Scene")]
    public static void Open() => EditorSceneManager.OpenScene("Assets/Scenes/Editor/BiomeBackgrounds.unity");
    [MenuItem("Tools/Eternal Enigma/Campaign Backgrounds/Bake Selected Biome")]
    public static void BakeSelected() => FindFirstObjectByType<BiomeBackgroundAuthoring>().Bake(false);
    [MenuItem("Tools/Eternal Enigma/Campaign Backgrounds/Bake All Biomes")]
    public static void BakeAll() => FindFirstObjectByType<BiomeBackgroundAuthoring>().Bake(true);
    [MenuItem("Tools/Eternal Enigma/Campaign Backgrounds/Create Initial Artwork")]
    public static void CreateInitial()
    {
        const string scenePath="Assets/Scenes/Editor/BiomeBackgrounds.unity";
        if(File.Exists(scenePath)) throw new InvalidOperationException("Authoring scene exists. Open and edit it; initial creation never replaces artwork.");
        Directory.CreateDirectory("Assets/Scenes/Editor"); Directory.CreateDirectory("Assets/Art/CampaignBackgrounds");
        // Recover only an unsaved scene left by an interrupted initial creation.
        for(int i=SceneManager.sceneCount-1;i>=0;i--)
        {
            var candidate=SceneManager.GetSceneAt(i);
            var roots=candidate.GetRootGameObjects();
            if(string.IsNullOrEmpty(candidate.path) && roots.Length==1 && roots[0].name=="Biome Background Authoring")
                EditorSceneManager.CloseScene(candidate,true);
        }
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var author=new GameObject("Biome Background Authoring").AddComponent<BiomeBackgroundAuthoring>();
        var kit=EnvironmentKit.Load();
        var biomes=(OverworldBiome[])Enum.GetValues(typeof(OverworldBiome));
        author.Catalog=ScriptableObject.CreateInstance<BiomeBackgroundCatalog>();
        author.Catalog.Backgrounds=biomes.Select(b=>new BiomeBackgroundCatalog.Entry { Biome=b }).ToArray();
        AssetDatabase.CreateAsset(author.Catalog,"Assets/Resources/CampaignBackgrounds.asset");
        author.Sets=biomes.Select(b=>new Set { Biome=b }).ToArray();
        foreach(var set in author.Sets)
        {
            var b=set.Biome; set.Root=new GameObject(b.ToString()); set.Root.transform.SetParent(author.transform);
            var cameraObject=new GameObject(b+" Camera"); cameraObject.transform.SetParent(set.Root.transform);
            var camera=set.Camera=cameraObject.AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true; camera.orthographicSize=9;
            camera.transform.position=new Vector3(0,-12,-20); camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,5,0)-camera.transform.position,Vector3.up);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.27f,.36f,.43f); camera.farClipPlane=150;
            var light=new GameObject(b+" Sun").AddComponent<Light>(); light.transform.SetParent(set.Root.transform); light.type=LightType.Directional; light.intensity=1.2f; light.transform.rotation=Quaternion.Euler(25,-30,0);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Quad); ground.name="Landscape floor"; ground.transform.SetParent(set.Root.transform); ground.transform.localPosition=new Vector3(0,5,1); ground.transform.localScale=new Vector3(60,50,1); ground.GetComponent<Renderer>().sharedMaterial=kit.Ground(b);
            string tree=b==OverworldBiome.Desert ? "Cactus" : b==OverworldBiome.Volcanic ? "CharredTree" : b==OverworldBiome.Marsh ? "Mangrove" : b==OverworldBiome.Water ? "Palm" : "Pine";
            for(int i=0;i<13;i++)
            {
                float x=-16+i*2.6f, y=9+Mathf.Sin(i*2.1f)*2;
                kit.Create(b==OverworldBiome.Volcanic ? "Basalt" : "Mountain",b,set.Root.transform,new Vector3(x,y,0),2.8f+(i%3));
            }
            int count=b==OverworldBiome.Forest ? 25 : 12;
            for(int i=0;i<count;i++)
            {
                float x=(i%2==0 ? -1 : 1)*(7+(i%5)*1.8f), y=-3+(i/2)*1.2f;
                kit.Create(tree,b,set.Root.transform,new Vector3(x,y,0),1.6f+(i%3)*.35f);
            }
            for(int i=0;i<8;i++) kit.Create(b==OverworldBiome.Volcanic ? "Basalt" : "Rock",b,set.Root.transform,new Vector3(-10+i*3,4+(i%2),0),1.2f);
            set.Root.SetActive(false);
        }
        author.Preview(0);
        // A separate neutral plate is intentionally independent of campaign data and biome mapping.
        var neutral=new Texture2D(1920,1080,TextureFormat.RGB24,false); var pixels=new Color[1920*1080];
        for(int y=0;y<1080;y++) for(int x=0;x<1920;x++) pixels[y*1920+x]=Color.Lerp(new Color(.07f,.09f,.12f),new Color(.24f,.29f,.32f),y/1080f);
        neutral.SetPixels(pixels);neutral.Apply();File.WriteAllBytes("Assets/Art/CampaignBackgrounds/Neutral.png",neutral.EncodeToPNG());DestroyImmediate(neutral);AssetDatabase.Refresh();
        author.Catalog.Neutral=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/CampaignBackgrounds/Neutral.png");
        author.Bake(true); author.Catalog.Validate();
        EditorSceneManager.SaveScene(scene,scenePath);
        SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true);
        // Existing sleeping animation, exposed as a runtime resource without duplicating its clips.
        var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/RPGTinyHeroWavePolyart/Animator/NoWeaponStanceExtraAnim.controller");
        if(!File.Exists("Assets/Resources/HomeSleeping.overrideController")) AssetDatabase.CreateAsset(new AnimatorOverrideController(controller),"Assets/Resources/HomeSleeping.overrideController");
        AssetDatabase.SaveAssets();
    }
#endif
}
#if UNITY_EDITOR
[CustomEditor(typeof(BiomeBackgroundAuthoring))]
public sealed class BiomeBackgroundAuthoringInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var author=(BiomeBackgroundAuthoring)target;
        int selected=EditorGUILayout.Popup("Preview biome",author.Selected,author.Sets.Select(s=>s.Biome.ToString()).ToArray());
        if(selected!=author.Selected) author.Preview(selected);
        if(GUILayout.Button("Bake Selected Biome")) author.Bake(false);
        if(GUILayout.Button("Bake All Biomes")) author.Bake(true);
    }
}

#endif
