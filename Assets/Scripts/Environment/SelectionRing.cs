using System.Collections.Generic;
using UnityEngine;

public static class SelectionRing
{
    private static readonly Dictionary<Sprite,Sprite> variants=new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach(var ring in variants.Values)
        {
            if(ring==null)continue;
            Object.Destroy(ring.texture);Object.Destroy(ring);
        }
        variants.Clear();
        Application.quitting-=Reset;Application.quitting+=Reset;
    }
    public static void Apply(SpriteRenderer renderer)
    {
        if(renderer==null || renderer.sprite==null || renderer.sprite.name=="Jade selection ring")return;
        var original=renderer.sprite;
        if(!variants.TryGetValue(original,out var ring))
        {
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Selection ring",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float r=new Vector2(x+.5f-size*.5f,y+.5f-size*.5f).magnitude;
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(63-r)*Mathf.Clamp01(r-55));
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            ring=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size/original.bounds.size.x);
            ring.name="Jade selection ring";ring.hideFlags=HideFlags.HideAndDontSave;variants.Add(original,ring);
        }
        renderer.sprite=ring;
    }
}
