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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows nolightmap nodynlightmap
        #pragma target 3.0
        sampler2D _Grass,_Sand,_Mountain,_Forest,_Snow,_Marsh,_Ash,_Dirt,_Cobble;
        float _TileSize;fixed4 _Color;
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
            float3 color=tex2D(_Grass,uv).rgb*a.x+tex2D(_Sand,uv).rgb*a.y+tex2D(_Mountain,uv).rgb*a.z+tex2D(_Forest,uv).rgb*a.w
                +tex2D(_Snow,uv).rgb*b.x+tex2D(_Marsh,uv).rgb*b.y+tex2D(_Ash,uv).rgb*b.z+tex2D(_Dirt,uv).rgb*b.w+tex2D(_Cobble,uv).rgb*c;
            float rim=saturate(min(a.x+a.w,b.w)*5)*.16;
            o.Albedo=color/total*(1-rim)*_Color.rgb; o.Metallic=0; o.Smoothness=.06; o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
