// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>A trigger/pinch draws one real 3D stroke at the pointer tip.</summary>
    public sealed class SpatialDrawing : MonoBehaviour
    {
        public RoomEditor Editor;
        readonly List<Vector3> points = new();
        int owner = -1;
        System.IDisposable write;
        PencilMarks preview;
        Color color;
        float nextPreview;
        public bool IsDrawing => owner != -1;

        public void Begin(int id, Ray ray)
        {
            if (!Editor || !Editor.DrawingMode || owner != -1) return;
            write=Editor.WriteGate.TryWrite(out var blocked);if(write==null){Editor.ReportStatus(blocked);return;}
            owner = id; color = Editor.Paint; points.Clear(); nextPreview = 0;
            var root = new GameObject("Pencil stroke in progress"); root.transform.SetParent(transform, false);
            preview = root.AddComponent<PencilMarks>();
            Move(id,ray);
        }
        public void Move(int id, Ray ray)
        {
            if (owner != id) return;
            var point = ray.GetPoint(.12f);
            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1],point) < .005f) return;
            // A tracking jump ends the stroke instead of drawing a long line across the room.
            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1],point) > .35f) { End(id); return; }
            points.Add(point);
            if (points.Count >= RoomDocument.MaximumStrokePoints) { End(id); return; }
            if (points.Count < 2 || Time.unscaledTime < nextPreview) return;
            nextPreview = Time.unscaledTime + 1f / 30;
            var local = new Vector3[points.Count];
            for (int i = 0; i < local.Length; i++) local[i] = preview.transform.InverseTransformPoint(points[i]);
            preview.SetPaths(new[] { local },.003f); preview.SetColor(color);
        }
        public void End(int id)
        {
            if (owner != id) return;
            owner = -1;
            try {if (Editor && points.Count >= 2) Editor.AddDrawing(points,color);}
            finally {points.Clear(); if (preview) Destroy(preview.gameObject); preview = null;write?.Dispose();write=null;}
        }
        public void Cancel(int id) => End(id);
        void OnDisable() { if (owner != -1) End(owner); }
    }
}
