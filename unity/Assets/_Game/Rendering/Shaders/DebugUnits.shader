// Debug capsules for every soldier, drawn in one indirect instanced call (M2 debug renderer). Each instance reads its
// ground position and regiment from _Units and its body size from _Shapes; the regiment picks a colour from
// _Palette. A plain lambert light keeps shapes readable from the strategic camera. Presentation only.
Shader "GodGame/DebugUnits"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4> _Units;   // xyz: feet on the ground (m), w: palette index
            StructuredBuffer<float4> _Shapes;  // x: body radius (m), y: height (m)
            StructuredBuffer<float4> _Palette;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 color : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                float4 unit = _Units[input.instanceID];
                float4 shape = _Shapes[input.instanceID];
                // Unity's capsule is 2 m tall and 1 m across, centred on the origin.
                float3 local = float3(
                    input.positionOS.x * shape.x * 2.0,
                    (input.positionOS.y + 1.0) * shape.y * 0.5,
                    input.positionOS.z * shape.x * 2.0);
                Varyings output;
                output.positionCS = TransformWorldToHClip(unit.xyz + local);
                output.normalWS = input.normalOS;
                output.color = _Palette[(uint)unit.w].rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float light = 0.45 + 0.55 * saturate(dot(normalize(input.normalWS), normalize(float3(0.4, 1.0, 0.3))));
                return half4(input.color * light, 1.0);
            }
            ENDHLSL
        }
    }
}
