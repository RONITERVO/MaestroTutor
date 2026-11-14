// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#ifndef COSMOS_COMMON_INCLUDED
#define COSMOS_COMMON_INCLUDED

#define COSMOS_PI 3.14159265358979323846
#define COSMOS_TAU 6.28318530717958647692
#define COSMOS_INV_PI 0.31830988618379067154

// --- OLED Luminance & Sobotka AgX Curve ---

float CosmosLuminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// Preserves literal #000000 OLED deep-space void by clamping sub-epsilon residuals to zero.
float3 CosmosOledVoidClamp(float3 c)
{
    float lum = CosmosLuminance(c);
    return (lum > 1e-5) ? max(c, 0.0) : float3(0.0, 0.0, 0.0);
}

float3 CosmosAgxContrastPoly(float3 x)
{
    float3 x2 = x * x;
    float3 x4 = x2 * x2;
    return 15.5 * x4 * x2
        - 40.14 * x4 * x
        + 31.96 * x4
        - 6.868 * x2 * x
        + 0.4298 * x2
        + 0.1191 * x
        - 0.00232;
}

// In-shader Troy Sobotka AgX Rec.709 OLED curve (single-pass, 4x MSAA TBDR safe).
// Inputs and outputs are linear Rec.709 RGB matching Unity ColorSpace.Linear.
float3 CosmosAgxOled(float3 hdrRgb)
{
    float3 hdr = max(hdrRgb, 0.0);
    float maxCh = max(hdr.r, max(hdr.g, hdr.b));
    if (maxCh <= 1e-5)
    {
        return float3(0.0, 0.0, 0.0);
    }

    const float minEv = -12.473931188;
    const float maxEv = 4.026068812;

    float3 val = max(float3(
        dot(float3(0.842479062, 0.078433600, 0.079223745), hdr),
        dot(float3(0.042328242, 0.878468636, 0.079166127), hdr),
        dot(float3(0.042375655, 0.078433600, 0.879142974), hdr)
    ), 1e-10);

    val = saturate((log2(val) - minEv) / (maxEv - minEv));
    val = saturate(CosmosAgxContrastPoly(val));

    val = max(float3(
        dot(float3( 1.196879005, -0.098020881, -0.099029744), val),
        dot(float3(-0.052896852,  1.151903130, -0.098961177), val),
        dot(float3(-0.052971636, -0.098043450,  1.151073673), val)
    ), 0.0);

    // Smoothly blend ultra-dark linear nebula tail (<0.004) into AgX so faint clouds never clip hard.
    float agxBlend = smoothstep(0.0, 0.004, maxCh);
    float3 blended = lerp(hdr, val, agxBlend);

    // Subtle OLED saturation punch preserving literal zero floor.
    float luma = CosmosLuminance(blended);
    float3 punched = saturate(lerp(luma.xxx, blended, 1.08));
    return CosmosOledVoidClamp(punched);
}

// Gentle shoulder for additive (Blend One One) layers so overlapping glows retain chromaticity.
float3 CosmosSoftAdditive(float3 rgb)
{
    float3 c = max(rgb, 0.0);
    float peak = max(c.r, max(c.g, c.b));
    if (peak <= 1e-5)
    {
        return float3(0.0, 0.0, 0.0);
    }
    float compressed = peak / (1.0 + peak * 0.42);
    return CosmosOledVoidClamp(c * (compressed / peak));
}

// --- Hashes & Low-ALU 3D Procedural Noise (Quest 3 Mobile VR Optimized) ---

float CosmosHash13(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

float3 CosmosHash33(float3 p3)
{
    p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yxx) * p3.zyx);
}

float CosmosNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = p - i;
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float n000 = CosmosHash13(i + float3(0.0, 0.0, 0.0));
    float n100 = CosmosHash13(i + float3(1.0, 0.0, 0.0));
    float n010 = CosmosHash13(i + float3(0.0, 1.0, 0.0));
    float n110 = CosmosHash13(i + float3(1.0, 1.0, 0.0));
    float n001 = CosmosHash13(i + float3(0.0, 0.0, 1.0));
    float n101 = CosmosHash13(i + float3(1.0, 0.0, 1.0));
    float n011 = CosmosHash13(i + float3(0.0, 1.0, 1.0));
    float n111 = CosmosHash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);

    float nxy0 = lerp(nx00, nx10, u.y);
    float nxy1 = lerp(nx01, nx11, u.y);
    return lerp(nxy0, nxy1, u.z) * 2.0 - 1.0;
}

float CosmosFbm3(float3 p)
{
    float sum = 0.50 * CosmosNoise3(p);
    p = p * 2.03 + float3(13.7, -9.2, 5.4);
    sum += 0.25 * CosmosNoise3(p);
    p = p * 2.03 + float3(13.7, -9.2, 5.4);
    sum += 0.125 * CosmosNoise3(p);
    return sum;
}

float CosmosFbm4(float3 p)
{
    float sum = 0.50 * CosmosNoise3(p);
    p = p * 2.03 + float3(13.7, -9.2, 5.4);
    sum += 0.25 * CosmosNoise3(p);
    p = p * 2.03 + float3(13.7, -9.2, 5.4);
    sum += 0.125 * CosmosNoise3(p);
    p = p * 2.03 + float3(13.7, -9.2, 5.4);
    sum += 0.0625 * CosmosNoise3(p);
    return sum;
}

// --- Color Physics ---

float CosmosPowSafe(float x, float p)
{
    return pow(max(x, 1e-6), p);
}

float CosmosAtan2Safe(float y, float x)
{
    return (abs(x) + abs(y) > 1e-6) ? atan2(y, x) : 0.0;
}

float3 CosmosGammaToLinear(float3 col)
{
    col = saturate(col);
    return col * (col * (col * 0.305306011 + 0.682171111) + 0.012522878);
}

// Physically grounded Planckian blackbody color (Kelvin in [1000..40000]) in linear Rec.709 RGB.
float3 CosmosBlackbody(float kelvinIn)
{
    float t = clamp(kelvinIn, 1000.0, 40000.0) * 0.01;
    float3 rgb = 1.0;
    if (t <= 66.0)
    {
        rgb.r = 1.0;
        rgb.g = saturate((99.4708025861 * log(max(t, 1.0)) - 161.1195681661) / 255.0);
        rgb.b = (t <= 19.0) ? 0.0 : saturate((138.5177312231 * log(max(t - 10.0, 1.0)) - 305.0447927307) / 255.0);
    }
    else
    {
        float tp = max(t - 60.0, 1.0);
        rgb.r = saturate((329.698727446 * pow(tp, -0.1332047592)) / 255.0);
        rgb.g = saturate((288.1221695283 * pow(tp, -0.0755148492)) / 255.0);
        rgb.b = 1.0;
    }
    return CosmosGammaToLinear(rgb);
}

// Thin-film spectral cosine interference palette in linear RGB.
float3 CosmosIridescence(float cosTheta, float filmPhase)
{
    float angleTerm = 1.0 - saturate(abs(cosTheta));
    float phase = filmPhase + angleTerm * 2.4;
    float3 col = 0.5 + 0.5 * cos(COSMOS_TAU * (phase + float3(0.0, 0.33, 0.67)));
    return CosmosGammaToLinear(col);
}

float CosmosMedian3(float a, float b, float c)
{
    return max(min(a, b), min(max(a, b), c));
}

float CosmosResolveTime(float customTime)
{
    return (abs(customTime) > 1e-4) ? customTime : _Time.y;
}

#endif
