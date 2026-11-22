// Stylized sunlit skybox with golden horizon glow, cerulean zenith, drifting painterly cumulus clouds,
// and a soft studio-slate lower hemisphere so tabletop strategic views frame the map plinth cleanly.
// Presentation only.
Shader "GodGame/StylizedSky"
{
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Lighting.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.viewDir = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }

            float SkyHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float SkyNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = SkyHash(i);
                float b = SkyHash(i + float2(1.0, 0.0));
                float c = SkyHash(i + float2(0.0, 1.0));
                float d = SkyHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float CloudFbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.55;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * SkyNoise(p);
                    p = float2(p.x * 1.92 - p.y * 0.64, p.x * 0.64 + p.y * 1.92) + float2(11.3, 5.7);
                    amp *= 0.50;
                }
                return v;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);
                float y = dir.y;

                // Upper hemisphere: warm sunlit horizon -> vivid midday sky -> rich azure zenith.
                half3 horizonCol = half3(0.76, 0.83, 0.89);
                half3 midSkyCol = half3(0.42, 0.62, 0.84);
                half3 zenithCol = half3(0.22, 0.42, 0.72);

                // Lower hemisphere (seen around the tabletop map edges at strategic zoom): calm desaturated slate-blue.
                half3 lowerMidCol = half3(0.42, 0.52, 0.64);
                half3 nadirCol = half3(0.26, 0.34, 0.44);

                half3 col;
                if (y >= 0.0)
                {
                    float tMid = saturate(y / 0.32);
                    float tZen = saturate((y - 0.22) / 0.78);
                    col = lerp(horizonCol, midSkyCol, (half)smoothstep(0.0, 1.0, tMid));
                    col = lerp(col, zenithCol, (half)smoothstep(0.0, 1.0, tZen));
                }
                else
                {
                    float tDown = saturate(-y / 0.65);
                    col = lerp(horizonCol, lowerMidCol, (half)smoothstep(0.0, 0.35, tDown));
                    col = lerp(col, nadirCol, (half)smoothstep(0.30, 1.0, tDown));
                }

                // Sun glow & crisp solar disc aligned with GODGAME_SUN_DIR.
                float sunDot = saturate(dot(dir, GODGAME_SUN_DIR));
                float sunHalo = pow(sunDot, 10.0) * 0.24 + pow(sunDot, 64.0) * 0.38;
                float sunDisc = smoothstep(0.9985, 0.9996, sunDot);
                col += half3(1.0, 0.92, 0.74) * (half)sunHalo;
                col = lerp(col, half3(1.0, 0.98, 0.90), (half)sunDisc);

                // Soft drifting painterly cumulus clouds above the horizon.
                if (y > 0.015)
                {
                    float2 cloudUv = dir.xz / (y + 0.16) * 0.85 + float2(_Time.y * 0.006, _Time.y * 0.0025);
                    float c = CloudFbm(cloudUv);
                    float cloudMask = smoothstep(0.50, 0.76, c) * smoothstep(0.015, 0.14, y) * (1.0 - smoothstep(0.65, 0.96, y));
                    float cloudLight = smoothstep(0.45, 0.85, CloudFbm(cloudUv + GODGAME_SUN_DIR.xz * 0.12));
                    half3 cloudDark = half3(0.66, 0.74, 0.84);
                    half3 cloudBright = half3(0.98, 0.97, 0.93);
                    half3 cloudCol = lerp(cloudDark, cloudBright, (half)(0.45 + 0.55 * cloudLight));
                    col = lerp(col, cloudCol, (half)(cloudMask * 0.72));
                }

                // Warm horizon atmospheric band right at y = 0.
                float horizonBand = exp(-abs(y) * 14.0);
                col = lerp(col, half3(0.84, 0.86, 0.85), (half)(horizonBand * 0.22));

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
