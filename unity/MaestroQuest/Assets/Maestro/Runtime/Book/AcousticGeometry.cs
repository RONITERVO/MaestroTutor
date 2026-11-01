// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Book
{
    // Native inputs may belong to an active view or to a bounded retained cache.
    // Neither the SDK nor diagnostics need a renderer/physics object to own them.
    internal interface IAcousticGeometry
    {
        bool Active { get; }
        bool HasGeometry { get; }
        bool Scanned { get; }
        bool ReflectionBoundary { get; }
        int Revision { get; }
        Matrix4x4 Pose { get; }
        string Issue { get; set; }
        bool Read(out float[] vertices, out int[] indices, out string issue);
    }
}
