// The simulation's fields on the GPU (GodGame.Bridge.FieldUpload): one uint4 per cell, row-major, x fastest. Every
// shader that reads fields decodes them here, in the simulation's units:
//   x  water depth, µm
//   y  flow velocity, mm/s: east + 32768 in the low 16 bits, north + 32768 in the high 16
//   z  mud (0-65535) in the low 16 bits, wetness (0-65535) in the high 16
//   w  navigation speed codes, one byte per class (foot, horse, wheeled): 0 swept, 1 too deep, else speed permille / 4
#ifndef GODGAME_FIELDS_INCLUDED
#define GODGAME_FIELDS_INCLUDED

StructuredBuffer<uint4> _Fields;
float4 _FieldSize; // columns, rows

uint4 FieldAt(float2 uv)
{
    uint2 cell = min((uint2)max(uv * _FieldSize.xy, 0), (uint2)_FieldSize.xy - 1);
    return _Fields[cell.y * (uint)_FieldSize.x + cell.x];
}

float FieldWaterDepthM(uint4 f) { return f.x * 1e-6; }
float2 FieldFlowMPerS(uint4 f) { return float2((float)(f.y & 0xFFFF) - 32768.0, (float)(f.y >> 16) - 32768.0) * 0.001; }
float FieldMud(uint4 f) { return (f.z & 0xFFFF) / 65535.0; }
float FieldWetness(uint4 f) { return (f.z >> 16) / 65535.0; }
uint FieldSpeedCode(uint4 f, uint movementClass) { return (f.w >> (movementClass * 8)) & 0xFF; }

uint4 FieldAtCell(int2 cell)
{
    int2 maxCell = max(int2(0, 0), (int2)_FieldSize.xy - 1);
    int2 clamped = clamp(cell, int2(0, 0), maxCell);
    return _Fields[(uint)clamped.y * (uint)_FieldSize.x + (uint)clamped.x];
}

float FieldWetnessSmooth(float2 uv)
{
    if (_FieldSize.x < 1.0 || _FieldSize.y < 1.0)
    {
        return 0.0;
    }
    float2 coord = uv * _FieldSize.xy - 0.5;
    int2 c0 = (int2)floor(coord);
    float2 f = frac(coord);
    float w00 = FieldWetness(FieldAtCell(c0));
    float w10 = FieldWetness(FieldAtCell(c0 + int2(1, 0)));
    float w01 = FieldWetness(FieldAtCell(c0 + int2(0, 1)));
    float w11 = FieldWetness(FieldAtCell(c0 + int2(1, 1)));
    return lerp(lerp(w00, w10, f.x), lerp(w01, w11, f.x), f.y);
}

float FieldMudSmooth(float2 uv)
{
    if (_FieldSize.x < 1.0 || _FieldSize.y < 1.0)
    {
        return 0.0;
    }
    float2 coord = uv * _FieldSize.xy - 0.5;
    int2 c0 = (int2)floor(coord);
    float2 f = frac(coord);
    float m00 = FieldMud(FieldAtCell(c0));
    float m10 = FieldMud(FieldAtCell(c0 + int2(1, 0)));
    float m01 = FieldMud(FieldAtCell(c0 + int2(0, 1)));
    float m11 = FieldMud(FieldAtCell(c0 + int2(1, 1)));
    return lerp(lerp(m00, m10, f.x), lerp(m01, m11, f.x), f.y);
}

float FieldWaterDepthSmoothM(float2 uv)
{
    if (_FieldSize.x < 1.0 || _FieldSize.y < 1.0)
    {
        return 0.0;
    }
    float2 coord = uv * _FieldSize.xy - 0.5;
    int2 c0 = (int2)floor(coord);
    float2 f = frac(coord);
    float d00 = FieldWaterDepthM(FieldAtCell(c0));
    float d10 = FieldWaterDepthM(FieldAtCell(c0 + int2(1, 0)));
    float d01 = FieldWaterDepthM(FieldAtCell(c0 + int2(0, 1)));
    float d11 = FieldWaterDepthM(FieldAtCell(c0 + int2(1, 1)));
    return lerp(lerp(d00, d10, f.x), lerp(d01, d11, f.x), f.y);
}

float2 FieldFlowSmoothMPerS(float2 uv)
{
    if (_FieldSize.x < 1.0 || _FieldSize.y < 1.0)
    {
        return float2(0.0, 0.0);
    }
    float2 coord = uv * _FieldSize.xy - 0.5;
    int2 c0 = (int2)floor(coord);
    float2 f = frac(coord);
    float2 v00 = FieldFlowMPerS(FieldAtCell(c0));
    float2 v10 = FieldFlowMPerS(FieldAtCell(c0 + int2(1, 0)));
    float2 v01 = FieldFlowMPerS(FieldAtCell(c0 + int2(0, 1)));
    float2 v11 = FieldFlowMPerS(FieldAtCell(c0 + int2(1, 1)));
    return lerp(lerp(v00, v10, f.x), lerp(v01, v11, f.x), f.y);
}

#endif
