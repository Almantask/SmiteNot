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

#endif
