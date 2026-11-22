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
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Lighting.hlsl"

            // Cell mode (_IsAuthoredBridge < 0.5): xyz = cell center at deck top (m), w = thickness (m)
            // Bridge mode (_IsAuthoredBridge > 0.5): xyz = segment center at deck top (m), w = span yaw (rad)
            StructuredBuffer<float4> _DeckInstances;
            StructuredBuffer<float4> _DeckStates;    // x: load01, y: strength01, z: stage (0..4), w: kind (0 wood, 1 stone, 2 ice)
            StructuredBuffer<float4> _DeckExtra;     // xyz: scale (authored bridge mode)
            StructuredBuffer<float4> _CellColors;    // 16x16 palette grid for authored bridge props
            float _CellSizeM;
            float _ShowStrainOverlay;
            float _IsAuthoredBridge;
            float _DeckTopOffset;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 state : TEXCOORD2;
                float3 paletteCol : TEXCOORD3;
            };

            float DeckHash21(float2 p)
            {
                float h = dot(p, float2(127.1, 311.7));
                return frac(sin(h) * 43758.5453123);
            }

            float3 RotateDeckY(float3 v, float rad)
            {
                float s, c;
                sincos(rad, s, c);
                return float3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
            }

            Varyings Vert(Attributes input)
            {
                float4 inst = _DeckInstances[input.instanceID];
                float4 st = _DeckStates[input.instanceID];
                float4 extra = _DeckExtra[input.instanceID];
                float load01 = saturate(st.x);
                float strength01 = saturate(st.y);
                float rot = saturate(1.0 - strength01);
                float stage = st.z;
                float kind = st.w;

                float cellHash = DeckHash21(inst.xz);
                float3 posOS = input.positionOS;
                float3 nOS = input.normalOS;
                half3 palCol = half3(0.46, 0.30, 0.16);

                if (_IsAuthoredBridge > 0.5)
                {
                    // Authored Blender FBX: transform Euler(-90,0,0) mesh space into upright +Y up, +Z span.
                    posOS = float3(posOS.x, -posOS.z, posOS.y);
                    nOS = float3(nOS.x, -nOS.z, nOS.y);
                    uint cx = (uint)clamp(floor(input.uv.x * 16.0), 0.0, 15.0);
                    uint cy = (uint)clamp(floor(input.uv.y * 16.0), 0.0, 15.0);
                    palCol = (half3)_CellColors[cy * 16u + cx].rgb;
                }

                // Stage-driven structural deformation:
                //   1 = creak: high-frequency micro-tremor & slight elastic dip
                //   2 = crack: mid-cell buckling dip + alternating plank step/tilt as stringers split
                //   3+ = sag:  deep V-sag deflection + inward-canted normal tilt + slow structural groan
                float vertDeflect = 0.0;
                float sagWeight = 0.0;
                if (stage >= 0.5 && stage < 1.5)
                {
                    float tremor = sin(_Time.y * 28.0 + inst.x * 4.1 + inst.z * 5.7);
                    vertDeflect = -0.04 * max(0.35, load01) + tremor * 0.016 * max(0.35, load01);
                }
                else if (stage >= 1.5 && stage < 2.5)
                {
                    float jitter = sin(_Time.y * 22.0 + inst.x * 3.1 + inst.z * 4.7);
                    float stepOffset = (cellHash - 0.5) * 0.06 * sign(posOS.x);
                    vertDeflect = -0.14 * lerp(0.55, 1.0, load01) + jitter * 0.012 + stepOffset;
                    nOS.x += stepOffset * 1.4;
                }
                else if (stage >= 2.5)
                {
                    float groan = sin(_Time.y * 4.2 + inst.x * 1.9 + inst.z * 2.3);
                    float edgeFactor = 0.45 + 0.55 * saturate(1.0 - abs(posOS.x) * 0.65);
                    vertDeflect = -lerp(0.18, 0.44, load01) * edgeFactor + groan * 0.022;
                    sagWeight = 1.0;
                    nOS.x -= clamp(posOS.x, -1.0, 1.0) * 0.45;
                }

                // Subtle plank-edge cupping & warp as wood rots toward collapse.
                float rotWarp = kind < 0.5 ? rot * 0.045 * sin(posOS.x * 4.5 + posOS.z * 4.0 + cellHash * 6.283) : 0.0;

                float3 worldPos;
                float3 normalWS;
                if (_IsAuthoredBridge > 0.5)
                {
                    float3 scaled = float3(
                        posOS.x * extra.x,
                        (posOS.y - _DeckTopOffset) * extra.y + vertDeflect + rotWarp,
                        posOS.z * extra.z
                    );
                    float yaw = inst.w;
                    worldPos = inst.xyz + RotateDeckY(scaled, yaw);
                    normalWS = normalize(RotateDeckY(nOS + float3(0.0, sagWeight * 0.15, 0.0), yaw));
                }
                else
                {
                    float thickness = inst.w;
                    worldPos.x = inst.x + posOS.x * _CellSizeM;
                    worldPos.y = (inst.y + vertDeflect + rotWarp) + (posOS.y - 0.5) * thickness;
                    worldPos.z = inst.z + posOS.z * _CellSizeM;
                    normalWS = normalize(nOS + float3(0.0, sagWeight * 0.15, 0.0));
                }

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.worldPos = worldPos;
                output.normalWS = normalWS;
                output.state = st;
                output.paletteCol = palCol;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float load01 = saturate(input.state.x);
                float strength01 = saturate(input.state.y);
                float rot = saturate(1.0 - strength01);
                float stage = input.state.z;
                float kind = input.state.w;

                half3 wood = _IsAuthoredBridge > 0.5 ? (half3)input.paletteCol : half3(0.46, 0.30, 0.16);
                half3 stone = half3(0.52, 0.53, 0.54);
                half3 ice = half3(0.72, 0.88, 0.98);
                half3 baseCol = kind < 0.5 ? wood : (kind < 1.5 ? stone : ice);

                // Material-specific micro-detail: wood planks, ashlar stone blocks, or glacial ice veins.
                float plankCoord = input.worldPos.x * 1.35 + input.worldPos.z * 0.35;
                float plankIdx = floor(plankCoord);
                float plankEdge = abs(frac(plankCoord) - 0.5) * 2.0;
                float plankJitter = DeckHash21(float2(plankIdx, floor(input.worldPos.z * 0.5)));
                float grain = 0.5 + 0.5 * sin(input.worldPos.z * 9.5 + plankJitter * 12.0 + sin(input.worldPos.x * 4.0) * 1.5);

                if (kind < 0.5)
                {
                    float gapWidth = lerp(0.14, 0.34, rot);
                    float seamMask = 1.0 - smoothstep(1.0 - gapWidth, 1.0, plankEdge);
                    half3 driftwoodGrey = lerp(half3(0.46, 0.46, 0.44), half3(0.58, 0.57, 0.54), (half)grain);
                    half3 fungalRot = half3(0.22, 0.21, 0.19);
                    float rotSpotNoise = sin(input.worldPos.x * 2.3 + plankIdx * 1.7) * cos(input.worldPos.z * 2.7 - plankIdx);
                    float fungalMask = smoothstep(0.15, 0.75, rotSpotNoise * 0.5 + 0.5) * saturate(rot * 1.25);

                    baseCol *= (half)(0.90 + 0.12 * grain * (1.0 - rot * 0.5));
                    baseCol = lerp(baseCol, driftwoodGrey, (half)smoothstep(0.08, 0.85, rot));
                    baseCol = lerp(baseCol, fungalRot, (half)(fungalMask * 0.65));
                    if (_IsAuthoredBridge < 0.5)
                    {
                        baseCol *= (half)lerp(lerp(0.55, 0.30, rot), 1.0, seamMask);
                    }
                    else
                    {
                        baseCol *= (half)lerp(lerp(0.84, 0.62, rot), 1.0, seamMask);
                    }
                }
                else if (kind < 1.5)
                {
                    // Coursed ashlar masonry for stone bridges.
                    float2 stoneUv = input.worldPos.xz * 0.85;
                    float row = floor(stoneUv.y);
                    float col = floor(stoneUv.x + fmod(row, 2.0) * 0.5);
                    float blockTone = 0.88 + 0.22 * DeckHash21(float2(col, row));
                    float mortarX = abs(frac(stoneUv.x + fmod(row, 2.0) * 0.5) - 0.5);
                    float mortarY = abs(frac(stoneUv.y) - 0.5);
                    float mortar = smoothstep(0.44, 0.49, max(mortarX, mortarY));
                    baseCol = lerp(baseCol * (half)blockTone, half3(0.28, 0.29, 0.30), (half)(mortar * 0.65));
                }
                else
                {
                    // Frozen pond ice: rich glacial teal-blue depth with crisp frost fracture veins (never blows out to flat white).
                    float veinA = abs(sin(input.worldPos.x * 0.55 + cos(input.worldPos.z * 0.62) * 1.8));
                    float veinB = abs(cos(input.worldPos.z * 0.90 - sin(input.worldPos.x * 0.75) * 1.4));
                    float frost = 1.0 - smoothstep(0.0, 0.18, min(veinA, veinB));
                    baseCol = lerp(half3(0.30, 0.58, 0.74), half3(0.52, 0.78, 0.90), (half)(veinA * 0.65));
                    baseCol = lerp(baseCol, half3(0.78, 0.92, 0.98), (half)(frost * 0.55));
                    baseCol = lerp(baseCol, half3(0.36, 0.48, 0.58), (half)(rot * 0.45));
                }

                // Screen-space aware procedural fracture / splinter crack pattern used across Crack & Sag stages.
                float2 crackUv = input.worldPos.xz * 0.45 + float2(input.worldPos.z * 0.31, -input.worldPos.x * 0.27);
                float zig = abs(frac(crackUv.x + 0.5 * sin(crackUv.y * 3.1)) - 0.5);
                float diag = abs(frac(crackUv.y - 0.5 * crackUv.x) - 0.5);
                float distScale = saturate(length(_WorldSpaceCameraPos - input.worldPos) / 280.0);
                float crackWidth = lerp(0.06, 0.19, distScale);
                float crackMask = 1.0 - smoothstep(0.0, crackWidth, min(zig, diag));
                float fineStress = 1.0 - smoothstep(0.0, crackWidth * 0.65, abs(frac(input.worldPos.x * 1.35 + input.worldPos.z * 0.35) - 0.5));

                if (_ShowStrainOverlay > 0.5)
                {
                    if (stage < 0.5)
                    {
                        half quietTint = (half)saturate((load01 - 0.3) * 0.7);
                        baseCol = lerp(baseCol, half3(0.85, 0.78, 0.30), quietTint * (half)0.45);
                    }
                    else if (stage < 1.5)
                    {
                        float rib = 0.5 + 0.5 * sin((input.worldPos.x + input.worldPos.z) * 2.2 + _Time.y * 4.5);
                        half3 creakCol = lerp(half3(0.95, 0.72, 0.14), half3(0.72, 0.48, 0.08), (half)(0.35 * rib));
                        baseCol = lerp(creakCol, half3(1.0, 0.88, 0.42), (half)(fineStress * 0.45));
                    }
                    else if (stage < 2.5)
                    {
                        half3 orange = half3(1.0, 0.48, 0.08);
                        baseCol = lerp(orange, half3(0.14, 0.05, 0.02), (half)(crackMask * 0.92));
                    }
                    else
                    {
                        float pulse = 0.75 + 0.25 * sin(_Time.y * 8.0);
                        half3 crimson = half3(0.96, 0.12, 0.10) * (half)pulse;
                        baseCol = lerp(crimson, half3(0.08, 0.02, 0.02), (half)(crackMask * 0.95));
                    }
                }
                else
                {
                    if (stage >= 0.5 && stage < 1.5)
                    {
                        baseCol = lerp(baseCol, half3(0.82, 0.66, 0.34), (half)(fineStress * 0.40));
                    }
                    else if (stage >= 1.5 && stage < 2.5)
                    {
                        float rimMask = saturate(crackMask * 1.4 - crackMask * crackMask);
                        baseCol = lerp(baseCol, half3(0.86, 0.52, 0.20), (half)(rimMask * 0.55));
                        baseCol = lerp(baseCol, half3(0.12, 0.09, 0.07), (half)(crackMask * 0.85));
                    }
                    else if (stage >= 2.5)
                    {
                        float pulse = 0.65 + 0.35 * sin(_Time.y * 8.0);
                        baseCol = lerp(baseCol, half3(0.75, 0.26, 0.16), (half)(0.35 * pulse));
                        baseCol = lerp(baseCol, half3(0.10, 0.03, 0.02), (half)(crackMask * 0.90));
                    }
                }

                half3 lit = GodGameShade(input.normalWS, input.worldPos, baseCol, kind > 1.5 ? 0.32 : 0.12);
                if (kind > 1.5)
                {
                    // Crisp ice specular highlight.
                    float3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos + float3(0.0, 1e-4, 0.0));
                    float3 halfVec = normalize(GODGAME_SUN_DIR + viewDir);
                    float iceSpec = pow(saturate(dot(normalize(input.normalWS), halfVec)), 54.0);
                    lit += half3(0.92, 0.97, 1.0) * (half)(iceSpec * 0.55);
                }
                lit = GodGameApplyHaze(lit, input.worldPos, 0.70);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }
}
