// The debug ground: a height tint with contour lines, darkened where the soil is wet and browned where it is mud (from
// the simulation's fields, _Fields: see GodGame.Bridge.FieldUpload), plus an overlay texture (cost, flow field, lethal
// distance) blended on top by its alpha, or, with _FieldMode 2, the foot speed the fields leave (green full speed to
// yellow slow, orange too deep, red swept). One texel per simulation cell; point-filtered so cells stay readable.
// Presentation only.
Shader "GodGame/DebugGround"
{
    Properties
    {
        _Overlay ("Overlay", 2D) = "black" {}
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.height = world.y;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = saturate((input.height - _HeightRange.x) / max(_HeightRange.y - _HeightRange.x, 0.001));
                float3 ground = lerp(float3(0.36, 0.45, 0.30), float3(0.62, 0.58, 0.48), t);
                float contour = frac(input.height / 2.0); // a line every 2 m of height
                ground *= contour < 0.04 ? 0.8 : 1.0;
                float light = 0.55 + 0.45 * saturate(dot(normalize(input.normalWS), normalize(float3(0.4, 1.0, 0.3))));
                uint4 field = FieldAt(input.uv);
                if (_FieldMode > 0.5)
                {
                    float wet = saturate((FieldWetness(field) - 0.125) / 0.875); // above the natural 8,192
                    float mud = FieldMud(field);
                    ground *= lerp(1.0, 0.62, wet);
                    ground = lerp(ground, float3(0.33, 0.22, 0.12), saturate(mud * 1.2));
                }
                half4 overlay = SAMPLE_TEXTURE2D(_Overlay, sampler_point_clamp, input.uv);
                if (_FieldMode > 1.5)
                {
                    uint code = FieldSpeedCode(field, 0); // foot
                    float3 tint = code == 0 ? float3(0.85, 0.1, 0.1)
                        : code == 1 ? float3(0.95, 0.5, 0.1)
                        : lerp(float3(0.95, 0.85, 0.15), float3(0.2, 0.75, 0.3), saturate((code - 2) / 248.0));
                    overlay = half4(tint, code >= 250 ? 0.0 : 0.65);
                }
                return half4(lerp(ground * light, overlay.rgb, overlay.a), 1.0);
            }
            ENDHLSL
        }
    }
}
