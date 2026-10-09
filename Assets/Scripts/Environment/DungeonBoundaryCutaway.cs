using System.Linq;
using TWC;
using UnityEngine;

/// <summary>Dungeon-only foreground cutaway, derived from the existing floor mask.</summary>
public sealed class DungeonBoundaryCutaway : MonoBehaviour
{
    public Texture2D FloorMask;
    public Vector4 FloorRect;
    public float GroundZ;
    public float CellSize,Height;

    public static void Install(TileWorldCreator creator, GameObject root, EnvironmentMeshOwner owner, BiomeDecorationSurfaceSet faces)
    {
        if(owner.Meshes.Count==0)return;
        var maps=creator.twcAsset.mapBuildLayers.OfType<DungeonThemeTileLayer>()
            .Where(l=>l.active && l.Role is DungeonThemeRole.Floor or DungeonThemeRole.Accent)
            .Select(l=>creator.GetMapOutputFromBlueprintLayer(l.assignedGenerationLayerGuid)).Where(m=>m!=null).ToArray();
        if(maps.Length==0)return;
        int width=maps.Max(m=>m.GetLength(0)),height=maps.Max(m=>m.GetLength(1));
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true) {
            name="Dungeon cutaway floor mask",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
        var pixels=new Color32[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            pixels[y*width+x]=maps.Any(m=>DungeonThemeTileLayer.At(m,x,y))?new Color32(255,255,255,255):new Color32(0,0,0,255);
        texture.SetPixels32(pixels);texture.Apply();owner.Textures.Add(texture);
        var cutaway=root.AddComponent<DungeonBoundaryCutaway>();cutaway.FloorMask=texture;
        var origin=creator.worldObject.transform.position;
        cutaway.FloorRect=new Vector4(origin.x,origin.y,width*creator.twcAsset.cellSize,height*creator.twcAsset.cellSize);
        cutaway.GroundZ=root.transform.position.z;
        cutaway.CellSize=creator.twcAsset.cellSize;
        cutaway.Height=cutaway.GroundZ-root.GetComponentsInChildren<MeshRenderer>().Min(r=>r.bounds.min.z);
        cutaway.ApplyMaterials(root,owner);
        var cosmetics=creator.worldObject.transform.Find("Theme cosmetics");
        if(cosmetics!=null)cutaway.ApplyMaterials(cosmetics.gameObject,cosmetics.GetComponent<EnvironmentMeshOwner>(),true);
        // Decorations need a complete supporting face. Keep cosmetic placement callbacks
        // unchanged and omit faces whose rectangle is cut by the gameplay view.
        var view=new Vector3(0,12,14);
        faces.Faces.RemoveAll(face=>{
            var side=Vector3.Cross(face.Normal,Vector3.forward).normalized*face.Width*.5f;
            foreach(float x in new[]{-1f,0,1f})foreach(float z in new[]{-1f,0,1f})
                if(cutaway.Cuts(root.transform.TransformPoint(face.Center+side*x+Vector3.forward*face.Height*.5f*z),view))return true;
            return false;
        });
    }

    public void ApplyMaterials(GameObject root,EnvironmentMeshOwner owner,bool wholeCell=false)
    {
        if(owner==null)return;
        var shader=Resources.Load<Shader>("DungeonThemes/BoundaryCutaway");
        foreach(var group in root.GetComponentsInChildren<MeshRenderer>()
            .Where(r=>r.GetComponent<SilhouetteParticipant>()?.Role==SilhouetteRole.Caster).GroupBy(r=>r.sharedMaterial))
        {
            var material=group.Key;
            if(material.shader!=shader || !owner.Materials.Contains(material))
            {material=new Material(material){name=material.name+" dungeon cutaway",shader=shader,hideFlags=HideFlags.DontSave};owner.Materials.Add(material);}
            material.SetTexture("_FloorMask",FloorMask);material.SetVector("_FloorRect",FloorRect);material.SetFloat("_GroundZ",GroundZ);
            material.SetFloat("_WholeCell",wholeCell?1:0);material.SetFloat("_CellSize",CellSize);material.SetFloat("_CutawayHeight",Height);
            foreach(var renderer in group)renderer.sharedMaterial=material;
        }
    }

    public bool Cuts(Vector3 point,Vector3 direction)
    {
        if(FloorMask==null || point.z>=GroundZ-.05f || direction.z<=.001f)return false;
        var end=point+direction*((GroundZ-point.z)/direction.z);
        for(int i=1;i<=12;i++)
        {
            var p=Vector3.Lerp(point,end,i/12f);
            float u=(p.x-FloorRect.x)/FloorRect.z,v=(p.y-FloorRect.y)/FloorRect.w;
            if(u>=0&&u<1&&v>=0&&v<1&&FloorMask.GetPixel((int)(u*FloorMask.width),(int)(v*FloorMask.height)).r>.5f)return true;
        }
        return false;
    }
}
