using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable,ActionName(Name="Diorama shop goods and park wildlife")]
public sealed class DioramaTownAccentLayer : TWCBuildLayer
{
    public override TWCBuildLayer Clone()=>new DioramaTownAccentLayer{guid=guid,assignedGenerationLayerGuid=assignedGenerationLayerGuid,layerName=layerName,active=active};
    public override void Execute(TileWorldCreator creator,bool force)
    {
        try
        {
            var catalog=DioramaItemCatalog.Load();if(catalog==null||!CoreLayoutCache.TryGetTown(creator,out var plan))return;
            var root=creator.AddLayerObject(layerName,guid);root.transform.SetParent(creator.worldObject.transform,false);root.transform.localRotation=Quaternion.identity;
            foreach(var child in root.transform.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);DungeonPresentation.Release(child.gameObject);}
            float size=creator.twcAsset.cellSize;
            foreach(var interior in plan.Interiors.Where(i=>i.Spec.HasVendor))
            {
                var marker=Place(interior.Spec.Kind==TownInteriorKind.Trainer?"question mark":"exclamation mark",
                    new Vector3(interior.Door.X+.5f,interior.Door.Y+.6f,-DioramaScale.Door/size-.22f)*size,.6f);
                if(marker!=null)marker.AddComponent<DioramaBob>().Phase=(OverworldCosmetics.Hash(plan.Seed,interior.Door.X,interior.Door.Y)%1000)*.00628f;
            }
            // Prefab renderers retain shared atlas materials. They are created by
            // this TWC layer without requiring Read/Write on the Adorable pack.
            foreach(var interior in plan.Interiors.Where(i=>i.Spec.Kind==TownInteriorKind.Shop))
            {
                string[] goods=interior.Spec.Theme==TownShopTheme.Bakery?new[]{"cake","waffle","egg"}:interior.Spec.Theme==TownShopTheme.Consumables?new[]{"potion_red","potion_blue","jar"}:Array.Empty<string>();
                if(goods.Length==0)continue;
                int good=(int)(OverworldCosmetics.Hash(plan.Seed,interior.Door.X,interior.Door.Y)%(uint)goods.Length);
                foreach(var p in interior.Props.Where(p=>p.Asset is "Basket" or "Potions"))
                    Place(goods[good++%goods.Length],new Vector3(p.Cell.X+.5f,p.Cell.Y+.5f,-p.Elevation-.04f)*size,.9f);
            }
            int count=0;
            var gardens=Enumerable.Range(0,plan.Width*plan.Height).Select(i=>new GridPoint(i%plan.Width,i/plan.Width))
                .Where(p=>p.X>=2&&p.Y>=2&&p.X<plan.Width-2&&p.Y<plan.Height-2&&!plan.IsReserved(p)&&
                    !new[]{TownLayers.Roads,TownLayers.Trees,TownLayers.Props,TownLayers.Buildings,TownLayers.Roofs,TownLayers.ShopFloor}.Any(l=>plan.Layers[l].At(p)))
                .OrderByDescending(p=>plan.Layers[TownLayers.Parks].At(p)).ThenBy(p=>OverworldCosmetics.Hash(plan.Seed^32731,p.X,p.Y));
            foreach(var p in gardens.Take(5))
            {
                Place(new[]{"hen","duck","butterfly","bee","ladybug"}[count],new Vector3(p.X+.5f,p.Y+.5f,count is 2 or 3?-.35f:0)*size,count is 2 or 3?.55f:.9f);count++;
            }
            GameObject Place(string id,Vector3 position,float scale)
            {
                var prefab=catalog.GetProp(id);if(prefab==null)return null;
                var obj=UnityEngine.Object.Instantiate(prefab,root.transform);obj.name=id;obj.transform.localPosition=position;obj.transform.localScale=Vector3.one*scale;
                obj.AddComponent<EnvironmentMeshOwner>();
                foreach(var r in obj.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
                return obj;
            }
        }
        finally {creator.executedBuildLayersCount++;}
    }
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)=>layerName=UnityEditor.EditorGUILayout.TextField("Layer name",layerName);
#endif
}
