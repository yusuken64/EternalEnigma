Shader "EternalEnigma/Animated Ocean"
{
    Properties
    {
        _MainTex ("Wave texture", 2D) = "white" {}
        _Color ("Water tint", Color) = (0.2,0.47,0.64,1)
        _WaveSpeed ("Two wave directions", Vector) = (0.018,0.007,-0.009,0.013)
        _WaveStrength ("Wave contrast", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _WaveSpeed;
            float _WaveStrength;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                // The authored texture is painted blue water, not a grayscale
                // height map. Preserve its color while the two wave fields move.
                float3 a=tex2D(_MainTex,i.uv+_Time.y*_WaveSpeed.xy).rgb;
                float3 b=tex2D(_MainTex,i.uv*1.37+_Time.y*_WaveSpeed.zw).rgb;
                return fixed4(lerp(a,b,saturate(_WaveStrength)*.5)*_Color.rgb,1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
