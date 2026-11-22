// Shared diorama lighting, atmospheric horizon haze, and lightweight ACES filmic tone mapping for GodGame shaders.
// Warm sunlit key light + cool sky hemisphere fill + warm ground bounce + subtle Fresnel rim + far aerial haze.
// Presentation only; zero gameplay effect.
#ifndef GODGAME_LIGHTING_INCLUDED
#define GODGAME_LIGHTING_INCLUDED

static const float3 GODGAME_SUN_DIR = normalize(float3(0.52, 0.78, 0.35));
static const half3 GODGAME_SUN_COLOR = half3(1.14, 1.05, 0.88);
static const half3 GODGAME_SKY_FILL = half3(0.27, 0.36, 0.50);
static const half3 GODGAME_GROUND_BOUNCE = half3(0.24, 0.20, 0.14);
static const half3 GODGAME_HAZE_COLOR = half3(0.58, 0.69, 0.82);

half3 GodGameToneMap(half3 x)
{
    // Narkowicz ACES filmic curve gives warm sunlit highlight rolloff and rich midtone contrast at 3 ALU ops.
    half3 c = max(x, 0.0h) * 1.04h;
    return saturate((c * (2.51h * c + 0.03h)) / (c * (2.43h * c + 0.59h) + 0.14h));
}

half3 GodGameShade(float3 normalWS, float3 worldPos, half3 albedo, float rimStrength)
{
    float3 n = normalize(normalWS);
    float ndl = saturate(dot(n, GODGAME_SUN_DIR));
    // Sculptural wrap key light gives riverbanks, bluffs, channels and low-poly units crisp 3D depth.
    float wrapNdl = saturate((dot(n, GODGAME_SUN_DIR) + 0.12) / 1.12);
    half3 hemi = lerp(GODGAME_GROUND_BOUNCE, GODGAME_SKY_FILL, (half)(n.y * 0.5 + 0.5));
    half3 lit = albedo * (hemi + GODGAME_SUN_COLOR * (half)(0.64 * wrapNdl + 0.24 * ndl));

    if (rimStrength > 0.001)
    {
        float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos + float3(0.0, 1e-4, 0.0));
        float fresnel = pow(1.0 - saturate(dot(n, viewDir)), 3.0);
        float sunSide = 0.35 + 0.65 * saturate(dot(n, GODGAME_SUN_DIR) * 0.5 + 0.5);
        lit += half3(0.95, 0.96, 1.0) * (half)(fresnel * rimStrength * sunSide);
    }
    return lit;
}

half3 GodGameApplyHaze(half3 color, float3 worldPos, float hazeScale)
{
    float dist = length(_WorldSpaceCameraPos - worldPos);
    // Keep strategic tabletop overview crisp and saturated; soft horizon haze starts past 950 m.
    float haze = saturate((dist - 950.0) / 2200.0) * hazeScale;
    haze = min(haze * haze * (3.0 - 2.0 * haze), 0.26);
    half3 hazed = lerp(color, GODGAME_HAZE_COLOR, (half)haze);
    return GodGameToneMap(hazed);
}

#endif
