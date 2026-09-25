// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using Maestro.Quest.Book;
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

        void Start()
        {
            var prefab = Resources.Load<GameObject>("Avatars/DefaultMaestro");
            if (!prefab) { Debug.LogError("The included Maestro model is missing."); return; }
            var model = Instantiate(prefab, transform);
            model.name = "Maestro drawing";
            model.AddComponent<PencilModelStyle>().Apply();
            animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Avatars/MaestroAnimations");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            greetingUntil = Time.unscaledTime + (ReducedMotion ? 0 : 2.5f);
            if (!ReducedMotion && animator.runtimeAnimatorController) animator.Play("Greeting");
        }

        void Update()
        {
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

    }
}
