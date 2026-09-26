// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    /// <summary>Gravity is available only while the room is aligned and the user has started physics.</summary>
    public sealed class RoomPhysicsWorld : MonoBehaviour
    {
        public bool SurfacesReady { get; private set; }
        public bool Running { get; private set; }
        public string Status { get; private set; } = "Load or scan your room to use gravity";
        public event Action Changed;
        public Func<Vector3, bool> Contains;
        bool paused,focused=true;
        public void SetSurfaces(bool ready, string message)
        {
            SurfacesReady = ready;
            if (!ready) Running = false;
            Status = message; Changed?.Invoke();
        }
        public void StartPhysics() => SetRunning(true,out _);
        public void PausePhysics() => SetRunning(false,out _);
        public bool SetRunning(bool running,out string status)
        {
            if (running && (paused || !focused || !isActiveAndEnabled)) {Status="Return to the active room before starting physics";status=Status;Changed?.Invoke();return false;}
            if (running && !SurfacesReady) { Status="Load the room scan and check its alignment first";status=Status;Changed?.Invoke();return false; }
            Running=running;
            Status=running ? "Physics on — grip to pick up, release to throw" : SurfacesReady ? "Physics paused — Start resumes without old throw speeds" : "Load or scan your room to use gravity";
            status=Status;Changed?.Invoke();return true;
        }
        public bool CanSimulate(Vector3 position) => Running && SurfacesReady && (Contains == null || Contains(position));
        void OnApplicationPause(bool value) { paused=value;if (paused) PausePhysics(); }
        void OnApplicationFocus(bool value) { focused=value;if (!focused) PausePhysics(); }
        void OnDisable() => PausePhysics();
    }
}
