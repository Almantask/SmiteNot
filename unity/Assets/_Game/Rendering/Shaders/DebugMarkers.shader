Shader "GodGame/DebugMarkers"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
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

            // xyz: world base position (m), w: yaw radians (or radiusM for herd marker variant 7)
            StructuredBuffer<float4> _MarkerInstances;
            // variant st.z:
            //   0 = wreck (x=scale, y=cleared, z=0, w=clearYScale)
            //   1/2 = salvo (x=ageSeconds, y=crushedCount, z=1|2, w=0)
            //   3 = standing/falling tree (x=scale, y=0, z=3, w=fallProgress)
            //   4 = felled trunk (x=scale, y=cleared, z=4, w=clearYScale)
            //   5 = fire cell (x=scale, y=phase, z=5, w=extinguished)
            //   6 = lightning strike (x=ageSeconds, y=0, z=6, w=0)
            //   7 = herd marker (x=mode 0..3, y=driven 0/1, z=7, w=yawRad)
            //   8 = terrain prop: boulder / hedgerow / drystone wall / herd pen (x=scaleXZ, y=scaleY, z=8, w=sway)
            //   9 = flying siege stone (x=scale, y=progress, z=9, w=spinRad)
            //  10 = flying arrow (x=dirX, y=dirY, z=10, w=dirZ; inst.w = progress)
            StructuredBuffer<float4> _MarkerStates;
            StructuredBuffer<float4> _CellColors;
            float _IsAuthoredFbx;
            float _TimeSeconds;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : TEXCOORD1;
                float4 vfx : TEXCOORD2; // x: 0=default, 1=active fire, 2=herd dust; y: local height01; z: phase; w: intensity/mask
                float3 worldPos : TEXCOORD3;
            };

            float3 RotateY(float3 v, float rad)
            {
                float s;
                float c;
                sincos(rad, s, c);
                return float3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
            }

            float3 PitchForwardZ(float3 v, float rad)
            {
                float s;
                float c;
                sincos(rad, s, c);
                return float3(v.x, v.y * c - v.z * s, v.y * s + v.z * c);
            }

            half3 SamplePropAlbedo(float2 uv, half3 fallbackColor, out uint cx, out uint cy)
            {
                cx = (uint)clamp(floor(uv.x * 16.0), 0.0, 15.0);
                cy = (uint)clamp(floor(uv.y * 16.0), 0.0, 15.0);
                if (_IsAuthoredFbx > 0.5)
                {
                    return (half3)_CellColors[cy * 16u + cx].rgb;
                }
                return fallbackColor;
            }

            Varyings Vert(Attributes input)
            {
                float4 inst = _MarkerInstances[input.instanceID];
                float4 st = _MarkerStates[input.instanceID];
                float yaw = inst.w;
                float3 p = input.positionOS;
                float3 n = input.normalOS;
                half3 tint = (half3)input.color.rgb;
                half selfLit = 0.0h;
                float4 vfx = float4(0.0, 0.0, 0.0, 0.0);

                if (_IsAuthoredFbx > 0.5)
                {
                    p = float3(p.x, -p.z, p.y);
                    n = float3(n.x, -n.z, n.y);
                }

                if (st.z < 0.5)
                {
                    // Siege wreck cell: tilted charred beams and iron hardware; flattened if cleared.
                    uint cx;
                    uint cy;
                    tint = SamplePropAlbedo(input.uv, tint, cx, cy);
                    p *= st.x;
                    float yScale = max(0.05, st.w > 0.001 ? st.w : st.x);
                    p.y *= yScale;
                    p = RotateY(p, yaw);
                    n = RotateY(n, yaw);
                    n = normalize(float3(n.x, n.y / yScale, n.z));
                    if (st.y > 0.5)
                    {
                        tint = lerp(tint, half3(0.28, 0.27, 0.26), 0.72h);
                    }
                }
                else if (st.z < 2.5)
                {
                    // Stone landing salvo marker: flash pulse near impact time, billowing impact dust cloud, crater & stone core.
                    float age = max(0.0, st.x);
                    float crushed = st.y;
                    float flash = saturate(1.0 - age / 1.8);
                    float ringPulse = 1.0 + 0.55 * flash;
                    bool isRing = input.color.a < 0.6;
                    bool isDust = input.color.a >= 0.6 && input.color.a < 0.9;
                    bool isCrushCrest = input.color.r > 0.8 && input.color.g < 0.35;

                    if (isCrushCrest)
                    {
                        if (crushed < 0.5)
                        {
                            p = 0.0;
                        }
                        else
                        {
                            float crestScale = 1.0 + min(3.0, crushed) * 0.25 + 0.35 * flash;
                            p.xz *= crestScale;
                            p.y *= crestScale;
                            tint = lerp(half3(0.88, 0.16, 0.12), half3(1.0, 0.52, 0.18), (half)flash);
                        }
                    }
                    else if (isDust)
                    {
                        float dustLife = saturate(1.0 - age / 2.2);
                        if (dustLife <= 0.01)
                        {
                            p = 0.0;
                        }
                        else
                        {
                            float grow = 0.6 + (1.0 - dustLife) * 2.4;
                            p.xz *= grow;
                            p.y = p.y * (0.7 + (1.0 - dustLife) * 1.9) + (1.0 - dustLife) * 1.4;
                            p *= dustLife;
                            p = RotateY(p, yaw);
                            n = RotateY(n, yaw);
                            tint = half3(0.82, 0.74, 0.60);
                            selfLit = 0.55h;
                            vfx = float4(2.0, saturate(p.y / 2.5), age * 3.0 + p.x, 0.8);
                        }
                    }
                    else if (isRing)
                    {
                        p.xz *= ringPulse;
                        half3 safeRing = lerp(half3(0.55, 0.49, 0.42), half3(1.0, 0.72, 0.22), (half)flash);
                        half3 fatalRing = lerp(half3(0.85, 0.20, 0.14), half3(1.0, 0.45, 0.15), (half)flash);
                        tint = crushed > 0.5 ? fatalRing : safeRing;
                    }
                    else
                    {
                        p = RotateY(p, yaw);
                        n = RotateY(n, yaw);
                        tint = lerp(tint, half3(0.95, 0.65, 0.32), (half)(flash * 0.45));
                    }
                }
                else if (st.z < 3.5)
                {
                    // Standing or falling grove tree: pitches around its base along yaw heading.
                    uint cx;
                    uint cy;
                    tint = SamplePropAlbedo(input.uv, tint, cx, cy);
                    p *= st.x;
                    float fall = saturate(st.w);
                    if (fall > 0.001)
                    {
                        float fallAngle = (fall * fall) * 1.5707963;
                        p = PitchForwardZ(p, fallAngle);
                        n = PitchForwardZ(n, fallAngle);
                    }
                    p = RotateY(p, yaw);
                    n = RotateY(n, yaw);
                }
                else if (st.z < 4.5)
                {
                    // Felled tree trunk + crown across cells.
                    uint cx;
                    uint cy;
                    tint = SamplePropAlbedo(input.uv, tint, cx, cy);
                    p *= st.x;
                    float yScale = max(0.05, st.w);
                    p.y *= yScale;
                    p = RotateY(p, yaw);
                    n = RotateY(n, yaw);
                    n = normalize(float3(n.x, n.y / yScale, n.z));
                    if (st.y > 0.5)
                    {
                        tint = lerp(tint, half3(0.28, 0.27, 0.26), 0.72h);
                    }
                }
                else if (st.z < 5.5)
                {
                    // Fire patch cell: turbulent flame licks + ember core glow, or charred ashes once extinguished.
                    uint cx;
                    uint cy;
                    tint = SamplePropAlbedo(input.uv, tint, cx, cy);
                    p *= st.x;
                    float phase = st.y;
                    if (st.w > 0.5)
                    {
                        p.y *= 0.22;
                        tint = lerp(tint, half3(0.18, 0.16, 0.15), 0.75h);
                    }
                    else
                    {
                        bool isFlameTongue = (_IsAuthoredFbx > 0.5 && cy == 1u && cx >= 6u && cx <= 7u)
                            || (_IsAuthoredFbx <= 0.5 && input.color.g > 0.35);
                        bool isEmber = (_IsAuthoredFbx > 0.5 && cy == 1u && cx == 5u)
                            || (_IsAuthoredFbx <= 0.5 && !isFlameTongue);
                        float flameH = saturate(p.y / max(0.01, st.x * 1.32));
                        if (isFlameTongue)
                        {
                            float flicker = sin(_TimeSeconds * 11.0 + phase * 6.283185 + p.x * 4.5) * 0.16
                                + sin(_TimeSeconds * 19.3 + phase * 12.56637 + p.z * 5.2) * 0.09
                                + cos(_TimeSeconds * 27.1 + p.y * 6.0) * 0.05;
                            float swirlA = _TimeSeconds * 7.2 + phase * 6.283185 + flameH * 2.4;
                            p.y *= max(0.42, 1.0 + flicker * flameH);
                            p.x += cos(swirlA) * 0.11 * flameH * st.x;
                            p.z += sin(swirlA * 1.13) * 0.11 * flameH * st.x;
                            selfLit = 1.0h;
                            float isInnerCore = (_IsAuthoredFbx > 0.5 && cx == 7u) ? 1.0 : 0.65;
                            vfx = float4(1.0, flameH, phase + p.y * 1.7 + p.x * 2.3, isInnerCore);
                        }
                        else if (isEmber)
                        {
                            float throb = 0.04 * sin(_TimeSeconds * 6.4 + phase * 6.283185);
                            p *= (1.0 + throb);
                            selfLit = 0.72h;
                            vfx = float4(1.0, 0.05, phase + p.x * 3.1 + p.z * 2.7, 0.25);
                        }
                    }
                    p = RotateY(p, yaw);
                    n = RotateY(n, yaw);
                }
                else if (st.z < 6.5)
                {
                    // Lightning strike spire + expanding ground shockwave.
                    float age = max(0.0, st.x);
                    bool isRing = input.color.a < 0.6;
                    if (isRing)
                    {
                        float waveLife = saturate(1.0 - age / 1.4);
                        float ringScale = 1.0 + age * 8.0;
                        p.xz *= ringScale;
                        p.y *= waveLife;
                        if (waveLife <= 0.001)
                        {
                            p = 0.0;
                        }
                        tint = lerp(half3(1.0, 0.68, 0.18), half3(1.0, 0.95, 0.65), (half)waveLife);
                        selfLit = 0.92h;
                    }
                    else
                    {
                        float boltLife = saturate(1.0 - age / 0.85);
                        p.xz *= (0.35 + 1.15 * boltLife);
                        p.y *= boltLife;
                        if (boltLife <= 0.001)
                        {
                            p = 0.0;
                        }
                        tint = half3(1.0, 0.97, 0.72);
                        selfLit = 1.0h;
                    }
                }
                else if (st.z < 7.5)
                {
                    // Herd bounding ring, billowing hoof/stampede dust plumes, and driving chevron.
                    float mode = st.x;
                    float driven = st.y;
                    float herdYaw = st.w;
                    float radiusM = max(2.0, inst.w);
                    bool isRing = input.color.a < 0.6;
                    bool isDust = input.color.a >= 0.6 && input.color.a < 0.9;
                    bool isStampede = mode >= 1.5 && mode < 2.5;
                    bool isAgitated = mode > 0.5 && mode < 1.5;

                    half3 ringColor = driven > 0.5
                        ? half3(0.58, 0.82, 0.42)
                        : half3(0.76, 0.68, 0.44);
                    if (isAgitated)
                    {
                        ringColor = half3(1.00, 0.78, 0.14);
                    }
                    else if (isStampede)
                    {
                        ringColor = half3(1.00, 0.28, 0.10);
                    }
                    else if (mode >= 2.5)
                    {
                        ringColor = half3(0.58, 0.56, 0.50);
                    }
                    tint = ringColor;
                    selfLit = 0.78h;

                    if (isRing)
                    {
                        float pulse = 1.0;
                        if (isAgitated)
                        {
                            pulse = 1.0 + 0.04 * sin(_TimeSeconds * 7.5);
                        }
                        else if (isStampede)
                        {
                            pulse = 1.0 + 0.06 * sin(_TimeSeconds * 12.0);
                        }
                        p.xz *= radiusM * pulse;
                    }
                    else if (isDust)
                    {
                        bool showDust = isStampede || isAgitated || driven > 0.5;
                        if (!showDust || mode >= 2.5)
                        {
                            p = 0.0;
                        }
                        else
                        {
                            float puffHash = p.x * 3.7 + p.z * 5.3 + inst.x * 0.13 + inst.z * 0.17;
                            float billow = 0.5 + 0.5 * sin(_TimeSeconds * (isStampede ? 8.5 : 4.2) + puffHash);
                            float drift = cos(_TimeSeconds * (isStampede ? 6.8 : 3.4) + puffHash * 1.3);
                            p.xz *= radiusM * (isStampede ? 1.05 : 0.92);
                            if (isStampede)
                            {
                                p.z -= (0.45 + 0.35 * saturate(-p.z / max(1.0, radiusM))) * radiusM + billow * 0.85;
                                p.x *= 1.15 + 0.22 * drift;
                                p.y = p.y * (1.85 + 0.85 * billow) + 0.15;
                            }
                            else
                            {
                                p.x += drift * 0.45;
                                p.z += cos(_TimeSeconds * 3.1 + puffHash) * 0.45;
                                p.y = p.y * (1.05 + 0.45 * billow) + 0.05;
                            }
                            p = RotateY(p, herdYaw);
                            n = RotateY(n, herdYaw);
                            tint = isStampede ? half3(0.84, 0.72, 0.50) : half3(0.76, 0.68, 0.52);
                            selfLit = 0.58h;
                            vfx = float4(2.0, saturate(p.y / 2.2), puffHash + _TimeSeconds * 2.5, isStampede ? 1.0 : 0.55);
                        }
                    }
                    else
                    {
                        bool showChevron = isAgitated || isStampede || driven > 0.5;
                        if (!showChevron)
                        {
                            p = 0.0;
                        }
                        else
                        {
                            p.z += radiusM + 1.0;
                            p = RotateY(p, herdYaw);
                            n = RotateY(n, herdYaw);
                        }
                    }
                }
                else if (st.z < 8.5)
                {
                    // Static terrain props: boulders, hedgerows (with gentle wind sway), drystone walls, herd pens.
                    uint cx;
                    uint cy;
                    tint = SamplePropAlbedo(input.uv, tint, cx, cy);
                    p.x *= st.x;
                    p.y *= st.y;
                    p.z *= st.x;
                    if (st.w > 0.001)
                    {
                        float sway = sin(_TimeSeconds * 2.2 + inst.x * 0.23 + inst.z * 0.19) * st.w * saturate(p.y);
                        p.x += sway;
                        p.z += sway * 0.6;
                    }
                    p = RotateY(p, yaw);
                    n = RotateY(n, yaw);
                }
                else if (st.z < 9.5)
                {
                    // Mid-air tumbling siege stone projectile.
                    float spin = st.w;
                    p *= st.x;
                    p = PitchForwardZ(p, spin);
                    n = PitchForwardZ(n, spin);
                    p = RotateY(p, spin * 0.65);
                    n = RotateY(n, spin * 0.65);
                    tint = lerp(tint, half3(0.85, 0.78, 0.68), 0.25h);
                    selfLit = 0.45h;
                }
                else
                {
                    // Mid-air feathered arrow oriented along exact 3D parabolic tangent (st.x, st.y, st.w).
                    float3 fwd = normalize(float3(st.x, st.y, st.w) + float3(0.0, 0.0, 1e-5));
                    float3 upRef = abs(fwd.y) > 0.95 ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0);
                    float3 right = normalize(cross(upRef, fwd));
                    float3 up = cross(fwd, right);
                    // Scale slightly with camera distance so arrow volleys read cleanly at strategic zoom too.
                    float camDist = length(_WorldSpaceCameraPos - inst.xyz);
                    float arrowScale = lerp(1.0, 2.2, smoothstep(120.0, 520.0, camDist));
                    float3 sp = p * arrowScale;
                    p = right * sp.x + up * sp.y + fwd * sp.z;
                    n = normalize(right * n.x + up * n.y + fwd * n.z);
                    selfLit = 0.72h;
                }

                float3 worldPos = inst.xyz + p;
                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = n;
                output.color = half4(tint, selfLit);
                output.vfx = vfx;
                output.worldPos = worldPos;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 col = input.color.rgb;
                if (input.vfx.x > 0.75 && input.vfx.x < 1.5)
                {
                    // Active fire: 3-band thermal gradient (white-yellow core -> orange mid -> soot tip) + pulsing embers.
                    float flicker = 0.5 + 0.5 * sin(_TimeSeconds * 9.5 + input.vfx.z * 3.1);
                    if (input.vfx.w < 0.4)
                    {
                        col = lerp(col * (half)0.45, half3(0.96, 0.28, 0.05), (half)(0.35 + 0.65 * flicker));
                    }
                    else
                    {
                        float h = saturate(input.vfx.y + (flicker - 0.5) * 0.14);
                        half3 coreCol = input.vfx.w > 0.85 ? half3(1.00, 0.96, 0.68) : half3(1.00, 0.80, 0.30);
                        half3 midCol = half3(0.98, 0.42, 0.08);
                        half3 tipCol = half3(0.32, 0.12, 0.09);
                        col = h < 0.52
                            ? lerp(coreCol, midCol, (half)(h / 0.52))
                            : lerp(midCol, tipCol, (half)((h - 0.52) / 0.48));
                    }
                }
                else if (input.vfx.x >= 1.5)
                {
                    // Herd agitation / stampede dust cloud shading: dark churned soil at bottom, warm sunlit ochre crest above.
                    float billowNoise = 0.5 + 0.5 * sin(input.vfx.z * 1.9);
                    half3 earthBase = half3(0.38, 0.30, 0.21);
                    half3 sunlitCrest = half3(0.90, 0.80, 0.58);
                    col = lerp(earthBase, sunlitCrest, (half)saturate(input.vfx.y * 0.75 + billowNoise * 0.35));
                    col = lerp(col, half3(0.95, 0.82, 0.54), (half)(input.vfx.w * 0.25));
                }

                half3 shaded = GodGameShade(input.normalWS, input.worldPos, col, 0.14);
                half3 lit = max(shaded, col * input.color.a);
                lit = GodGameApplyHaze(lit, input.worldPos, 0.65);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }
}
