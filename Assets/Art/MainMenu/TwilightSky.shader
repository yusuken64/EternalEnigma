Shader "EternalEnigma/Menu Twilight Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (.035, .035, .10, 1)
        _Horizon ("Horizon", Color) = (.28, .15, .27, 1)
        _Clouds ("Clouds", Color) = (.12, .10, .22, 1)
        _MoonDirection ("Moon direction", Vector) = (-.42, .58, -1, 0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Zenith, _Horizon, _Clouds, _MoonDirection;
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o; o.position = UnityObjectToClipPos(vertex); o.direction = vertex.xyz; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x),
                    lerp(hash(i + float2(0,1)), hash(i + 1), f.x), f.y);
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float2 uv = float2(atan2(d.x, d.z), asin(clamp(d.y, -1, 1)));
                float3 color = lerp(_Horizon.rgb, _Zenith.rgb, saturate(d.y * 1.7));
                float2 p = uv * float2(3.5, 9) + float2(_Time.y * .001, 0);
                float cloud = noise(p) * .55 + noise(p * 2.1) * .3 + noise(p * 4.2) * .15;
                cloud = smoothstep(.43, .73, cloud) * smoothstep(-.05, .2, d.y);
                color = lerp(color, _Clouds.rgb, cloud * .7);
                float2 stars = uv * 190;
                float sparkle = step(.986, hash(floor(stars))) *
                    (1 - smoothstep(.04, .16, length(frac(stars) - .5)));
                color += sparkle * smoothstep(.1, .6, d.y) * (1 - cloud) * .6;
                float moonDistance = length(d - normalize(_MoonDirection.xyz));
                float moon = 1 - smoothstep(.058, .061, moonDistance);
                float halo = exp(-moonDistance * 18) * .14;
                color += float3(.66, .73, .92) * (moon * .65 + halo) * (1 - cloud * .45);
                return float4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
