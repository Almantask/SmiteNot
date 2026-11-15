// The ground shader: height tint and elevation biome blending using seamless ground tiles (riverbed, grass, woodland,
// packed road), 2 m contour lines, wet soil porosity darkening + puddle sheen and churned mud + rut relief from the
// simulation's wetness and mud fields (_Fields: see GodGame.Bridge.FieldUpload), plus an overlay texture blended on top,
// or with _FieldMode 2, the foot speed the fields leave. Presentation only.
Shader "GodGame/DebugGround"
{
    Properties
    {
        _Overlay ("Overlay", 2D) = "black" {}
        _GrassTex ("Grass tile", 2D) = "white" {}
        _RoadTex ("Road tile", 2D) = "white" {}
        _MudTex ("Mud tile", 2D) = "white" {}
        _RiverbedTex ("Riverbed tile", 2D) = "white" {}
        _WoodlandTex ("Woodland tile", 2D) = "white" {}
        _TileScale ("Tile world scale (m)", Float) = 12.0
        _HeightRange ("Height range (min, max) m", Vector) = (0, 10, 0, 0)
        _FieldMode ("Fields: 0 off, 1 wet and mud, 2 foot speed", Float) = 1
    }
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
            #include "Fields.hlsl"

            TEXTURE2D(_Overlay);
            SAMPLER(sampler_point_clamp);
            TEXTURE2D(_GrassTex);
            TEXTURE2D(_RoadTex);
            TEXTURE2D(_MudTex);
            TEXTURE2D(_RiverbedTex);
            TEXTURE2D(_WoodlandTex);
            SAMPLER(sampler_linear_repeat);
            float _TileScale;
            float4 _HeightRange;
            float _FieldMode;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.height = world.y;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.worldPos = world;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 nWS = normalize(input.normalWS);
                float slope = 1.0 - saturate(nWS.y);
                float t = saturate((input.height - _HeightRange.x) / max(_HeightRange.y - _HeightRange.x, 0.001));

                float2 tileUv = input.worldPos.xz / max(_TileScale, 1.0);
                float2 macroUv = tileUv * 0.27 + float2(0.17, 0.31);
                half3 grassTile = SAMPLE_TEXTURE2D(_GrassTex, sampler_linear_repeat, tileUv).rgb;
                half3 roadTile = SAMPLE_TEXTURE2D(_RoadTex, sampler_linear_repeat, tileUv * 1.15).rgb;
                half3 mudTile = SAMPLE_TEXTURE2D(_MudTex, sampler_linear_repeat, tileUv * 1.1).rgb;
                half3 riverbedTile = SAMPLE_TEXTURE2D(_RiverbedTex, sampler_linear_repeat, tileUv * 1.2).rgb;
                half3 woodlandTile = SAMPLE_TEXTURE2D(_WoodlandTex, sampler_linear_repeat, tileUv * 0.9).rgb;

                // Natural elevation & slope blend over seamless terrain tiles while keeping height readability.
                float riverMask = 1.0 - smoothstep(0.06, 0.24, t);
                float woodMask = smoothstep(0.58, 0.86, t) * (1.0 - slope * 0.45);
                float ridgeRoadMask = smoothstep(0.18, 0.45, t) * smoothstep(0.04, 0.14, slope) * (1.0 - smoothstep(0.18, 0.34, slope));

                half3 biomeTile = grassTile;
                biomeTile = lerp(biomeTile, riverbedTile, (half)riverMask);
                biomeTile = lerp(biomeTile, woodlandTile, (half)(woodMask * 0.75));
                biomeTile = lerp(biomeTile, roadTile, (half)(ridgeRoadMask * 0.45));

                float3 heightTint = lerp(float3(0.42, 0.50, 0.35), float3(0.66, 0.62, 0.52), t);
                float3 tileLum = lerp(float3(1.0, 1.0, 1.0), (float3)biomeTile * 1.45, 0.55);
                float3 ground = heightTint * tileLum;

                float contour = frac(input.height / 2.0); // a line every 2 m of height
                ground *= contour < 0.04 ? 0.8 : 1.0;

                float3 sunDir = normalize(float3(0.4, 1.0, 0.3));
                float3 effNormal = nWS;
                float specHighlight = 0.0;

                uint4 field = FieldAt(input.uv);
                if (_FieldMode > 0.5)
                {
                    float wetRaw = max(FieldWetness(field), FieldWetnessSmooth(input.uv));
                    float mudRaw = max(FieldMud(field), FieldMudSmooth(input.uv));
                    float wet = saturate((wetRaw - 0.125) / 0.875); // above the natural 8,192
                    float mud = saturate(mudRaw * 1.25);

                    // Wet soil porosity darkening and damp cool slate tinting.
                    float puddleNoise = 0.5 + 0.5 * sin(macroUv.x * 11.3) * cos(macroUv.y * 9.7);
                    float puddleMask = smoothstep(0.42, 0.88, wet * (0.65 + 0.55 * puddleNoise)) * saturate(1.0 - slope * 3.5);
                    ground *= lerp(1.0, 0.55, wet);
                    ground = lerp(ground, ground * float3(0.76, 0.82, 0.88), wet * 0.28);

                    // Churned mud blend: mud tile texture, mire brown darkening, and hoof/wheel rut micro-relief.
                    float3 mudAlbedo = lerp(float3(0.30, 0.19, 0.10), (float3)mudTile * float3(0.56, 0.40, 0.25), 0.72);
                    ground = lerp(ground, mudAlbedo, mud);

                    // Micro-normal rutting & slick wet sheen for rain-soaked / muddy earth.
                    float rutPhaseA = input.worldPos.x * 2.4 + sin(input.worldPos.z * 0.7) * 1.6;
                    float rutPhaseB = input.worldPos.z * 2.1 + cos(input.worldPos.x * 0.8) * 1.4;
                    float3 rutPerturb = float3(cos(rutPhaseA) * 0.22, 0.0, sin(rutPhaseB) * 0.22) * mud;
                    effNormal = normalize(effNormal + rutPerturb);

                    float3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos + float3(0.0, 1e-4, 0.0));
                    float3 halfVec = normalize(sunDir + viewDir);
                    float gloss = saturate(max(wet * 0.78, mud * 0.65));
                    float ndh = saturate(dot(effNormal, halfVec));
                    float puddleSpec = pow(ndh, lerp(18.0, 72.0, gloss)) * lerp(0.18, 0.65, puddleMask);
                    specHighlight = puddleSpec * gloss;
                }

                float light = 0.55 + 0.45 * saturate(dot(effNormal, sunDir));
                float3 shadedGround = ground * light + float3(0.85, 0.92, 0.98) * specHighlight;

                half4 overlay = SAMPLE_TEXTURE2D(_Overlay, sampler_point_clamp, input.uv);
                if (_FieldMode > 1.5)
                {
                    uint code = FieldSpeedCode(field, 0); // foot
                    float3 tint = code == 0 ? float3(0.85, 0.1, 0.1)
                        : code == 1 ? float3(0.95, 0.5, 0.1)
                        : lerp(float3(0.95, 0.85, 0.15), float3(0.2, 0.75, 0.3), saturate((code - 2) / 248.0));
                    overlay = half4(tint, code >= 250 ? 0.0 : 0.65);
                }
                return half4(lerp(shadedGround, overlay.rgb, overlay.a), 1.0);
            }
            ENDHLSL
        }
    }
}
