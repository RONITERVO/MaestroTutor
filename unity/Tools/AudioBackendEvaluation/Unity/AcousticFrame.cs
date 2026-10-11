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
        internal const int MaximumSources = 32;
        internal readonly CoordinateSpace3 Listener;
        readonly CoordinateSpace3[] sources;
        internal readonly bool Room;
        internal int SourceCount => sources.Length;
        internal CoordinateSpace3 Source(int slot) => sources[slot];
        internal AcousticFrame(bool room, CoordinateSpace3 listener, params CoordinateSpace3[] sources)
        {
            Validate(listener);
            if (sources == null || sources.Length < 1 || sources.Length > MaximumSources)
                throw new ArgumentException("Invalid acoustic voice capacity");
            foreach (var source in sources) Validate(source);
            this.sources = (CoordinateSpace3[])sources.Clone();
            Room = room;
            Listener = listener;
        }
        internal AcousticFrame WithListener(CoordinateSpace3 listener) => new AcousticFrame(Room, listener, sources);
        internal static AcousticFrame CreateDefault(int capacity)
        {
            if (capacity < 1 || capacity > MaximumSources) throw new ArgumentOutOfRangeException(nameof(capacity));
            var poses = new CoordinateSpace3[capacity];
            for (int i = 0; i < capacity; i++) poses[i] = Pose(1, 0, .4f * i);
            return new AcousticFrame(true, Pose(-1, 0, 0), poses);
        }
        internal static readonly AcousticFrame Default = CreateDefault(2);
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
