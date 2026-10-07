using System;
using System.IO;
using System.Linq;
using TWC;
using TWC.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.U2D;
using Object=UnityEngine.Object;

public static class DioramaAuditAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Apply Art Audit Fixes")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        const string materialFolder="Assets/Art/Diorama/Characters";Directory.CreateDirectory(materialFolder);AssetDatabase.Refresh();
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Dungeon/Enemies"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root=PrefabUtility.LoadPrefabContents(path);bool changed=false;
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        var source=materials[i];if(source==null||!AssetDatabase.GetAssetPath(source).Contains("RPGMonsterBundlePolyart"))continue;
                        string own=materialFolder+"/"+source.name+"_Lit.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(own);
                        if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,own);}
                        material.mainTexture=source.mainTexture;material.color=source.HasProperty("_Color")?source.color:Color.white;
                        material.SetFloat("_Glossiness",.12f);EditorUtility.SetDirty(material);materials[i]=material;changed=true;
                    }
                    renderer.sharedMaterials=materials;
                }
                var animator=root.GetComponent<Enemy>()?.Animator;
                var controller=animator?.runtimeAnimatorController;
                var loopingHits=controller==null?Array.Empty<AnimationClip>():controller.animationClips.Distinct().Where(c=>c.name.IndexOf("GetHit",StringComparison.OrdinalIgnoreCase)>=0&&AnimationUtility.GetAnimationClipSettings(c).loopTime).ToArray();
                if(loopingHits.Length>0)
                {
                    var replacement=new AnimatorOverrideController(controller);
                    foreach(var clip in loopingHits)
                    {
                        string clipPath=materialFolder+"/"+root.name+"_"+clip.name+".anim";
                        var own=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if(own==null){own=Object.Instantiate(clip);AssetDatabase.CreateAsset(own,clipPath);}
                        var settings=AnimationUtility.GetAnimationClipSettings(own);settings.loopTime=false;settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(own,settings);
                        replacement[clip.name]=own;EditorUtility.SetDirty(own);
                    }
                    AssetDatabase.CreateAsset(replacement,materialFolder+"/"+root.name+"_HitOverrides.overrideController");animator.runtimeAnimatorController=replacement;changed=true;
                }
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string allyPath in new[]{"Assets/Prefabs/Dungeon/Ally.prefab","Assets/Prefabs/Town/Allies/TownAlly.prefab"})
        {
        var ally=PrefabUtility.LoadPrefabContents(allyPath);
        try
        {
            var old=ally.transform.Find("GameObject/RPGHeroHP");
            if(old!=null&&old.GetComponentsInChildren<Renderer>(true).Length>0)
            {
                var anchor=new GameObject("RPGHeroHP").transform;anchor.SetParent(old.parent,false);
                anchor.localPosition=old.localPosition;anchor.localRotation=old.localRotation;anchor.localScale=old.localScale;
                Object.DestroyImmediate(old.gameObject);PrefabUtility.SaveAsPrefabAsset(ally,allyPath);
            }
        }
        finally {PrefabUtility.UnloadPrefabContents(ally);}
        }
        var items=DioramaItemCatalog.Load();
        const string dungeonPath="Assets/Prefabs/Dungeon/TileWorldDungeon.prefab";var dungeon=PrefabUtility.LoadPrefabContents(dungeonPath);
        try{dungeon.GetComponent<TileWorldDungeon>().SmallKeyPrefab=items.GetProp("key");PrefabUtility.SaveAsPrefabAsset(dungeon,dungeonPath);}
        finally{PrefabUtility.UnloadPrefabContents(dungeon);}
        const string profilePath="Assets/Resources/UI/GamePresentationProfile.asset";
        var profile=AssetDatabase.LoadAssetAtPath<GamePresentationProfile>(profilePath);
        foreach(var orphan in AssetDatabase.LoadAllAssetsAtPath(profilePath).OfType<Sprite>().Where(s=>s!=profile.BagIcon&&s!=profile.CoinIcon&&!profile.ItemIcons.Contains(s)).ToArray())
            Object.DestroyImmediate(orphan,true);
        foreach(var cls in Resources.LoadAll<ClassDefinition>(""))
        {if(cls.Icon==null)cls.Icon=Resources.Load<Sprite>("UI/"+cls.DisplayName);EditorUtility.SetDirty(cls);}
        var theme=GameUITheme.Current;
        if(theme.HeadingFont.fallbackFontAssetTable==null)theme.HeadingFont.fallbackFontAssetTable=new();
        if(!theme.HeadingFont.fallbackFontAssetTable.Contains(TMP_Settings.defaultFontAsset))theme.HeadingFont.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
        EditorUtility.SetDirty(theme.HeadingFont);
        foreach(string path in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/MainMenu"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material.HasProperty("_MainTex")&&material.mainTexture!=null&&AssetDatabase.GetAssetPath(material.mainTexture).StartsWith("Assets/TileWorldCreator/Tiles/"))
            {material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PaintedEnvironment/Masonry.png");EditorUtility.SetDirty(material);}
        }
        // Legacy presets remain functional with our own four-piece assets.
        var dungeonThemes=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");var baseline=dungeonThemes.Themes.First();
        foreach(string path in new[]{"Assets/Prefabs/Dungeon/DungeonAsset.asset","Assets/Prefabs/Dungeon/DungeonThroneAsset.asset"})
        {
            var asset=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(path);
            foreach(var layer in asset.mapBuildLayers.OfType<InstantiateTiles>())
                foreach(var tile in layer.tiles??new())
                    if(tile.preset!=null&&AssetDatabase.GetAssetPath(tile.preset).StartsWith("Assets/TileWorldCreator/Tiles/"))
                        tile.preset=layer.layerName.ToLowerInvariant().Contains("floor")?baseline.Floor:layer.layerName.ToLowerInvariant().Contains("carpet")?baseline.Accent:baseline.RegularBoundary;
            EditorUtility.SetDirty(asset);
        }
        foreach(string folder in new[]{"Assets/Art/EnvironmentKit/Textures","Assets/Art/PaintedEnvironment","Assets/Art/Diorama/Ground"})
            foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                var settings=importer.GetPlatformTextureSettings("WebGL");settings.name="WebGL";settings.overridden=true;settings.maxTextureSize=1024;
                importer.SetPlatformTextureSettings(settings);importer.SaveAndReimport();
            }
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Common.unity");
            foreach(var cursor in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CursorManager>(true)))cursor.enabled=true;
            foreach(var dialog in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MessageDialog>(true)))
            {
                dialog.AuthorLayout();dialog.PortraitFrame.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Bamao/BamaoUIPack/Sprites/QUEST/avatar_bg_orange.png");
            }
            var trophy=UnifiedPresentationAuthoring.RenderIcon(items.GetProp("trophy"),"ResultTrophy",true,true);
            var chest=UnifiedPresentationAuthoring.RenderIcon(items.GetProp("treasure chest"),"ResultChest",true,true);
            foreach(var result in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameOverScreen>(true)))
            {result.AuthorLayout();result.VictoryIcon=trophy;result.DefeatIcon=chest;result.ResultIcon.sprite=trophy;}
            foreach(var text in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)))
                if(text.name.IndexOf("title",StringComparison.OrdinalIgnoreCase)>=0||text.name.IndexOf("heading",StringComparison.OrdinalIgnoreCase)>=0)text.font=theme.HeadingFont;
            EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/DungeonScene.unity");
            foreach(var result in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameOverScreen>(true)))
            {result.AuthorLayout();result.VictoryIcon=trophy;result.DefeatIcon=chest;result.ResultIcon.sprite=trophy;}
            EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var skull=AssetDatabase.LoadAssetAtPath<GameObject>(DioramaItemAuthoring.Atlas+"skull.prefab");
            foreach(var filter in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)))
                if(AssetDatabase.GetAssetPath(filter.sharedMesh).Contains("Adorable 3D Items/FBX/skull"))
                {
                    filter.sharedMesh=skull.GetComponentInChildren<MeshFilter>().sharedMesh;
                    filter.GetComponent<Renderer>().sharedMaterials=skull.GetComponentInChildren<Renderer>().sharedMaterials.Select(m=>DioramaItemAuthoring.Lit(m,Color.white)).ToArray();
                }
            foreach(var animator in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Animator>(true)))
                if(AssetDatabase.GetAssetPath(animator.avatar).Contains("Adorable 3D Items/FBX/skull"))animator.avatar=null;
            var skullMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/MainMenu/Scenery_skull.mat");
            if(skullMaterial!=null){skullMaterial.mainTexture=skull.GetComponentInChildren<Renderer>().sharedMaterial.mainTexture;EditorUtility.SetDirty(skullMaterial);}
            EditorSceneManager.SaveScene(scene);
        }
        finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        // Only used Bamao sprites, resource UI and NPC portraits enter this atlas.
        var uiSprites=AssetDatabase.FindAssets("t:Sprite",new[]{"Assets/Resources/UI"}).Select(AssetDatabase.GUIDToAssetPath)
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Sprite>().ToList();
        var dependencies=AssetDatabase.GetDependencies(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),true);
        uiSprites.AddRange(dependencies.Where(p=>p.StartsWith("Assets/Bamao/")||p.Contains("Portrait"))
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Sprite>());
        uiSprites.AddRange(AssetDatabase.FindAssets("t:TownNpcDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TownNpcDefinition>).Where(n=>n.Portrait!=null).Select(n=>n.Portrait));
        const string atlasPath="Assets/Art/Diorama/GameUI.spriteatlas";var atlas=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if(atlas==null){atlas=new SpriteAtlas();AssetDatabase.CreateAsset(atlas,atlasPath);}
        atlas.Remove(atlas.GetPackables());atlas.Add(uiSprites.Distinct().Cast<Object>().ToArray());
        atlas.SetPackingSettings(new SpriteAtlasPackingSettings{enableRotation=false,enableTightPacking=false,padding=4});
        atlas.SetTextureSettings(new SpriteAtlasTextureSettings{generateMipMaps=false,readable=false,sRGB=true,filterMode=FilterMode.Bilinear});
        EditorUtility.SetDirty(atlas);
        AutoplayPanelAuthoring.Build();
        AssetDatabase.SaveAssets();Debug.Log("Diorama audit fixes saved: lit enemies, empty Ally anchor, portraits, class icons, fonts, own legacy presets, WebGL texture limits.");
    }
}

// Build scenes contain editor previews. Runtime generation recreates these worlds.
public sealed class DioramaPreviewBuildStrip : IProcessSceneWithReport
{
    public int callbackOrder=>0;
    public void OnProcessScene(Scene scene,BuildReport report)
    {
        if(report==null||scene.name is not ("Town" or "DungeonScene"))return;
        foreach(var creator in scene.GetRootGameObjects().Where(r=>r!=null).SelectMany(r=>r.GetComponentsInChildren<TileWorldCreator>(true)).ToArray())
        {
            var preview=creator.worldObject;if(preview==null)continue;
            Object.DestroyImmediate(preview);creator.worldObject=null;
        }
    }
}
