using UnityEngine;

public static class DioramaCliffGeometry
{
    public const float Height=.42f;
    public static Mesh Adapt(Mesh source,int neighbors,int diagonals)
    {
        // Deform the Blender Fill lattice for concave combinations. Shared border
        // heights use exactly the same terrace profile as the six authored pieces.
        var mesh=Object.Instantiate(source);mesh.name="Diorama concave cliff "+neighbors+"/"+diagonals;mesh.hideFlags=HideFlags.DontSave;
        var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++)
        {
            var p=vertices[i];float n=.5f-p.y,e=.5f-p.x,s=.5f+p.y,w=.5f+p.x,d=2;
            if((neighbors&1)==0)d=Mathf.Min(d,n);if((neighbors&2)==0)d=Mathf.Min(d,e);
            if((neighbors&4)==0)d=Mathf.Min(d,s);if((neighbors&8)==0)d=Mathf.Min(d,w);
            if((neighbors&3)==3&&(diagonals&1)==0)d=Mathf.Min(d,Mathf.Sqrt(n*n+e*e));
            if((neighbors&6)==6&&(diagonals&2)==0)d=Mathf.Min(d,Mathf.Sqrt(s*s+e*e));
            if((neighbors&12)==12&&(diagonals&4)==0)d=Mathf.Min(d,Mathf.Sqrt(s*s+w*w));
            if((neighbors&9)==9&&(diagonals&8)==0)d=Mathf.Min(d,Mathf.Sqrt(n*n+w*w));
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(d/.26f));
            vertices[i].z=-Height*(t>0&&t<1?.8f*t+.2f*Mathf.Round(t*4)/4:t);
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();Paint(mesh);return mesh;
    }
    public static void Paint(Mesh mesh)
    {
        var vertices=mesh.vertices;var colors=new Color[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            float t=Mathf.Clamp01(-vertices[i].z/Height);
            colors[i]=new Color(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.92f,1,t)),.75f+.23f*t-.10f*(.5f+.5f*Mathf.Sin(t*24)),1,1);
        }
        mesh.colors=colors;
    }
}
