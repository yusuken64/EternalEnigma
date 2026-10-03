using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded mesh glow and four pooled nearest-visible non-shadow-casting accents.</summary>
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
    private static readonly Light[] accents=new Light[4];
    private static Vector3 cameraPosition;
    private static readonly System.Comparison<BiomeDecorationEffect> compareDistance=(a,b)=>(a.transform.position-cameraPosition).sqrMagnitude.CompareTo((b.transform.position-cameraPosition).sqrMagnitude);
    public void SetScale(Vector3 scale) { original=scale;transform.localScale=scale;initialized=true; }
    private void OnEnable() { visual = GetComponent<Renderer>(); if(!initialized)SetScale(transform.localScale);live.Add(this); }
    private void OnDisable()
    {
        live.Remove(this);
        if(live.Count==0)for(int i=0;i<accents.Length;i++)if(accents[i]!=null){Destroy(accents[i].gameObject);accents[i]=null;}
    }
    private void LateUpdate()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        var camera = Camera.main;
        for(int i=0;i<accents.Length;i++)if(accents[i]!=null)accents[i].enabled=false;
        if (camera == null) { foreach (var e in live) if(e.visual != null) e.visual.enabled=false; return; }
        GeometryUtility.CalculateFrustumPlanes(camera,planes);
        visible.Clear();
        foreach (var e in live) {
            if(e.visual == null) continue;
            e.visual.enabled = false;
            if(e.FloorVisible && GeometryUtility.TestPlanesAABB(planes,e.visual.bounds)) visible.Add(e);
        }
        cameraPosition=camera.transform.position;
        visible.Sort(compareDistance);
        for(int i=0;i<Mathf.Min(32,visible.Count);i++) {
            var e=visible[i]; e.visual.enabled=true;
            float phase=e.transform.position.x*.73f+e.transform.position.y*.39f;
            e.transform.localScale=e.original*(.92f+.08f*Mathf.Sin(Time.time*2.3f+phase));
            if(i<accents.Length)
            {
                if(accents[i]==null)
                {
                    var light=new GameObject("Fixture accent").AddComponent<Light>();
                    light.type=LightType.Point;light.shadows=LightShadows.None;light.range=3.5f;light.intensity=.55f;
                    light.color=new Color(1,.73f,.42f);accents[i]=light;
                }
                accents[i].transform.position=e.transform.position+Vector3.back*.35f;accents[i].enabled=true;
            }
        }
    }
}
