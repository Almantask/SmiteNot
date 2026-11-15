Shader "GodGame/DebugDeck"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4> _DeckInstances; // xyz: cell center at deck top (m), w: thickness (m)
            StructuredBuffer<float4> _DeckStates;    // x: load01, y: strength01, z: stage (0..4), w: kind (0 wood, 1 stone, 2 ice)
            float _CellSizeM;
            float _ShowStrainOverlay;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 state : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                float4 inst = _DeckInstances[input.instanceID];
                float4 st = _DeckStates[input.instanceID];
                float stage = st.z;
                float thickness = inst.w;
                float sagDrop = stage > 2.5 ? lerp(0.0, 0.42, saturate(st.x)) : 0.0;

                float3 worldPos;
                worldPos.x = inst.x + input.positionOS.x * _CellSizeM;
                worldPos.y = (inst.y - sagDrop) + (input.positionOS.y - 0.5) * thickness;
                worldPos.z = inst.z + input.positionOS.z * _CellSizeM;

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.worldPos = worldPos;
                output.normalWS = input.normalOS;
                output.state = st;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float load01 = input.state.x;
                float strength01 = input.state.y;
                float stage = input.state.z;
                float kind = input.state.w;

                half3 wood = half3(0.46, 0.30, 0.16);
                half3 stone = half3(0.50, 0.52, 0.55);
                half3 ice = half3(0.70, 0.86, 0.96);
                half3 baseCol = kind < 0.5 ? wood : (kind < 1.5 ? stone : ice);

                // Rot greying on wood/ice before overlay so decay is readable even without the strain toggle.
                half3 rottedCol = half3(0.54, 0.53, 0.50);
                baseCol = lerp(rottedCol, baseCol, (half)saturate(strength01));

                // Plank / block edge shading so bridge spans read crisply.
                float plank = abs(frac(input.worldPos.x * 0.8 + input.worldPos.z * 0.25) - 0.5) * 2.0;
                baseCol *= (half)(0.88 + 0.12 * smoothstep(0.0, 0.35, plank));

                if (_ShowStrainOverlay > 0.5)
                {
                    // Screen-space derivative makes cracks thicken automatically at strategic zoom.
                    float2 crackUv = input.worldPos.xz * 0.45 + float2(input.worldPos.z * 0.31, -input.worldPos.x * 0.27);
                    float zig = abs(frac(crackUv.x + 0.5 * sin(crackUv.y * 3.1)) - 0.5);
                    float diag = abs(frac(crackUv.y - 0.5 * crackUv.x) - 0.5);
                    float distScale = saturate(length(_WorldSpaceCameraPos - input.worldPos) / 280.0);
                    float crackWidth = lerp(0.06, 0.19, distScale);
                    float crackMask = 1.0 - smoothstep(0.0, crackWidth, min(zig, diag));

                    if (stage < 0.5)
                    {
                        // Quiet: gentle amber tint once load exceeds ~30%.
                        half quietTint = (half)saturate((load01 - 0.3) * 0.7);
                        baseCol = lerp(baseCol, half3(0.85, 0.78, 0.30), quietTint * (half)0.45);
                    }
                    else if (stage < 1.5)
                    {
                        // Creak (>= 700 permille): vivid ochre-amber with rhythmic stress bands.
                        float rib = 0.5 + 0.5 * sin((input.worldPos.x + input.worldPos.z) * 2.2);
                        baseCol = lerp(half3(0.95, 0.72, 0.14), half3(0.72, 0.48, 0.08), (half)(0.35 * rib));
                    }
                    else if (stage < 2.5)
                    {
                        // Crack (>= 850 permille): hot orange-amber scored by dark procedural cracks.
                        half3 orange = half3(1.0, 0.48, 0.08);
                        baseCol = lerp(orange, half3(0.14, 0.05, 0.02), (half)(crackMask * 0.92));
                    }
                    else
                    {
                        // Sag (>= 950 permille): urgent warning crimson with pulsing fractures.
                        float pulse = 0.75 + 0.25 * sin(_Time.y * 8.0);
                        half3 crimson = half3(0.96, 0.12, 0.10) * (half)pulse;
                        baseCol = lerp(crimson, half3(0.08, 0.02, 0.02), (half)(crackMask * 0.95));
                    }
                }
                else if (stage > 2.5)
                {
                    // Even with overlay off, Sag gives a subtle stressed warm tint.
                    baseCol = lerp(baseCol, half3(0.75, 0.34, 0.22), (half)0.35);
                }

                float light = 0.45 + 0.55 * saturate(dot(normalize(input.normalWS), normalize(float3(0.4, 1.0, 0.3))));
                return half4(baseCol * light, 1.0);
            }
            ENDHLSL
        }
    }
}
