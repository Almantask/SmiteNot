// Water surface: the ground mesh lifted by each cell's water depth from the simulation's fields (_Fields: see
// GodGame.Bridge.FieldUpload) and shaded with bilinearly smoothed depth/flow, dual-phase flow-map advection
// (zero 1.5 m cell seams), shallow-to-deep absorption, wading-edge shoreline foam, turbulent whitewater rapids
// above sweep speeds, Fresnel sky reflection, and sun specular glints. Presentation only: the depth and flow
// are the exact cells the soldiers feel.
Shader "GodGame/DebugWater"
{
    Properties
    {
        _DryDepthUm ("Dry below (um)", Float) = 5000
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Fields.hlsl"
            #include "Lighting.hlsl"

            float _DryDepthUm;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                // A vertex sits on a cell corner: rise by the deepest of the four cells around it, so the surface
                // covers the cells that are wet and the fragment test trims the rest.
                float2 texel = 0.5 / _FieldSize.xy;
                float depth = 0;
                depth = max(depth, FieldAt(input.uv + float2(-texel.x, -texel.y)).x);
                depth = max(depth, FieldAt(input.uv + float2(texel.x, -texel.y)).x);
                depth = max(depth, FieldAt(input.uv + float2(-texel.x, texel.y)).x);
                depth = max(depth, FieldAt(input.uv + float2(texel.x, texel.y)).x);
                float3 world = TransformObjectToWorld(input.positionOS);
                world.y += depth * 1e-6 + 0.025;
                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.world = world;
                return output;
            }

            // Smooth procedural wave height & gradient evaluated at an advected world-space position.
            float3 WaterWaveSample(float2 p, float t)
            {
                float w1 = sin(p.x * 0.95 + p.y * 0.42 + t * 1.6);
                float w2 = cos(p.x * 0.38 - p.y * 1.18 - t * 1.3);
                float w3 = sin((p.x + p.y) * 1.75 + cos(p.x * 0.65 - p.y * 0.55) * 1.2 + t * 2.1);
                float w4 = cos((p.x * 1.35 - p.y * 1.62) - t * 1.9);
                float gx = (cos(p.x * 0.95 + p.y * 0.42 + t * 1.6) * 0.95
                    - sin(p.x * 0.38 - p.y * 1.18 - t * 1.3) * 0.38
                    + cos((p.x + p.y) * 1.75 + t * 2.1) * 0.85
                    - sin(p.x * 1.35 - p.y * 1.62 - t * 1.9) * 0.65) * 0.048;
                float gz = (cos(p.x * 0.95 + p.y * 0.42 + t * 1.6) * 0.42
                    + sin(p.x * 0.38 - p.y * 1.18 - t * 1.3) * 1.18
                    + cos((p.x + p.y) * 1.75 + t * 2.1) * 0.85
                    + sin(p.x * 1.35 - p.y * 1.62 - t * 1.9) * 0.78) * 0.048;
                float crest = saturate((w1 + w2 + w3 * 0.8 + w4 * 0.6) * 0.28 + 0.5);
                return float3(gx, gz, crest);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint4 field = FieldAt(input.uv);
                float depthSmooth = FieldWaterDepthSmoothM(input.uv);
                float dryM = _DryDepthUm * 1e-6;
                if (field.x < _DryDepthUm && depthSmooth < dryM * 1.1)
                {
                    discard;
                }
                float depthM = max(FieldWaterDepthM(field) * 0.35 + depthSmooth * 0.65, 0.001);
                float2 flow = FieldFlowSmoothMPerS(input.uv);
                float speed = length(flow);

                // Shallow clear turquoise-teal -> rich river slate-navy at lethal/deep depth.
                float depthT = saturate(depthM / 1.45);
                half3 shallowCol = half3(0.28, 0.60, 0.64);
                half3 midCol = half3(0.10, 0.33, 0.52);
                half3 deepCol = half3(0.04, 0.14, 0.32);
                half3 color = depthT < 0.45
                    ? lerp(shallowCol, midCol, (half)(depthT / 0.45))
                    : lerp(midCol, deepCol, (half)((depthT - 0.45) / 0.55));

                // Dual-phase flow-map advection: smoothly drifts waves along local flow velocity without any
                // discontinuities across 1.5 m simulation cells.
                float cycle = _Time.y * 0.42;
                float phase0 = frac(cycle);
                float phase1 = frac(cycle + 0.5);
                float flowBlend = abs(2.0 * phase0 - 1.0);
                float2 advect0 = input.world.xz - flow * (phase0 * 2.4);
                float2 advect1 = input.world.xz - flow * (phase1 * 2.4);
                float3 s0 = WaterWaveSample(advect0, _Time.y);
                float3 s1 = WaterWaveSample(advect1, _Time.y);
                float3 wave = lerp(s0, s1, flowBlend);

                float chop = 0.85 + saturate(speed * 0.55);
                float3 waterNormal = normalize(float3(wave.x * chop, 1.0, wave.y * chop));

                // Fresnel sky reflection and sunlit specular sparkle on wave crests.
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.world + float3(0.0, 1e-4, 0.0));
                float ndv = saturate(dot(waterNormal, viewDir));
                float fresnel = pow(1.0 - ndv, 3.4);
                half3 skyRefl = half3(0.58, 0.73, 0.88);
                color = lerp(color, skyRefl, (half)(fresnel * 0.46));

                float3 halfVec = normalize(GODGAME_SUN_DIR + viewDir);
                float spec = pow(saturate(dot(waterNormal, halfVec)), 110.0);
                color += half3(1.0, 0.96, 0.86) * (half)(spec * 0.68);

                // Smooth shoreline wading-edge foam where shallow water meets banks or boulders (5..160 mm).
                float shoreNoise = 0.5 + 0.5 * sin(input.world.x * 1.4 + _Time.y * 1.2) * cos(input.world.z * 1.5 - _Time.y * 0.9);
                float shoreFoam = (1.0 - smoothstep(0.015, 0.17, depthM)) * (0.45 + 0.55 * shoreNoise);

                // Smooth advected current streaks & frothy whitewater rapids above ~1.1 m/s (sweep hazard readability).
                float streak0 = smoothstep(0.68, 0.94, s0.z);
                float streak1 = smoothstep(0.68, 0.94, s1.z);
                float streak = lerp(streak0, streak1, flowBlend);
                float flowFoam = streak * saturate(speed / 1.5) * 0.34
                    + smoothstep(1.05, 2.35, speed) * smoothstep(0.45, 0.85, wave.z) * 0.44;

                float totalFoam = saturate(shoreFoam * 0.46 + flowFoam);
                color = lerp(color, half3(0.90, 0.95, 0.99), (half)totalFoam);
                color = GodGameApplyHaze(color, input.world, 0.70);

                float edgeSoft = smoothstep(dryM * 0.6, dryM * 2.5, depthSmooth);
                float alpha = saturate((lerp(0.38, 0.88, saturate(depthM / 0.75)) + totalFoam * 0.24) * edgeSoft);
                return half4(color, (half)alpha);
            }
            ENDHLSL
        }
    }
}
