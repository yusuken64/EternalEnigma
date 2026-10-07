Shader "EternalEnigma/Diorama Cliff"
{
    Properties { _RockTex ("Strata", 2D) = "white" {} _TopTex ("Biome lip", 2D) = "white" {} _Color ("Stone tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows nolightmap nodynlightmap
        #pragma target 3.0
        sampler2D _RockTex,_TopTex;fixed4 _Color;
        struct Input {float3 worldPos;float4 color:COLOR;};
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 stone=tex2D(_RockTex,IN.worldPos.xy*.35).rgb*_Color.rgb;
            float3 top=tex2D(_TopTex,IN.worldPos.xy*.25).rgb;
            o.Albedo=lerp(stone,top,saturate(IN.color.r))*IN.color.g*.86;
            o.Smoothness=.06;o.Metallic=0;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
