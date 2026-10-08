// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#ifndef MAESTRO_WORLD_LIGHTING_INCLUDED
#define MAESTRO_WORLD_LIGHTING_INCLUDED
// Uniforms deliberately are not material properties: one accepted active region,
// no per-object material allocation and no writes to authored appearance data.
float _MaestroLightingEnabled, _WorldLighting;
float4 _MaestroAmbient, _MaestroSun, _MaestroSunDirection;
float3 MaestroLight(float3 worldNormal, float legacyShade)
{
    float3 illumination = _MaestroAmbient.rgb + _MaestroSun.rgb * saturate(dot(normalize(worldNormal), _MaestroSunDirection.xyz));
    return lerp(legacyShade.xxx, illumination, saturate(_MaestroLightingEnabled * _WorldLighting));
}
#endif
