// Translucent falling rain curtain and gathering storm-cloud shadow for active Rain casts.
// Driven strictly by the simulation's planned cast circle, wind-up and active duration. Presentation only.
Shader "GodGame/RainCurtain"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }
        LOD 100

        Pass
        {
            Name "RainForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // xyz: cast centre at ground (m), w: cast radius (m)
            StructuredBuffer<float4> _RainInstances;
            // x: active rainfall intensity (0..1), y: wind-up gathering progress (0..1), z: windX (m/s), w: windZ (m/s)
            StructuredBuffer<float4> _RainStates;
            float _TimeSeconds;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // a < 0.5 = ground shadow/ripple disc, a >= 0.5 = vertical rain sheet
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 state : TEXCOORD1;
                float kind : TEXCOORD2; // 0 = disc/shadow, 1 = vertical sheet
                float radiusM : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                float4 inst = _RainInstances[input.instanceID];
                float4 st = _RainStates[input.instanceID];
                float radius = max(1.0, inst.w);
                float isSheet = input.color.a >= 0.5 ? 1.0 : 0.0;

                float3 p = input.positionOS;
                p.xz *= radius;
                if (isSheet > 0.5)
                {
                    // Tall rain curtain slanted subtly with prevailing wind.
                    float heightM = 42.0;
                    p.y *= heightM;
                    float lean = p.y / heightM;
                    p.x += clamp(st.z, -8.0, 8.0) * lean * 0.45;
                    p.z += clamp(st.w, -8.0, 8.0) * lean * 0.45;
                }
                else
                {
                    p.y = 0.25;
                }

                Varyings output;
                output.positionCS = TransformWorldToHClip(inst.xyz + p);
                output.uv = input.uv;
                output.state = st;
                output.kind = isSheet;
                output.radiusM = radius;
                return output;
            }

            float RainHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float active = saturate(input.state.x);
                float windUp = saturate(input.state.y);

                if (input.kind < 0.5)
                {
                    // Ground storm-cloud shadow & subtle animated splash ripples inside the cast circle.
                    float2 centered = input.uv * 2.0 - 1.0;
                    float r = length(centered);
                    if (r > 1.0)
                    {
                        discard;
                    }
                    float softEdge = 1.0 - smoothstep(0.70, 1.0, r);
                    float shadowAlpha = softEdge * lerp(0.16 * windUp, 0.30, active);

                    // Pulsing wind-up perimeter ring before downpour starts.
                    float windRing = smoothstep(0.89, 0.96, r) * (1.0 - smoothstep(0.96, 1.0, r));
                    float pulse = 0.55 + 0.45 * sin(_TimeSeconds * 5.0);
                    half3 shadowCol = half3(0.05, 0.09, 0.16);
                    half3 ringCol = half3(0.50, 0.76, 0.96);

                    // Subtle ground raindrop splash glints during active rain.
                    float2 cell = floor(input.uv * max(18.0, input.radiusM * 0.65));
                    float h = RainHash(cell);
                    float rippleT = frac(_TimeSeconds * 2.1 + h * 7.0);
                    float2 local = frac(input.uv * max(18.0, input.radiusM * 0.65)) - 0.5;
                    float ringDist = abs(length(local) - rippleT * 0.42);
                    float splash = (1.0 - smoothstep(0.0, 0.06, ringDist)) * (1.0 - rippleT) * active * softEdge;

                    half3 col = lerp(shadowCol, ringCol, (half)saturate(windRing * windUp * 0.85 + splash * 0.70));
                    float alpha = saturate(shadowAlpha + windRing * windUp * 0.42 * pulse + splash * 0.20);
                    return half4(col, (half)alpha);
                }

                // Vertical rain sheets: crisp diagonal falling rain shafts fading softly at top and bottom.
                if (active <= 0.001)
                {
                    discard;
                }
                float slantU = input.uv.x + input.uv.y * 0.08;
                float colsA = max(28.0, input.radiusM * 1.25);
                float colsB = max(44.0, input.radiusM * 1.9);

                float uA = slantU * colsA;
                float colIdA = floor(uA);
                float colFracA = frac(uA);
                float hA = RainHash(float2(colIdA, 1.7));
                float vA = frac(input.uv.y * 3.8 + _TimeSeconds * (3.8 + hA * 1.4) + hA * 11.0);
                float streakA = (1.0 - smoothstep(0.0, 0.09, abs(colFracA - 0.5)))
                    * smoothstep(0.0, 0.22, vA)
                    * (1.0 - smoothstep(0.55, 0.92, vA));

                float uB = slantU * colsB + 0.37;
                float colIdB = floor(uB);
                float colFracB = frac(uB);
                float hB = RainHash(float2(colIdB, 5.3));
                float vB = frac(input.uv.y * 5.6 + _TimeSeconds * (4.6 + hB * 1.8) + hB * 17.0);
                float streakB = (1.0 - smoothstep(0.0, 0.10, abs(colFracB - 0.5)))
                    * smoothstep(0.0, 0.18, vB)
                    * (1.0 - smoothstep(0.48, 0.86, vB));

                float vertFade = smoothstep(0.0, 0.10, input.uv.y) * (1.0 - smoothstep(0.74, 1.0, input.uv.y));
                float cloudTop = smoothstep(0.80, 1.0, input.uv.y) * 0.16;
                float streak = saturate(streakA * 0.78 + streakB * 0.50) * vertFade;
                float alpha = saturate((streak * 0.36 + cloudTop) * active);
                if (alpha < 0.01)
                {
                    discard;
                }
                half3 rainCol = lerp(half3(0.22, 0.30, 0.42), half3(0.76, 0.88, 0.98), (half)saturate(streak * 1.35));
                return half4(rainCol, (half)alpha);
            }
            ENDHLSL
        }
    }
}
