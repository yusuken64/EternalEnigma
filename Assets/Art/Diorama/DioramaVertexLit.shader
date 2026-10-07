Shader "EternalEnigma/Diorama Vertex Lit"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) _Wind ("Foliage motion", Range(0,.04)) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows nolightmap nodynlightmap
        #pragma target 3.0
        fixed4 _Color;float _Wind;
        struct Input { float4 color : COLOR; };
        void vert(inout appdata_full v)
        {
            float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
            float sway=sin(_Time.y*1.35+p.x*.77+p.y*.48)*_Wind*smoothstep(.35,2.4,-p.z);
            p.xy+=float2(sway,sway*.45);v.vertex=mul(unity_WorldToObject,float4(p,1));
        }
        void surf(Input IN,inout SurfaceOutputStandard o) {o.Albedo=IN.color.rgb*_Color.rgb;o.Smoothness=.08;o.Metallic=0;o.Alpha=1;}
        ENDCG
    }
    Fallback "Diffuse"
}
