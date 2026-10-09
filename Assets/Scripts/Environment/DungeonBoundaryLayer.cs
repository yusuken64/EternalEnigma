using System;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable, ActionName(Name = "Dungeon smart boundary")]
public sealed class DungeonBoundaryLayer : DungeonThemeTileLayer
{
    public DungeonBoundaryPreset SmartPreset;
    public TileWorldCreator4TilesPreset GroundPreset;
    public float WallHeight = 4.234727f;
    public bool CutawayForeground = true;
    public int PresentationVersion = 1;
    public DungeonBoundaryLayer() { Role = DungeonThemeRole.Boundary; }

    // Bits clockwise from north. Diagonal bits are NE, SE, SW, NW.
    public static (int cardinal, int diagonal) Neighbors(bool[,] mask, int x, int y)
    {
        int cardinal = (At(mask,x,y+1)?1:0) | (At(mask,x+1,y)?2:0) |
            (At(mask,x,y-1)?4:0) | (At(mask,x-1,y)?8:0);
        int diagonal = (At(mask,x+1,y+1)?1:0) | (At(mask,x+1,y-1)?2:0) |
            (At(mask,x-1,y-1)?4:0) | (At(mask,x-1,y+1)?8:0);
        return (cardinal, diagonal);
    }
    public static int RotateMask(int mask, int turns) => ((mask << turns) | (mask >> (4-turns))) & 15;
    public static (int piece, int turns) Classify(int neighbors)
    {
        int[] canonical = { 0, 1, 5, 3, 7, 15 };
        for (int piece=0;piece<canonical.Length;piece++)
            for (int turns=0;turns<4;turns++)
                if (RotateMask(canonical[piece], turns) == neighbors) return (piece, turns);
        throw new ArgumentOutOfRangeException(nameof(neighbors));
    }
    public override void Execute(TileWorldCreator creator, bool force)
    {
        if (SmartPreset == null) { base.Execute(creator, force); return; }
        try
        {
            DungeonLayerOutput.Remove(creator, guid);
            var source = creator.GetMapOutputFromBlueprintLayer(assignedGenerationLayerGuid);
            if (!active || source == null || SmartPreset.Tiles == null) return;
            var exclusions = Exclusions(creator);
            var mask = (bool[,])source.Clone();
            for (int y=0;y<mask.GetLength(1);y++) for (int x=0;x<mask.GetLength(0);x++)
                if (exclusions.Any(excluded => At(excluded,x,y))) mask[x,y] = false;
            var root = DungeonLayerOutput.Create(creator, this, Offset);
            var batch = new EnvironmentBatch(root.transform);
            var faces = root.AddComponent<BiomeDecorationSurfaceSet>();
            var kit = SmartPreset.Tiles;
            var pieces = new[] { kit.singleTile, kit.deadEndTile, kit.straightTile, kit.cornerTile, kit.threeWayTile, kit.fourWayTile };
            float size = creator.twcAsset.cellSize;
            var scale = new Vector3(size / Mathf.Max(.001f,SmartPreset.AuthoredCellSize),
                size / Mathf.Max(.001f,SmartPreset.AuthoredCellSize), Mathf.Max(.01f,WallHeight) / Mathf.Max(.001f,SmartPreset.AuthoredHeight));
            for (int y=0;y<mask.GetLength(1);y++) for (int x=0;x<mask.GetLength(0);x++)
            {
                if (!mask[x,y]) continue;
                var (cardinal, diagonal) = Neighbors(mask,x,y);
                var position = new Vector3((x+.5f)*size,(y+.5f)*size,0);
                // Whole-cell boundaries are narrower than a blocked cell. Continue paving
                // beneath them so the space between the wall and walkable floor is grounded.
                if (GroundPreset != null && GroundPreset.fillTile != null)
                    AddPrefab(batch,faces,GroundPreset.fillTile,position,new Vector3(size,size,1),Quaternion.identity,false,size);
                if (cardinal == 15 && diagonal == 15 && SmartPreset.SolidFill != null)
                { Add(SmartPreset.SolidFill,0); continue; }
                var (piece, turns) = Classify(cardinal);
                var prefab = pieces[piece];
                var variants = SmartPreset.StraightVariants;
                if (piece == 2 && variants != null && variants.Length > 0)
                    prefab = variants[DungeonPresentation.Hash(creator.currentSeed,x,y) % (uint)variants.Length] ?? prefab;
                Add(prefab,turns);
                for (int quadrant=0;quadrant<4;quadrant++)
                    if ((cardinal & RotateMask(3,quadrant)) == RotateMask(3,quadrant))
                        Add((diagonal & (1<<quadrant)) != 0 ? SmartPreset.QuadrantFill : SmartPreset.ConcaveCorner,quadrant);
                void Add(GameObject part, int rotation) => AddPrefab(batch, faces, part, position, scale,
                    Quaternion.Euler(0,0,-90*rotation),true,size,SmartPreset.MaterialOverride);
            }
            batch.Finish();
            if(CutawayForeground)DungeonBoundaryCutaway.Install(creator,root,batch.Owner,faces);
        }
        finally { creator.executedBuildLayersCount++; }
    }
#if UNITY_EDITOR
    protected override void DrawExtraGUI()
    {
        SmartPreset = (DungeonBoundaryPreset)UnityEditor.EditorGUILayout.ObjectField(
            "Six-piece boundary", SmartPreset, typeof(DungeonBoundaryPreset), false);
        GroundPreset = (TileWorldCreator4TilesPreset)UnityEditor.EditorGUILayout.ObjectField(
            "Boundary paving", GroundPreset, typeof(TileWorldCreator4TilesPreset), false);
        WallHeight = Mathf.Max(.01f,UnityEditor.EditorGUILayout.FloatField("Wall height",WallHeight));
        CutawayForeground = UnityEditor.EditorGUILayout.Toggle("Foreground cutaway",CutawayForeground);
        UnityEditor.EditorGUILayout.HelpBox("A six-piece boundary uses whole blueprint cells and diagonal fills. Leave it empty to use the four-piece preset.", UnityEditor.MessageType.Info);
    }
#endif
}
