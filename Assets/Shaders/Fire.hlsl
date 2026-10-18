#ifndef LOFILIFE_FIRE_INCLUDED
#define LOFILIFE_FIRE_INCLUDED

// One stylised flame on a particle quad, used by Fire.shadergraph (Custom Function node "Flame").
// Scrolling value noise eats into a teardrop: hot yellow core, orange edge. No textures, two noise taps.
// UV: the particle's UV0 (0..1, y up). Tint: the particle colour; r is a per-flame random offset so flames
// never move in sync, a is the fade (the flame shrinks and breaks up as it dies).
// Kept just over 1 so bloom (threshold 0.9) gives a soft halo without washing the core out to white.

float FireHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float FireNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(FireHash(i), FireHash(i + float2(1, 0)), f.x),
                lerp(FireHash(i + float2(0, 1)), FireHash(i + float2(1, 1)), f.x), f.y);
}

void Flame_float(float4 UV, float4 Tint, out float3 Color, out float Alpha)
{
    float2 uv = float2(UV.x - 0.5, UV.y);
    float t = _TimeParameters.x;
    float2 p = uv * float2(3.0, 2.6) - float2(0.0, t * 2.6) + Tint.r * 7.0;
    float n = FireNoise(p) * 0.65 + FireNoise(p * 2.1) * 0.35;

    float x = uv.x + (n - 0.5) * 0.3 * uv.y;
    float y = saturate(uv.y);
    float width = 0.36 * pow(1.0 - y, 0.9) * smoothstep(-0.08, 0.22, uv.y);
    float v = 1.0 - abs(x) / max(width, 1e-3) - n * 0.75 * y - y * 0.15;
    v -= (1.0 - Tint.a) * 0.6;

    float a = smoothstep(0.0, 0.18, v);
    float core = smoothstep(0.3, 0.75, v);
    float3 c = lerp(float3(1.0, 0.25, 0.05), float3(1.0, 0.68, 0.18), core);
    c += float3(1.0, 0.9, 0.6) * core * core * core * 0.3;
    Color = c * 1.25;
    Alpha = a;
}

void Flame_half(half4 UV, half4 Tint, out half3 Color, out half Alpha)
{
    float3 c;
    float a;
    Flame_float(UV, Tint, c, a);
    Color = c;
    Alpha = a;
}

#endif
