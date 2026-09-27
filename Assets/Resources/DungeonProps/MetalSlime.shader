Shader "EternalEnigma/MetalSlime"
{
    Properties { _BaseMap("Original slime texture", 2D)="white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _BaseMap;
        struct Input { float2 uv_BaseMap; };
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            half3 original=tex2D(_BaseMap,i.uv_BaseMap).rgb;
            half gray=dot(original,half3(.299,.587,.114));
            half chroma=max(original.r,max(original.g,original.b))-min(original.r,min(original.g,original.b));
            gray=lerp(gray,.8,saturate(chroma*12));
            o.Albedo=gray*half3(.78,.84,.9);
            o.Emission=o.Albedo*.12;
            o.Metallic=.35; o.Smoothness=.75; o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
