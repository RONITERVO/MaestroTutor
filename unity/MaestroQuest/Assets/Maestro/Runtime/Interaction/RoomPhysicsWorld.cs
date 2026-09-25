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
        public void SetSurfaces(bool ready, string message)
        {
            SurfacesReady = ready;
            if (!ready) Running = false;
            Status = message; Changed?.Invoke();
        }
        public void StartPhysics()
        {
            if (!SurfacesReady) { Status = "Load the room scan and check its alignment first"; Changed?.Invoke(); return; }
            Running = true; Status = "Physics on — grip to pick up, release to throw"; Changed?.Invoke();
        }
        public void PausePhysics()
        {
            Running = false; Status = SurfacesReady ? "Physics paused — Start resumes without old throw speeds" : "Load or scan your room to use gravity"; Changed?.Invoke();
        }
        public bool CanSimulate(Vector3 position) => Running && SurfacesReady && (Contains == null || Contains(position));
        void OnApplicationPause(bool paused) { if (paused) PausePhysics(); }
        void OnApplicationFocus(bool focused) { if (!focused) PausePhysics(); }
        void OnDisable() => PausePhysics();
    }
}
