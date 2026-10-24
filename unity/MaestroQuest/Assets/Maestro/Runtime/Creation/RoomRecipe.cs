// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class RecipePart
    {
        public string id, parent, shape = "box";
        public Vector2[] profile = Array.Empty<Vector2>();
        public Vector3[] path = Array.Empty<Vector3>();
        public int segments;
        public Vector3 position, size = Vector3.one * .1f;
        public Quaternion rotation = Quaternion.identity;
        public Color color = Color.white;
        public RecipePattern pattern = new();
    }
    [Serializable] public sealed class RecipeKey { public float time; public Quaternion rotation = Quaternion.identity; }
    [Serializable] public sealed class RecipeTrack { public string part; public RecipeKey[] keys; }
    /// <summary>Saved, bounded construction data. No code, expressions, URLs or asset loading.</summary>
    [Serializable] public sealed class RoomRecipe
    {
        public int version = 1;
        public RecipePart[] parts = Array.Empty<RecipePart>();
        public RecipeTrack[] tracks = Array.Empty<RecipeTrack>();
        public bool playing, loop;
        public float duration = 2;
        public RoomRecipe Copy() => JsonUtility.FromJson<RoomRecipe>(JsonUtility.ToJson(this));
        public bool Validate(out string error)
        {
            error = "The construction recipe is invalid or exceeds its limits";
            var validation=new Validation(this);if(!validation.Header)return false;
            foreach(var part in parts)if(!validation.Part(part))return false;
            if(!validation.Tracks())return false;
            error=null;return true;
        }
        // Both synchronous authoring and private native preparation use this
        // ordered validator. Parents must have been accepted before their child.
        internal sealed class Validation
        {
            readonly RoomRecipe source;
            readonly Dictionary<string,float> ids=new();
            internal Validation(RoomRecipe source){this.source=source;}
            internal bool Header=>source.version==1&&source.parts!=null&&source.parts.Length>=1&&source.parts.Length<=32&&source.tracks!=null&&source.tracks.Length<=17&&float.IsFinite(source.duration)&&source.duration>=.1f&&source.duration<=30;
            internal bool Part(RecipePart p)
            {
                if (p == null || !ValidId(p.id) || ids.ContainsKey(p.id) || (p.shape != "box" && p.shape != "sphere" && p.shape != "cylinder" && p.shape != "lathe" && p.shape != "extrude" && p.shape != "sweep") || !RecipeGeometry.Valid(p) ||
                    !MotionFrame.ValidRotation(p.rotation) || !Finite(p.position) || p.position.magnitude > 2 || !Finite(p.size) ||
                    p.size.x < .005f || p.size.y < .005f || p.size.z < .005f || p.size.x > 2 || p.size.y > 2 || p.size.z > 2 || !ValidColor(p.color) || p.pattern!=null&&!p.pattern.Valid()) return false;
                float distance = 0;
                if (!string.IsNullOrEmpty(p.parent) && !ids.TryGetValue(p.parent,out distance)) return false;
                // Ordered parents prevent cycles. Sum of edge lengths bounds all animated poses.
                distance += p.position.magnitude;
                if (distance + p.size.magnitude * .5f > 3) return false;
                ids.Add(p.id,distance);
                return true;
            }
            internal bool Tracks()
            {
                var animated = new HashSet<string>();
                foreach (var track in source.tracks)
                {
                    if (track == null || track.part == null || !ids.ContainsKey(track.part) || !animated.Add(track.part) || track.keys == null || track.keys.Length < 2 || track.keys.Length > 16) return false;
                    float previous = -1;
                    foreach (var key in track.keys)
                    {
                        if (key == null || !float.IsFinite(key.time) || key.time <= previous || key.time > source.duration || previous < 0 && key.time != 0 || !MotionFrame.ValidRotation(key.rotation)) return false;
                        previous = key.time;
                    }
                    if (Mathf.Abs(previous-source.duration) > .001f) return false;
                }
                if (source.playing && source.tracks.Length == 0) return false;
                return true;
            }
        }
        public static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 32 && id.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_');
        public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        public static bool ValidColor(Color c) => float.IsFinite(c.r) && c.r >= 0 && c.r <= 1 && float.IsFinite(c.g) && c.g >= 0 && c.g <= 1 && float.IsFinite(c.b) && c.b >= 0 && c.b <= 1 && c.a == 1;
        public Quaternion Sample(RecipeTrack track,float time,bool? repeat=null)
        {
            time = (repeat ?? loop) ? Mathf.Repeat(time,duration) : Mathf.Clamp(time,0,duration);
            int high = 1; while (high < track.keys.Length-1 && track.keys[high].time < time) high++;
            var a = track.keys[high-1]; var b = track.keys[high];
            return Quaternion.Slerp(a.rotation,b.rotation,Mathf.InverseLerp(a.time,b.time,time));
        }
    }
}
