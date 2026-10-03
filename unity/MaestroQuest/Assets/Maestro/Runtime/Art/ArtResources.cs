// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Art
{
    static class ArtResources
    {
        public static void Release(Object resource)
        {
            if (!resource) return;
            if (Application.isPlaying) Object.Destroy(resource);
            else Object.DestroyImmediate(resource);
        }
    }
}
