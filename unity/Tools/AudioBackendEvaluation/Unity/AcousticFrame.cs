// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using SteamAudio;

namespace Maestro.AudioEvaluation
{
    // Immutable snapshot in the SDK's right-handed metre coordinates. This probe's
    // fixed topology is deliberately separate from any saved Maestro room schema.
    internal sealed class AcousticFrame
    {
        internal readonly CoordinateSpace3 Listener, First, Second;
        internal readonly bool Room;
        internal AcousticFrame(bool room, CoordinateSpace3 listener, CoordinateSpace3 first, CoordinateSpace3 second)
        {
            Validate(listener);
            Validate(first);
            Validate(second);
            Room = room;
            Listener = listener;
            First = first;
            Second = second;
        }
        internal static readonly AcousticFrame Default =
            new AcousticFrame(true, Pose(-1, 0, 0), Pose(1, 0, 0), Pose(1, 0, .4f));
        internal static CoordinateSpace3 Pose(float x, float y, float z, float yaw = 0)
        {
            float c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw);
            return new CoordinateSpace3 { origin = V(x, y, z), right = V(c, 0, -s), up = V(0, 1, 0),
                                          ahead = V(-s, 0, -c) };
        }
        static Vector3 V(float x, float y, float z) => new Vector3 { x = x, y = y, z = z };
        static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        static void Validate(CoordinateSpace3 pose)
        {
            foreach (var v in new[] { pose.origin, pose.right, pose.up, pose.ahead })
                if (!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))
                    throw new ArgumentException("Non-finite acoustic pose");
            if (Math.Abs(Dot(pose.right, pose.right) - 1) > .001 || Math.Abs(Dot(pose.up, pose.up) - 1) > .001 ||
                Math.Abs(Dot(pose.ahead, pose.ahead) - 1) > .001 || Math.Abs(Dot(pose.right, pose.up)) > .001 ||
                Math.Abs(Dot(pose.right, pose.ahead)) > .001 || Math.Abs(Dot(pose.up, pose.ahead)) > .001)
                throw new ArgumentException("Acoustic axes must be orthonormal");
        }
    }
}
