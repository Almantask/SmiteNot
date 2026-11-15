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
                    // Stone landing salvo marker: flash pulse near impact time, crater & stone core stay readable.
                    float age = max(0.0, st.x);
                    float crushed = st.y;
                    float flash = saturate(1.0 - age / 1.8);
                    float ringPulse = 1.0 + 0.55 * flash;
                    bool isRing = input.color.a < 0.6;
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
                    // Fire patch cell: animated flame sway/flicker or charred embers once out.
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
                        float pulse = sin(_TimeSeconds * 11.0 + phase * 6.283185) * 0.16
                            + sin(_TimeSeconds * 19.3 + phase * 12.56637) * 0.07;
                        p.y *= max(0.35, 1.0 + pulse * saturate(p.y * 0.9));
                        p.x += cos(_TimeSeconds * 8.5 + phase * 6.283185) * 0.06 * saturate(p.y);
                        if ((_IsAuthoredFbx > 0.5 && cy == 1u && cx >= 5u && cx <= 7u)
                            || (_IsAuthoredFbx <= 0.5 && input.color.r > 0.7))
                        {
                            selfLit = 0.95h;
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
                else
                {
                    // Herd bounding ring and stampede/driving chevron.
                    float mode = st.x;
                    float driven = st.y;
                    float herdYaw = st.w;
                    float radiusM = max(2.0, inst.w);
                    bool isRing = input.color.a < 0.6;

                    half3 ringColor = driven > 0.5
                        ? half3(0.58, 0.82, 0.42)
                        : half3(0.76, 0.68, 0.44);
                    if (mode > 0.5 && mode < 1.5)
                    {
                        ringColor = half3(1.00, 0.78, 0.14);
                    }
                    else if (mode >= 1.5 && mode < 2.5)
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
                        if (mode > 0.5 && mode < 1.5)
                        {
                            pulse = 1.0 + 0.04 * sin(_TimeSeconds * 7.5);
                        }
                        else if (mode >= 1.5 && mode < 2.5)
                        {
                            pulse = 1.0 + 0.06 * sin(_TimeSeconds * 12.0);
                        }
                        p.xz *= radiusM * pulse;
                    }
                    else
                    {
                        bool showChevron = (mode > 0.5 && mode < 2.5) || driven > 0.5;
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

                Varyings output;
                output.positionCS = TransformWorldToHClip(inst.xyz + p);
                output.normalWS = n;
                output.color = half4(tint, selfLit);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float light = 0.45 + 0.55 * saturate(dot(normalize(input.normalWS), normalize(float3(0.4, 1.0, 0.3))));
                float shade = max(light, (float)input.color.a);
                return half4(input.color.rgb * (half)shade, 1.0h);
            }
            ENDHLSL
        }
    }
}
