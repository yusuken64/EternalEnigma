using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Visuals for movable stones and the floor mechanisms in encounter rooms.</summary>
internal sealed class EncounterPuzzlePiece : MonoBehaviour
{
    private static readonly int Tint = Shader.PropertyToID("_Color");
    private Transform tread;
    private Renderer framePaint, treadPaint;
    private MaterialPropertyBlock paint;
    private PuzzleObjectKind kind;
    private float cellSize;
    private bool painted;
    internal bool Pressed { get; private set; }

    internal static EncounterPuzzlePiece Create(TileWorldDungeon dungeon, PuzzleObjectPlacement placement)
    {
        var root = new GameObject(placement.Id);
        root.transform.SetParent(dungeon.transform, false);
        var piece = root.AddComponent<EncounterPuzzlePiece>();
        piece.kind = placement.Kind;
        piece.cellSize = dungeon.CellSize;
        piece.MoveTo(dungeon, placement.Position, false);
        if (placement.Kind == PuzzleObjectKind.Boulder)
        {
            var rock = Instantiate(DioramaCatalog.Load().Get("BoulderLarge").Prefab, root.transform, false);
            rock.transform.localRotation = Quaternion.Euler(0, 0, (uint)EncounterRuntime.StableHash(placement.Id) % 360);
            var renderer = rock.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterial = GameContent.Load<Material>("DungeonProps/PuzzleStone");
            var bounds = renderer.bounds;
            float width = piece.cellSize * .72f;
            rock.transform.localScale *= width / Mathf.Max(bounds.size.x, bounds.size.y);
            DungeonPickupFootprint.Place(rock.transform, root.transform.position, width);
        }
        else
        {
            var material = GameContent.Load<Material>("DungeonProps/PressurePlate");
            Transform AddPart(string name, out Renderer renderer)
            {
                var part = Instantiate(GameContent.Load<GameObject>("DungeonProps/" + name), root.transform, false);
                part.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                part.transform.localScale = Vector3.one * piece.cellSize;
                renderer = part.GetComponentInChildren<MeshRenderer>();
                renderer.sharedMaterial = material;
                return part.transform;
            }
            AddPart("PressurePlateFrame", out piece.framePaint);
            piece.tread = AddPart("PressurePlateTread", out piece.treadPaint);
            piece.paint = new MaterialPropertyBlock();
            piece.SetPressed(placement.Kind == PuzzleObjectKind.LightTarget ? placement.TargetState : placement.InitialState);
        }
        return piece;
    }

    internal void MoveTo(TileWorldDungeon dungeon, GridPoint cell, bool onPlate)
    {
        var center = dungeon.CellCenterToWorld(cell.ToCell());
        center.z = DungeonPresentation.GroundPlaneZ - (onPlate ? cellSize * .058f : 0);
        transform.position = center;
    }

    internal void SetPressed(bool pressed)
    {
        if (painted && Pressed == pressed) return;
        painted = true;
        Pressed = pressed;
        tread.localPosition = Vector3.back * (cellSize * (pressed ? .021f : .072f));
        Color face = kind switch
        {
            PuzzleObjectKind.Goal => pressed ? new Color(.65f, 1f, .64f) : new Color(.57f, .86f, .72f),
            PuzzleObjectKind.Trap => new Color(1f, .5f, .38f),
            PuzzleObjectKind.Destination => new Color(.45f, .94f, 1f),
            _ => pressed ? new Color(1f, .91f, .42f) : new Color(.44f, .51f, .62f)
        };
        paint.SetColor(Tint, face);
        treadPaint.SetPropertyBlock(paint);
        paint.SetColor(Tint, pressed ? Color.Lerp(Color.white, face, .62f) : Color.white);
        framePaint.SetPropertyBlock(paint);
    }
}
