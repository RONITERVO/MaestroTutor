// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Art
{
    // Unity may omit OnDestroy on components which were never active. Every
    // resource-owning component used in an inactive room candidate supplies the
    // same idempotent cleanup for rollback and ordinary Unity destruction.
    internal interface INativeResourceOwner { void ReleaseNativeResources(); }
    internal static class NativeResourceLifetime
    {
        internal static void Release(GameObject root)
        {
            if(!root)return;
            foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if(component is INativeResourceOwner owner)owner.ReleaseNativeResources();
        }
    }
}
