using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests
{
    public sealed class DungeonPresentationAuthoringTests
    {
        [Test] public void HeightFieldNormalsJoinAcrossChunkBoundaries()
        {
            var a=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};
            var b=new Mesh{vertices=new[]{Vector3.right,Vector3.zero,new Vector3(0,-1,1)},triangles=new[]{0,1,2}};
            try
            {
                EnvironmentBatch.SmoothTerrain(new List<Mesh>{a,b});
                Assert.That(a.normals[0],Is.EqualTo(b.normals[1]));
                Assert.That(a.normals[1],Is.EqualTo(b.normals[0]));
                Assert.That(a.normals[0].magnitude,Is.EqualTo(1).Within(.0001f));
                Assert.That(a.normals[0],Is.Not.EqualTo(a.normals[2]));
            }
            finally{Object.DestroyImmediate(a);Object.DestroyImmediate(b);}
        }
        [Test] public void HeightFieldProjectionAndGateAtlasAreStable()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/EnvironmentKit/SmartTiles/SixTerrain/MountainSurface.mat");
            Assert.That(material.GetTag("EnvironmentProjection",false),Is.EqualTo("Planar"));
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Overworld/GateMesh.asset");
            var uv=mesh.uv;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                var cell=Vector2Int.FloorToInt(uv[indices[i]]*4);
                Assert.That(Vector2Int.FloorToInt(uv[indices[i+1]]*4),Is.EqualTo(cell));
                Assert.That(Vector2Int.FloorToInt(uv[indices[i+2]]*4),Is.EqualTo(cell));
            }
        }
        [Test] public void RepeatedGenericBakingPreservesDungeonRolesAndTravelGeometry()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GameUIButtonAuthoring")).First(t=>t!=null);
            var bake=type.GetMethod("StyleHierarchy",BindingFlags.Static|BindingFlags.NonPublic);
            var paths=new[]{"Assets/Resources/UI/Dungeon/PartyMenu.prefab","Assets/Resources/UI/Dungeon/ItemActions.prefab",
                "Assets/Resources/UI/Dungeon/InventoryPicker.prefab","Assets/Resources/UI/Dungeon/History.prefab","Assets/Resources/UI/PartyMenu.prefab"};
            foreach(var path in paths)
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rects=root.GetComponentsInChildren<RectTransform>(true);
                    var anchors=rects.Select(r=>(r.anchorMin,r.anchorMax)).ToArray();
                    bake.Invoke(null,new object[]{root});bake.Invoke(null,new object[]{root});
                    foreach(var role in root.GetComponentsInChildren<DungeonUIRole>(true))Assert.That(role.IsValid(),Is.True,path+"/"+role.name);
                    foreach(var style in root.GetComponentsInChildren<DungeonTextStyle>(true))
                        Assert.That(style.GetComponent<TMPro.TMP_Text>().color,Is.EqualTo(style.OnWood?GameUITheme.LightInk:GameUITheme.Ink));
                    for(int i=0;i<rects.Length;i++)Assert.That((rects[i].anchorMin,rects[i].anchorMax),Is.EqualTo(anchors[i]),path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
    }
}
