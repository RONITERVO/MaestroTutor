// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using UniGLTF;
using UniVRM10;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    /// <summary>One owned imported instance. Playback never starts from a file or saved room.</summary>
    public sealed class ImportedModel : MonoBehaviour
    {
        static readonly SemaphoreSlim loadQueue = new(1, 1);
        static int liveVertices, livePixels, liveModels, liveMorphVertices;
        ModelInspection reservation;
        RuntimeGltfInstance instance;
        UnityEngine.Avatar generatedAvatar;
        Animation animationPlayer;
        Bounds rawBounds;
        bool destroyed;
        readonly Dictionary<SkinnedMeshRenderer, float[]> initialWeights = new();
        public bool Ready => instance;
        public int ClipCount => instance ? instance.AnimationClips.Count : 0;
        public bool IsPlaying => animationPlayer && animationPlayer.isPlaying;
        public Bounds LocalBounds { get; private set; }
        public RuntimeGltfInstance Instance => instance;
        public Animator Humanoid => instance ? instance.GetComponent<Animator>() : null;
        public bool IsHumanoid => Humanoid && Humanoid.avatar && Humanoid.avatar.isHuman && Humanoid.avatar.isValid;
        public string HumanoidIssue { get; private set; }
        public string ClipName(int index) => index >= 0 && index < ClipCount ? ModelLibrary.SafeName(instance.AnimationClips[index].name) : "No embedded clips";

        public async Task LoadAsync(ModelAsset asset, IAwaitCaller awaitCaller = null)
        {
            if (reservation != null) throw new InvalidOperationException("Model already loaded");
            await loadQueue.WaitAsync();
            RuntimeGltfInstance loaded = null;
            try
            {
                if (!this || destroyed) return;
                var info = asset.Inspection;
                if (liveModels >= 6 || liveVertices + info.Vertices > 500000 || livePixels + info.TexturePixels > 64 * 1024 * 1024 || liveMorphVertices + info.MorphVertices > 8000000)
                    throw new ModelImportException("This room has reached its model memory budget. Erase an imported object before adding another.");
                reservation = info; liveModels++; liveVertices += info.Vertices; livePixels += info.TexturePixels; liveMorphVertices += info.MorphVertices;
                awaitCaller ??= new RuntimeOnlyAwaitCaller();
                if (info.IsAvatar)
                {
                    var avatar = await Vrm10.LoadBytesAsync(asset.Bytes, controlRigGenerationOption: ControlRigGenerationOption.None, showMeshes: false, awaitCaller: awaitCaller, materialGenerator: new BuiltInGltfMaterialDescriptorGenerator());
                    avatar.UpdateType = Vrm10Instance.UpdateTypes.None;
                    loaded = avatar.GetComponent<RuntimeGltfInstance>();
                }
                else loaded = await GltfUtility.LoadBytesAsync("selected.glb", asset.Bytes, awaitCaller, new BuiltInGltfMaterialDescriptorGenerator());
                if (!this || destroyed) { loaded.Dispose(); return; }
                instance = loaded;
                if (!info.IsAvatar) { generatedAvatar = NamedHumanoid.TryCreate(instance,out var issue); HumanoidIssue = issue; }
                animationPlayer = instance.GetComponent<Animation>();
                if (animationPlayer) { animationPlayer.playAutomatically = false; animationPlayer.cullingType = AnimationCullingType.AlwaysAnimate; animationPlayer.Stop(); }
                foreach (var skin in instance.SkinnedMeshRenderers) if (skin.sharedMesh) initialWeights[skin] = Enumerable.Range(0, skin.sharedMesh.blendShapeCount).Select(skin.GetBlendShapeWeight).ToArray();
                foreach (var animator in instance.GetComponentsInChildren<Animator>()) animator.enabled = false;
                if (instance.Renderers.Count == 0) throw new ModelImportException("This model has no supported visible mesh.");
                var bounds = instance.Renderers[0].bounds; foreach (var renderer in instance.Renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                rawBounds = bounds;
                float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (!float.IsFinite(size) || size < .00001f || size > 10000) throw new ModelImportException("The model has invalid dimensions. Apply transforms and export it again.");
                float factor = .35f / size;
                instance.transform.SetParent(transform, false); instance.transform.localScale = Vector3.one * factor;
                instance.transform.localRotation = info.IsAvatar ? Quaternion.Euler(0,180,0) : Quaternion.identity;
                instance.transform.localPosition = -(instance.transform.localRotation * bounds.center) * factor;
                LocalBounds = new Bounds(Vector3.zero, bounds.size * factor);
                instance.gameObject.AddComponent<PencilModelStyle>().Apply(); instance.ShowMeshes();
            }
            catch { if (loaded) loaded.Dispose(); instance = null; ArtResources.Release(generatedAvatar); generatedAvatar = null; ReleaseBudget(); throw; }
            finally { loadQueue.Release(); }
        }
        public void FitAsMaestro(float height = 1.7f)
        {
            if (!IsHumanoid || rawBounds.size.y < .01f) throw new ModelImportException(HumanoidIssue ?? "Choose a GLB or VRM with a valid humanoid skeleton to replace Maestro.");
            Stop();
            float scale = height / rawBounds.size.y;
            var left = Humanoid.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var right = Humanoid.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var across = instance.transform.InverseTransformDirection(left.position-right.position);
            var forward = Vector3.Cross(Vector3.up,across); forward.y = 0;
            if (forward.sqrMagnitude < .000001f) throw new ModelImportException("The avatar's left and right hips cannot establish a facing direction.");
            // FromToRotation has an ambiguous axis for opposite vectors and can
            // flip an otherwise upright model. Facing correction is yaw only.
            var rotation = Quaternion.AngleAxis(-Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg,Vector3.up);
            instance.transform.localRotation = rotation;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = -(rotation * new Vector3(rawBounds.center.x,rawBounds.min.y,rawBounds.center.z)) * scale;
            var size = rawBounds.size;
            var x = rotation * (Vector3.right * size.x); var z = rotation * (Vector3.forward * size.z);
            LocalBounds = new Bounds(Vector3.up * height * .5f,new Vector3(Mathf.Abs(x.x)+Mathf.Abs(z.x),size.y,Mathf.Abs(x.z)+Mathf.Abs(z.z)) * scale);
        }
        public void Play(int index, bool loop)
        {
            Stop(); if (!animationPlayer || index < 0 || index >= ClipCount) return;
            var clip = instance.AnimationClips[index]; var state = animationPlayer[clip.name];
            if (state == null) return;
            state.wrapMode = loop ? WrapMode.Loop : WrapMode.Once; state.time = 0;
            animationPlayer.Play(clip.name);
        }
        public void Stop()
        {
            if (animationPlayer) animationPlayer.Stop(); if (!instance) return;
            foreach (var pair in instance.InitialTransformStates)
            {
                if (!pair.Key) continue;
                pair.Key.SetLocalPositionAndRotation(pair.Value.LocalPosition, pair.Value.LocalRotation); pair.Key.localScale = pair.Value.LocalScale;
            }
            foreach (var pair in initialWeights) if (pair.Key) for (int i = 0; i < pair.Value.Length; i++) pair.Key.SetBlendShapeWeight(i, pair.Value[i]);
        }
        void ReleaseBudget() { if (reservation == null) return; liveModels--; liveVertices -= reservation.Vertices; livePixels -= reservation.TexturePixels; liveMorphVertices -= reservation.MorphVertices; reservation = null; }
        void OnApplicationPause(bool value) { if (value) Stop(); }
        void OnApplicationFocus(bool value) { if (!value) Stop(); }
        void OnDisable() => Stop();
        void OnDestroy() { destroyed = true; ReleaseBudget(); if (instance) instance.Dispose(); ArtResources.Release(generatedAvatar); }
    }
}
