Shader "Eternal Enigma/Home Furniture"
{
    Properties
    {
        _MainTex ("Furniture palette", 2D) = "white" {}
        _Fabric ("Bed linen", Color) = (.478,.502,.396,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Fabric;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 c = tex2D(_MainTex,IN.uv_MainTex).rgb;
            // Teal palette swatches identify linen; wood, crockery and warm trim keep their authored colours.
            if (c.r < .55 && c.g > .08 && c.b > .08 && c.g > c.r*1.3 && c.b > c.r*1.3)
                c = _Fabric.rgb * (.75 + c.g*.35);
            o.Albedo = c; o.Metallic = 0; o.Smoothness = .04; o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
