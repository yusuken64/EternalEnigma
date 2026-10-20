Shader "EternalEnigma/Painted Ground"
{
    Properties
    {
        _Grass ("Grass", 2D) = "white" {}
        _Sand ("Sand", 2D) = "white" {}
        _Mountain ("Mountain", 2D) = "white" {}
        _Forest ("Forest", 2D) = "white" {}
        _Snow ("Snow", 2D) = "white" {}
        _Marsh ("Marsh", 2D) = "white" {}
        _Ash ("Ash", 2D) = "white" {}
        _Dirt ("Dirt", 2D) = "white" {}
        _Cobble ("Cobble", 2D) = "white" {}
        _TileSize ("Painted repeat in world units", Float) = 4
        _Color ("Ground albedo tint", Color) = (.8,.84,.8,1)
        _DetailSoftness ("Texture mip softness", Range(0,3)) = 0
        _DetailContrast ("Surface contrast", Range(0,1)) = 1
        _DetailStrength ("Painted detail strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows nolightmap nodynlightmap
        #pragma target 3.0
        sampler2D _Grass,_Sand,_Mountain,_Forest,_Snow,_Marsh,_Ash,_Dirt,_Cobble;
        float _TileSize, _DetailSoftness, _DetailContrast, _DetailStrength;fixed4 _Color;
        struct Input { float3 worldPos; float4 weights0; float4 weights1; float cobble; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.weights0=v.color; o.weights1=v.texcoord1; o.cobble=v.texcoord2.x;
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float2 uv=IN.worldPos.xy/max(.1,_TileSize);
            float noise=frac(sin(dot(floor(IN.worldPos.xy*13),float2(12.9898,78.233)))*43758.5453)-.5;
            float4 a=max(0,IN.weights0*(1+noise*float4(.25,-.25,.18,-.18)));
            float4 b=max(0,IN.weights1*(1+noise*float4(-.2,.2,.3,-.3)));
            float c=max(0,IN.cobble); float total=max(.0001,dot(a,1)+dot(b,1)+c);
            float4 sampleUV=float4(uv,0,_DetailSoftness);
            float3 color=tex2Dbias(_Grass,sampleUV).rgb*a.x+tex2Dbias(_Sand,sampleUV).rgb*a.y+tex2Dbias(_Mountain,sampleUV).rgb*a.z+tex2Dbias(_Forest,sampleUV).rgb*a.w
                +tex2Dbias(_Snow,sampleUV).rgb*b.x+tex2Dbias(_Marsh,sampleUV).rgb*b.y+tex2Dbias(_Ash,sampleUV).rgb*b.z+tex2Dbias(_Dirt,sampleUV).rgb*b.w+tex2Dbias(_Cobble,sampleUV).rgb*c;
            float rim=saturate(min(a.x+a.w,b.w)*5)*.16;
            color/=total;
            float3 broad=(float3(.35,.46,.25)*a.x+float3(.68,.57,.38)*a.y+float3(.44,.46,.43)*a.z+float3(.24,.35,.23)*a.w+
                float3(.77,.80,.80)*b.x+float3(.29,.36,.23)*b.y+float3(.28,.27,.29)*b.z+float3(.48,.36,.24)*b.w+float3(.43,.43,.40)*c)/total;
            color=lerp(broad,color,_DetailStrength);
            float luminance=dot(color,float3(.299,.587,.114));
            float quiet=.36+(luminance-.36)*_DetailContrast;
            color*=quiet/max(luminance,.01);
            o.Albedo=color*(1-rim)*_Color.rgb; o.Metallic=0; o.Smoothness=.06; o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
