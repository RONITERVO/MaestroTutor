// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    /// <summary>Presentation follows the existing tutor; never starts a second audio session.</summary>
    public sealed class MaestroAvatar : MonoBehaviour
    {
        public NativeBookBrowser Browser;
        public bool ReducedMotion;
        Animator animator;
        string activity;
        float greetingUntil;
        JointPose[] savedPose;
        bool editing;
        public AvatarPoseRig PoseRig { get; private set; }

        void Start()
        {
            var prefab = Resources.Load<GameObject>("Avatars/DefaultMaestro");
            if (!prefab) { Debug.LogError("The included Maestro model is missing."); return; }
            var model = Instantiate(prefab, transform);
            model.name = "Maestro drawing";
            model.AddComponent<PencilModelStyle>().Apply();
            if (!model.TryGetComponent(out animator)) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Avatars/MaestroAnimations");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            PoseRig = gameObject.AddComponent<AvatarPoseRig>(); PoseRig.Initialize(animator);
            if (savedPose != null) { PoseRig.SetManual(true); PoseRig.Apply(savedPose); }
            greetingUntil = Time.unscaledTime + (ReducedMotion ? 0 : 2.5f);
            if (savedPose == null && !ReducedMotion && animator.runtimeAnimatorController) animator.Play("Greeting");
        }

        void Update()
        {
            if (editing || savedPose != null) return;
            if (!animator || !animator.runtimeAnimatorController || Time.unscaledTime < greetingUntil) return;
            string state = ReducedMotion ? "Idle" : Browser?.Snapshot?.activity switch
            {
                "speaking" => "Speaking",
                "listening" => "Listening",
                "thinking" => "Listening",
                _ => "Idle"
            };
            if (activity != state) { activity = state; animator.CrossFadeInFixedTime(state, .25f); }
            animator.speed = ReducedMotion ? 0 : 1;
        }

        public void SetSavedPose(JointPose[] pose)
        {
            savedPose = MotionFrame.CopyJoints(pose);
            if (!PoseRig || editing) return;
            PoseRig.SetManual(savedPose != null); PoseRig.Apply(savedPose);
            if (savedPose == null) activity = null;
        }
        public void SetEditing(bool value)
        {
            editing = value;
            if (!PoseRig) return;
            PoseRig.SetManual(value || savedPose != null);
            if (!value) { PoseRig.SetPosing(false); PoseRig.Apply(savedPose); activity = null; }
        }
        public void Gesture(string name)
        {
            if (!animator || !animator.runtimeAnimatorController || (name != "Greeting" && name != "Pointing" && name != "Listening" && name != "Speaking" && name != "Idle")) return;
            PoseRig.SetManual(false); animator.Play(name,0,0); animator.Update(0);
            // A gesture can be sampled into a pose while authoring; live tutor activity
            // resumes when authoring ends and no saved static pose is active.
        }

    }
}
