Shader "EternalEnigma/Ocean Noise"
{
    Properties
    {
        _MainTex ("Broad noise", 2D) = "gray" {}
        _Color ("Deep variation", Color) = (.04,.17,.30,.3)
        _LightColor ("Light variation", Color) = (.3,.65,.64,.3)
        _Speed ("Drift", Vector) = (.0007,.0004,0,0)
        _MapSize ("Land bounds", Vector) = (512,512,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_ST,_Color,_LightColor,_Speed,_MapSize;
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;};
            Output vert(Input v) {Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.world=v.uv*8;return o;}
            fixed4 frag(Output i):SV_Target
            {
                float n=tex2D(_MainTex,i.uv+_Time.y*_Speed.xy).r;
                fixed4 color=lerp(_Color,_LightColor,smoothstep(.15,.85,n));
                float distance=max(max(-i.world.x,i.world.x-_MapSize.x),max(-i.world.y,i.world.y-_MapSize.y));
                color.a*=smoothstep(0,16,distance);return color;
            }
            ENDCG
        }
    }
}
