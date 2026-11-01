// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
using CoordinateSpace3 = SteamAudio.CoordinateSpace3;

namespace Maestro.AudioEvaluation
{
    [DefaultExecutionOrder(300)]
    public sealed class PcmAnchorFollower : MonoBehaviour
    {
        PcmTransformBinding binding;
        internal void Bind(OwnedLifecycle owner, string id, PcmVoice voice)
        {
            if (binding != null) throw new InvalidOperationException("PCM anchor already bound");
            binding = new PcmTransformBinding(owner, id, voice, transform);
            if (!binding.Refresh()) enabled = false;
        }
        void LateUpdate() { if (binding != null && !binding.Refresh()) enabled = false; }
        void OnDisable() => binding?.Dispose();
    }
    // Main-thread borrowed anchor, refreshed after avatar/world transforms move.
    // Closing an anchor binding never destroys its caller-owned scene hierarchy.
    internal sealed class PcmTransformBinding : IDisposable
    {
        readonly OwnedLifecycle owner;
        readonly string id;
        readonly PcmVoice voice;
        readonly Transform anchor;
        bool closed;
        internal PcmTransformBinding(OwnedLifecycle owner, string id, PcmVoice voice, Transform anchor)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.id = id; this.voice = voice; this.anchor = anchor;
        }
        internal static CoordinateSpace3 Pose(Transform value)
        {
            if (!value) throw new ArgumentException("Missing PCM anchor");
            return new CoordinateSpace3 { origin = Convert(value.position), right = Convert(value.right),
                up = Convert(value.up), ahead = Convert(value.forward) };
        }
        static SteamAudio.Vector3 Convert(Vector3 value) => new() { x = value.x, y = value.y, z = -value.z };
        internal bool Refresh()
        {
            if (closed) return false;
            try
            {
                if (anchor && anchor.gameObject.activeInHierarchy && owner.SetPcmPose(id, voice, Pose(anchor))) return true;
            }
            catch { Dispose(); throw; }
            Dispose(); return false;
        }
        public void Dispose()
        {
            if (closed) return;
            closed = true; owner.StopPcm(id, voice);
        }
    }
}
