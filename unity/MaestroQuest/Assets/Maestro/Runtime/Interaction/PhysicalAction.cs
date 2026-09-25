// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    /// <summary>A click target on a solid object. Shared capture rules keep tools and pages consistent.</summary>
    public abstract class PhysicalAction : MonoBehaviour
    {
        public string AccessibleName;
        float lastActivated = -1;
        public void Activate()
        {
            if (!isActiveAndEnabled || Time.unscaledTime - lastActivated < .3f) return;
            lastActivated = Time.unscaledTime; OnActivate();
        }
        protected abstract void OnActivate();
    }
}
