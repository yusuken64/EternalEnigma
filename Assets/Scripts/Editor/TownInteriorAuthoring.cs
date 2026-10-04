using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Imports Blender's exact XY mesh/rig transport, preserving one shared material and asset GUIDs.</summary>
public static class TownInteriorAuthoring
{
    const string Folder="Assets/Resources/TownInteriors";
    [Serializable] public class Bone { public string name; public Vector3 position; public int parent; }
    [Serializable] public class Source { public string name;public bool animated,bird;public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles,weights;public Bone[] bones; }
    static T Save<T>(T value,string path) where T:Object
    {
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing==null){AssetDatabase.CreateAsset(value,path);return value;}
        if(existing is Mesh target && value is Mesh mesh)
        {
            // Mesh native skin buffers can survive CopySerialized when vertex counts match.
            // Rebuild the buffers explicitly while keeping the asset GUID and references.
            target.Clear();target.vertices=mesh.vertices;target.triangles=mesh.triangles;
            target.normals=mesh.normals;target.uv=mesh.uv;target.bindposes=mesh.bindposes;
            target.boneWeights=mesh.boneWeights;target.bounds=mesh.bounds;
        }
        else EditorUtility.CopySerialized(value,existing);
        Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;
    }
    [MenuItem("Tools/Eternal Enigma/Art/Import Town Interiors")]
    public static void Import()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/TownInteriors/Palette.png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var catalog=AssetDatabase.LoadAssetAtPath<TownInteriorCatalog>(Folder+"/Catalog.asset");
        if(catalog==null){catalog=ScriptableObject.CreateInstance<TownInteriorCatalog>();AssetDatabase.CreateAsset(catalog,Folder+"/Catalog.asset");}
        Material Material(string name,Color tint)
        {
            var material=new Material(Shader.Find("Standard")){name=name,mainTexture=texture,color=tint};material.SetFloat("_Glossiness",.05f);
            return Save(material,Folder+"/"+name+".mat");
        }
        catalog.CharacterMaterial=Material("CharacterPalette",Color.white);
        var tints=new[]{new Color(1,1,1),new Color(1,.92f,.80f),new Color(.90f,.98f,1),new Color(.93f,.94f,1),new Color(.91f,1,.89f),new Color(.93f,.98f,1),new Color(.93f,.96f,.86f),new Color(.91f,.86f,.86f)};
        catalog.BiomeMaterials=Enum.GetValues(typeof(OverworldBiome)).Cast<OverworldBiome>().Select(b=>Material("Furniture_"+b,tints[(int)b])).ToArray();
        var assets=new List<TownInteriorAsset>();
        foreach(string file in Directory.GetFiles("Assets/Art/TownInteriors/Models","*.json").OrderBy(p=>p,StringComparer.Ordinal))
        {
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(file));
            int budget=source.bird?1200:source.animated?6000:2000;
            if(source.triangles.Length/3>budget)throw new InvalidOperationException(source.name+" exceeds triangle budget");
            var mesh=new Mesh {name=source.name,vertices=source.vertices,triangles=source.triangles,uv=source.uv,normals=source.normals};mesh.RecalculateBounds();
            var root=new GameObject(source.name);
            if(source.animated)
            {
                var bones=new Transform[source.bones.Length];
                for(int i=0;i<bones.Length;i++)
                {
                    var b=source.bones[i];bones[i]=new GameObject(b.name).transform;bones[i].SetParent(b.parent<0?root.transform:bones[b.parent],false);
                    bones[i].position=b.position;
                }
                mesh.boneWeights=source.weights.Select(b=>new BoneWeight {boneIndex0=b,weight0=1}).ToArray();
                mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
                mesh=Save(mesh,Folder+"/"+source.name+".asset");
                var renderer=root.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=bones[0];
                renderer.sharedMaterial=catalog.CharacterMaterial;var bounds=mesh.bounds;bounds.Expand(.4f);renderer.localBounds=bounds;renderer.updateWhenOffscreen=false;
                var idle=Clip(source,bones,"Idle");var greeting=source.bird?null:Clip(source,bones,"Greeting");
                string path=Folder+"/"+source.name+".controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path)??AnimatorController.CreateAnimatorControllerAtPath(path);
                var stateMachine=controller.layers[0].stateMachine;
                foreach(var state in stateMachine.states)stateMachine.RemoveState(state.state);
                controller.parameters=Array.Empty<AnimatorControllerParameter>();
                var idleState=stateMachine.AddState("Idle");idleState.motion=idle;stateMachine.defaultState=idleState;
                if(greeting!=null)
                {
                    controller.AddParameter("Greeting",AnimatorControllerParameterType.Trigger);
                    var greet=stateMachine.AddState("Greeting");greet.motion=greeting;
                    var enter=idleState.AddTransition(greet);enter.hasExitTime=false;enter.duration=.12f;enter.AddCondition(AnimatorConditionMode.If,0,"Greeting");
                    var leave=greet.AddTransition(idleState);leave.hasExitTime=true;leave.exitTime=1;leave.duration=.15f;
                }
                var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                root.AddComponent<TownAmbientAnimation>();EditorUtility.SetDirty(controller);
            }
            else
            {
                mesh=Save(mesh,Folder+"/"+source.name+".asset");root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=catalog.BiomeMaterials[0];
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>()){renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
            root.AddComponent<SilhouetteParticipant>().Role=SilhouetteRole.Caster;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+source.name+".prefab");Object.DestroyImmediate(root);
            assets.Add(new TownInteriorAsset {Id=source.name,Mesh=mesh,Prefab=prefab});
            var fbx=(ModelImporter)AssetImporter.GetAtPath(Path.ChangeExtension(file,"fbx"));
            if(fbx!=null){fbx.addCollider=false;fbx.materialImportMode=ModelImporterMaterialImportMode.None;fbx.animationType=source.animated?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;fbx.importAnimation=source.animated;fbx.SaveAndReimport();}
        }
        catalog.Assets=assets.ToArray();
        catalog.Carpet=Preset("Carpet",catalog);catalog.Counter=Preset("Counter",catalog);
        var definitions=new List<TownNpcDefinition>();
        string[] names={"Cat","Dog","Bear","Sheep","Bunny","GuineaPig","Merchant"};
        string[] display={"Mochi the Traveler","Rowan the Shepherd","Bram the Innkeeper","Mallow the Baker","Poppy the Herbalist","Professor Pip","Vesper the Merchant"};
        string[] greetings={"Every road has a story. This town smells like a good one!","The flock is settled. Take a moment to enjoy the town.","Welcome in. A warm bed and a fresh start await.","Fresh bread makes a long journey kinder.","A little blossom can brighten the whole day.","Practice patiently; even the smallest spark can become magic.","A sturdy buckle, a sharp blade... what does your journey need?"};
        string[] portraits={"Select Character/Character_color ver.png","QUEST/avatar_shepherd_only.png","QUEST/avatar_bear_only.png","QUEST/avatar_sheep only.png","QUEST/avatar_bunny only.png","QUEST/avatar_guineapig_only.png","Shop/attachted/shop owner.png"};
        for(int i=0;i<names.Length;i++)
        {
            var d=ScriptableObject.CreateInstance<TownNpcDefinition>();d.Id=names[i];d.DisplayName=display[i];d.Greeting=greetings[i];d.Prefab=catalog.Get(names[i]).Prefab;
            d.Portrait=AssetDatabase.LoadAllAssetsAtPath("Assets/Bamao/BamaoUIPack/Sprites/"+portraits[i]).OfType<Sprite>().FirstOrDefault();
            d=Save(d,Folder+"/NPC_"+names[i]+".asset");definitions.Add(d);
        }
        catalog.Characters=definitions.ToArray();
        foreach(var definition in Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings"))
        {
            string id=definition.Id switch {"inn"=>"Bear","bakery"=>"Sheep","consumables"=>"Bunny","trainer"=>"GuineaPig","items" or "shop"=>"Merchant",_=>null};
            if(id==null)continue;
            definition.Npc=definitions.Single(d=>d.Id==id);
            definition.InteriorKind=id=="Bear"?TownInteriorKind.Inn:id=="GuineaPig"?TownInteriorKind.Trainer:TownInteriorKind.Shop;
            definition.ShopTheme=id=="Sheep"?TownShopTheme.Bakery:id=="Bunny"?TownShopTheme.Consumables:TownShopTheme.Equipment;
            // A service prefab retains the existing ShopVendor interaction contract.
            var vendor=new GameObject(id+" vendor");var behavior=vendor.AddComponent<ShopVendor>();
            var facing=new GameObject("Facing").transform;facing.SetParent(vendor.transform,false);behavior.VisualParent=facing.gameObject;
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(catalog.Get(id).Prefab);visual.transform.SetParent(facing,false);
            visual.transform.localRotation=Quaternion.Euler(0,0,180);visual.transform.localScale=Vector3.one*.65f;
            definition.VendorPrefab=PrefabUtility.SaveAsPrefabAsset(vendor,Folder+"/Vendor_"+id+".prefab").GetComponent<ShopVendor>();Object.DestroyImmediate(vendor);EditorUtility.SetDirty(definition);
        }
        var defaultTown=Resources.Load<TownConfiguration>("Towns/DefaultTown");defaultTown.FurnishInteriors=true;EditorUtility.SetDirty(defaultTown);
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log($"Imported {assets.Count} town models, seven NPCs, three birds, eight palettes, two native TWC presets.");
    }
    static AnimationClip Clip(Source source,Transform[] bones,string name)
    {
        var clip=new AnimationClip {name=name,frameRate=24};
        void Curve(string bone,string property,Func<float,float> value)
        {
            var transform=bones.Single(b=>b.name==bone);string path=AnimationUtility.CalculateTransformPath(transform,bones[0].parent);
            var keys=Enumerable.Range(0,97).Select(i=>new Keyframe(i/24f,value(i/96f*Mathf.PI*2))).ToArray();
            var curve=new AnimationCurve(keys);for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
            clip.SetCurve(path,typeof(Transform),property,curve);
        }
        float rest=bones.Single(b=>b.name=="Body").localPosition.z;
        Curve("Body","m_LocalPosition.z",p=>rest-.012f*Mathf.Sin(p));
        Curve("Head","localEulerAnglesRaw.y",p=>3.7f*Mathf.Sin(p));
        Curve("Left","localEulerAnglesRaw.y",p=>source.bird?-7*Mathf.Max(0,Mathf.Sin(p*2)):-2*Mathf.Sin(p));
        Curve("Right","localEulerAnglesRaw.y",p=>name=="Greeting"?32+10*Mathf.Sin(p*3):source.bird?7*Mathf.Max(0,Mathf.Sin(p*2)):2*Mathf.Sin(p));
        Curve("Eyes","m_LocalScale.z",p=>p>2.85f&&p<3.2f?.1f:1);
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=name=="Idle";AnimationUtility.SetAnimationClipSettings(clip,settings);
        return Save(clip,Folder+"/"+source.name+"_"+name+".anim");
    }
    static TileWorldCreator4TilesPreset Preset(string name,TownInteriorCatalog catalog)
    {
        var set=AssetDatabase.LoadAssetAtPath<TileWorldCreator4TilesPreset>(Folder+"/"+name+".asset");
        if(set==null){set=ScriptableObject.CreateInstance<TileWorldCreator4TilesPreset>();AssetDatabase.CreateAsset(set,Folder+"/"+name+".asset");}
        set.fillTile=catalog.Get(name+"_Fill").Prefab;set.edgeTile=catalog.Get(name+"_Edge").Prefab;
        set.interiorCornerTile=catalog.Get(name+"_Inner").Prefab;set.exteriorCornerTile=catalog.Get(name+"_Outer").Prefab;
        EditorUtility.SetDirty(set);return set;
    }
}
