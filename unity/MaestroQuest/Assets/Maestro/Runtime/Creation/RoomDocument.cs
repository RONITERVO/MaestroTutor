// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;

namespace Maestro.Quest.Creation
{
    public enum RoomObjectKind { Book, Maestro, Block, Ball, Cylinder, Drawing, ImportedModel, Assembly }

    [Serializable]
    public sealed class RoomObjectData
    {
        public string id;
        public string name;
        public RoomRecipe recipe;
        public RoomObjectKind kind;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public float scale = 1;
        public Color color = Color.white;
        public Vector3[] points;
        public float radius = .003f;
        public JointPose[] joints;
        public RoomMotion motion;
        public string modelHash;
        public ItemPhysics physics;
        public ItemCollider collisionShape;
        public float mass = .5f;
        // Zero preserves the defaults when loading rooms created before movement controls.
        public float followDistance, walkSpeed;
        // Zero means the included gait; positive values are one-based clips of modelHash.
        public int walkClip;
        public string walkMotionId;
        public bool IsBuiltIn => kind == RoomObjectKind.Book || kind == RoomObjectKind.Maestro;
        public RoomObjectData Copy() => new() { id = id, name = name, recipe = recipe?.Copy(), kind = kind, position = position, rotation = rotation, scale = scale, color = color, radius = radius, points = points == null ? null : (Vector3[])points.Clone(), joints = MotionFrame.CopyJoints(joints), motion = motion?.Copy(), modelHash = modelHash, physics = physics, mass = mass, collisionShape = collisionShape, followDistance = followDistance, walkSpeed = walkSpeed, walkClip = walkClip, walkMotionId = walkMotionId };
    }

    [Serializable]
    public sealed class RoomDocument
    {
        public const int MaximumObjects = 64;
        public const int MaximumStrokePoints = 2048;
        public const int MaximumTotalPoints = 32768;
        public int version;
        public RoomObjectData[] objects = Array.Empty<RoomObjectData>();

        public static (float minimum, float maximum) ScaleLimits(RoomObjectKind kind) => kind switch {
            RoomObjectKind.Book => (.65f, 1.8f), RoomObjectKind.Maestro => (.3f, 1.5f), _ => (.1f, 4f)
        };

