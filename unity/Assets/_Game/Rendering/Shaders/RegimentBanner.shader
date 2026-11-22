// Instanced animated medieval regiment standards/banners anchored over each live regiment.
// Ripples in the map's prevailing wind, displays role heraldry (infantry/cavalry/ranged/siege/specialist),
// tilts and flashes when shaken or routing, and scales smoothly for strategic-zoom tabletop readability.
// Presentation only.
Shader "GodGame/RegimentBanner"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "BannerForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Lighting.hlsl"

            // xyz: regiment anchor ground pos (m), w: wind flutter yaw (rad)
            StructuredBuffer<float4> _BannerInstances;
            // x: morale01 (0..1), y: strength01 (0..1), z: emblemKind (0..4), w: side + routing*4 + regSeed*0.01
            StructuredBuffer<float4> _BannerStates;
            // rgb: team/regiment banner linear color, a: unused
            StructuredBuffer<float4> _BannerColors;
            float _TimeSeconds;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;       // cloth: x=0..1 pole->tail, y=0..1 bottom->top
                float4 color : COLOR;        // r: 0=wood pole, 0.5=brass finial, 1=banner cloth
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 matInfo : TEXCOORD3;   // rgb = base color, a = part mask (0 wood, 0.5 brass, 1 cloth)
                float4 state : TEXCOORD4;    // x: morale01, y: routing, z: emblemKind, w: phase
            };

            float3 RotateBannerY(float3 v, float rad)
            {
                float s, c;
                sincos(rad, s, c);
                return float3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
            }

            float3 TiltBannerZ(float3 v, float rad)
            {
                float s, c;
                sincos(rad, s, c);
                return float3(v.x * c - v.y * s, v.x * s + v.y * c, v.z);
            }

            Varyings Vert(Attributes input)
            {
                float4 inst = _BannerInstances[input.instanceID];
                float4 st = _BannerStates[input.instanceID];
                half3 bannerCol = (half3)_BannerColors[input.instanceID].rgb;

                float morale01 = saturate(st.x);
                float strength01 = saturate(st.y);
                float emblemKind = st.z;
                float routing = st.w >= 2.0 ? 1.0 : 0.0;
                float phase = frac(st.w) * 100.0;

                float3 p = input.positionOS;
                float3 n = input.normalOS;
                float part = input.color.r;

                // Wind ripple along banner cloth (+X from pole to tail).
                if (part > 0.75)
                {
                    float u = saturate(input.uv.x);
                    float waveFreq = routing > 0.5 ? 14.0 : 6.5;
                    float waveAmp = (routing > 0.5 ? 0.30 : 0.17) * u * u;
                    float wA = sin(_TimeSeconds * waveFreq - u * 5.2 + phase);
                    float wB = cos(_TimeSeconds * (waveFreq * 1.47) - u * 8.4 + phase * 1.7 + input.uv.y * 2.1);
                    float dispZ = (wA * 0.72 + wB * 0.28) * waveAmp;
                    p.z += dispZ;
                    // Slight vertical droop when morale drops.
                    p.y -= (1.0 - morale01) * 0.35 * u;
                    n = normalize(n + float3(0.0, 0.0, -wA * 0.45 * u));
                }

                // Pole tilts forward when shaken, sharply when routing.
                float tiltAngle = routing > 0.5
                    ? 0.72 + 0.08 * sin(_TimeSeconds * 10.0 + phase)
                    : (1.0 - smoothstep(0.35, 0.85, morale01)) * 0.32;
                p = TiltBannerZ(p, tiltAngle);
                n = TiltBannerZ(n, tiltAngle);

                // Scale gently with regiment strength and boost smoothly at strategic zoom for crisp tabletop reading.
                float camDist = length(_WorldSpaceCameraPos - inst.xyz);
                float zoomScale = lerp(1.0, 2.85, smoothstep(140.0, 680.0, camDist));
                float sizeScale = lerp(0.74, 1.04, strength01) * zoomScale;
                p *= sizeScale;

                // Lift slightly above tallest helmets at close range.
                p = RotateBannerY(p, inst.w);
                n = RotateBannerY(n, inst.w);
                float3 worldPos = inst.xyz + p + float3(0.0, 0.35, 0.0);

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = n;
                output.worldPos = worldPos;
                output.uv = input.uv;
                output.matInfo = half4(bannerCol, (half)part);
                output.state = float4(morale01, routing, emblemKind, phase);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float part = (float)input.matInfo.a;
                half3 albedo;
                float rim = 0.18;

                if (part < 0.25)
                {
                    // Dark ash standard pole.
                    albedo = half3(0.28, 0.20, 0.12);
                    rim = 0.08;
                }
                else if (part < 0.75)
                {
                    // Gleaming brass finial spearhead.
                    albedo = half3(0.88, 0.72, 0.28);
                    rim = 0.35;
                }
                else
                {
                    // Heraldic banner cloth with gold border and unit-role emblem pattern.
                    float u = input.uv.x;
                    float v = input.uv.y;
                    float morale01 = input.state.x;
                    float routing = input.state.y;
                    float emblemKind = input.state.z;

                    half3 cloth = input.matInfo.rgb;
                    half3 gold = half3(0.95, 0.82, 0.36);
                    half3 iron = half3(0.20, 0.22, 0.26);
                    half3 cream = half3(0.96, 0.94, 0.88);

                    // Desaturate cloth slightly when shaken; flash high-contrast cream band when routing.
                    half lum = dot(cloth, half3(0.299h, 0.587h, 0.114h));
                    cloth = lerp(half3(lum, lum, lum), cloth, (half)lerp(0.45, 1.0, morale01));

                    // Crisp gold trim border along top, bottom, and pole edge.
                    float border = step(v, 0.11) + step(0.89, v) + step(u, 0.08);
                    float emblem = 0.0;
                    half3 emblemCol = gold;

                    if (emblemKind < 0.5)
                    {
                        // Infantry: bold vertical heraldic pale bar.
                        emblem = step(0.26, u) * step(u, 0.42) * step(0.18, v) * step(v, 0.82);
                    }
                    else if (emblemKind < 1.5)
                    {
                        // Cavalry: sweeping diagonal bend/chevron sash.
                        float diag = abs((v - 0.5) - (u - 0.38) * 1.15);
                        emblem = step(diag, 0.12) * step(0.14, u) * step(u, 0.72);
                    }
                    else if (emblemKind < 2.5)
                    {
                        // Ranged: twin horizontal arrow fess stripes.
                        float bandA = step(0.28, v) * step(v, 0.38);
                        float bandB = step(0.62, v) * step(v, 0.72);
                        emblem = saturate(bandA + bandB) * step(0.14, u) * step(u, 0.78);
                    }
                    else if (emblemKind < 3.5)
                    {
                        // Siege: heavy dark iron border + fortress square.
                        float sq = step(0.20, u) * step(u, 0.54) * step(0.26, v) * step(v, 0.74);
                        float inner = step(0.29, u) * step(u, 0.45) * step(0.38, v) * step(v, 0.62);
                        emblem = saturate(sq - inner);
                        emblemCol = iron;
                    }
                    else
                    {
                        // Specialists: bright heraldic cross / star.
                        float crossH = step(0.42, v) * step(v, 0.58) * step(0.18, u) * step(u, 0.56);
                        float crossV = step(0.31, u) * step(u, 0.43) * step(0.20, v) * step(v, 0.80);
                        emblem = saturate(crossH + crossV);
                        emblemCol = cream;
                    }

                    albedo = lerp(cloth, emblemCol, (half)saturate(emblem));
                    albedo = lerp(albedo, gold, (half)saturate(border));

                    if (routing > 0.5)
                    {
                        float pulse = 0.55 + 0.45 * sin(_TimeSeconds * 9.0 + input.state.w);
                        albedo = lerp(albedo, cream, (half)(0.52 * pulse));
                    }
                    rim = 0.24;
                }

                // Double-sided shading so wind-rippled cloth looks luminous from both sides.
                float3 n = normalize(input.normalWS);
                half3 lit = GodGameShade(n, input.worldPos, albedo, rim);
                // Subtle sunlit fabric translucency boost on cloth.
                if (part > 0.75)
                {
                    lit = max(lit, albedo * 0.58h);
                }
                lit = GodGameApplyHaze(lit, input.worldPos, 0.50);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }
}
