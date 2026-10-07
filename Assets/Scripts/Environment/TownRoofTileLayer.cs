using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

/// <summary>Builds the TWC Roofs blueprint as pitched, per-building shingle tiles.</summary>
[Serializable, ActionName(Name = "Town roof tiles")]
public sealed class TownRoofTileLayer : TWCBuildLayer
{
    public EnvironmentKit Kit;
    public TileWorldCreator4TilesPreset Preset;
    public override TWCBuildLayer Clone() => new TownRoofTileLayer { guid = guid, layerName = layerName,
        assignedGenerationLayerGuid = assignedGenerationLayerGuid, active = active, Kit = Kit, Preset = Preset };

    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            if (Kit == null || !CoreLayoutCache.TryGetTown(creator, out var plan)) return;
            // Disabling a TWC build layer does not remove its baked scene output.
            // Retire the former shop walls when the replacement houses are built.
            var retiredWalls = creator.twcAsset.mapBuildLayers
                .Where(l => l.layerName == "Smart/Walls" && !l.active)
                .Select(l => l.guid).ToHashSet();
            foreach (var old in creator.worldObject.GetComponentsInChildren<LayerIdentifier>(true))
            {
                if (!retiredWalls.Contains(old.assignedLayer)) continue;
                old.gameObject.SetActive(false);
                DungeonPresentation.ClearOutput(old.gameObject);
                DungeonPresentation.Release(old.gameObject);
            }
            var root = creator.AddLayerObject(layerName, guid);
            root.transform.SetParent(creator.worldObject.transform, false);
            root.transform.localRotation = Quaternion.identity;
            var output = root.GetComponent<TownRoofTileOutput>() ?? root.AddComponent<TownRoofTileOutput>();
            output.Roofs.Clear();
            foreach (var child in root.transform.Cast<Transform>().ToArray()) Release(child.gameObject);
            float size = creator.twcAsset.cellSize;
            var biome = creator.GetComponent<TownBiomeStyle>()?.Current ?? OverworldBiome.Grassland;
            var roofCells = plan.Layers[TownLayers.Roofs];
            var diorama=DioramaCatalog.Load();
            foreach (var door in plan.BuildingSlots)
            {
                var footprint = plan.Footprints.FirstOrDefault(f => f.Door.Equals(door));
                var body = footprint != null && footprint.Cells.Count > 0
                    ? footprint.Cells.ToHashSet()
                    : Fallback(roofCells, door);
                if (body.Count == 0) continue;
                var obj = new GameObject("Roof " + door);
                obj.transform.SetParent(root.transform, false);
                var visual = obj.AddComponent<TownRoofVisual>();
                visual.Initialize(door, plan.ShopRoomAt(door));
                output.Roofs.Add(visual);
                var batch = new EnvironmentBatch(obj.transform);
                var color = TownHouseTiles.Palette(biome).roof;
                var source = Preset != null && Preset.fillTile != null
                    ? Preset.fillTile.GetComponentInChildren<MeshRenderer>().sharedMaterial
                    : Kit.BuildingMaterial(biome);
                var material = TownHouseTiles.ColorMaterial(source, color, batch.Owner, "Roof shingles", true);
                var alternate = TownHouseTiles.ColorMaterial(source, color * .88f, batch.Owner, "Roof shingle variation", true);
                var shingles = TownHouseTiles.ShingleTexture(batch.Owner);
                material.mainTexture = shingles;
                alternate.mainTexture = shingles;
                var gable = TownHouseTiles.ColorMaterial(Kit.BuildingMaterial(biome),
                    TownHouseTiles.Palette(biome).plaster, batch.Owner, "Gable plaster");
                foreach (var cell in body)
                {
                    int left = cell.X, right = cell.X;
                    while (body.Contains(new GridPoint(left - 1, cell.Y))) left--;
                    while (body.Contains(new GridPoint(right + 1, cell.Y))) right++;
                    float center = (left + right + 1) * .5f;
                    float halfWidth = (right - left + 1) * .5f;
                    bool frontEdge=!body.Contains(new GridPoint(cell.X,cell.Y-1)),backEdge=!body.Contains(new GridPoint(cell.X,cell.Y+1));
                    var tile = TownHouseTiles.RoofTile(cell.X-(cell.X==left?.10f:0),cell.X+1+(cell.X==right?.10f:0), center, halfWidth,
                        cell.X == Mathf.FloorToInt(center),frontEdge,backEdge);
                    batch.Owner.Meshes.Add(tile);
                    batch.Add(tile, (cell.X + cell.Y) % 3 == 0 ? alternate : material,
                        new Vector3(0, cell.Y, 0) * size, Vector3.one * size, role: SilhouetteRole.Caster);
                    if (!body.Contains(new GridPoint(cell.X, cell.Y - 1)))
                    {
                        var face = TownHouseTiles.GableTile(cell.X, center, halfWidth);
                        batch.Owner.Meshes.Add(face);
                        batch.Add(face, gable, new Vector3(0, cell.Y, 0) * size,
                            Vector3.one * size, role: SilhouetteRole.Caster);
                    }
                    if(diorama!=null)
                    {
                        if(cell.X==left||cell.X==right)
                            diorama.Add(batch,"RoofEave",biome,new Vector3(cell.X==left?left-.06f:right+1.06f,cell.Y+.5f,-DioramaScale.Eaves/size)*size,
                                90,scale:new Vector3(size,size,.85f));
                        if(cell.X==Mathf.FloorToInt(center))
                            diorama.Add(batch,"RoofRidge",biome,new Vector3(center,cell.Y+.5f,TownHouseTiles.RoofHeight(center,center,halfWidth))*size,
                                scale:new Vector3(size*.65f,size,size*.65f));
                    }
                }
                if(diorama!=null)
                {
                    uint hash=OverworldCosmetics.Hash(plan.Seed^37811,door.X,door.Y);
                    int firstRow=body.Min(c=>c.Y),lastRow=body.Max(c=>c.Y);
                    int left=body.Where(c=>c.Y==firstRow).Min(c=>c.X),right=body.Where(c=>c.Y==firstRow).Max(c=>c.X);
                    float center=(left+right+1)*.5f,halfWidth=(right-left+1)*.5f;
                    float chimneyX=center+(hash%2==0?-.55f:.55f)*halfWidth;
                    diorama.Add(batch,"Chimney",biome,new Vector3(chimneyX,Mathf.Lerp(firstRow+.6f,lastRow+.4f,.67f),TownHouseTiles.RoofHeight(chimneyX,center,halfWidth)+.05f)*size,scale:Vector3.one*1.15f);
                    if(hash%3!=0 && right-left>=2)
                    {
                        float dormerX=center-halfWidth*.38f;
                        diorama.Add(batch,"Dormer",biome,new Vector3(dormerX,firstRow+.4f,TownHouseTiles.RoofHeight(dormerX,center,halfWidth)+.04f)*size,scale:Vector3.one*1.35f);
                    }
                }
                batch.Finish();
            }
        }
        finally { creator.executedBuildLayersCount += 1; }
    }

    private static HashSet<GridPoint> Fallback(GridLayer roofCells, GridPoint door)
    {
        var body = new HashSet<GridPoint>();
        for (int y = door.Y + 1; y <= door.Y + 4; y++)
            for (int x = door.X - 1; x <= door.X + 1; x++)
                if (roofCells.At(new GridPoint(x, y))) body.Add(new GridPoint(x, y));
        if (body.Count == 0) body.Add(door);
        return body;
    }

    private static void Release(GameObject obj)
    {
        obj.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
    }
}
