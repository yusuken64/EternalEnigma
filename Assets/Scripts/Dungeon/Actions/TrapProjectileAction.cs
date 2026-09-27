using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Presentation only: damage is resolved separately, once, before playback.</summary>
internal sealed class TrapProjectileAction : GameAction
{
    readonly Character target;
    readonly string model;
    Vector3Int impactCell;
    internal TrapProjectileAction(Character target, string model) { this.target = target; this.model = model; TrackAnimationTarget(target); }
    internal override bool IsValid(Character c) => true;
    internal override List<GameAction> ExecuteImmediate(Character c) { impactCell = target.TilemapPosition; return new(); }
    internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false)
    {
        if (skipAnimation) yield break;
        var game = Game.Instance;
        var camera = game.PlayerController.CameraController?.Camera;
        if (camera == null) yield break;
        float size = game.CurrentDungeon.CellToWorld(Vector3Int.right).x;
        var impact = game.CurrentDungeon.CellToWorld(impactCell) + new Vector3(size * .5f, size * .5f, -size * .35f);
        var screen = camera.WorldToViewportPoint(impact);
        if (screen.z <= 0) yield break;
        // The whole projectile begins outside the viewport, including on ultrawide cameras.
        var start = camera.ViewportToWorldPoint(new Vector3(-.2f, screen.y, screen.z));
        var root = new GameObject(model + " flight");
        try
        {
            var mesh = Resources.Load<Mesh>("DungeonProps/" + model);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("DungeonProps/Palette");
            float scale = size * (model == "LogProjectile" ? 1.1f : .8f) / mesh.bounds.size.x;
            root.transform.localScale = Vector3.one * scale;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.right, (impact - start).normalized);
            var offset = root.transform.rotation * (mesh.bounds.center * scale);
            root.transform.position = start - offset;
            yield return root.transform.DOMove(impact - offset, model == "LogProjectile" ? .48f : .28f).SetEase(Ease.Linear).WaitForCompletion();
        }
        finally { if (root != null) { root.transform.DOKill(); Object.Destroy(root); } }
    }
    internal override IEnumerable<Vector3Int> AnimationCells(Character c) { yield return impactCell; }
}
