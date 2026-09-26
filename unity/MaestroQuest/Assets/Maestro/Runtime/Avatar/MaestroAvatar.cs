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
    [DefaultExecutionOrder(110)]
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
        int importedClip = -1, walkClip = -1;
        float importedTime, importedSpeed = 1;
        bool importedLoop;
        MotionLibrary.Lease libraryMotion;
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
        public bool IsImportedClipPlaying => importedClip >= 0 || libraryMotion != null;
        public int WalkClip => walkClip;
        public string WalkClipName => custom && walkClip >= 0 && walkClip < custom.ClipCount ? custom.ClipName(walkClip) : "Included walk";
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
            StopImportedClip();
            if (PoseRig) PoseRig.SetDisplayRig(null);
            if (custom) { custom.gameObject.SetActive(false); Destroy(custom.gameObject); custom = null; }
            if (included) foreach (var renderer in included.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            ModelHash = "";
        }

        void Update()
        {
            if (editing || savedPose != null || IsImportedClipPlaying) return;
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
            StopImportedClip();
            editing = value;
            if (!PoseRig) return;
            PoseRig.SetManual(value || savedPose != null);
            if (!value) { PoseRig.SetPosing(false); PoseRig.Apply(savedPose); activity = null; spatialWalking = false; animator.speed = 1; }
        }
        public void SpatialWalk(float metresPerSecond)
        {
            bool walking = metresPerSecond > .025f && !ReducedMotion;
            float rate = walking ? Mathf.Clamp(metresPerSecond/(.65f*transform.lossyScale.y),.25f,2) : 1;
            if (walking && custom && walkClip >= 0 && walkClip < custom.ClipCount && custom.ClipDuration(walkClip) >= .1f)
            {
                if (!spatialWalking || activity != "spatial" || importedClip != walkClip) PlayImportedClip(walkClip,true);
                importedSpeed = rate; spatialWalking = true; activity = "spatial"; return;
            }
            StopImportedClip();
            PoseRig.SetManual(false);
            if (walking != spatialWalking || activity != "spatial") animator.CrossFadeInFixedTime(walking ? "Walk" : "Idle",.15f);
            spatialWalking = walking; activity = "spatial";
            animator.speed = ReducedMotion ? 0 : rate;
        }
        public void SetWalkClip(int index) { walkClip = index; }
        public bool PlayImportedClip(int index, bool loop)
        {
            if (ModelBusy || !custom || index < 0 || index >= custom.ClipCount || custom.ClipDuration(index) <= 0) return false;
            StopImportedClip(); custom.Stop();
            importedClip = index; importedLoop = loop; importedTime = 0; importedSpeed = 1;
            PoseRig.SetManual(true); activity = "imported";
            custom.SampleClip(index,0,loop); PoseRig.CaptureImportedPose(); return true;
        }
        // Ownership of the lease transfers only on success; Stop always releases it.
        public bool PlayLibraryMotion(MotionLibrary.Lease motion,bool loop)
        {
            if (ModelBusy || !custom || motion == null || !motion.Clip || motion.RigHash != custom.MotionRigHash) return false;
            StopImportedClip(); custom.Stop(); libraryMotion = motion; importedLoop = loop; importedTime = 0; importedSpeed = 1;
            PoseRig.SetManual(true); activity = "imported";
            custom.SampleMotion(motion,0,loop); PoseRig.CaptureImportedPose(); return true;
        }
        float ImportedDuration => libraryMotion != null ? (libraryMotion.Clip ? libraryMotion.Clip.length : 0) : custom.ClipDuration(importedClip);
        public void StopImportedClip()
        {
            if (!IsImportedClipPlaying) return;
            libraryMotion?.Dispose(); libraryMotion = null;
            importedClip = -1; if (custom) custom.Stop();
            if (PoseRig) PoseRig.SetManual(editing || savedPose != null);
        }
        void LateUpdate()
        {
            if (!IsImportedClipPlaying || !custom) return;
            importedTime += Mathf.Min(Time.deltaTime,.05f)*importedSpeed;
            if (ImportedDuration <= 0 || !importedLoop && importedTime >= ImportedDuration) { StopImportedClip(); return; }
            if (libraryMotion != null ? custom.SampleMotion(libraryMotion,importedTime,importedLoop) : custom.SampleClip(importedClip,importedTime,importedLoop)) PoseRig.CaptureImportedPose();
        }
        public void Gesture(string name)
        {
            if (!animator || !animator.runtimeAnimatorController || (name != "Greeting" && name != "Pointing" && name != "Listening" && name != "Speaking" && name != "Idle" && name != "Walk")) return;
            StopImportedClip();
            PoseRig.SetManual(false); animator.Play(name,0,0); animator.Update(0);
            // A gesture can be sampled into a pose while authoring; live tutor activity
            // resumes when authoring ends and no saved static pose is active.
        }
        void OnApplicationPause(bool value) { if (value) StopImportedClip(); }
        void OnApplicationFocus(bool value) { if (!value) StopImportedClip(); }
        void OnDisable() => StopImportedClip();
        void OnDestroy() { StopImportedClip(); disposed = true; modelGeneration++; }
    }
}