        public bool Validate(out string error)
        {
            error = null;
            if (version != 1 && version != 2 || objects == null || objects.Length < 2 || objects.Length > MaximumObjects + 2)
                return Fail("This room file has an unsupported version or object count.", out error);
            var ids = new HashSet<string>(); int partCount = 0; int pointCount = 0, builtIns = 0, frameCount = 0, jointCount = 0;
            foreach (var item in objects)
            {
                if (item == null || !Enum.IsDefined(typeof(RoomObjectKind), item.kind) || string.IsNullOrEmpty(item.id) || !ids.Add(item.id))
                    return Fail("This room contains invalid or duplicate objects.", out error);
                if (item.name != null && (item.name.Length > 80 || item.name.Any(char.IsControl))) return Fail("Object names must be at most 80 readable characters.",out error);
                if (item.kind == RoomObjectKind.Assembly ? item.recipe == null || !item.recipe.Validate(out _) : item.recipe != null) return Fail("An object has an invalid construction recipe.",out error);
                partCount += item.recipe?.parts.Length ?? 0;
                bool mayHaveModel = item.kind == RoomObjectKind.ImportedModel || item.kind == RoomObjectKind.Maestro;
                if (!string.IsNullOrEmpty(item.walkMotionId) && (version < 2 || item.kind != RoomObjectKind.Maestro || !Guid.TryParseExact(item.walkMotionId,"N",out _) || item.walkClip != 0))
                    return Fail("The library walking motion reference is invalid.",out error);
                if (item.walkClip < 0 || item.walkClip > 32 || item.walkClip > 0 && (item.kind != RoomObjectKind.Maestro || !ModelLibrary.ValidHash(item.modelHash)))
                    return Fail("The walking clip reference is invalid.",out error);
                if (!float.IsFinite(item.followDistance) || !float.IsFinite(item.walkSpeed) ||
                    item.followDistance != 0 && (item.kind != RoomObjectKind.Maestro || item.followDistance < .8f || item.followDistance > 2.5f) ||
                    item.walkSpeed != 0 && (item.kind != RoomObjectKind.Maestro || item.walkSpeed < .2f || item.walkSpeed > 1.2f))
                    return Fail("Maestro movement settings are invalid.",out error);
                if (item.kind == RoomObjectKind.ImportedModel ? !ModelLibrary.ValidHash(item.modelHash) :
                    !string.IsNullOrEmpty(item.modelHash) && (!mayHaveModel || !ModelLibrary.ValidHash(item.modelHash)))
                    return Fail("This room contains an invalid model reference.", out error);
                if (!Enum.IsDefined(typeof(ItemPhysics),item.physics) || !Enum.IsDefined(typeof(ItemCollider),item.collisionShape) || !float.IsFinite(item.mass) || item.mass < .05f || item.mass > 20 || (item.IsBuiltIn && (item.physics != ItemPhysics.Fixed || item.collisionShape != ItemCollider.Automatic)))
                    return Fail("An object has invalid physics settings.",out error);
                if (item.IsBuiltIn)
                {
                    if (item.id != (item.kind == RoomObjectKind.Book ? "book" : "maestro")) return Fail("The included book and Maestro identities are invalid.", out error);
                    builtIns++;
                }
                else if (!Guid.TryParseExact(item.id, "N", out _)) return Fail("This room contains an invalid object identity.", out error);
                var limits = ScaleLimits(item.kind);
                float norm = item.rotation.x * item.rotation.x + item.rotation.y * item.rotation.y + item.rotation.z * item.rotation.z + item.rotation.w * item.rotation.w;
                if (!Finite(item.position) || item.position.sqrMagnitude > 625 || !float.IsFinite(norm) || Mathf.Abs(norm - 1) > .01f ||
                    !float.IsFinite(item.scale) || item.scale < limits.minimum - .0001f || item.scale > limits.maximum + .0001f)
                    return Fail("An object has an invalid position, rotation or size.", out error);
                var c = item.color;
                if (!Unit(c.r) || !Unit(c.g) || !Unit(c.b) || !float.IsFinite(c.a) || Mathf.Abs(c.a - 1) > .001f)
                    return Fail("An object has an invalid paint color.", out error);
                if (item.kind == RoomObjectKind.Drawing)
                {
                    if (item.points == null || item.points.Length < 2 || item.points.Length > MaximumStrokePoints || !float.IsFinite(item.radius) || item.radius < .001f || item.radius > .02f)
                        return Fail("A drawing exceeds the supported size.", out error);
                    pointCount += item.points.Length;
                    bool hasLength = false;
                    foreach (var point in item.points)
                    {
                        if (!Finite(point) || point.sqrMagnitude > 100) return Fail("A drawing contains invalid coordinates.", out error);
                        hasLength |= (point - item.points[0]).sqrMagnitude > .000001f;
                    }
                    if (!hasLength) return Fail("A drawing must have a visible stroke.", out error);
                }
                else if (item.points != null && item.points.Length != 0) return Fail("Only drawings can contain stroke points.", out error);
                if (!MotionFrame.ValidJoints(item.joints) || (item.kind != RoomObjectKind.Maestro && item.joints?.Length > 0) || (item.motion != null && !item.motion.Validate(item.kind)))
                    return Fail("An object has an invalid pose or animation.",out error);
                frameCount += item.motion?.frames.Length ?? 0;
                jointCount += item.motion?.frames.Sum(frame => frame.joints?.Length ?? 0) ?? 0;
            }
            if (builtIns != 2 || !ids.Contains("book") || !ids.Contains("maestro")) return Fail("The included book and Maestro must remain in the room.", out error);
            if (partCount > 256) return Fail("Keep at most 256 recipe parts in this room.",out error);
            if (pointCount > MaximumTotalPoints) return Fail("This room has reached its drawing limit.", out error);
            if (frameCount > 1200 || jointCount > 6000) return Fail("This room has reached its animation limit.",out error);
            if (objects.Count(item => item.kind == RoomObjectKind.ImportedModel) > 4) return Fail("Keep at most four imported models in this room.", out error);
            return true;
        }

        static bool Unit(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static bool Fail(string message, out string error) { error = message; return false; }
        public RoomDocument Copy() => new() { version = version, objects = objects.Select(item => item.Copy()).ToArray() };
    }

