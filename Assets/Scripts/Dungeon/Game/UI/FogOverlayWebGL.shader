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
                ZTest Always Cull Off
                Blend SrcAlpha OneMinusSrcAlpha

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0

                #include "UnityCG.cginc"

                sampler2D _FogTex;
                UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
                float4 _FogWorldSize;
                float4 _FogWorldOrigin;

                struct appdata {
                    float4 vertex : POSITION;
                };

                struct v2f {
                    float4 pos : SV_POSITION;
                    float3 worldPos : TEXCOORD0;
                    float4 screenPos : TEXCOORD1;
                };

                v2f vert(appdata v) {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    o.screenPos = ComputeScreenPos(o.pos);
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target{
                      float rawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                      float orthoDepth = rawDepth;
                      #if defined(UNITY_REVERSED_Z)
                      orthoDepth = 1 - orthoDepth;
                      #endif
                      float surfaceDepth = lerp(LinearEyeDepth(rawDepth),
                          lerp(_ProjectionParams.y, _ProjectionParams.z, orthoDepth), unity_OrthoParams.w);
                      if (surfaceDepth >= _ProjectionParams.z * .999) return fixed4(0, 0, 0, 1);

                      float3 forward = -UNITY_MATRIX_V[2].xyz;
                      float3 ray = lerp(normalize(i.worldPos - _WorldSpaceCameraPos), forward, unity_OrthoParams.w);
                      float overlayDepth = -mul(UNITY_MATRIX_V, float4(i.worldPos, 1)).z;
                      float3 surface = i.worldPos + ray * ((surfaceDepth - overlayDepth) / max(.001, dot(ray, forward)));
                      float2 uv = (surface.xy - _FogWorldOrigin.xy) / _FogWorldSize.xy;

                      if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                          return fixed4(0.0, 0.0, 0.0, 1.0);

                      // Smooth the bilinear boundary while keeping near-visible
                      // surfaces clear and explored terrain at half opacity.
                      float fog = smoothstep(.05, .95, tex2D(_FogTex, uv).a);

                      return fixed4(0.0, 0.0, 0.0, 1.0 - fog);
                }

                ENDCG
            }
        }
}
