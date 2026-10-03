using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded mesh glow, with a global nearest-visible budget; never creates lights.</summary>
public sealed class BiomeDecorationEffect : MonoBehaviour
{
    private static readonly List<BiomeDecorationEffect> live = new();
    private static readonly List<BiomeDecorationEffect> visible = new();
    private static int lastFrame = -1;
    public bool FloorVisible = true;
    private Renderer visual;
    private Vector3 original;
    private bool initialized;
    private static readonly Plane[] planes=new Plane[6];
    public void SetScale(Vector3 scale) { original=scale;transform.localScale=scale;initialized=true; }
    private void OnEnable() { visual = GetComponent<Renderer>(); if(!initialized)SetScale(transform.localScale);live.Add(this); }
    private void OnDisable() { live.Remove(this); }
    private void LateUpdate()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        var camera = Camera.main;
        if (camera == null) { foreach (var e in live) if(e.visual != null) e.visual.enabled=false; return; }
        GeometryUtility.CalculateFrustumPlanes(camera,planes);
        visible.Clear();
        foreach (var e in live) {
            if(e.visual == null) continue;
            e.visual.enabled = false;
            if(e.FloorVisible && GeometryUtility.TestPlanesAABB(planes,e.visual.bounds)) visible.Add(e);
        }
        visible.Sort((a,b) => (a.transform.position-camera.transform.position).sqrMagnitude.CompareTo((b.transform.position-camera.transform.position).sqrMagnitude));
        for(int i=0;i<Mathf.Min(32,visible.Count);i++) {
            var e=visible[i]; e.visual.enabled=true;
            float phase=e.transform.position.x*.73f+e.transform.position.y*.39f;
            e.transform.localScale=e.original*(.92f+.08f*Mathf.Sin(Time.time*2.3f+phase));
        }
    }
}
