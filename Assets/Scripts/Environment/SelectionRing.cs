using System.Collections.Generic;
using UnityEngine;

public static class SelectionRing
{
    private static readonly Dictionary<(Sprite, bool),Sprite> variants=new();
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
    public static void Apply(SpriteRenderer renderer, bool hostile = false)
    {
        if(renderer==null || renderer.sprite==null || renderer.sprite.name=="Jade selection ring" || renderer.sprite.name=="Hostile outline")return;
        var original=renderer.sprite;
        if(!variants.TryGetValue((original, hostile),out var ring))
        {
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Selection ring",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float r=new Vector2(x+.5f-size*.5f,y+.5f-size*.5f).magnitude;
                float angle = Mathf.Atan2(y+.5f-size*.5f, x+.5f-size*.5f);
                // Four small gaps distinguish threats from the continuous allied ring.
                float gap = hostile ? Mathf.Clamp01((Mathf.Abs(Mathf.Sin(angle * 2)) - .12f) * 12) : 1;
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(63-r)*Mathf.Clamp01(r-(hostile ? 58 : 55))*gap);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            ring=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size/original.bounds.size.x);
            ring.name=hostile ? "Hostile outline" : "Jade selection ring";ring.hideFlags=HideFlags.HideAndDontSave;variants.Add((original,hostile),ring);
        }
        renderer.sprite=ring;
    }
}
