using System.Linq;
using UnityEngine;

public sealed class BiomeSignLabel : MonoBehaviour
{
    public string DisplayName;
    public Vector3 Direction;
    public float CameraOffset;
    private Renderer visual;
    private MeshRenderer[] bodies;
    private Vector3 anchor;
    private bool anchored;
    private TMPro.TextMeshPro label;
    private string previousArrow;
    private static readonly Plane[] planes=new Plane[6];
    private static int frame=-1;
    private static Camera frameCamera;
    private void LateUpdate()=>Face(Camera.main);
    public void Face(Camera camera)
    {
        if(camera==null)return;
        if(!anchored){anchor=transform.localPosition;anchored=true;}
        transform.position=transform.parent.TransformPoint(anchor)-camera.transform.forward*CameraOffset;
        if(visual==null)visual=GetComponent<Renderer>();
        transform.rotation=camera.transform.rotation;
        if(!string.IsNullOrEmpty(DisplayName)) {
            if(label==null)label=GetComponent<TMPro.TextMeshPro>();
            var projected=camera.transform.InverseTransformDirection(Direction);
            string arrow=Mathf.Abs(projected.x)>Mathf.Abs(projected.y)?projected.x>0?"→":"←":projected.y>0?"↑":"↓";
            if(arrow!=previousArrow){label.text=arrow+" "+DisplayName;previousArrow=arrow;}
        }
        if(frame!=Time.frameCount||frameCamera!=camera){GeometryUtility.CalculateFrustumPlanes(camera,planes);frame=Time.frameCount;frameCamera=camera;}
        if(bodies==null)bodies=transform.parent.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TMPro.TMP_Text>()==null).ToArray();
        var bounds=bodies.Length>0?bodies[0].bounds:new Bounds(transform.parent.position,Vector3.zero);
        for(int i=1;i<bodies.Length;i++)if(bodies[i]!=null)bounds.Encapsulate(bodies[i].bounds);
        if(visual!=null)visual.enabled=GeometryUtility.TestPlanesAABB(planes,bounds);
    }
}
