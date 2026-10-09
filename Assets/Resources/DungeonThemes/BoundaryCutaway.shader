Shader "Eternal Enigma/Dungeon Boundary Cutaway"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
        _Metallic ("Metallic", Range(0,1)) = 0
        _FloorMask ("Walkable floor", 2D) = "black" {}
        _FloorRect ("Floor bounds", Vector) = (0,0,1,1)
        _GroundZ ("Ground plane", Float) = 0.001
        _WholeCell ("Hide entire supported decoration", Float) = 0
        _CellSize ("Cell size", Float) = 2
        _CutawayHeight ("Wall height", Float) = 4.23
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _FloorMask;
        fixed4 _Color;
        half _Glossiness, _Metallic;
        float4 _FloorRect;
        float _GroundZ;
        float _WholeCell, _CellSize, _CutawayHeight;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // The camera stays unchanged. Remove only wall fragments whose view ray
            // crosses walkable floor, keeping tall back and side walls fully visible.
            float3 direction=-UNITY_MATRIX_V[2].xyz;
            float3 samplePoint=IN.worldPos;
            if(_WholeCell>.5)
            {
                samplePoint.xy=(floor((samplePoint.xy-_FloorRect.xy)/_CellSize)+.5)*_CellSize+_FloorRect.xy;
                samplePoint.z=_GroundZ-_CutawayHeight;
            }
            if(samplePoint.z<_GroundZ-.05 && direction.z>.001)
            {
                float2 end=samplePoint.xy+direction.xy*((_GroundZ-samplePoint.z)/direction.z);
                float covered=0;
                [unroll] for(int i=1;i<=12;i++)
                {
                    float2 uv=(lerp(samplePoint.xy,end,i/12.0)-_FloorRect.xy)/_FloorRect.zw;
                    float inside=step(0,uv.x)*step(0,uv.y)*(1-step(1,uv.x))*(1-step(1,uv.y));
                    covered=max(covered,tex2D(_FloorMask,uv).r*inside);
                }
                clip(.5-covered);
            }
            fixed4 color=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=color.rgb;o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
