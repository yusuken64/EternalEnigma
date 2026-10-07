Shader "EternalEnigma/Diorama Foliage"
{
    Properties
    {
        _MainTex ("Painted atlas", 2D) = "white" {}
        _Color ("Biome tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = .08
        _Metallic ("Metallic", Range(0,1)) = 0
        _Wind ("Canopy motion", Range(0,.1)) = .018
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic, _Wind;
        struct Input { float2 uv_MainTex; };
        void vert(inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            // Both adapters and TWC batches use XY ground with negative Z elevation.
            float canopy = smoothstep(.45, 2.3, -world.z);
            world.x += sin(world.y * 1.7 + world.x * .65 + _Time.y * 1.4) * canopy * _Wind;
            world.y += cos(world.x * .9 + _Time.y) * canopy * _Wind * .4;
            v.vertex.xyz = mul(unity_WorldToObject, float4(world,1)).xyz;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb; o.Metallic = _Metallic; o.Smoothness = _Glossiness; o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
