// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Meta.XR.EnvironmentDepth;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Maestro.Quest.Interaction
{
    /// <summary>Live room depth follows the physical tracking origin, never artificial locomotion.</summary>
    public sealed class RoomDepthOcclusion : MonoBehaviour
    {
        Transform trackingSpace;
        ARCameraManager passthrough;
        VirtualRoomView virtualView;
        EnvironmentDepthManager depth;
        bool focused = true, paused;
        public bool DepthAvailable => depth && depth.enabled && depth.IsDepthAvailable;
        public void Initialize(Transform tracking, ARCameraManager camera, VirtualRoomView view)
        { trackingSpace = tracking; passthrough = camera; virtualView = view; }

        void LateUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool wanted = focused && !paused && passthrough && passthrough.enabled && (!virtualView || !virtualView.Active);
            if (wanted && !depth && EnvironmentDepthManager.IsSupported)
            {
                var owner = new GameObject("Live environment depth");
                owner.SetActive(false); owner.transform.SetParent(transform, false);
                depth = owner.AddComponent<EnvironmentDepthManager>();
                depth.CustomTrackingSpace = trackingSpace;
                depth.OcclusionShadersMode = OcclusionShadersMode.HardOcclusion;
                // Keep physical hands in depth until a validated hand-mesh mask replaces them.
                depth.RemoveHands = false;
                owner.SetActive(true);
            }
            if (depth && depth.enabled != wanted) depth.enabled = wanted;
#endif
        }
        void OnApplicationFocus(bool value) { focused = value; if (!value) StopDepth(); }
        void OnApplicationPause(bool value) { paused = value; if (value) StopDepth(); }
        void OnDisable() => StopDepth();
        void StopDepth() { if (depth) depth.enabled = false; }
        void OnDestroy() { if (depth) Destroy(depth.gameObject); }

        /// <summary>Virtual-only snapshots must not sample the physical room's depth image.</summary>
        internal sealed class VirtualCapture : IDisposable
        {
            static readonly int Bypass = Shader.PropertyToID("_MaestroEnvironmentDepthBypass");
            readonly float previous = Shader.GetGlobalFloat(Bypass);
            bool disposed;
            internal VirtualCapture() { Shader.SetGlobalFloat(Bypass, 1); }
            public void Dispose() { if (disposed) return; disposed = true; Shader.SetGlobalFloat(Bypass, previous); }
        }
    }
}
