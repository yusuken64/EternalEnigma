using System.Linq;
using TWC;
using UnityEngine;

/// <summary>Camera-side wall lowering and clipping, using the existing floor mask.</summary>
[ExecuteAlways]
public sealed class DungeonBoundaryCutaway : MonoBehaviour
{
    public Texture2D FloorMask;
    public Texture2D ForegroundWallMask;
    public Vector4 FloorRect;
    public float GroundZ;
    public float CellSize,Height;
    internal float ForegroundHeight => CellSize * .25f;

    private void OnEnable() => Camera.onPreCull += SetView;
    private void OnDisable() => Camera.onPreCull -= SetView;

    private void SetView(Camera camera)
    {
        var direction = camera.transform.forward;
        var center = camera.transform.position;
        if (direction.z > .001f) center += direction * ((GroundZ - center.z) / direction.z);
        var forward = direction.z > .001f ? ((Vector2)direction).normalized : Vector2.zero;
        Shader.SetGlobalVector("_DungeonWallView", new Vector4(center.x, center.y, forward.x, forward.y));
        Shader.SetGlobalVector("_DungeonWallViewDirection", direction);
    }

    public static void Install(TileWorldCreator creator, GameObject root, EnvironmentMeshOwner owner, BiomeDecorationSurfaceSet faces, bool[,] walls)
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
        var foregroundWalls = new Texture2D(width,height,TextureFormat.RGBA32,false,true) {
            name="Dungeon foreground wall mask",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
        var wallPixels = new Color32[pixels.Length];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            bool pillar = DungeonThemeTileLayer.At(walls,x,y) && DungeonBoundaryLayer.Neighbors(walls,x,y).cardinal == 0;
            bool southOfFloor = false;
            if (!pillar && pixels[y*width+x].r == 0)
                for(int distance=1;distance<=3 && y+distance<height;distance++)
                    southOfFloor |= pixels[(y+distance)*width+x].r != 0;
            // Red is the continuous lowering field; green is sampled at cell centers
            // so a neighboring wall's bilinear falloff never shortens a single pillar.
            wallPixels[y*width+x] = new Color32((byte)(southOfFloor?255:0),(byte)(pillar?0:255),0,255);
        }
        foregroundWalls.SetPixels32(wallPixels);foregroundWalls.Apply();owner.Textures.Add(foregroundWalls);
        var cutaway=root.AddComponent<DungeonBoundaryCutaway>();cutaway.FloorMask=texture;
        cutaway.ForegroundWallMask=foregroundWalls;
        var origin=creator.worldObject.transform.position;
        cutaway.FloorRect=new Vector4(origin.x,origin.y,width*creator.twcAsset.cellSize,height*creator.twcAsset.cellSize);
        cutaway.GroundZ=root.transform.position.z;
        cutaway.CellSize=creator.twcAsset.cellSize;
        cutaway.Height=cutaway.GroundZ-root.GetComponentsInChildren<MeshRenderer>().Min(r=>r.bounds.min.z);
        cutaway.ApplyMaterials(root,owner);
        var cosmetics=creator.worldObject.transform.Find("Theme cosmetics");
        if(cosmetics!=null)cutaway.ApplyMaterials(cosmetics.gameObject,cosmetics.GetComponent<EnvironmentMeshOwner>(),true);
        // Keep decoration sockets within the part of a lowered wall that always remains.
        // Foreground sockets still require a complete, unclipped supporting rectangle.
        var view=new Vector3(0,12,14);
        faces.Faces.RemoveAll(face=>{
            var side=Vector3.Cross(face.Normal,Vector3.forward).normalized*face.Width*.5f;
            bool lowers = new[]{-1f,0,1f}.Any(x=>cutaway.LowersSupportingFace(
                root.transform.TransformPoint(face.Center+side*x-Vector3.forward*face.Height*.5f)));
            if (lowers)
            {
                float bottom = face.Center.z + face.Height * .5f;
                float top = Mathf.Max(face.Center.z - face.Height * .5f, -cutaway.ForegroundHeight);
                face.Height = bottom - top;
                if (face.Height <= .01f) return true;
                face.Center.z = (bottom + top) * .5f;
            }
            foreach(float x in new[]{-1f,0,1f})foreach(float z in new[]{-1f,0,1f})
            {
                var point=root.transform.TransformPoint(face.Center+side*x+Vector3.forward*face.Height*.5f*z);
                if(cutaway.Cuts(point,view))return true;
            }
            return false;
        });
    }

    public void ApplyMaterials(GameObject root,EnvironmentMeshOwner owner,bool wholeCell=false)
    {
        if(owner==null)return;
        var shader=GameContent.Load<Shader>("DungeonThemes/BoundaryCutaway");
        foreach(var group in root.GetComponentsInChildren<MeshRenderer>()
            .Where(r=>r.GetComponent<SilhouetteParticipant>()?.Role==SilhouetteRole.Caster).GroupBy(r=>r.sharedMaterial))
        {
            var material=group.Key;
            if(material.shader!=shader || !owner.Materials.Contains(material))
            {material=new Material(material){name=material.name+" dungeon cutaway",shader=shader,hideFlags=HideFlags.DontSave};owner.Materials.Add(material);}
            material.SetTexture("_FloorMask",FloorMask);material.SetVector("_FloorRect",FloorRect);material.SetFloat("_GroundZ",GroundZ);
            material.SetTexture("_ForegroundWallMask",ForegroundWallMask);
            material.SetFloat("_WholeCell",wholeCell?1:0);material.SetFloat("_CellSize",CellSize);material.SetFloat("_CutawayHeight",Height);
            material.SetFloat("_ForegroundWallHeight",ForegroundHeight);
            foreach(var renderer in group)renderer.sharedMaterial=material;
        }
    }

    private bool LowersSupportingFace(Vector3 point)
    {
        if (FloorMask == null || point.z >= GroundZ - ForegroundHeight) return false;
        var sample = (Vector2)point;
        float u=(sample.x-FloorRect.x)/FloorRect.z, v=(sample.y-FloorRect.y)/FloorRect.w;
        return u>=0 && u<1 && v>=0 && v<1 &&
            ForegroundWallMask.GetPixel((int)(u*ForegroundWallMask.width),(int)(v*ForegroundWallMask.height)).g>.5f &&
            ForegroundWallMask.GetPixelBilinear(u,v).r>.01f;
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
