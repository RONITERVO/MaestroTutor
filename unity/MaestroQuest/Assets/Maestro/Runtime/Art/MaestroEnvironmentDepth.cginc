// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#ifndef MAESTRO_ENVIRONMENT_DEPTH_INCLUDED
#define MAESTRO_ENVIRONMENT_DEPTH_INCLUDED
#include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/BiRP/EnvironmentOcclusionBiRP.cginc"
float _MaestroEnvironmentDepthBypass;
void MaestroOccludeEnvironment(float3 world)
{
#if defined(HARD_OCCLUSION) || defined(SOFT_OCCLUSION)
    if (_MaestroEnvironmentDepthBypass < .5)
    {
        // Discard both pigment and silhouette fragments, leaving passthrough
        // visible without writing invisible geometry into the depth buffer.
        float visibility = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(world, 0.0);
        clip(visibility - .5);
    }
#endif
}
#endif
