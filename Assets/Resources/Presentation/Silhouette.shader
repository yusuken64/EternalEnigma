Shader "Hidden/EternalEnigma/GroundSilhouette"
{
    SubShader
    {
        CGINCLUDE
        #include "UnityCG.cginc"
        float4x4 _SilhouetteVP;
        float3 _SilhouetteOrigin, _SilhouetteDirection;
        sampler2D _SilhouetteDepth, _SilhouetteFog, _SilhouetteMaskTexture;
        float4 _SilhouetteFogBounds, _SilhouetteColor;
        float _SilhouetteDynamic;
        struct MeshInput { float4 vertex:POSITION; float3 normal:NORMAL; };
        struct Pixel { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
        void Explored(float3 world)
        {
            if(_SilhouetteFogBounds.z>0)
            {
                float2 uv=(world.xy-_SilhouetteFogBounds.xy)/_SilhouetteFogBounds.zw;
                clip(uv);clip(1-uv);
                clip(tex2D(_SilhouetteFog,uv).a-.25);
            }
        }
        Pixel Caster(MeshInput v)
        {
            Pixel o;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
            o.position=mul(_SilhouetteVP,float4(o.world,1));o.normal=0;return o;
        }
        fixed4 Depth(Pixel i):SV_Target
        {
            if(_SilhouetteDynamic<.5)Explored(i.world);
            return EncodeFloatRGBA(saturate(dot(i.world-_SilhouetteOrigin,_SilhouetteDirection)/240));
        }
        Pixel Receiver(MeshInput v)
        {
            Pixel o;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
            o.position=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);return o;
        }
        fixed4 Mask(Pixel i):SV_Target
        {
            Explored(i.world);
            clip(-normalize(i.normal).z-.45);
            float4 light=mul(_SilhouetteVP,float4(i.world,1));
            float2 uv=light.xy/light.w*.5+.5;
            clip(uv);clip(1-uv);
            float depth=DecodeFloatRGBA(tex2D(_SilhouetteDepth,uv));
            float receiver=dot(i.world-_SilhouetteOrigin,_SilhouetteDirection)/240;
            clip(receiver-depth-.0003);
            return 1;
        }
        fixed4 Composite(v2f_img i):SV_Target
        { return float4(_SilhouetteColor.rgb,_SilhouetteColor.a*tex2D(_SilhouetteMaskTexture,i.uv).r); }
        ENDCG
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Caster
            #pragma fragment Depth
            ENDCG
        }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Receiver
            #pragma fragment Mask
            ENDCG
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Composite
            ENDCG
        }
    }
    Fallback Off
}
