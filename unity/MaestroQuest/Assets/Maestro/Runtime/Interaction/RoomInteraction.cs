// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    public sealed class RoomInteraction : MonoBehaviour
    {
        public Transform Viewer;
        Maestro.Quest.Persistence.WorkspaceWriteGate writes;
        internal void ConfigureWrites(Maestro.Quest.Persistence.WorkspaceWriteGate gate){writes=gate;}
        readonly List<RoomItem> items = new();
        public event Action Restoring, Restored;

        public void Register(RoomItem item) => items.Add(item);
        public void Unregister(RoomItem item) => items.Remove(item);

        public void RestoreInFrontOfViewer()
        {
            using var write=writes?.TryWrite(out _);if(writes!=null&&write==null)return;
            if (!Viewer) return;
            Restoring?.Invoke();
            var forward = Vector3.ProjectOnPlane(Viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            // Content origin follows the user's current heading, without moving the XR camera origin.
            transform.SetPositionAndRotation(Viewer.position - Vector3.up * 1.55f, Quaternion.LookRotation(forward.normalized, Vector3.up));
            foreach (var item in items) if (item) item.RestoreHome();
            Restored?.Invoke();
        }
    }
}
