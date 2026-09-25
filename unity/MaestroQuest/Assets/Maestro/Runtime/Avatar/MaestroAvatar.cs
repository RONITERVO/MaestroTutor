// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using System;
using System.Threading.Tasks;
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
        bool spatialWalking;
        GameObject included;
        ImportedModel custom;
        string requestedModel = "";
        int modelGeneration;
        bool disposed;
        public AvatarPoseRig PoseRig { get; private set; }
        public string ModelHash { get; private set; } = "";
        public string ModelStatus { get; private set; } = "Included Maestro";
        public bool ModelBusy { get; private set; }
        public Task<bool> ModelLoad { get; private set; } = Task.FromResult(true);
        public ImportedModel CustomModel => custom;
        public event Action ModelChanged;

        void Awake()
        {
            var prefab = Resources.Load<GameObject>("Avatars/DefaultMaestro");
            if (!prefab) { Debug.LogError("The included Maestro model is missing."); return; }
            var model = Instantiate(prefab, transform);
            included = model;
            model.name = "Maestro drawing";
            model.AddComponent<PencilModelStyle>().Apply();
            if (!model.TryGetComponent(out animator)) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Avatars/MaestroAnimations");
            animator.applyRootMotion = false;
            // The included skeleton continues driving the selected humanoid even
            // while its own meshes are hidden.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PoseRig = gameObject.AddComponent<AvatarPoseRig>(); PoseRig.Initialize(animator);
            if (savedPose != null) { PoseRig.SetManual(true); PoseRig.Apply(savedPose); }
            greetingUntil = Time.unscaledTime + (ReducedMotion ? 0 : 2.5f);
            if (savedPose == null && !ReducedMotion && animator.runtimeAnimatorController) animator.Play("Greeting");
        }

        public Task<bool> SetModel(string hash, ModelLibrary library, bool retry = false)
        {
            hash ??= "";
            if (requestedModel == hash && (!retry || ModelBusy)) return ModelLoad;
            requestedModel = hash; int generation = ++modelGeneration;
            if (hash.Length == 0)
            {
                UseIncluded(); ModelBusy = false; ModelStatus = "Included Maestro"; ModelChanged?.Invoke();
                return ModelLoad = Task.FromResult(true);
            }
            ModelBusy = true; ModelStatus = "Loading custom Maestro…"; ModelChanged?.Invoke();
            return ModelLoad = LoadModel(hash,library,generation);
        }
        async Task<bool> LoadModel(string hash, ModelLibrary library, int generation)
        {
            GameObject candidateRoot = null;
            try
            {
                var asset = await library.ReadAsync(hash);
                if (!this || disposed || generation != modelGeneration) return false;
                candidateRoot = new GameObject("Custom Maestro"); candidateRoot.SetActive(false); candidateRoot.transform.SetParent(transform,false);
                var candidate = candidateRoot.AddComponent<ImportedModel>(); await candidate.LoadAsync(asset);
                if (!this || disposed || generation != modelGeneration) return false;
                candidate.FitAsMaestro();
                var retargeter = candidateRoot.AddComponent<HumanoidRetargeter>(); retargeter.Initialize(PoseRig,candidate.Humanoid);
                UseIncluded(); custom = candidate; candidateRoot = null;
                PoseRig.SetDisplayRig(retargeter);
                foreach (var renderer in included.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                custom.gameObject.SetActive(true); retargeter.ApplyPose();
                ModelHash = hash; ModelStatus = "Custom Maestro ready — gestures, poses and recordings retained";
                return true;
            }
            catch (Exception error)
            {
                if (this && !disposed && generation == modelGeneration)
                {
                    UseIncluded();
                    ModelStatus = (error is ModelImportException ? error.Message : "The custom avatar could not load.") + " Using included Maestro.";
                }
                return false;
            }
            finally
            {
                if (candidateRoot) Destroy(candidateRoot);
                if (this && !disposed && generation == modelGeneration) { ModelBusy = false; ModelChanged?.Invoke(); }
            }
        }
        void UseIncluded()
        {
            if (PoseRig) PoseRig.SetDisplayRig(null);
            if (custom) { custom.gameObject.SetActive(false); Destroy(custom.gameObject); custom = null; }
            if (included) foreach (var renderer in included.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            ModelHash = "";
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
            if (!value) { PoseRig.SetPosing(false); PoseRig.Apply(savedPose); activity = null; spatialWalking = false; animator.speed = 1; }
        }
        public void SpatialWalk(float metresPerSecond)
        {
            bool walking = metresPerSecond > .025f && !ReducedMotion;
            PoseRig.SetManual(false);
            if (walking != spatialWalking || activity != "spatial") animator.CrossFadeInFixedTime(walking ? "Walk" : "Idle",.15f);
            spatialWalking = walking; activity = "spatial";
            animator.speed = ReducedMotion ? 0 : walking ? Mathf.Clamp(metresPerSecond/(.65f*transform.lossyScale.y),.25f,2) : 1;
        }
        public void Gesture(string name)
        {
            if (!animator || !animator.runtimeAnimatorController || (name != "Greeting" && name != "Pointing" && name != "Listening" && name != "Speaking" && name != "Idle")) return;
            PoseRig.SetManual(false); animator.Play(name,0,0); animator.Update(0);
            // A gesture can be sampled into a pose while authoring; live tutor activity
            // resumes when authoring ends and no saved static pose is active.
        }
        void OnDestroy() { disposed = true; modelGeneration++; }
    }
}
