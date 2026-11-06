// The debug water: the ground mesh again, lifted by each cell's water depth from the simulation's fields (_Fields: see
// GodGame.Bridge.FieldUpload) and drawn see-through: pale where shallow, dark blue where deep, with streaks that run
// along the flow at its speed. Cells shallower than the simulation's dry depth (5 mm) are not drawn. Presentation only:
// the depth and flow are the cells the soldiers feel.
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
                world.y += depth * 1e-6 + 0.02;
                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.world = world;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint4 field = FieldAt(input.uv);
                if (field.x < _DryDepthUm)
                {
                    discard;
                }
                float depthM = FieldWaterDepthM(field);
                float2 flow = FieldFlowMPerS(field);
                float speed = length(flow);
                float3 color = lerp(float3(0.45, 0.75, 0.85), float3(0.05, 0.18, 0.45), saturate(depthM / 1.5));
                if (speed > 0.02)
                {
                    float2 along = flow / speed;
                    float2 across = float2(-along.y, along.x);
                    // Streaks 3 m long drift downstream at the water's speed; their gaps wobble across the stream.
                    float phase = dot(input.world.xz, along) / 3.0 - _Time.y * speed / 3.0;
                    float lane = frac(dot(input.world.xz, across) / 1.7);
                    float streak = smoothstep(0.75, 1.0, frac(phase + lane * 0.37)) * smoothstep(0.0, 0.3, lane) * smoothstep(1.0, 0.7, lane);
                    color += streak * saturate(speed / 1.5) * 0.35;
                }
                float alpha = lerp(0.45, 0.85, saturate(depthM / 0.8));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
