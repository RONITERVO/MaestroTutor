// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using UnityEngine;

namespace Maestro.Quest.Cosmos
{
    /// <summary>
    /// Root HyperCosmosVR runtime orchestrator inside MaestroRoom.
    /// Connects CosmosArchipelagoBuilder, CosmosLandmarksPresenter, CosmosFlightController,
    /// CosmosNarrationFsm, CosmosInkPresenter, CosmosWristRing, and CosmosAudioEngine to Maestro's
    /// VirtualRoomView & WorkspaceHost without mutating WorkshopDefaults, RoomWorldMotion, or Acoustics invariants.
    /// </summary>
    public sealed class HyperCosmosWorld : MonoBehaviour
    {
        const int MaxFocusCandidates = 64;

        Transform roomContent;
        Transform trackingOrigin;
        Camera viewerCamera;
        BookControllerInput controllerInput;
        VirtualRoomView virtualView;
        WorkspaceHost workspace;
        Func<bool> headTracked;
        bool allowAutoEnterInSession;

        CosmosArchipelagoBuilder archipelago;
        CosmosLandmarksPresenter landmarks;
        CosmosFlightController flight;
        CosmosNarrationFsm narrationFsm;
        CosmosInkPresenter ink;
        CosmosWristRing wristRing;
        CosmosAudioEngine audioEngine;

        readonly CosmosFocusCandidate[] focusCandidates = new CosmosFocusCandidate[MaxFocusCandidates];
        WorkspaceContent boundSession;
        CosmosLocale activeLocale = CosmosLocale.EnUs;
        bool virtualModeActive;
        bool autoEnterAttempted;
        bool initialized;
        CosmosFsmStateKind prevFsmKind = CosmosFsmStateKind.Moving;
        byte lastAmbientExperienceId;

        public CosmosArchipelagoBuilder Archipelago => archipelago;
        public CosmosLandmarksPresenter Landmarks => landmarks;
        public CosmosFlightController Flight => flight;
        public CosmosNarrationFsm NarrationFsm => narrationFsm;
        public CosmosInkPresenter Ink => ink;
        public CosmosWristRing WristRing => wristRing;
        public CosmosAudioEngine Audio => audioEngine;
        public CosmosLocale ActiveLocale => activeLocale;
        public bool VirtualModeActive => virtualModeActive;

        internal void Initialize(
            Transform contentTransform,
            Transform originTransform,
            Camera mainCamera,
            BookControllerInput input,
            VirtualRoomView roomView,
            WorkspaceHost host,
            Func<bool> headTrackedProbe,
            bool allowAutoEnter)
        {
            if (initialized) return;
            initialized = true;

            roomContent = contentTransform;
            trackingOrigin = originTransform;
            viewerCamera = mainCamera;
            controllerInput = input;
            virtualView = roomView;
            workspace = host;
            headTracked = headTrackedProbe;
            allowAutoEnterInSession = allowAutoEnter;

            if (viewerCamera && viewerCamera.farClipPlane < 1000f)
                viewerCamera.farClipPlane = 1500f;

            archipelago = gameObject.AddComponent<CosmosArchipelagoBuilder>();
            archipelago.Initialize(roomContent);

            landmarks = gameObject.AddComponent<CosmosLandmarksPresenter>();
            landmarks.Initialize(roomContent, archipelago, viewerCamera);

            flight = gameObject.AddComponent<CosmosFlightController>();
            flight.Initialize(roomContent, trackingOrigin, viewerCamera, virtualView, null, null, controllerInput, archipelago);

            narrationFsm = new CosmosNarrationFsm();

            ink = gameObject.AddComponent<CosmosInkPresenter>();
            ink.Initialize(viewerCamera, roomContent);

            wristRing = gameObject.AddComponent<CosmosWristRing>();
            wristRing.Initialize(viewerCamera, activeLocale);

            audioEngine = gameObject.AddComponent<CosmosAudioEngine>();
            audioEngine.Initialize();

            if (virtualView)
                virtualView.PresentationApplied += OnPresentationApplied;
            if (workspace)
                workspace.Changed += RefreshSessionBindings;

            RefreshSessionBindings();
            ApplyVirtualPresentation(virtualView ? virtualView.BackdropOpacity : 0f, virtualView ? virtualView.RealDepth : true);
        }

        public void SetLocale(CosmosLocale locale)
        {
            if (locale == CosmosLocale.EnUs || locale == CosmosLocale.FiFi)
            {
                activeLocale = locale;
                wristRing?.SetLocale(locale);
            }
        }

