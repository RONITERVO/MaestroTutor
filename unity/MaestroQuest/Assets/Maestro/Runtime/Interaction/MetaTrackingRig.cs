// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Interaction
{
    /// <summary>MRUK tracking-space contract. Existing OpenXR/Input System drivers retain pose ownership.</summary>
    public sealed class MetaTrackingRig : OVRCameraRig
    {
        protected override void UpdateAnchors(bool eyes, bool hands) { }
    }
}
