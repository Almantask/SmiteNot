// Ultra-fast instanced directional ground contact shadows for units, trees, boulders, hedgerows and walls.
// One indirect draw call of soft radial quads offset subtly away from GODGAME_SUN_DIR. Presentation only.
Shader "GodGame/ContactShadow"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        LOD 100

        Pass
        {
            Name "ContactShadowForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -2

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Lighting.hlsl"

            // xyz: world ground position (m), w: radiusX (m)
            StructuredBuffer<float4> _ShadowInstances;
            // x: radiusZ (m), y: opacity (0..1), z: yawRad (0 = align along sun dir), w: sunOffsetM (0 = centered)
            StructuredBuffer<float4> _ShadowStates;

            struct Attributes
            {
                float3 positionOS : POSITION; // unit quad in XZ: [-1..1] x [-1..1], y = 0
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float opacity : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                float4 inst = _ShadowInstances[input.instanceID];
                float4 st = _ShadowStates[input.instanceID];

                float rx = max(0.05, inst.w);
                float rz = max(0.05, st.x);
                float yaw = st.z;
                float sunOffset = st.w;

                float2 sunXZ = normalize(-GODGAME_SUN_DIR.xz);
                float3 local;
                if (abs(yaw) > 0.0001)
                {
                    float s, c;
                    sincos(yaw, s, c);
                    float px = input.positionOS.x * rx;
                    float pz = input.positionOS.z * rz;
                    local = float3(px * c - pz * s, 0.04, px * s + pz * c);
                }
                else
                {
                    // Orient ellipse along the sun cast axis for natural directional ground shadows.
                    float2 perp = float2(-sunXZ.y, sunXZ.x);
                    float2 p2 = perp * (input.positionOS.x * rx) + sunXZ * (input.positionOS.z * rz);
                    local = float3(p2.x, 0.04, p2.y);
                }

                float3 worldPos = inst.xyz + local;
                worldPos.xz += sunXZ * sunOffset;

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv * 2.0 - 1.0;
                output.opacity = saturate(st.y);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r2 = dot(input.uv, input.uv);
                if (r2 >= 1.0)
                {
                    discard;
                }
                // Crisp soft-edged Gaussian-like shadow disc with subtle cool sunlit-sky shadow tint.
                float falloff = 1.0 - r2;
                float alpha = falloff * falloff * input.opacity;
                return half4(0.07h, 0.10h, 0.16h, (half)alpha);
            }
            ENDHLSL
        }
    }
}