        void Update()
        {
            if (!initialized) return;

            RefreshSessionBindings();
            TryAutoEnterVirtualCosmos();

            if (virtualView)
            {
                bool expectedVirtual = virtualView.BackdropOpacity > 0.05f || virtualView.Active;
                if (expectedVirtual != virtualModeActive)
                    ApplyVirtualPresentation(virtualView.BackdropOpacity, virtualView.RealDepth);
            }

            if (!virtualModeActive) return;

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
            CosmosInputSnapshot input = default;
            if (controllerInput)
                controllerInput.TryReadCosmosInput(out input);

            flight.SetHeadTrackedState(headTracked == null || headTracked());

            // 1. Diegetic Proximal Wrist Ring
            CosmosWristAction wristAction = wristRing.Step(dt, in input, flight.Mode);
            HandleWristAction(in wristAction);

            // 2. Build camera-space focus candidates (zero heap allocations)
            int candCount = BuildFocusCandidates(in input, out float nearestFocusDist, out Vector3 rawRayDirCam);

            // 3. 6DoF Scale-Invariant Flight + Grounded <-> FreeFlight <-> AutoLanding
            flight.Step(dt, in input, nearestFocusDist, wristRing.CapturesPointer);
            wristRing.SetFreeFlightActive(flight.Mode != CosmosLocomotionMode.GroundedWalk);

            // 4. Rest-to-Learn Bilingual Dwell Narration FSM
            float linearSpeed = flight.ScaleState.VelocityLocal.magnitude;
            float scaleSpeed = flight.ScaleState.ScaleVelocity;
            bool narrationFinished = ink.ConsumeNarrationFinished();
            CosmosFsmOutput fsmOut = narrationFsm.Step(rawRayDirCam, linearSpeed, scaleSpeed, dt, focusCandidates, candCount, narrationFinished);

            // 5. Audio Synth & Lock Shimmer
            CosmosScaleState scaleState = flight.ScaleState;
            UpdateAudioFromState(in fsmOut, in scaleState);

            // 6. Bilingual Live-Ink Typography & Glowing Bezier Tethers
            bool activelyNarrating = ink.Step(dt, in fsmOut, in scaleState, activeLocale, archipelago.AllFocusTargets);
            audioEngine.SetNarrationActive(activelyNarrating);

            // 7. Living Landmarks & Archipelago Pulse
            float timeSec = Time.unscaledTime;
            float focusPulse = Mathf.Clamp01(Mathf.Max(fsmOut.DwellProgress, activelyNarrating ? 1f : 0f));
            int highlightTargetId = fsmOut.State.HasTarget ? (int)fsmOut.State.Target.EntityId : -1;
            archipelago.UpdateVisualPulse(timeSec, focusPulse);
            landmarks.UpdateCosmosFrame(in scaleState, highlightTargetId, timeSec, focusPulse);
        }

        void HandleWristAction(in CosmosWristAction action)
        {
            switch (action.Kind)
            {
                case CosmosWristActionKind.SetSpeedMultiplier:
                    flight.SetSpeedMultiplier(action.SpeedMultiplier);
                    audioEngine.TriggerSpeedTick(action.SpeedBeadIndex, action.WorldPos);
                    break;
                case CosmosWristActionKind.SetLocale:
                    SetLocale(action.Locale);
                    audioEngine.TriggerSpeedTick(action.Locale == CosmosLocale.EnUs ? 2 : 5, action.WorldPos);
                    break;
                case CosmosWristActionKind.ToggleWalkFly:
                    flight.ToggleWalkFly();
                    audioEngine.TriggerSpeedTick(3, action.WorldPos);
                    break;
                case CosmosWristActionKind.TriggerEarthHome:
                    flight.TriggerEarthHome();
                    audioEngine.TriggerLockChime(action.WorldPos);
                    break;
            }
        }

        int BuildFocusCandidates(in CosmosInputSnapshot input, out float nearestFocusDist, out Vector3 rawRayDirCam)
        {
            nearestFocusDist = -1f;
            rawRayDirCam = Vector3.forward;
            if (!viewerCamera) return 0;

            Transform camT = viewerCamera.transform;
            Vector3 rayOrigin = camT.position;
            Vector3 rayWorld = camT.forward;
            if (input.HasPointerRay && input.PointerDir.sqrMagnitude > 1e-6f)
            {
                rayOrigin = input.PointerOrigin;
                rayWorld = input.PointerDir.normalized;
            }
            rawRayDirCam = camT.InverseTransformDirection(rayWorld).normalized;

            IReadOnlyList<CosmosFocusTarget> targets = archipelago.AllFocusTargets;
            if (targets == null || targets.Count == 0) return 0;

            int written = 0;
            float bestConeScore = -1f;
            double curU = flight.ScaleState.ScaleExp;

            for (int i = 0; i < targets.Count && written < MaxFocusCandidates; i++)
            {
                CosmosFocusTarget t = targets[i];
                Vector3 worldPos = roomContent ? roomContent.TransformPoint(t.LocalPosition) : t.LocalPosition;
                Vector3 toTarget = worldPos - rayOrigin;
                float dist = toTarget.magnitude;
                if (dist < 0.08f) continue;

                Vector3 dirWorld = toTarget / dist;
                float dot = Vector3.Dot(dirWorld, rayWorld);
                if (dot < 0.5f) continue; // Outside 60 deg forward cone

                Vector3 dirCam = camT.InverseTransformDirection(dirWorld).normalized;
                float angRadiusDeg = Mathf.Clamp(Mathf.Atan2(Mathf.Max(0.25f, t.FocusRadius), dist) * Mathf.Rad2Deg, 0.5f, 18f);
                double uDelta = Math.Abs(curU - t.FocusScaleExp);
                float visWeight = (float)Math.Exp(-0.08 * uDelta * uDelta);

                focusCandidates[written++] = new CosmosFocusCandidate
                {
                    EntityId = (ulong)t.TargetId,
                    DirCam = dirCam,
                    AngularRadiusDeg = angRadiusDeg,
                    SalienceTier = t.Salience,
                    VisibilityWeight = visWeight,
                    ExperienceId = t.ExperienceId,
                    ZoneIndex = t.ZoneIndex,
                    FeatureIndex = 0
                };

                if (dot > bestConeScore)
                {
                    bestConeScore = dot;
                    nearestFocusDist = Mathf.Max(0.1f, dist - t.FocusRadius);
                }
            }
            return written;
        }

