// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Rebuilds stable room acoustics after edits or travel. A bounded
    /// listener-point cache avoids a bake for each head movement. Failures back
    /// off; neither an observation nor an agent request performs this work.</summary>
    [DefaultExecutionOrder(210)]
    public sealed class RoomAcousticMapScheduler : MonoBehaviour
    {
        const int MaximumPoints = 4;
        const double StructureSettlingSeconds = 2, ListenerSettlingSeconds = .5;
        const float CoverageRadius = 2.5f, MovementThreshold = .2f;
        readonly List<Vector3> points = new(MaximumPoints);
        RoomAcoustics room;
        Transform listener;
        Func<bool> tracked;
        long revision = -1, requestedRevision;
        double structureReadyAt, listenerReadyAt, retryAt;
        Vector3 sampleAnchor;
        bool haveAnchor, requested;
        int failures;
        internal Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;

        internal void Configure(RoomAcoustics acoustics, Transform viewer, Func<bool> isTracked = null)
        {
            Cancel(); room = acoustics; listener = viewer; tracked = isTracked;
            Reset();
        }
        void Reset()
        {
            points.Clear(); revision = -1; requested = haveAnchor = false;
            failures = 0; retryAt = 0;
        }
        internal void Tick()
        {
            if (!isActiveAndEnabled || !room || !listener || !listener.gameObject.activeInHierarchy) return;
            if (tracked != null && !tracked()) { Cancel(); Reset(); return; }
            double now = Clock();
            if (!double.IsFinite(now)) return;
            if (revision != room.StructureRevision)
            {
                revision = room.StructureRevision; points.Clear();
                structureReadyAt = now + StructureSettlingSeconds;
                failures = 0; retryAt = 0;
            }
            if (requested && !room.MapComputing)
            {
                requested = false;
                if (requestedRevision == revision)
                {
                    if (room.MapReady) { failures = 0; retryAt = 0; }
                    else { failures = Math.Min(failures + 1, 4); retryAt = now + Math.Min(120, Math.Pow(4, failures)); }
                }
            }
            var position = listener.position;
            if (!float.IsFinite(position.sqrMagnitude) || Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.y), Mathf.Abs(position.z)) > 10000) return;
            if (!haveAnchor || (position - sampleAnchor).sqrMagnitude > MovementThreshold * MovementThreshold)
            {
                sampleAnchor = position; haveAnchor = true; listenerReadyAt = now + ListenerSettlingSeconds;
            }
            bool covered = false;
            foreach (var point in points) if ((point - position).sqrMagnitude <= CoverageRadius * CoverageRadius) { covered = true; break; }
            if (room.MapReady && covered || !room.Ready || room.BoundaryCount == 0 || room.OmittedBoundaries != 0
                || now < structureReadyAt || now < listenerReadyAt || now < retryAt) return;
            var candidate = new List<Vector3>(points);
            if (!covered)
            {
                if (candidate.Count == MaximumPoints) candidate.RemoveAt(0);
                candidate.Add(position);
            }
            if (room.RequestMap(candidate.ToArray()))
            {
                points.Clear(); points.AddRange(candidate);
                requested = true; requestedRevision = room.StructureRevision;
            }
            else retryAt = now + StructureSettlingSeconds;
        }
        void Cancel() { if (requested && room) room.CancelMapRequest(); requested = false; }
        void LateUpdate() => Tick();
        void OnDisable() { Cancel(); Reset(); }
        void OnApplicationPause(bool value) { if (value) { Cancel(); Reset(); } }
        void OnApplicationFocus(bool value) { if (!value) { Cancel(); Reset(); } }
    }
}