    /// <summary>Bounded object deltas preserve drawings without retaining whole scene copies.</summary>
    public sealed class RoomJournal
    {
        sealed class Change { public RoomObjectData[] Before, After; }
        readonly List<Change> undo = new(), redo = new();
        readonly Dictionary<string, RoomObjectData> items = new();
        readonly Dictionary<string,int> revisions = new();
        // Forks share one monotonic clock: an Undo, discard or recreated object
        // must never make a stale observation current again.
        sealed class RevisionClock { public int Next = 1; }
        readonly RevisionClock clock = new();
        public int ObjectRevision(string id) => id != null && revisions.TryGetValue(id,out var value) ? value : 0;
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public bool UsesMotion(string id) => items.Values.Any(x => x.walkMotionId == id);
        public string[] HistoricalMotionIds => undo.Concat(redo).SelectMany(x => x.Before.Concat(x.After)).Select(x => x.walkMotionId).Where(x => x != null).Distinct().ToArray();
        public RoomJournal(RoomDocument document)
        {
            if (!document.Validate(out var error)) throw new ArgumentException(error, nameof(document));
            foreach (var item in document.objects) { items.Add(item.id, item.Copy()); revisions[item.id]=clock.Next++; }
        }
        RoomJournal(RoomJournal source)
        {
            clock=source.clock;
            foreach(var pair in source.items) items.Add(pair.Key,pair.Value.Copy());
            foreach(var pair in source.revisions) revisions.Add(pair.Key,pair.Value);
        }
        // A temporary room has its own local Undo history; the saved history
        // remains untouched until a successfully written snapshot is accepted.
        public RoomJournal Fork() => new(this);
        public bool ApplySnapshot(RoomDocument document,out string error)
        {
            if(document==null) {error="Room snapshot is missing";return false;}
            if(!document.Validate(out error))return false;
            var ids=document.objects.Select(x=>x.id).ToHashSet();
            var replacements=document.objects.Where(x=>!items.TryGetValue(x.id,out var before)||!Equivalent(new[]{before},new[]{x})).ToArray();
            return Apply(replacements,items.Keys.Where(id=>!ids.Contains(id)).ToArray(),out error);
        }
        public void InvalidateChangedObservations(RoomJournal other)
        {
            foreach(var id in items.Keys)
                if(ObjectRevision(id)!=other.ObjectRevision(id))revisions[id]=clock.Next++;
        }
        public RoomObjectData Read(string id) => id != null && items.TryGetValue(id, out var value) ? value.Copy() : null;
        // Physics updates persisted placement without filling Undo with every simulation step.
        public bool UpdatePlacement(string id, Vector3 position, Quaternion rotation)
        {
            if (!items.TryGetValue(id,out var data) || !float.IsFinite(position.sqrMagnitude) || position.sqrMagnitude > 625 || !MotionFrame.ValidRotation(rotation)) return false;
            if ((data.position-position).sqrMagnitude < .000001f && Quaternion.Angle(data.rotation,rotation) < .1f) return false;
            data.position = position; data.rotation = rotation; revisions[id]=clock.Next++; return true;
        }
        public RoomDocument Snapshot() => new() { version = 2, objects = items.Values.Select(item => item.Copy()).OrderBy(item => item.id, StringComparer.Ordinal).ToArray() };

        public bool Apply(RoomObjectData[] replacements, string[] removals, out string error)
        {
            var changedIds = replacements.Select(item => item.id).Concat(removals).ToHashSet();
            var candidate = new Dictionary<string, RoomObjectData>(items);
            foreach (var id in removals) candidate.Remove(id);
            foreach (var item in replacements) candidate[item.id] = item.Copy();
            if (!(new RoomDocument { version = 2, objects = candidate.Values.ToArray() }).Validate(out error)) return false;
            var change = new Change {
                Before = changedIds.Where(items.ContainsKey).Select(id => items[id].Copy()).ToArray(),
                After = changedIds.Where(candidate.ContainsKey).Select(id => candidate[id].Copy()).ToArray()
            };
            if (Equivalent(change.Before, change.After)) return true;
            Set(change.Before, change.After);
            undo.Add(change); if (undo.Count > 32) undo.RemoveAt(0); redo.Clear(); return true;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            var change = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            Set(change.After, change.Before); redo.Add(change); return true;
        }
        public bool Redo()
        {
            if (!CanRedo) return false;
            var change = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            Set(change.Before, change.After); undo.Add(change); return true;
        }
        void Set(RoomObjectData[] before, RoomObjectData[] after)
        {
            foreach (var item in before) { items.Remove(item.id); revisions.Remove(item.id); }
            foreach (var item in after) { items[item.id] = item.Copy(); revisions[item.id]=clock.Next++; }
        }
        static bool Equivalent(RoomObjectData[] a, RoomObjectData[] b) => JsonUtility.ToJson(new RoomDocument { objects = a.OrderBy(x => x.id).ToArray() }) == JsonUtility.ToJson(new RoomDocument { objects = b.OrderBy(x => x.id).ToArray() });
    }
}
