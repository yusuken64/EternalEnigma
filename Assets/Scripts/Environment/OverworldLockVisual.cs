using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Route fiction selects a closed obstacle silhouette. Logical locks remain in Core.</summary>
public sealed class OverworldLockVisual : MonoBehaviour
{
    public string SkinId { get; private set; }
    public string ModelId { get; private set; }
    public GameObject Portcullis { get; private set; }
    private bool waystone;
    public void SetOpen(bool open)
    {
        gameObject.SetActive(waystone || Portcullis != null || !open);
        if (Portcullis != null) Portcullis.SetActive(!open);
        if (waystone)
        {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.sharedMaterial.name.EndsWith("_Glow")) continue;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_Color", open ? new Color(1, .82f, .30f) : new Color(.16f, .24f, .28f));
                renderer.SetPropertyBlock(properties);
            }
        }
    }
    private static int Variant(CampaignRoute route)
    {
        string suffix = route.SkinId?.Split('.').Last();
        return int.TryParse(suffix, out int value) ? value - 1 : 0;
    }
    public static Quaternion GateRotation(OverworldGrid grid, GridLock gate)
    {
        var cells = gate.Cells.Where(cell => !grid.RequiresBoat(cell)).ToArray();
        if (cells.Length == 0) return Quaternion.identity;
        int dx = cells.Max(cell => cell.X) - cells.Min(cell => cell.X);
        int dy = cells.Max(cell => cell.Y) - cells.Min(cell => cell.Y);
        // Multi-cell locks already describe the crossing direction. A single-cell lock needs its path.
        if (cells.Length == 1 && grid.Routes.TryGetValue(gate.RouteId, out var path))
        {
            int index = path.ToList().IndexOf(cells[0]);
            if (index >= 0)
            {
                var from = path[Mathf.Max(0, index - 1)]; var to = path[Mathf.Min(path.Count - 1, index + 1)];
                dx = Mathf.Abs(to.X - from.X); dy = Mathf.Abs(to.Y - from.Y);
            }
        }
        return Quaternion.Euler(0, 0, dx > dy ? 90 : 0);
    }
    public static (string Model,string Detail) Models(CampaignRoute route)
    {
        var skin=LockSkinCatalog.Skins.FirstOrDefault(s=>s.Id==route.SkinId);
        int variant = Variant(route);
        string Choice(params string[] choices) => choices[variant % choices.Length];
        if (route.SkinId == LockSkinCatalog.WaystoneKeySkin)
        {
            var themes = new[] { LockSkinCatalog.Highlands, LockSkinCatalog.Marsh, LockSkinCatalog.Coast, LockSkinCatalog.Forest,
                LockSkinCatalog.Desert, LockSkinCatalog.Tundra, LockSkinCatalog.Ruins, LockSkinCatalog.Volcanic };
            int index = System.Array.FindIndex(themes, theme => LockSkinCatalog.WaystoneKeyName(theme) == route.KeyLabel);
            return ("LockWaystone" + (Mathf.Max(0, index) % 4 + 1), null);
        }
        if (route.SkinId == LockSkinCatalog.TownGateKeySkin || route.SkinId == LockSkinCatalog.TownAreaKeySkin)
        {
            var names = route.SkinId == LockSkinCatalog.TownGateKeySkin ? LockSkinCatalog.TownGateKeyNames : LockSkinCatalog.TownAreaKeyNames;
            int key = Mathf.Max(0, names.ToList().IndexOf(route.KeyLabel));
            var gates = route.SkinId == LockSkinCatalog.TownGateKeySkin
                ? new[] { "CastleGate", "LockGate1", "LockGate2", "LockGate5", "LockGate4", "LockGate6", "LockGate3", "CastleGate" }
                : new[] { "LockGate3", "LockGate2", "LockGate6", "LockGate1", "LockGate4", "LockGate5", "CastleGate", "LockGate3" };
            return (gates[key], key == 7 ? "CastleFlag" : null);
        }
        if(skin==null)return ("CastleGate",null);
        return skin.Capability switch
        {
            Capability.Climb=>(Choice("LockCliff1","LockCliff2","LockCliff3"),null),
            Capability.Grapple=>(Choice("LockAnchorGap1","LockAnchorGap2","LockAnchorGap3"),null),
            Capability.Icewalk or Capability.Icebreaker=>(Choice("LockIceFloes1","LockIceFloes2","LockIceFloes3"),null),
            Capability.DeepDive=>(Choice("LockDrownedStair1","LockDrownedStair2","LockDrownedStair3"),variant%2==0?"Reeds":null),
            Capability.Breach=>(Choice("LockCliff2","LockMine2","BoulderLarge","PackRock05","DeadTree","LockMine1"),"BoulderSmall"),
            Capability.Tunneler=>(Choice("LockMine1","LockMine2"),"BoulderSmall"),
            Capability.WaystoneStep=>(Choice("LockWaystone1","LockWaystone2","LockWaystone3","LockWaystone4"),null),
            Capability.PhaseShift=>(Choice("LockVeil1","LockVeil2","LockVeil3"),null),
            Capability.HazardWard=>(Choice("DeadTree","CharredTree","MushroomRing"),"MushroomRing"),
            Capability.Boat=>(Choice("LockBrokenDock1","LockBrokenDock2","LockBrokenDock3"),"Reeds"),
            Capability.Airship=>(Choice("LockAnchorGap3","LockCliff2","LockAnchorGap1"),"CastleFlag"),
            Capability.Engineering=>(Choice("LockWinch1","LockWinch2","LockWaterwheel2"),null),
            Capability.Translation=>(Choice("Signboard","FacadeDoor","Signboard","LockGate5","LockWaystone3"),variant%2==0?"LockCrate":"Signboard"),
            Capability.RoyalAuthority=>(Choice("LockGate2","LockGate3","CastleGate","LockGate1","LockGate5"),"CastleFlag"),
            Capability.AncientAttunement=>(Choice("LockWaystone4","LockVeil2","LockWaystone2","LockWaystone1","LockWaystone3"),null),
            Capability.BeastSpeech=>("LockDog",null),
            Capability.GuildStanding=>(Choice("FacadeDoor","LockGate3","LockGate1","LockGate6","LockGate4"),"LockCrate"),
            Capability.Remedy=>(Choice("LockSickCart1","LockSickCart2","LockSickCart3"),null),
            Capability.Diplomacy=>(Choice("LockGate6","LockGate3","Fence"),"CastleFlag"),
            Capability.SacredRite=>(Choice("LockAltar1","LockAltar2","LockAltar3"),null),
            Capability.MillwrightTraining=>(Choice("LockWaterwheel1","LockWaterwheel2","LockWinch2"),null),
            Capability.ForestersWrit=>(Choice("LockGate4","LockGate1","LockGate6","Fence","LockGate3"),"PackTree03"),
            Capability.FerrymansFavor=>(Choice("LockFerry1","LockFerry2"),"Reeds"),
            Capability.GravediggersCharge=>(Choice("LockGate2","LockGraves1","LockGraves2","LockGate5","LockGraves3"),null),
            Capability.SmugglersMark=>(Choice("LockMine2","FacadeDoor","LockGate1","LockFerry1","LockMine1"),"LockBarrel"),
            Capability.BellkeepersOffice=>(Choice("LockBelfry1","LockBelfry2"),null),
            Capability.FuneraryRites=>(Choice("LockGraves3","LockGraves2","LockGraves1"),null),
            Capability.RoyalSurveyorsMaps=>(Choice("LockSurvey1","LockSurvey2"),"BoulderSmall"),
            _=>("CastleGate",null)
        };
    }
    public static void Build(GameObject marker,CampaignRoute route,OverworldBiome biome)
    {
        var catalog=DioramaCatalog.Load();
        var selected=Models(route);
        bool horizontal = Mathf.Abs(Mathf.DeltaAngle(marker.transform.eulerAngles.z, 90)) < 1;
        // An arch seen edge-on hides the barrier. Use its matching open-top iron wicket.
        if (horizontal && selected.Model == "CastleGate") selected = ("LockGate2", selected.Detail);
        if(catalog?.Get(selected.Model)?.Prefab==null)return;
        foreach(var renderer in marker.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
        var view=marker.AddComponent<OverworldLockVisual>();view.SkinId=route.SkinId;view.ModelId=selected.Model;
        view.waystone = route.IsWarp;
        var root=new GameObject("Lock obstacle "+selected.Model);root.transform.SetParent(marker.transform,false);
        var source = catalog.Get(selected.Model).Prefab;
        var frame = (horizontal ? source.transform.Find("Side frame") : null) ?? source.transform.Find("Frame");
        var skin = LockSkinCatalog.Skins.FirstOrDefault(s => s.Id == route.SkinId);
        int variant = Variant(route);
        bool naturalStone = selected.Model.StartsWith("LockCliff") || selected.Model.StartsWith("LockMine") || selected.Model.StartsWith("LockAnchorGap");
        if (frame != null)
        {
            AddPiece(root, frame.gameObject, catalog, biome, naturalStone);
            view.Portcullis = new GameObject("Closed barrier");
            view.Portcullis.transform.SetParent(marker.transform, false);
            AddPiece(view.Portcullis, source.transform.Find("Closed barrier").gameObject, catalog, biome, naturalStone);
        }
        else
        {
            var batch=new EnvironmentBatch(root.transform);
            if (selected.Model == "LockDog")
            {
                int count = new[] { 3, 2, 1, 1, 3 }[variant % 5];
                for (int i = 0; i < count; i++)
                    AddReused(batch, source, new Vector3((i-(count-1)*.5f)*.48f,(i%2)*.18f,0), count==1?1.1f:.68f, i%2==0?-20:20);
            }
            else
            {
                float scale = selected.Model.StartsWith("Lock") ? 1 : selected.Model.Contains("Tree") ? .42f : selected.Model=="FacadeDoor"?.53f:.75f;
                var dimensions = Vector3.one * catalog.Get(selected.Model).Scale * scale;
                if (skin?.Capability == Capability.Icebreaker) dimensions.z *= 2.6f;
                if (selected.Model=="BoulderLarge" || selected.Model=="PackRock05") dimensions *= 1.5f;
                catalog.Add(batch,selected.Model,biome,Vector3.zero,scale:dimensions);
                if (skin?.Capability==Capability.HazardWard && variant%3==2)
                {
                    catalog.Add(batch,"MushroomRing",biome,new Vector3(-.4f,.30f,0),variation:.8f);
                    catalog.Add(batch,"MushroomRing",biome,new Vector3(.4f,.32f,0),variation:.7f);
                }
            }
            batch.Finish();
            if (selected.Model.StartsWith("Lock")) PaintStone(root, biome, naturalStone);
        }
        if (selected.Model == "CastleGate")
        {
            ApplyGateMasonry(root, biome);
            view.BuildPortcullis(root, catalog, biome);
        }
        if(selected.Detail!=null && catalog.Get(selected.Detail)?.Prefab!=null)
        {
            var detail = new GameObject("Lock detail " + selected.Detail);
            detail.transform.SetParent(root.transform, false);
            var details = new EnvironmentBatch(detail.transform);
            float side = variant % 2 == 0 ? 1 : -1;
            var position = new Vector3(.68f * side,.20f,0);
            if (selected.Detail=="LockCrate" || selected.Detail=="LockBarrel")
                AddReused(details, catalog.Get(selected.Detail).Prefab, position, .47f, 12*side);
            else catalog.Add(details, selected.Detail, biome, position, variation:selected.Detail.Contains("Tree")?.28f:selected.Detail=="CastleFlag"?.38f:.43f);
            details.Finish();
        }
        if (skin?.Capability is Capability.Boat or Capability.DeepDive or Capability.FerrymansFavor or Capability.Icewalk or Capability.Icebreaker or Capability.MillwrightTraining)
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>().Where(r => r.sharedMaterial.name.EndsWith("_Glass")))
            {
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_Color", new Color(.39f,.67f,.76f)); renderer.SetPropertyBlock(properties);
            }
        view.SetOpen(false);
    }

    private static void AddPiece(GameObject parent, GameObject source, DioramaCatalog catalog, OverworldBiome biome, bool naturalStone)
    {
        var batch = new EnvironmentBatch(parent.transform);
        batch.AddPrefab(source, Vector3.zero, Vector3.one, Quaternion.identity,
            materialMap: material => catalog.Material(material.name.Split('_').Last(), biome));
        batch.Finish(); PaintStone(parent, biome, naturalStone);
    }

    private static void PaintStone(GameObject root, OverworldBiome biome, bool naturalStone)
    {
        var masonry = EnvironmentKit.Load()?.BuildingMaterial(biome);
        if (masonry == null) return;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (!renderer.sharedMaterial.name.EndsWith("_Stone")) continue;
            if (naturalStone && DioramaCatalog.Load().Cliffs.Length == 8)
            {
                renderer.sharedMaterial = DioramaCatalog.Load().Cliffs[(int)biome];
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                // The existing cliff shader uses red for grass coverage and green for shading.
                mesh.colors = mesh.colors.Select(color => new Color(0, color.g, 0, 1)).ToArray();
            }
            else renderer.sharedMaterial = masonry;
        }
    }

    private static void AddReused(EnvironmentBatch batch, GameObject prefab, Vector3 position, float span, float angle)
    {
        var bounds = prefab.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).Select(filter =>
        {
            var b = filter.sharedMesh.bounds;
            var matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i=0;i<8;i++) result.Encapsulate(matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return result;
        }).Aggregate((a,b) => { a.Encapsulate(b); return a; });
        float scale = span / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        var rotation = Quaternion.Euler(0,0,angle);
        var offset = new Vector3(-bounds.center.x,-bounds.center.y,-bounds.max.z) * scale;
        batch.AddPrefab(prefab,position + rotation*offset,Vector3.one*scale,rotation);
    }

    private void BuildPortcullis(GameObject frame, DioramaCatalog catalog, OverworldBiome biome)
    {
        var bounds = frame.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh.bounds).ToArray();
        var opening = bounds[0];
        foreach (var bound in bounds.Skip(1)) opening.Encapsulate(bound);
        float width = opening.size.x * .62f, height = opening.size.z * .72f;
        Portcullis = new GameObject("Closed portcullis");
        Portcullis.transform.SetParent(transform, false);
        var batch = new EnvironmentBatch(Portcullis.transform);
        var source = catalog.Get("LockGate2").Prefab.transform.Find("Closed barrier").gameObject;
        var sourceBounds = source.GetComponent<MeshFilter>().sharedMesh.bounds;
        var scale = new Vector3(width / sourceBounds.size.x, .7f, height / sourceBounds.size.z);
        var position = new Vector3(opening.center.x - sourceBounds.center.x * scale.x,
            opening.min.y + .04f - sourceBounds.center.y * scale.y, opening.max.z - sourceBounds.max.z * scale.z);
        batch.AddPrefab(source, position, scale, Quaternion.identity,
            materialMap: material => catalog.Material(material.name.Split('_').Last(), biome));
        batch.Finish();
    }

    internal static void ApplyGateMasonry(GameObject root, OverworldBiome biome, bool untexturedWallsOnly = false)
    {
        var masonry = EnvironmentKit.Load()?.BuildingMaterial(biome);
        if (masonry == null) return;
        var filters = root.GetComponentsInChildren<MeshFilter>().Where(filter =>
        {
            if (!untexturedWallsOnly) return true;
            var material = filter.GetComponent<MeshRenderer>()?.sharedMaterial;
            return material != null && material.name.StartsWith("wall", System.StringComparison.OrdinalIgnoreCase) &&
                material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == null;
        }).ToArray();
        if (filters.Length == 0) return;
        var bounds = filters[0].sharedMesh.bounds;
        foreach (var filter in filters.Skip(1)) bounds.Encapsulate(filter.sharedMesh.bounds);
        float span = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        foreach (var filter in filters)
        {
            // These are owned, combined meshes. The source castle prefab keeps its original UVs.
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = (vertices[i] - bounds.min) / span; var n = normals[i];
                var face = Mathf.Abs(n.z) >= Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.y)) ? new Vector2(p.x, p.y) :
                    Mathf.Abs(n.x) > Mathf.Abs(n.y) ? new Vector2(p.y, 1 - p.z) : new Vector2(p.x, 1 - p.z);
                // The shared building atlas stores painted masonry in cell (3, 0).
                uv[i] = new Vector2((3 + .065f + face.x * .87f) / 4, (.065f + face.y * .87f) / 4);
            }
            mesh.uv = uv;
            var renderer = filter.GetComponent<MeshRenderer>();
            bool trim = renderer.sharedMaterial.name.Contains("wallDark");
            renderer.sharedMaterial = masonry;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", trim ? new Color(.8f, .8f, .8f) : Color.white);
            renderer.SetPropertyBlock(properties);
        }
    }
}
