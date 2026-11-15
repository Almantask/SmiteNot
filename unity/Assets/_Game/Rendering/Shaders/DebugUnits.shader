// Debug capsules for every soldier, drawn in one indirect instanced call (M2 debug renderer). Each instance reads its
// ground position and regiment from _Units and its body size from _Shapes; the regiment picks a colour from
// _Palette. A plain lambert light keeps shapes readable from the strategic camera. Presentation only.
Shader "GodGame/DebugUnits"
{
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

            StructuredBuffer<float4> _Units;       // xyz: feet on the ground (m), w: palette index
            StructuredBuffer<float4> _Shapes;      // capsule: x=radius, y=height (>0); mesh: x=scale or animPhase, y<=0 (-animBlend), z=cosYaw, w=sinYaw
            StructuredBuffer<float4> _UnitExtras;  // x: tumble pitch spin (rad), y: launched flag (>0)
            StructuredBuffer<float4> _Palette;     // rgb per side/regiment (linear)
            StructuredBuffer<float4> _CellColors;  // 256 linear RGB entries for 16x16 palette grid (cy * 16 + cx)
            StructuredBuffer<float4> _VatPositions;
            StructuredBuffer<float4> _VatNormals;
            int _VatVertexCount;
            int _VatFrameCount;
            int _VatClipCount;
            int _BaseInstance;
            float _IsGhost;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 color : TEXCOORD1;
                float2 capsuleInfo : TEXCOORD2; // x: variant (>0.5 = crew), y: normalised height 0..1
            };

            float3 PitchTumble(float3 v, float pivotY, float sinT, float cosT, bool isPos)
            {
                if (isPos)
                {
                    v.y -= pivotY;
                }
                float py = v.y * cosT + v.z * sinT;
                float pz = -v.y * sinT + v.z * cosT;
                v.y = isPos ? py + pivotY : py;
                v.z = pz;
                return v;
            }

            Varyings Vert(Attributes input)
            {
                uint idx = (uint)_BaseInstance + input.instanceID;
                float4 unit = _Units[idx];
                float4 shape = _Shapes[idx];
                float4 extra = _UnitExtras[idx];
                float tumble = extra.x;
                float sinT;
                float cosT;
                sincos(tumble, sinT, cosT);
                float3 localPos;
                float3 normalWS;
                half3 albedo;
                float capsuleVariant = 0.0;
                float capsuleHeight01 = 0.5;

                if (shape.y > 0.0)
                {
                    // Capsule fallback: Unity's capsule is 2 m tall and 1 m across, centred on the origin.
                    capsuleVariant = shape.z;
                    capsuleHeight01 = saturate((input.positionOS.y + 1.0) * 0.5);
                    localPos = float3(
                        input.positionOS.x * shape.x * 2.0,
                        (input.positionOS.y + 1.0) * shape.y * 0.5,
                        input.positionOS.z * shape.x * 2.0);
                    normalWS = input.normalOS;
                    if (abs(tumble) > 0.0001)
                    {
                        float pivotY = shape.y * 0.5;
                        localPos = PitchTumble(localPos, pivotY, sinT, cosT, true);
                        normalWS = PitchTumble(normalWS, 0.0, sinT, cosT, false);
                    }
                    albedo = (half3)_Palette[(uint)unit.w].rgb;
                }
                else
                {
                    // Authored FBX mesh: either sample pre-baked upright VAT (+Y up, -Z forward) by SV_VertexID, or
                    // transform raw SkinnedMesh vertices from Euler(-90, 0, 0) into Unity upright space, then yaw so -Z
                    // faces (cosYaw, 0, sinYaw).
                    float3 p;
                    float3 n;
                    float strikePulse = 0.0;
                    float deathFlash = 0.0;
                    float corpseShade = 1.0;
                    if (_VatFrameCount > 0 && (int)input.vertexID < _VatVertexCount)
                    {
                        uint clipCount = (uint)max(1, _VatClipCount);
                        uint framesPerClip = (uint)max(1, (int)((uint)_VatFrameCount / clipCount));
                        float animPhase = frac(shape.x);
                        float deathBlend = clipCount > 4u ? saturate(-shape.y - 2.0) : 0.0;
                        float combatBlend = (clipCount > 1u && deathBlend <= 0.001) ? saturate(-shape.y - 1.0) : 0.0;
                        float animBlend = (deathBlend > 0.001 || combatBlend > 0.001) ? 1.0 : saturate(-shape.y);
                        uint targetClip = clipCount > 1u ? clamp((uint)floor(shape.x), 1u, clipCount - 1u) : 0u;
                        float frameCoord = animPhase * (float)framesPerClip;
                        uint vCount = (uint)_VatVertexCount;
                        uint f0 = ((uint)floor(frameCoord)) % framesPerClip;
                        uint f1 = (f0 + 1u) % framesPerClip;
                        float fracF = frac(frameCoord);
                        uint vid = input.vertexID;

                        float3 pWalk = lerp(
                            _VatPositions[f0 * vCount + vid].xyz,
                            _VatPositions[f1 * vCount + vid].xyz,
                            fracF);
                        float3 nWalk = lerp(
                            _VatNormals[f0 * vCount + vid].xyz,
                            _VatNormals[f1 * vCount + vid].xyz,
                            fracF);

                        uint cBase = targetClip * framesPerClip;
                        uint cf0 = f0;
                        uint cf1 = f1;
                        float cFrac = fracF;
                        if (deathBlend > 0.001)
                        {
                            float dCoord = animPhase * (float)(framesPerClip - 1u);
                            cf0 = min((uint)floor(dCoord), framesPerClip - 1u);
                            cf1 = min(cf0 + 1u, framesPerClip - 1u);
                            cFrac = frac(dCoord);
                        }
                        float3 pCbt = lerp(
                            _VatPositions[(cBase + cf0) * vCount + vid].xyz,
                            _VatPositions[(cBase + cf1) * vCount + vid].xyz,
                            cFrac);
                        float3 nCbt = lerp(
                            _VatNormals[(cBase + cf0) * vCount + vid].xyz,
                            _VatNormals[(cBase + cf1) * vCount + vid].xyz,
                            cFrac);

                        float activeBlend = max(combatBlend, deathBlend);
                        float3 pAnim = lerp(pWalk, pCbt, activeBlend);
                        float3 nAnim = lerp(nWalk, nCbt, activeBlend);
                        p = lerp(_VatPositions[vid].xyz, pAnim, animBlend);
                        n = normalize(lerp(_VatNormals[vid].xyz, nAnim, animBlend));

                        if (combatBlend > 0.001)
                        {
                            float dtPeak = (animPhase - 0.43) * 6.5;
                            strikePulse = exp(-(dtPeak * dtPeak)) * combatBlend;
                            p *= (1.0 + 0.08 * strikePulse);
                        }
                        if (deathBlend > 0.001)
                        {
                            float dtHit = (animPhase - 0.08) * 10.0;
                            deathFlash = exp(-(dtHit * dtHit)) * deathBlend * step(animPhase, 0.22);
                            corpseShade = lerp(1.0, 0.62, smoothstep(0.35, 0.95, animPhase) * deathBlend);
                        }
                    }
                    else
                    {
                        float scale = shape.x;
                        float3 vRaw = input.positionOS * scale;
                        p = float3(vRaw.x, -vRaw.z, vRaw.y);
                        float3 nRaw = input.normalOS;
                        n = float3(nRaw.x, -nRaw.z, nRaw.y);
                    }
                    if (abs(tumble) > 0.0001)
                    {
                        p = PitchTumble(p, 0.9, sinT, cosT, true);
                        n = PitchTumble(n, 0.0, sinT, cosT, false);
                    }
                    float cosY = shape.z;
                    float sinY = shape.w;
                    localPos = float3(
                        -p.x * sinY - p.z * cosY,
                        p.y,
                        p.x * cosY - p.z * sinY);
                    normalWS = float3(
                        -n.x * sinY - n.z * cosY,
                        n.y,
                        n.x * cosY - n.z * sinY);

                    uint cx = (uint)clamp(floor(input.uv.x * 16.0), 0.0, 15.0);
                    uint cy = (uint)clamp(floor(input.uv.y * 16.0), 0.0, 15.0);
                    half3 cell = (half3)_CellColors[cy * 16u + cx].rgb;
                    half3 team = (half3)_Palette[(uint)unit.w].rgb;
                    half mask = saturate((half)input.color.r);
                    albedo = lerp(cell, team, mask);
                    albedo *= (half)corpseShade;
                    albedo = lerp(albedo, half3(1.0, 0.96, 0.85), (half)(0.16 * strikePulse));
                    albedo = lerp(albedo, half3(0.95, 0.22, 0.14), (half)(0.45 * deathFlash));
                    albedo = lerp(albedo, team, (half)(_IsGhost * 0.7));
                }

                Varyings output;
                output.positionCS = TransformWorldToHClip(unit.xyz + localPos);
                output.normalWS = normalWS;
                output.color = albedo;
                output.capsuleInfo = float2(capsuleVariant, capsuleHeight01);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = (half3)input.color;
                if (input.capsuleInfo.x > 0.5)
                {
                    // Crew distinct capsule silhouette: dark oiled-leather apron below belt, bright brass belt & warm engineer cap over team tunic.
                    float y01 = input.capsuleInfo.y;
                    half3 crewAlbedo = albedo;
                    if (y01 < 0.38)
                    {
                        crewAlbedo = lerp(half3(0.24, 0.17, 0.12), albedo, 0.20h);
                    }
                    else if (y01 < 0.45)
                    {
                        crewAlbedo = half3(0.76, 0.58, 0.24);
                    }
                    else if (y01 >= 0.78)
                    {
                        crewAlbedo = lerp(albedo, half3(0.92, 0.76, 0.36), 0.65h);
                    }
                    albedo = lerp(crewAlbedo, albedo, (half)(_IsGhost * 0.7));
                }
                float light = 0.45 + 0.55 * saturate(dot(normalize(input.normalWS), normalize(float3(0.4, 1.0, 0.3))));
                return half4(albedo * (half)light, 1.0);
            }
            ENDHLSL
        }
    }
}
