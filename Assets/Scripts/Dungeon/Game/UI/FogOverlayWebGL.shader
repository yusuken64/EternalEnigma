Shader "Custom/FogOverlayWebGL"
{
    Properties{
        _FogTex("Fog Texture", 2D) = "white" {}
        _FogWorldSize("Fog World Size", Vector) = (100, 100, 0, 0)
        _FogWorldOrigin("Fog World Origin", Vector) = (0, 0, 0, 0)
    }

        SubShader{
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            Pass {
                ZWrite Off
                Blend SrcAlpha OneMinusSrcAlpha

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 2.0

                #include "UnityCG.cginc"

                sampler2D _FogTex;
                float4 _FogWorldSize;
                float4 _FogWorldOrigin;

                struct appdata {
                    float4 vertex : POSITION;
                };

                struct v2f {
                    float4 pos : SV_POSITION;
                    float3 worldPos : TEXCOORD0;
                };

                v2f vert(appdata v) {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target{
                      float2 uv = (i.worldPos.xy - _FogWorldOrigin.xy) / _FogWorldSize.xy;

                      if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                          discard;

                      float fog = tex2D(_FogTex, uv).a;

                      return fixed4(0.0, 0.0, 0.0, 1.0 - fog);
                }

                ENDCG
            }
        }
}
