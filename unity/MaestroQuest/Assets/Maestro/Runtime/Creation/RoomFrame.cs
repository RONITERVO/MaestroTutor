// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>
    /// One sampled mapping between authored room coordinates and Unity's current
    /// rendering/physics coordinates. It does not move tracking, grant alignment,
    /// or turn a sampled position into a persistent physical anchor.
    /// </summary>
    internal readonly struct RoomFrame
    {
        readonly Transform owner;
        readonly Matrix4x4 toWorld, toRoom;
        readonly Quaternion rotation;
        public bool Valid { get; }
        public float MetresPerUnit { get; }
        static Vector3 Invalid => new(float.NaN, float.NaN, float.NaN);

        internal RoomFrame(Transform room)
        {
            owner = room;
            toWorld = room ? room.localToWorldMatrix : Matrix4x4.zero;
            Valid = Decompose(toWorld, out _, out rotation, out var scale);
            MetresPerUnit = scale;
            toRoom = Valid ? toWorld.inverse : Matrix4x4.zero;
        }

        public Vector3 PointToRoom(Vector3 point) => Valid ? toRoom.MultiplyPoint3x4(point) : Invalid;
        public Vector3 PointToWorld(Vector3 point) => Valid ? toWorld.MultiplyPoint3x4(point) : Invalid;
        public Vector3 VectorToRoom(Vector3 vector) => Valid ? toRoom.MultiplyVector(vector) : Invalid;
        public Vector3 VectorToWorld(Vector3 vector) => Valid ? toWorld.MultiplyVector(vector) : Invalid;
        public Vector3 DirectionToRoom(Vector3 direction) => Valid ? Quaternion.Inverse(rotation) * direction : Invalid;
        public Vector3 DirectionToWorld(Vector3 direction) => Valid ? rotation * direction : Invalid;
        public Quaternion RotationToRoom(Quaternion value) => (Quaternion.Inverse(rotation) * value).normalized;
        public Quaternion RotationToWorld(Quaternion value) => (rotation * value).normalized;

        // Preserve exact direct-child values (and no-op save semantics). A frame
        // retained across a root move must instead use its captured matrices.
        bool Direct(Transform value) => value.parent == owner && owner && toWorld.Equals(owner.localToWorldMatrix);
        public bool Read(Transform value, out Vector3 position, out Quaternion orientation, out float scale)
        {
            position = Invalid; orientation = Quaternion.identity; scale = float.NaN;
            if (!Valid || !value) return false;
            if (Direct(value)) {
                var size = value.localScale;
                if (!UniformPositive(size) || !RoomRecipe.Finite(value.localPosition) || !MotionFrame.ValidRotation(value.localRotation)) return false;
                position = value.localPosition; orientation = value.localRotation.normalized; scale = size.x; return true;
            }
            // A compensating child scale can hide a distorted parent at one
            // pose, yet fail when rotated. Admit only parents that can express
            // every uniform room pose before a saved edit takes ownership.
            var parentInverse = value.parent ? value.parent.worldToLocalMatrix : Matrix4x4.identity;
            if (!Decompose(parentInverse * toWorld, out _, out _, out _)) return false;
            return Decompose(toRoom * value.localToWorldMatrix, out position, out orientation, out scale);
        }
        public ObjectPlacement Placement(string id, Transform value)
        {
            bool valid = Read(value, out var position, out var orientation, out var scale);
            return new ObjectPlacement { target = id, position = valid ? position : Invalid, rotation = orientation, scale = scale };
        }
        public bool Apply(Transform value, Vector3 position, Quaternion orientation, float scale)
        {
            if (!Valid || !value || !RoomRecipe.Finite(position) || !MotionFrame.ValidRotation(orientation) || !float.IsFinite(scale) || scale <= 0) return false;
            var localPosition = position; var localRotation = orientation; float localScale = scale;
            if (!Direct(value)) {
                var parentInverse = value.parent ? value.parent.worldToLocalMatrix : Matrix4x4.identity;
                if (!Decompose(parentInverse * toWorld * Matrix4x4.TRS(position, orientation, Vector3.one * scale), out localPosition, out localRotation, out localScale)) return false;
            }
            value.SetLocalPositionAndRotation(localPosition, localRotation);
            value.localScale = Vector3.one * localScale; return true;
        }
        static bool UniformPositive(Vector3 value) => RoomRecipe.Finite(value) && value.x > .000001f &&
            Mathf.Abs(value.y - value.x) <= value.x * .00001f && Mathf.Abs(value.z - value.x) <= value.x * .00001f;
        static bool Decompose(Matrix4x4 matrix, out Vector3 position, out Quaternion orientation, out float scale)
        {
            position = Invalid; orientation = Quaternion.identity; scale = float.NaN;
            for (int i = 0; i < 16; i++) if (!float.IsFinite(matrix[i])) return false;
            Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
            var sizes = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (!UniformPositive(sizes)) return false;
            x /= sizes.x; y /= sizes.y; z /= sizes.z;
            // No reflected, non-uniform or sheared frame can be represented by a
            // saved room pose. Refuse it instead of silently dropping distortion.
            if (Mathf.Abs(Vector3.Dot(x,y)) > .00001f || Mathf.Abs(Vector3.Dot(x,z)) > .00001f ||
                Mathf.Abs(Vector3.Dot(y,z)) > .00001f || Vector3.Dot(Vector3.Cross(x,y),z) < .99999f) return false;
            position = matrix.GetColumn(3); orientation = Quaternion.LookRotation(z,y).normalized; scale = sizes.x; return true;
        }
    }
}
