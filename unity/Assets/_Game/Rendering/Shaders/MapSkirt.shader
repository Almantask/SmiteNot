// Tabletop diorama map plinth / cutaway earth skirt framing the four borders of the terrain slab.
// Renders layered turf rim, rich topsoil, weathered limestone strata, and a clean charcoal base.
// Presentation only.
Shader "GodGame/MapSkirt"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Lighting.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0; // x: along border (m), y: 0 at terrain top -> 1 at plinth bottom
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.worldPos = world;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float depth01 = saturate(input.uv.y);
                float strataWave = sin(input.uv.x * 0.08) * 0.03 + cos(input.uv.x * 0.23) * 0.015;
                float d = saturate(depth01 + strataWave);

                half3 turfRim = half3(0.28, 0.36, 0.20);
                half3 topsoil = half3(0.32, 0.23, 0.15);
                half3 limestone = half3(0.30, 0.29, 0.27);
                half3 plinthBase = half3(0.16, 0.17, 0.19);

                half3 albedo = turfRim;
                albedo = lerp(albedo, topsoil, (half)smoothstep(0.02, 0.10, d));
                albedo = lerp(albedo, limestone, (half)smoothstep(0.22, 0.36, d));
                albedo = lerp(albedo, plinthBase, (half)smoothstep(0.72, 0.88, d));

                // Subtle horizontal rock bedding lines in the limestone stratum.
                float strataLine = 0.92 + 0.08 * sin(input.worldPos.y * 2.6 + input.uv.x * 0.05);
                albedo *= (half)strataLine;

                half3 lit = GodGameShade(input.normalWS, input.worldPos, albedo, 0.0);
                lit = GodGameApplyHaze(lit, input.worldPos, 0.70);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }
}
