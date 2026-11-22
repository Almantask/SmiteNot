// Instanced unit renderer (authored VAT meshes + capsule fallback). Reads per-soldier positions from _Units,
// orientation/VAT phase from _Shapes, and tumble/status flags from _UnitExtras. Uses shared diorama lighting
// (warm sunlit key, cool sky fill, subtle Fresnel rim, foot grounding AO, horizon haze) plus readable visual cues
// for swept, teetering, stunned and burning soldiers. Presentation only.
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
            #include "Lighting.hlsl"

            StructuredBuffer<float4> _Units;       // xyz: feet on the ground (m), w: palette index
            StructuredBuffer<float4> _Shapes;      // capsule: x=radius, y=height (>0); mesh: x=scale or animPhase, y<=0 (-animBlend), z=cosYaw, w=sinYaw
            StructuredBuffer<float4> _UnitExtras;  // x: tumble pitch spin (rad), y: launched flag (>0), z: UnitFlags bitmask, w: per-unit phase (0..1)
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
                float3 worldPos : TEXCOORD3;
                float4 status : TEXCOORD4;      // x: localY, y: swept, z: burning, w: phase
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

            float3 RollLean(float3 v, float pivotY, float sinR, float cosR, bool isPos)
            {
                if (isPos)
                {
                    v.y -= pivotY;
                }
                float rx = v.x * cosR - v.y * sinR;
                float ry = v.x * sinR + v.y * cosR;
                v.x = rx;
                v.y = isPos ? ry + pivotY : ry;
                return v;
            }

            Varyings Vert(Attributes input)
            {
                uint idx = (uint)_BaseInstance + input.instanceID;
                float4 unit = _Units[idx];
                float4 shape = _Shapes[idx];
                float4 extra = _UnitExtras[idx];
                uint flags = (uint)(extra.z + 0.5);
                bool isLaunched = extra.y > 0.5 || ((flags & 1u) != 0u);
                bool isStunned = (flags & 2u) != 0u;
                bool isTeetering = (flags & 4u) != 0u;
                bool isSwept = (flags & 8u) != 0u;
                bool isBurning = (flags & 16u) != 0u;
                float unitPhase = extra.w;

                float tumble = extra.x;
                float leanRoll = 0.0;
                float bobY = 0.0;
                if (!isLaunched)
                {
                    if (isSwept)
                    {
                        float sw = _Time.y * 7.5 + unitPhase * 6.283185;
                        tumble += 0.35 + 0.22 * sin(sw);
                        leanRoll = 0.28 * cos(sw * 1.17);
                        bobY = -0.25 + 0.14 * sin(sw * 1.4);
                    }
                    else if (isTeetering)
                    {
                        float wt = _Time.y * 14.0 + unitPhase * 6.283185;
                        leanRoll = 0.32 * sin(wt);
                        tumble += 0.18 * cos(wt * 0.7);
                    }
                    else if (isStunned)
                    {
                        float st = _Time.y * 4.5 + unitPhase * 6.283185;
                        leanRoll = 0.42 + 0.12 * sin(st);
                        bobY = -0.18;
                    }
                }

                float sinT, cosT;
                sincos(tumble, sinT, cosT);
                float sinR, cosR;
                sincos(leanRoll, sinR, cosR);

                float3 localPos;
                float3 normalWS;
                half3 albedo;
                float capsuleVariant = 0.0;
                float capsuleHeight01 = 0.5;
                float localHeightY = 1.0;

                if (shape.y > 0.0)
                {
                    // Capsule fallback: Unity's capsule is 2 m tall and 1 m across, centred on the origin.
                    capsuleVariant = shape.z;
                    capsuleHeight01 = saturate((input.positionOS.y + 1.0) * 0.5);
                    localPos = float3(
                        input.positionOS.x * shape.x * 2.0,
                        (input.positionOS.y + 1.0) * shape.y * 0.5,
                        input.positionOS.z * shape.x * 2.0);
                    localHeightY = localPos.y;
                    normalWS = input.normalOS;
                    if (abs(tumble) > 0.0001)
                    {
                        float pivotY = shape.y * 0.5;
                        localPos = PitchTumble(localPos, pivotY, sinT, cosT, true);
                        normalWS = PitchTumble(normalWS, 0.0, sinT, cosT, false);
                    }
                    if (abs(leanRoll) > 0.0001)
                    {
                        localPos = RollLean(localPos, shape.y * 0.25, sinR, cosR, true);
                        normalWS = RollLean(normalWS, 0.0, sinR, cosR, false);
                    }
                    localPos.y += bobY;
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
                    localHeightY = p.y;
                    if (abs(tumble) > 0.0001)
                    {
                        p = PitchTumble(p, 0.9, sinT, cosT, true);
                        n = PitchTumble(n, 0.0, sinT, cosT, false);
                    }
                    if (abs(leanRoll) > 0.0001)
                    {
                        p = RollLean(p, 0.45, sinR, cosR, true);
                        n = RollLean(n, 0.0, sinR, cosR, false);
                    }
                    p.y += bobY;
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

                // Gentle strategic-zoom silhouette boost so regiments stay crisp & readable from 500+ m up while
                // remaining exact 1:1 scale up close.
                float camDist = length(_WorldSpaceCameraPos - unit.xyz);
                float strategicScale = lerp(1.0, 1.38, smoothstep(260.0, 760.0, camDist));
                float3 worldPos = unit.xyz + localPos * strategicScale;
                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = normalWS;
                output.color = albedo;
                output.capsuleInfo = float2(capsuleVariant, capsuleHeight01);
                output.worldPos = worldPos;
                output.status = float4(localHeightY, isSwept ? 1.0 : 0.0, isBurning ? 1.0 : 0.0, unitPhase);
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

                // Subtle vertical grounding AO near feet so toy soldiers anchor cleanly onto terrain.
                float footAo = lerp(0.76, 1.0, saturate(input.status.x / 0.55));
                albedo *= (half)footAo;

                // Swept in water: foamy splash band near waist/shoulders so swept soldiers pop clearly.
                if (input.status.y > 0.5)
                {
                    float foamBand = 1.0 - smoothstep(0.35, 1.15, abs(input.status.x - 0.65));
                    float splashPulse = 0.65 + 0.35 * sin(_Time.y * 10.0 + input.status.w * 6.283185);
                    albedo = lerp(albedo, half3(0.84, 0.94, 0.99), (half)(foamBand * 0.58 * splashPulse));
                }

                // Burning (cartoon soot + pulsing ember glow, no gore).
                half3 lit;
                if (input.status.z > 0.5)
                {
                    float flicker = 0.5 + 0.5 * sin(_Time.y * 12.0 + input.status.w * 12.56637);
                    albedo = lerp(albedo * 0.35h, half3(1.0, 0.48, 0.10), (half)(0.45 + 0.45 * flicker));
                    lit = max(GodGameShade(input.normalWS, input.worldPos, albedo, 0.18), albedo * 0.85h);
                }
                else
                {
                    float rim = _IsGhost > 0.5 ? 0.0 : 0.22;
                    lit = GodGameShade(input.normalWS, input.worldPos, albedo, rim);
                }

                lit = GodGameApplyHaze(lit, input.worldPos, 0.55);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }
}
