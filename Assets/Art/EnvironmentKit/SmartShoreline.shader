Shader "EternalEnigma/Smart Shoreline"
{
    Properties
    {
        _MainTex ("Wave texture", 2D) = "white" {}
        _NoiseTex ("Shore noise", 2D) = "gray" {}
        _SandColor ("Sand", Color) = (.66,.59,.4,1)
        _ShallowColor ("Shallows", Color) = (.24,.64,.67,1)
        _FoamColor ("Foam", Color) = (.83,.94,.88,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_NoiseTex;float4 _SandColor,_ShallowColor,_FoamColor;
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;};
            Output vert(Input v) {Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
            fixed4 frag(Output i):SV_Target
            {
                float n=tex2D(_NoiseTex,i.world*.037).r;
                float wave=tex2D(_MainTex,i.world*.12+_Time.y*float2(.018,.01)).r;
                float t=i.uv.y+(n-.5)*.18;
                float foam=1-smoothstep(.018,.065,abs(t-(.22+(wave-.8)*.2)));
                float3 color=lerp(_SandColor.rgb,_ShallowColor.rgb,smoothstep(.14,.3,t));
                color=lerp(color,_FoamColor.rgb,foam*.85);
                float alpha=smoothstep(0,.06,i.uv.y)*(1-smoothstep(.3,1,i.uv.y));
                return fixed4(color*(.97+wave*.045),alpha);
            }
            ENDCG
        }
    }
}