        void UpdateAudioFromState(in CosmosFsmOutput fsmOut, in CosmosScaleState scale)
        {
            audioEngine.SetDwellProgress(fsmOut.DwellProgress);

            if (fsmOut.State.HasTarget && TryFindTargetWorldPos(fsmOut.State.Target, out var targetPos))
            {
                audioEngine.SetChimeWorldPosition(targetPos);
                if (fsmOut.State.Kind == CosmosFsmStateKind.Locked && prevFsmKind != CosmosFsmStateKind.Locked)
                    audioEngine.TriggerLockChime(targetPos);
            }
            prevFsmKind = fsmOut.State.Kind;

            byte zoneExpId = fsmOut.State.HasTarget ? fsmOut.State.Target.ExperienceId : scale.ActiveExperienceId;
            if (zoneExpId == 0) zoneExpId = 3; // Default Cold-Start Earth / Aurora
            if (zoneExpId != lastAmbientExperienceId)
            {
                lastAmbientExperienceId = zoneExpId;
                if (CosmosCatalogData.TryGetExperience(zoneExpId, out var expMeta))
                    audioEngine.SetZone(expMeta.RepresentativeSound, expMeta.Id);
            }
        }

        bool TryFindTargetWorldPos(in CosmosTargetIdentity id, out Vector3 worldPos)
        {
            IReadOnlyList<CosmosFocusTarget> targets = archipelago.AllFocusTargets;
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if ((ulong)targets[i].TargetId == id.EntityId)
                    {
                        worldPos = roomContent ? roomContent.TransformPoint(targets[i].LocalPosition) : targets[i].LocalPosition;
                        return true;
                    }
                }
            }
            worldPos = Vector3.zero;
            return false;
        }

        void RefreshSessionBindings()
        {
            if (!workspace) return;
            WorkspaceContent session = workspace.Current;
            if (session == boundSession) return;
            boundSession = session;

            MovementControls controls = session ? session.Controls : null;
            WorkspaceViewpoint vp = session ? session.GetComponent<WorkspaceViewpoint>() : null;
            flight.BindSession(controls, vp);
        }

        void TryAutoEnterVirtualCosmos()
        {
            if (autoEnterAttempted) return;
            if (!allowAutoEnterInSession || Application.isBatchMode)
            {
                autoEnterAttempted = true;
                return;
            }
            if (!virtualView || !virtualView.CanEnter) return;
            if (virtualView.PresentationChanged)
            {
                autoEnterAttempted = true;
                return;
            }
            if (headTracked != null && !headTracked()) return;
            if (!workspace || !workspace.Ready || workspace.Current == null) return;

            WorkspaceContent session = workspace.Current;
            if (session.Editor && session.Editor.RuntimeGate.Held) return;
            WorkspaceViewpoint vp = session.GetComponent<WorkspaceViewpoint>();
            if (vp && vp.Pending) return;

            autoEnterAttempted = true;
            virtualView.Enter();
        }

        void OnPresentationApplied(float opacity, bool realDepth) =>
            ApplyVirtualPresentation(opacity, realDepth);

        void ApplyVirtualPresentation(float opacity, bool realDepth)
        {
            bool nextActive = opacity > 0.05f || (virtualView && virtualView.Active);
            virtualModeActive = nextActive;
            archipelago?.SetVirtualModeActive(nextActive);
            landmarks?.SetVirtualModeActive(nextActive);
            flight?.SetVirtualModeActive(nextActive);

            if (!nextActive)
            {
                narrationFsm?.Reset();
                audioEngine?.SetNarrationActive(false);
                audioEngine?.SetDwellProgress(0f);
                audioEngine?.ClearZone();
                lastAmbientExperienceId = 0;
            }
            else if (lastAmbientExperienceId == 0)
            {
                lastAmbientExperienceId = 3;
                if (CosmosCatalogData.TryGetExperience(3, out var earthMeta))
                    audioEngine?.SetZone(earthMeta.RepresentativeSound, 3);
            }
        }

        void OnDestroy()
        {
            if (virtualView)
                virtualView.PresentationApplied -= OnPresentationApplied;
            if (workspace)
                workspace.Changed -= RefreshSessionBindings;
        }
    }
}
