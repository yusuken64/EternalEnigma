using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Seeded pickups are campaign state; collection leaves cached terrain untouched.</summary>
public sealed class OverworldPickups : MonoBehaviour
{
    OverworldScene world;
    readonly Dictionary<string,GameObject> markers = new();
    internal void Initialize(OverworldScene scene)
    {
        world=scene;
        foreach(var pickup in world.Context.Grid.Pickups.Where(p=>!world.Context.CollectedPickups.Contains(p.Id)))
        {
            var definition=Definition(world.Context,pickup,Common.Instance.ItemManager);
            var prefab=definition.DroppedItemPrefab;
            GameObject go;
            if(prefab!=null) go=Instantiate(prefab.gameObject,transform);
            else
            {
                go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(transform,false);
                go.GetComponent<Renderer>().material=new Material(Shader.Find("Standard")) {color=pickup.Kind==OverworldPickupKind.Weapon?new Color(.8f,.85f,1):new Color(1,.85f,.25f)};
                go.AddComponent<EncounterMarkerMaterial>();go.transform.localScale=new Vector3(.5f,.5f,.15f);
            }
            go.name=definition.ItemName+" ["+pickup.Id+"]";
            go.transform.position=world.CellCenterToWorld(pickup.Position)+Vector3.back*.2f;
            foreach(var collider in go.GetComponentsInChildren<Collider>()) Destroy(collider);
            markers.Add(pickup.Id,go);
        }
    }
    internal static ItemDefinition Definition(CampaignContext context,OverworldPickup pickup,ItemManager items)
    {
        int tier=context.Campaign.Regions.Single(r=>r.Id==pickup.RegionId).Tier;
        int floor=CampaignContext.Floors(tier).Start+1;
        var definitions=items.ItemDefinitions.Concat(items.StartingItems).Where(i=>i!=null&&!i.IsProgressionItem&&i.MinFloor<=floor&&i.MaxFloor>=floor);
        var pool=definitions.Where(i=>pickup.Kind==OverworldPickupKind.Weapon
            ? i is EquipmentItemDefinition equipment && !equipment.IsAmmunition && equipment.WeaponType!=WeaponType.OffhandShield && equipment.EquipmentSlot!=EquipmentSlot.Accessory
            // Production spell scrolls use dedicated item effects (bolt, warp, explosion),
            // while SkillItemEffectDefinition is also used by the demo loadout.
            : i is UsableItemDefinition && (i.DroppedItemVisual==DroppedItemVisual.SpellBook || i.ItemEffectDefinition is SkillItemEffectDefinition))
            .Distinct().OrderBy(i=>i.ItemName,System.StringComparer.Ordinal).ToArray();
        if(pool.Length==0)throw new System.InvalidOperationException("No eligible "+pickup.Kind+" definitions for "+pickup.RegionId);
        return pool[(uint)pickup.Roll%(uint)pool.Length];
    }
    internal bool TryCollect()
    {
        if(world.Context.IsSandbox)return false;
        var pickup=world.Context.Grid.Pickups.FirstOrDefault(p=>p.Position.Equals(world.Position)&&!world.Context.CollectedPickups.Contains(p.Id));
        if(pickup==null)return false;
        var bag=Common.Instance.GameSaveData.TownSaveData.InventoryItems;
        if(bag.Count>=Inventory.DefaultCapacity){GameMessages.Post("Inventory full. The pickup remains here.");AudioManager.Instance?.PlayCue(SoundCue.Denied);return false;}
        var definition=Definition(world.Context,pickup,Common.Instance.ItemManager);
        bool collected=world.Context.TryCollectPickup(pickup.Id,_=>{
            bag.Add(ItemSaveData.From(definition.AsInventoryItem(definition.StackMax>0?Mathf.Max(1,definition.StackStartMin):null)));return true;
        });
        if(!collected)return false;
        if(markers.TryGetValue(pickup.Id,out var marker)){Destroy(marker);markers.Remove(pickup.Id);}
        SaveSystem.Capture(Common.Instance);GameMessages.Post("Found "+definition.DisplayName+".");
        AudioManager.Instance?.PlayCue(SoundCue.Pickup);
        return true;
    }
}
