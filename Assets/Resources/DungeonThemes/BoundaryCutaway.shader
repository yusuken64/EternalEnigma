Shader "Eternal Enigma/Dungeon Boundary Cutaway"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
        _Metallic ("Metallic", Range(0,1)) = 0
        _FloorMask ("Walkable floor", 2D) = "black" {}
        _ForegroundWallMask ("Foreground wall lowering field", 2D) = "black" {}
        _FloorRect ("Floor bounds", Vector) = (0,0,1,1)
        _GroundZ ("Ground plane", Float) = 0.001
        _WholeCell ("Hide entire supported decoration", Float) = 0
        _CellSize ("Cell size", Float) = 2
        _CutawayHeight ("Wall height", Float) = 4.23
        _ForegroundWallHeight ("Nearby foreground wall height", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        #include "BoundaryCutaway.cginc"
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        void vert(inout appdata_full v)
        {
            float3 world = DungeonWallVertex(mul(unity_ObjectToWorld, v.vertex).xyz);
            v.vertex = mul(unity_WorldToObject, float4(world, 1));
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            DungeonWallClip(IN.worldPos);
            fixed4 color=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=color.rgb;o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
