// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

namespace Maestro.Quest.Creation
{
    // Semantic joint names let compatible imported humanoids use the same pose format.
    public enum PoseJoint { Hips, Spine, Chest, Neck, Head, LeftUpperArm, LeftLowerArm, LeftHand, RightUpperArm, RightLowerArm, RightHand, LeftUpperLeg, LeftLowerLeg, LeftFoot, RightUpperLeg, RightLowerLeg, RightFoot }

    [Serializable] public sealed class JointPose
    {
        public PoseJoint joint;
        public Quaternion rotation = Quaternion.identity;
        public JointPose Copy() => new() { joint = joint, rotation = rotation };
    }

    [Serializable] public sealed class MotionFrame
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public float scale = 1;
        public JointPose[] joints;
        public MotionFrame Copy() => new() { time = time, position = position, rotation = rotation, scale = scale, joints = CopyJoints(joints) };
        public static JointPose[] CopyJoints(JointPose[] value) => value?.Select(x => x.Copy()).ToArray();
        public static bool ValidRotation(Quaternion value)
        {
            float norm = Quaternion.Dot(value,value);
            return float.IsFinite(norm) && Mathf.Abs(norm - 1) < .01f;
        }
        public static bool ValidJoints(JointPose[] value)
        {
            if (value == null) return true;
            return value.Length <= 17 && value.All(x => x != null && Enum.IsDefined(typeof(PoseJoint),x.joint) && ValidRotation(x.rotation)) && value.Select(x => x.joint).Distinct().Count() == value.Length;
        }
    }

    [Serializable] public sealed class RoomMotion
    {
        public const int MaximumFrames = 301;
        public const float MaximumSeconds = 30;
        public bool loop;
        public MotionFrame[] frames = Array.Empty<MotionFrame>();
        public float Duration => frames.Length == 0 ? 0 : frames[^1].time;
        public RoomMotion Copy() => new() { loop = loop, frames = frames.Select(x => x.Copy()).ToArray() };
        public bool Validate(RoomObjectKind kind)
        {
            if (frames == null || frames.Length == 0 || frames.Length > MaximumFrames) return false;
            float previous = -1; var limits = RoomDocument.ScaleLimits(kind);
            PoseJoint[] skeleton = frames[0]?.joints?.Select(x => x == null ? (PoseJoint)(-1) : x.joint).ToArray();
            foreach (var frame in frames)
            {
                if (frame == null || !float.IsFinite(frame.time) || frame.time <= previous || frame.time > MaximumSeconds ||
                    (previous < 0 && frame.time != 0) || !float.IsFinite(frame.position.sqrMagnitude) || frame.position.sqrMagnitude > 625 ||
                    !MotionFrame.ValidRotation(frame.rotation) || !float.IsFinite(frame.scale) || frame.scale < limits.minimum - .0001f || frame.scale > limits.maximum + .0001f ||
                    !MotionFrame.ValidJoints(frame.joints) || (kind != RoomObjectKind.Maestro && frame.joints?.Length > 0)) return false;
                // Every frame has the same ordered channels; interpolation never changes skeletons.
                var channels = frame.joints?.Select(x => x.joint).ToArray();
                if ((skeleton == null) != (channels == null) || (skeleton != null && !skeleton.SequenceEqual(channels))) return false;
                previous = frame.time;
            }
            return true;
        }

        public MotionFrame Sample(float time)
        {
            if (frames.Length == 1 || time <= 0) return frames[0].Copy();
            time = loop && Duration > 0 ? Mathf.Repeat(time,Duration) : Mathf.Clamp(time,0,Duration);
            int high = 1; while (high < frames.Length - 1 && frames[high].time < time) high++;
            var a = frames[high-1]; var b = frames[high]; float t = Mathf.InverseLerp(a.time,b.time,time);
            var result = new MotionFrame { time = time, position = Vector3.Lerp(a.position,b.position,t), rotation = Quaternion.Slerp(a.rotation,b.rotation,t), scale = Mathf.Lerp(a.scale,b.scale,t), joints = MotionFrame.CopyJoints(a.joints) };
            if (result.joints != null) for (int i = 0; i < result.joints.Length; i++) result.joints[i].rotation = Quaternion.Slerp(a.joints[i].rotation,b.joints[i].rotation,t);
            return result;
        }
    }

    /// <summary>Runtime-authored transforms use a ScriptPlayable, avoiding Editor-only curve APIs.</summary>
    public sealed class RoomMotionPlayable : PlayableBehaviour
    {
        public RoomMotion Motion;
        public Action<MotionFrame> Apply;
        public override void PrepareFrame(Playable playable, FrameData info) => Apply?.Invoke(Motion.Sample((float)playable.GetTime()));
    }
}
