Shader "Eternal Enigma/Home Surface"
{
    Properties
    {
        _Color ("Surface", Color) = (.584,.392,.318,1)
        _Trim ("Woven border", Color) = (.808,.722,.561,1)
        _Woven ("Woven textile", Float) = 1
        _MainTex ("UV coordinates", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _Trim;
        float _Woven;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            float3 color = _Color.rgb;
            if (_Woven > .5)
            {
                // Broad cream border, two fine inner stripes, quiet filtered thread detail.
                float edge = min(min(uv.x, 1-uv.x) * 1.5, min(uv.y, 1-uv.y));
                float aa = max(fwidth(edge), .001);
                float border = 1-smoothstep(.045-aa,.045+aa,edge);
                float stripe = (1-smoothstep(.004-aa,.004+aa,abs(edge-.071))) * .55;
                color = lerp(color, _Trim.rgb, max(border,stripe));
                float2 thread = uv * float2(170,110);
                float visibility = 1-saturate(max(fwidth(thread.x), fwidth(thread.y)));
                color *= 1 + sin(thread.x*6.283)*sin(thread.y*6.283)*.035*visibility;
                color *= 1 + sin(uv.y*19)*.018;
            }
            else
            {
                float2 p = IN.worldPos.xy / 2.5;
                float plank = floor(p.x*3);
                float grain = sin(p.y*4.7+plank*2.1)*.025 + sin(p.y*1.2+plank)*.018;
                float seam = min(frac(p.x*3),1-frac(p.x*3));
                float joint = frac((p.y+floor(plank)*.73)/3);
                float aa = max(fwidth(p.x*3),.002);
                float seamLine = 1-smoothstep(.008-aa,.008+aa,seam);
                float end = 1-smoothstep(.003,.011,min(joint,1-joint));
                color *= (1 + grain + sin(plank*3.7)*.035) * (1-max(seamLine*.19,end*.12));
            }
            o.Albedo = color; o.Metallic = 0; o.Smoothness = .04; o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
