// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maestro.Quest.Cosmos
{
    public enum CosmosBilingualPhase : byte
    {
        Idle = 0,
        Phase1Spanish = 1,
        Phase2Translation = 2,
        Complete = 3
    }

    /// <summary>
    /// 3D world-space bilingual live-ink presenter ported from crates/cosmos_typo/src/layout.rs.
    /// Features vergence clamping [1.6m..2.8m], 12-deg flight foveal avoidance, +-1.5-deg vertical
    /// de-overlap solver, max 18 ambient labels, Phase 1 es-ES SyncVoice sweep -> Phase 2 16% ghost
    /// Spanish + en-US/fi-FI reveal, luminous tapered Bezier tethers, and zero per-frame allocations.
    /// </summary>
    public sealed class CosmosInkPresenter : MonoBehaviour
    {
        public const float AngularDistMinM = 0.85f;
        public const float AngularDistMaxM = 18.0f;
        public const int MaxAmbientLabels = 18;
        public const float MaxVerticalNudgeDeg = 1.5f;
        public const float VergenceMinM = 1.6f;
        public const float VergenceMaxM = 2.8f;
        public const float FovealAvoidanceDeg = 12.0f;
        public const float SpanishGhostAlpha = 0.16f;

        struct AngularRect
        {
            public float AzMin, AzMax, ElMin, ElMax;

            public readonly bool Overlaps(in AngularRect other) =>
                AzMin < other.AzMax && AzMax > other.AzMin && ElMin < other.ElMax && ElMax > other.ElMin;

            public readonly AngularRect Nudged(float deltaEl) => new AngularRect
            {
                AzMin = AzMin,
                AzMax = AzMax,
                ElMin = ElMin + deltaEl,
                ElMax = ElMax + deltaEl
            };
        }

        struct EvalCandidate
        {
            public int FocusIndex;
            public string Text;
            public Vector3 WorldAnchorPos;
            public Vector3 DirFromEye;
            public float AzimuthRad;
            public float ElevationRad;
            public float DistanceM;
            public float Salience;
            public float TargetAngleRad;
            public Color AccentColor;
            public AngularRect Rect;
        }

        sealed class TextSlot
        {
            public GameObject Root;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public LineRenderer Tether;
            public Mesh Mesh;
            public string CachedText = string.Empty;
            public int MaxWrapCols;
            public float MinX;
            public float SpanX;
            public float HalfWidthNorm;
            public float HalfHeightNorm;
            public bool FontDirty;
        }

        const int MaxEvalCandidates = 64;
        const int TetherCurvePoints = 6;
        static readonly float[] NudgeOffsetsRad =
        {
            0f,
             0.35f * Mathf.Deg2Rad, -0.35f * Mathf.Deg2Rad,
             0.75f * Mathf.Deg2Rad, -0.75f * Mathf.Deg2Rad,
             1.15f * Mathf.Deg2Rad, -1.15f * Mathf.Deg2Rad,
             1.50f * Mathf.Deg2Rad, -1.50f * Mathf.Deg2Rad
        };

        readonly TextSlot[] ambientPool = new TextSlot[MaxAmbientLabels];
        readonly EvalCandidate[] candidates = new EvalCandidate[MaxEvalCandidates];
        readonly int[] sortedOrder = new int[MaxEvalCandidates];
        readonly AngularRect[] occupiedRects = new AngularRect[MaxAmbientLabels + 4];
        readonly Vector3[] tetherScratch = new Vector3[TetherCurvePoints];

        readonly List<Vector3> meshVerts = new List<Vector3>(256);
        readonly List<Vector2> meshUv0 = new List<Vector2>(256);
        readonly List<Vector2> meshUv1 = new List<Vector2>(256);
        readonly List<int> meshTris = new List<int>(384);

        readonly CosmosNarrationMemory narrationMemory = new CosmosNarrationMemory();
        readonly CosmosMemorySegmentInput[] memorySegmentsScratch = new CosmosMemorySegmentInput[3];
        CosmosMemoryPlanState activePlanState;

        TextSlot narrationEsSlot;
        TextSlot narrationTrSlot;
        TextSlot narrationHeadingSlot;
        Font activeFont;
        Material inkMaterial;
        Material tetherMaterial;
        MaterialPropertyBlock propBlock;
        Camera viewerCamera;
        Transform roomContent;
        bool initialized;

        CosmosBilingualPhase activePhase = CosmosBilingualPhase.Idle;
        CosmosZoneEntry activeZone;
        bool hasActiveZone;
        CosmosTargetIdentity activeTarget;
        Vector3 activeAnchorWorld;
        CosmosLocale activeLocale = CosmosLocale.EnUs;
        float speedMultiplier = 1f;
        float phaseElapsedMs;
        float activeDurationEsMs = 2400f;
        float activeDurationTrMs = 2400f;
        bool skipHeadingSegment;
        bool skipMetricSegment;
        float cueProgressEs;
        float cueProgressTr;
        float activeVergenceDistM = 2.1f;
        bool narrationJustFinished;
        int visibleAmbientCount;
        float blockFadeAlpha;

        public CosmosNarrationMemory Memory => narrationMemory;
        public CosmosBilingualPhase ActivePhase => activePhase;
        public float ActiveCueProgressEs => cueProgressEs;
        public float ActiveCueProgressTranslation => cueProgressTr;
        public float NarrationVergenceDistanceM => activeVergenceDistM;
        public int VisibleAmbientLabelCount => visibleAmbientCount;

        public static float ComputeVergenceDistance(float targetDistanceM) =>
            Mathf.Clamp(targetDistanceM, VergenceMinM, VergenceMaxM);

        public static float ComputeApparentWorldHeight(float distM, float targetAngleRad) =>
            Mathf.Clamp(distM, AngularDistMinM, AngularDistMaxM) * 2f * Mathf.Tan(0.5f * Mathf.Max(0f, targetAngleRad));

        public static float ComputeCueRevealCursor(
            CosmosWordCue[] buffer,
            int offset,
            int count,
            int totalChars,
            uint durationMs,
            uint elapsedMs)
        {
            if (totalChars <= 0) return 1f;
            if (buffer == null || count <= 0 || offset < 0 || offset + count > buffer.Length)
                return Mathf.Clamp01(elapsedMs / (float)Math.Max(1u, durationMs));

            float invTotal = 1f / totalChars;
            int prevEnd = 0;
            for (int i = 0; i < count; i++)
            {
                CosmosWordCue cue = buffer[offset + i];
                if (elapsedMs < cue.StartMs)
                    return Mathf.Clamp01(prevEnd * invTotal);
                if (elapsedMs < cue.EndMs)
                {
                    float spanMs = Math.Max(1u, cue.EndMs - cue.StartMs);
                    float frac = (elapsedMs - cue.StartMs) / spanMs;
                    float charCursor = cue.CharStart + frac * cue.CharLength;
                    return Mathf.Clamp01(charCursor * invTotal);
                }
                prevEnd = cue.CharStart + cue.CharLength;
            }
            return 1f;
        }

        internal void Initialize(Camera camera, Transform contentTransform = null)
        {
            viewerCamera = camera;
            if (contentTransform) roomContent = contentTransform;
            if (initialized) return;
            initialized = true;

            propBlock = new MaterialPropertyBlock();
            activeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                      ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (activeFont)
                Font.textureRebuilt += OnFontTextureRebuilt;

            inkMaterial = CosmosShaderIds.ResolveMaterial(
                CosmosShaderIds.ResMsdfLiveInkMat,
                CosmosShaderIds.ShaderMsdfLiveInk,
                new Color(1.0f, 0.94f, 0.82f, 1.0f));
            tetherMaterial = CosmosShaderIds.ResolveMaterial(
                CosmosShaderIds.ResTetherBridgeBeamMat,
                CosmosShaderIds.ShaderTetherBridgeBeam,
                new Color(0.35f, 0.85f, 1.25f, 1.0f));

            SyncFontAtlasTexture();

            for (int i = 0; i < MaxAmbientLabels; i++)
                ambientPool[i] = CreateSlot($"CosmosAmbientLabel_{i}", maxWrapCols: 0, createTether: true);

            narrationHeadingSlot = CreateSlot("CosmosNarrationHeading", maxWrapCols: 34, createTether: false);
            narrationEsSlot = CreateSlot("CosmosNarrationSpanish", maxWrapCols: 36, createTether: true);
            narrationTrSlot = CreateSlot("CosmosNarrationTranslation", maxWrapCols: 36, createTether: false);
            SetAllHidden();
        }

        public bool ConsumeNarrationFinished()
        {
            bool res = narrationJustFinished;
            narrationJustFinished = false;
            return res;
        }

        public bool Step(
            float dtSec,
            in CosmosFsmOutput fsm,
            in CosmosScaleState scale,
            CosmosLocale locale,
            IReadOnlyList<CosmosFocusTarget> focusTargets)
        {
            if (!initialized) Initialize(viewerCamera);
            float dt = Mathf.Clamp(dtSec, 0f, 0.1f);
            activeLocale = locale;
            speedMultiplier = Mathf.Clamp(scale.SpeedMultiplier, 1f, 3f);
            narrationJustFinished = false;

            if (!scale.VirtualModeActive || !viewerCamera)
            {
                SetAllHidden();
                return false;
            }

            if (fsm.ShouldCancelAudio)
            {
                CancelActiveNarration();
            }

            if (fsm.HasStartNarrationTarget && TryResolveTargetAndZone(fsm.StartNarrationTarget, focusTargets, out var targetAnchor, out var zoneEntry))
            {
                StartNarration(fsm.StartNarrationTarget, targetAnchor, zoneEntry, locale);
            }

            // Update active narration timeline (Phase 1 es-ES -> Phase 2 translation reveal).
            UpdateNarrationTimeline(dt, in fsm);

            // Layout billboarded stereo-safe narration block & get blocker angular rect so ambient labels stay clear.
            bool hasBlocker = LayoutNarrationBlock(focusTargets, in fsm, scale.VelocityLocal.magnitude > 0.15f, out AngularRect blockerRect);

            // Layout <= 18 ambient labels with +-1.5 deg vertical de-overlap & 12 deg foveal flight avoidance.
            LayoutAmbientLabels(in scale, focusTargets, hasBlocker, in blockerRect);

            return activePhase == CosmosBilingualPhase.Phase1Spanish || activePhase == CosmosBilingualPhase.Phase2Translation;
        }

        void StartNarration(CosmosTargetIdentity target, Vector3 worldAnchor, CosmosZoneEntry zone, CosmosLocale locale)
        {
            activeTarget = target;
            activeAnchorWorld = worldAnchor;
            activeZone = zone;
            hasActiveZone = true;
            phaseElapsedMs = 0f;
            cueProgressEs = 0f;
            cueProgressTr = 0f;
            blockFadeAlpha = 1f;

            string trLocaleCode = locale.Bcp47Code();
            memorySegmentsScratch[0] = new CosmosMemorySegmentInput
            {
                Id = "region-heading",
                Role = CosmosMemoryRole.RegionHeading,
                Display = zone.Name.Es ?? string.Empty,
                Narration = zone.Name.Es ?? string.Empty,
                Translation = zone.Name.GetText(locale) ?? string.Empty
            };
            memorySegmentsScratch[1] = new CosmosMemorySegmentInput
            {
                Id = "shared-context",
                Role = CosmosMemoryRole.SharedContext,
                Display = zone.Metric.Es ?? string.Empty,
                Narration = zone.Metric.Es ?? string.Empty,
                Translation = zone.Metric.GetText(locale) ?? string.Empty
            };
            memorySegmentsScratch[2] = new CosmosMemorySegmentInput
            {
                Id = zone.ZoneId ?? "item-fact",
                Role = null,
                Display = zone.Fact.Es ?? string.Empty,
                Narration = zone.Fact.Es ?? string.Empty,
                Translation = zone.Fact.GetText(locale) ?? string.Empty
            };

            string regionKey = $"exp-{zone.ExperienceId}";
            activePlanState = narrationMemory.BeginPlan(regionKey, memorySegmentsScratch, trLocaleCode);
            skipHeadingSegment = activePlanState.Skipped != null && activePlanState.Skipped.Length > 0 && activePlanState.Skipped[0];
            skipMetricSegment = activePlanState.Skipped != null && activePlanState.Skipped.Length > 1 && activePlanState.Skipped[1];

            activeDurationEsMs = Mathf.Max(800f, zone.Fact.DurationMsEs > 0 ? zone.Fact.DurationMsEs : 2600f);
            ushort trDur = zone.Fact.GetDurationMs(locale);
            activeDurationTrMs = Mathf.Max(800f, trDur > 0 ? trDur : 2400f);
            activePhase = CosmosBilingualPhase.Phase1Spanish;
        }

        void CancelActiveNarration()
        {
            narrationMemory.InvalidatePending();
            if (activePhase == CosmosBilingualPhase.Phase1Spanish || activePhase == CosmosBilingualPhase.Phase2Translation)
                activePhase = CosmosBilingualPhase.Idle;
        }

        void UpdateNarrationTimeline(float dtSec, in CosmosFsmOutput fsm)
        {
            if (activePhase == CosmosBilingualPhase.Idle)
            {
                blockFadeAlpha = fsm.CancelFadeAlpha;
                return;
            }
            if (activePhase == CosmosBilingualPhase.Complete)
            {
                blockFadeAlpha = fsm.State.Kind == CosmosFsmStateKind.Moving ? fsm.CancelFadeAlpha : 1f;
                return;
            }

            blockFadeAlpha = 1f;
            float scaledDeltaMs = dtSec * 1000f * speedMultiplier;
            phaseElapsedMs += scaledDeltaMs;

            if (activePhase == CosmosBilingualPhase.Phase1Spanish)
            {
                uint elapsed = (uint)Mathf.Max(0f, phaseElapsedMs);
                uint dur = (uint)Mathf.Max(1f, activeDurationEsMs);
                CosmosWordCue[] buf = CosmosCatalogData.WordCues;
                CosmosCueSpan span = activeZone.FactCuesEs;
                cueProgressEs = ComputeCueRevealCursor(buf, span.Offset, span.Count, activeZone.Fact.Es?.Length ?? 0, dur, elapsed);

                if (phaseElapsedMs >= activeDurationEsMs)
                {
                    cueProgressEs = 1f;
                    phaseElapsedMs = 0f;
                    activePhase = CosmosBilingualPhase.Phase2Translation;
                }
            }
            else if (activePhase == CosmosBilingualPhase.Phase2Translation)
            {
                uint elapsed = (uint)Mathf.Max(0f, phaseElapsedMs);
                uint dur = (uint)Mathf.Max(1f, activeDurationTrMs);
                CosmosWordCue[] buf = CosmosCatalogData.WordCues;
                CosmosCueSpan span = activeZone.GetFactCues(activeLocale);
                string trText = activeZone.Fact.GetText(activeLocale);
                cueProgressTr = ComputeCueRevealCursor(buf, span.Offset, span.Count, trText?.Length ?? 0, dur, elapsed);

                if (phaseElapsedMs >= activeDurationTrMs)
                {
                    cueProgressTr = 1f;
                    narrationMemory.MarkPairCompleted(activePlanState, 0, true, true);
                    narrationMemory.MarkPairCompleted(activePlanState, 1, true, true);
                    activePhase = CosmosBilingualPhase.Complete;
                    narrationJustFinished = true;
                }
            }
        }

        bool LayoutNarrationBlock(IReadOnlyList<CosmosFocusTarget> focusTargets, in CosmosFsmOutput fsm, bool inFastFlight, out AngularRect blockerRect)
        {
            blockerRect = default;
            bool showActive = hasActiveZone && (activePhase != CosmosBilingualPhase.Idle || blockFadeAlpha > 0.01f);
            Vector3 dwellAnchor = default;
            CosmosZoneEntry dwellZone = default;
            bool showDwellPreview = !showActive && fsm.State.HasTarget && fsm.DwellProgress > 0.08f
                                    && TryResolveTargetAndZone(fsm.State.Target, focusTargets, out dwellAnchor, out dwellZone);

            if (!showActive && !showDwellPreview)
            {
                HideSlot(narrationHeadingSlot);
                HideSlot(narrationEsSlot);
                HideSlot(narrationTrSlot);
                return false;
            }

            Vector3 eye = viewerCamera.transform.position;
            Vector3 fwd = viewerCamera.transform.forward;
            Vector3 right = viewerCamera.transform.right;
            Vector3 up = viewerCamera.transform.up;

            CosmosZoneEntry zone = showActive ? activeZone : dwellZone;
            Vector3 anchorW = showActive ? activeAnchorWorld : dwellAnchor;
            float targetDist = Vector3.Distance(eye, anchorW);
            activeVergenceDistM = ComputeVergenceDistance(targetDist);

            // Offset comfortably off center / fovea (especially outside central 12 deg cone during flight).
            Vector3 toAnchorDir = targetDist > 0.05f ? (anchorW - eye) / targetDist : fwd;
            Vector3 dirNorm = toAnchorDir;
            if (inFastFlight)
            {
                float foveaAngle = Vector3.Angle(fwd, dirNorm);
                if (foveaAngle < FovealAvoidanceDeg)
                {
                    dirNorm = (fwd * Mathf.Cos(FovealAvoidanceDeg * Mathf.Deg2Rad) + (right * 0.65f - up * 0.75f).normalized * Mathf.Sin(FovealAvoidanceDeg * Mathf.Deg2Rad)).normalized;
                }
            }

            Vector3 panelCenter = eye + dirNorm * activeVergenceDistM + up * (activeVergenceDistM * 0.055f) + right * (activeVergenceDistM * 0.07f);
            Quaternion billboardRot = Quaternion.LookRotation(panelCenter - eye, up);

            float lineHeight = ComputeApparentWorldHeight(activeVergenceDistM, 1.32f * Mathf.Deg2Rad);
            float alphaScale = showActive ? blockFadeAlpha : Mathf.Clamp01((fsm.DwellProgress - 0.08f) / 0.45f) * 0.75f;

            string headingText = string.Concat(zone.Name.Es ?? string.Empty, "  |  ", zone.Metric.Es ?? string.Empty);
            string esText = zone.Fact.Es ?? string.Empty;
            string trText = zone.Fact.GetText(activeLocale) ?? string.Empty;

            float headingAlpha = skipHeadingSegment && skipMetricSegment ? 0.35f : 0.92f;
            float esAlpha = activePhase == CosmosBilingualPhase.Phase2Translation || activePhase == CosmosBilingualPhase.Complete
                ? SpanishGhostAlpha
                : 1.0f;
            float trAlpha = activePhase == CosmosBilingualPhase.Phase2Translation || activePhase == CosmosBilingualPhase.Complete
                ? 1.0f
                : 0.0f;

            Vector3 headPos = panelCenter + up * (lineHeight * 2.1f);
            Vector3 esPos = panelCenter + up * (lineHeight * 0.45f);
            Vector3 trPos = panelCenter - up * (lineHeight * 1.45f);

            UpdateSlotMeshAndMaterial(
                narrationHeadingSlot,
                headingText,
                headPos,
                billboardRot,
                lineHeight * 0.85f,
                new Color(0.75f, 0.92f, 1.0f, alphaScale * headingAlpha),
                new Vector4(0f, 1f, 0.05f, 1f));

            float revealEs = showActive ? (activePhase == CosmosBilingualPhase.Phase1Spanish ? cueProgressEs : 1f) : fsm.DwellProgress;
            UpdateSlotMeshAndMaterial(
                narrationEsSlot,
                esText,
                esPos,
                billboardRot,
                lineHeight,
                new Color(1.0f, 0.95f, 0.85f, alphaScale * esAlpha),
                new Vector4(0f, revealEs, 0.06f, showActive && activePhase == CosmosBilingualPhase.Phase1Spanish ? 0.16f : 1f));

            UpdateTether(narrationEsSlot, anchorW, esPos - billboardRot * new Vector3(narrationEsSlot.HalfWidthNorm * lineHeight, 0f, 0f), zone.SrgbColor, alphaScale);

            if (trAlpha > 0.02f && !string.IsNullOrEmpty(trText))
            {
                float revealTr = activePhase == CosmosBilingualPhase.Complete ? 1f : cueProgressTr;
                UpdateSlotMeshAndMaterial(
                    narrationTrSlot,
                    trText,
                    trPos,
                    billboardRot,
                    lineHeight * 0.96f,
                    new Color(0.82f, 0.95f, 1.0f, alphaScale * trAlpha),
                    new Vector4(0f, revealTr, 0.06f, 0.16f));
            }
            else
            {
                HideSlot(narrationTrSlot);
            }

            blockerRect = ComputeAngularRect(eye, fwd, right, up, panelCenter, lineHeight * 14f, lineHeight * 6f);
            return true;
        }

        void LayoutAmbientLabels(in CosmosScaleState scale, IReadOnlyList<CosmosFocusTarget> focusTargets, bool hasBlocker, in AngularRect blockerRect)
        {
            visibleAmbientCount = 0;
            int totalTargets = focusTargets?.Count ?? 0;
            if (totalTargets == 0)
            {
                for (int i = 0; i < MaxAmbientLabels; i++) HideSlot(ambientPool[i]);
                return;
            }

            Vector3 eye = viewerCamera.transform.position;
            Vector3 fwd = viewerCamera.transform.forward;
            Vector3 right = viewerCamera.transform.right;
            Vector3 up = viewerCamera.transform.up;
            bool inFlight = scale.Mode != CosmosLocomotionMode.GroundedWalk && scale.VelocityLocal.magnitude > 0.18f;

            int evalCount = 0;
            for (int i = 0; i < totalTargets && evalCount < MaxEvalCandidates; i++)
            {
                CosmosFocusTarget t = focusTargets[i];
                if (hasActiveZone && t.ExperienceId == activeTarget.ExperienceId && t.ZoneIndex == activeTarget.ZoneIndex)
                    continue;

                Vector3 worldAnchor = roomContent ? roomContent.TransformPoint(t.LocalPosition) : t.LocalPosition;
                Vector3 delta = worldAnchor - eye;
                float dist = delta.magnitude;
                if (dist < 0.25f || dist > 180f) continue;

                Vector3 dir = delta / dist;
                float fwdDot = Vector3.Dot(dir, fwd);
                if (fwdDot < 0.18f) continue;

                float az = Mathf.Atan2(Vector3.Dot(dir, right), fwdDot);
                float el = Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir, up), -1f, 1f));
                float foveaDeg = Mathf.Sqrt(az * az + el * el) * Mathf.Rad2Deg;

                // 12 deg foveal avoidance during high-speed flight.
                if (inFlight && foveaDeg < FovealAvoidanceDeg)
                    continue;

                string label = t.GetLabel(activeLocale);
                if (string.IsNullOrEmpty(label)) continue;

                float targetAngle = t.Kind == CosmosFocusTargetKind.ExperienceCore ? 1.15f * Mathf.Deg2Rad : 0.85f * Mathf.Deg2Rad;
                float comfortDist = Mathf.Clamp(dist, AngularDistMinM, AngularDistMaxM);
                float h = ComputeApparentWorldHeight(comfortDist, targetAngle);
                float halfH = 0.65f * (h / comfortDist);
                float halfW = halfH * Mathf.Clamp(label.Length * 0.56f, 2.2f, 14f);

                candidates[evalCount] = new EvalCandidate
                {
                    FocusIndex = i,
                    Text = label,
                    WorldAnchorPos = worldAnchor,
                    DirFromEye = dir,
                    AzimuthRad = az,
                    ElevationRad = el,
                    DistanceM = comfortDist,
                    Salience = t.Salience * (1f / (1f + 0.04f * dist)),
                    TargetAngleRad = targetAngle,
                    AccentColor = t.AccentLinear,
                    Rect = new AngularRect
                    {
                        AzMin = az - halfW,
                        AzMax = az + halfW,
                        ElMin = el - halfH,
                        ElMax = el + halfH
                    }
                };
                sortedOrder[evalCount] = evalCount;
                evalCount++;
            }

            // Simple allocation-free insertion sort by salience descending.
            for (int i = 1; i < evalCount; i++)
            {
                int keyIdx = sortedOrder[i];
                float keySal = candidates[keyIdx].Salience;
                int j = i - 1;
                while (j >= 0 && candidates[sortedOrder[j]].Salience < keySal)
                {
                    sortedOrder[j + 1] = sortedOrder[j];
                    j--;
                }
                sortedOrder[j + 1] = keyIdx;
            }

            int occupiedCount = 0;
            if (hasBlocker)
                occupiedRects[occupiedCount++] = blockerRect;

            for (int s = 0; s < evalCount && visibleAmbientCount < MaxAmbientLabels; s++)
            {
                ref readonly EvalCandidate c = ref candidates[sortedOrder[s]];
                float acceptedNudge = float.NaN;
                AngularRect chosenRect = default;

                for (int n = 0; n < NudgeOffsetsRad.Length; n++)
                {
                    AngularRect trial = c.Rect.Nudged(NudgeOffsetsRad[n]);
                    bool collides = false;
                    for (int o = 0; o < occupiedCount; o++)
                    {
                        if (trial.Overlaps(in occupiedRects[o])) { collides = true; break; }
                    }
                    if (!collides)
                    {
                        acceptedNudge = NudgeOffsetsRad[n];
                        chosenRect = trial;
                        break;
                    }
                }

                if (float.IsNaN(acceptedNudge))
                    continue;

                if (occupiedCount < occupiedRects.Length)
                    occupiedRects[occupiedCount++] = chosenRect;

                TextSlot slot = ambientPool[visibleAmbientCount++];
                Vector3 labelDir = Quaternion.AngleAxis(-acceptedNudge * Mathf.Rad2Deg, right) * c.DirFromEye;
                Vector3 placePos = eye + labelDir * c.DistanceM + up * (c.DistanceM * Mathf.Tan(0.014f));
                Quaternion rot = Quaternion.LookRotation(placePos - eye, up);
                float worldHeight = ComputeApparentWorldHeight(c.DistanceM, c.TargetAngleRad);

                Color ink = Color.Lerp(Color.white, c.AccentColor, 0.25f);
                ink.a = 0.92f;
                UpdateSlotMeshAndMaterial(slot, c.Text, placePos, rot, worldHeight, ink, new Vector4(0f, 1f, 0.04f, 1f));
                UpdateTether(slot, c.WorldAnchorPos, placePos - up * (worldHeight * 0.55f), c.AccentColor, 0.85f);
            }

            for (int i = visibleAmbientCount; i < MaxAmbientLabels; i++)
                HideSlot(ambientPool[i]);
        }

        bool TryResolveTargetAndZone(in CosmosTargetIdentity id, IReadOnlyList<CosmosFocusTarget> focusTargets, out Vector3 worldAnchor, out CosmosZoneEntry entry)
        {
            worldAnchor = Vector3.zero;
            if (focusTargets != null)
            {
                for (int i = 0; i < focusTargets.Count; i++)
                {
                    if ((ulong)focusTargets[i].TargetId == id.EntityId || (focusTargets[i].ExperienceId == id.ExperienceId && focusTargets[i].ZoneIndex == id.ZoneIndex))
                    {
                        worldAnchor = roomContent ? roomContent.TransformPoint(focusTargets[i].LocalPosition) : focusTargets[i].LocalPosition;
                        if (focusTargets[i].FlagshipZoneRecordIndex >= 0 && focusTargets[i].FlagshipZoneRecordIndex < CosmosCatalogData.FlagshipZones.Length)
                        {
                            entry = CosmosCatalogData.FlagshipZones[focusTargets[i].FlagshipZoneRecordIndex];
                            return true;
                        }
                        break;
                    }
                }
            }

            if (CosmosCatalogData.TryGetZone(id.ExperienceId, (byte)id.ZoneIndex, out entry)) return true;
            return CosmosCatalogData.TryGetPrimaryZoneForExperience(id.ExperienceId, out entry);
        }

        TextSlot CreateSlot(string slotName, int maxWrapCols, bool createTether)
        {
            var go = new GameObject(slotName);
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = inkMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mesh = new Mesh { name = slotName + "_Mesh" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;

            LineRenderer tether = null;
            if (createTether)
            {
                var tGo = new GameObject(slotName + "_Tether");
                tGo.transform.SetParent(go.transform, false);
                tether = tGo.AddComponent<LineRenderer>();
                tether.sharedMaterial = tetherMaterial;
                tether.useWorldSpace = true;
                tether.positionCount = TetherCurvePoints;
                tether.shadowCastingMode = ShadowCastingMode.Off;
                tether.receiveShadows = false;
                tether.widthCurve = AnimationCurve.Linear(0f, 0.008f, 1f, 0.0022f);
                tether.enabled = false;
            }

            return new TextSlot
            {
                Root = go,
                Filter = filter,
                Renderer = mr,
                Tether = tether,
                Mesh = mesh,
                MaxWrapCols = maxWrapCols
            };
        }

        void UpdateSlotMeshAndMaterial(
            TextSlot slot,
            string text,
            Vector3 worldPos,
            Quaternion rot,
            float lineHeight,
            Color inkColor,
            Vector4 revealRange)
        {
            if (slot == null || !slot.Root) return;
            if (!slot.Root.activeSelf) slot.Root.SetActive(true);
            slot.Root.transform.SetPositionAndRotation(worldPos, rot);
            slot.Root.transform.localScale = new Vector3(lineHeight, lineHeight, lineHeight);

            if (!string.Equals(slot.CachedText, text, StringComparison.Ordinal) || slot.FontDirty)
            {
                slot.CachedText = text ?? string.Empty;
                slot.FontDirty = false;
                RebuildSlotMesh(slot);
            }

            propBlock.Clear();
            Texture tex = activeFont && activeFont.material ? activeFont.material.mainTexture : Texture2D.whiteTexture;
            if (tex) propBlock.SetTexture(CosmosShaderIds.MainTex, tex);
            propBlock.SetColor(CosmosShaderIds.Color, inkColor);
            propBlock.SetColor(CosmosShaderIds.HaloColor, new Color(0.002f, 0.003f, 0.006f, 0.94f));
            propBlock.SetVector(CosmosShaderIds.RevealRange, revealRange);
            propBlock.SetFloat(CosmosShaderIds.GlyphIndex, -1f);
            propBlock.SetFloat(CosmosShaderIds.GlyphCount, Mathf.Max(1, slot.CachedText.Length));
            propBlock.SetFloat(CosmosShaderIds.SdfMode, 0f);
            propBlock.SetFloat(CosmosShaderIds.HaloWidth, 0.22f);
            propBlock.SetColor(CosmosShaderIds.NibColor, new Color(0.35f, 0.85f, 1.25f, 1f));
            propBlock.SetVector(CosmosShaderIds.PhraseBounds, new Vector4(slot.MinX, slot.SpanX, 0f, 0f));
            propBlock.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
            slot.Renderer.SetPropertyBlock(propBlock);
            slot.Renderer.enabled = true;
        }

        void UpdateTether(TextSlot slot, Vector3 anchorWorld, Vector3 labelEdgeWorld, Color color, float alpha)
        {
            if (slot?.Tether == null) return;
            slot.Tether.enabled = alpha > 0.02f;
            if (!slot.Tether.enabled) return;

            Vector3 mid = Vector3.Lerp(anchorWorld, labelEdgeWorld, 0.5f);
            mid.y += Vector3.Distance(anchorWorld, labelEdgeWorld) * 0.12f;

            for (int i = 0; i < TetherCurvePoints; i++)
            {
                float t = i / (float)(TetherCurvePoints - 1);
                float omt = 1f - t;
                tetherScratch[i] = omt * omt * anchorWorld + 2f * omt * t * mid + t * t * labelEdgeWorld;
            }
            slot.Tether.SetPositions(tetherScratch);

            propBlock.Clear();
            Color beam = color;
            beam.a = Mathf.Clamp01(alpha * 0.85f);
            propBlock.SetColor(CosmosShaderIds.Color, beam);
            propBlock.SetFloat(CosmosShaderIds.PulseSpeed, 1.8f);
            propBlock.SetFloat(CosmosShaderIds.ActiveFocus, alpha);
            propBlock.SetFloat(CosmosShaderIds.TimeSec, Time.unscaledTime);
            slot.Tether.SetPropertyBlock(propBlock);
        }

        void RebuildSlotMesh(TextSlot slot)
        {
            meshVerts.Clear();
            meshUv0.Clear();
            meshUv1.Clear();
            meshTris.Clear();

            string text = slot.CachedText;
            if (string.IsNullOrEmpty(text) || !activeFont)
            {
                slot.Mesh.Clear();
                slot.MinX = 0f;
                slot.SpanX = 1f;
                slot.HalfWidthNorm = 0f;
                slot.HalfHeightNorm = 0.5f;
                return;
            }

            const int FontPx = 48;
            const float NormScale = 1f / FontPx;
            activeFont.RequestCharactersInTexture(text, FontPx, FontStyle.Normal);

            // Simple word-wrap measurement & emission.
            int len = text.Length;
            int lineStart = 0;
            int row = 0;
            int wrapCols = slot.MaxWrapCols > 0 ? slot.MaxWrapCols : len + 1;
            float maxLineW = 0f;

            // Count lines first cleanly by estimating wrap points.
            int approxLines = Mathf.Max(1, Mathf.CeilToInt(len / (float)wrapCols));
            float baseTopY = (approxLines - 1) * 0.58f;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            int emittedChars = 0;

            while (lineStart < len)
            {
                int lineEnd = Mathf.Min(len, lineStart + wrapCols);
                if (lineEnd < len)
                {
                    int space = text.LastIndexOf(' ', lineEnd - 1, lineEnd - lineStart);
                    if (space > lineStart) lineEnd = space + 1;
                }

                float lineWidth = 0f;
                for (int i = lineStart; i < lineEnd; i++)
                {
                    if (activeFont.GetCharacterInfo(text[i], out var advInfo, FontPx, FontStyle.Normal))
                        lineWidth += advInfo.advance * NormScale;
                }
                if (lineWidth > maxLineW) maxLineW = lineWidth;

                float cursorX = -0.5f * lineWidth;
                float cursorY = baseTopY - row * 1.15f;

                for (int i = lineStart; i < lineEnd; i++)
                {
                    char ch = text[i];
                    if (!activeFont.GetCharacterInfo(ch, out var info, FontPx, FontStyle.Normal))
                        continue;

                    if (info.maxX > info.minX && info.maxY > info.minY)
                    {
                        int vBase = meshVerts.Count;
                        float x0 = cursorX + info.minX * NormScale;
                        float x1 = cursorX + info.maxX * NormScale;
                        float y0 = cursorY + info.minY * NormScale;
                        float y1 = cursorY + info.maxY * NormScale;

                        minX = Mathf.Min(minX, x0);
                        maxX = Mathf.Max(maxX, x1);

                        float progress = (i + 0.5f) / Mathf.Max(1, len);
                        Vector2 progUv = new Vector2(progress, 0f);

                        meshVerts.Add(new Vector3(x0, y0, 0f));
                        meshVerts.Add(new Vector3(x1, y0, 0f));
                        meshVerts.Add(new Vector3(x1, y1, 0f));
                        meshVerts.Add(new Vector3(x0, y1, 0f));

                        meshUv0.Add(info.uvBottomLeft);
                        meshUv0.Add(info.uvBottomRight);
                        meshUv0.Add(info.uvTopRight);
                        meshUv0.Add(info.uvTopLeft);

                        meshUv1.Add(progUv);
                        meshUv1.Add(progUv);
                        meshUv1.Add(progUv);
                        meshUv1.Add(progUv);

                        meshTris.Add(vBase);
                        meshTris.Add(vBase + 2);
                        meshTris.Add(vBase + 1);
                        meshTris.Add(vBase);
                        meshTris.Add(vBase + 3);
                        meshTris.Add(vBase + 2);
                        emittedChars++;
                    }
                    cursorX += info.advance * NormScale;
                }

                lineStart = lineEnd;
                row++;
            }

            slot.Mesh.Clear();
            if (meshVerts.Count > 0)
            {
                slot.Mesh.SetVertices(meshVerts);
                slot.Mesh.SetUVs(0, meshUv0);
                slot.Mesh.SetUVs(1, meshUv1);
                slot.Mesh.SetTriangles(meshTris, 0);
                slot.Mesh.RecalculateBounds();
            }

            slot.MinX = minX < float.MaxValue ? minX : -0.5f * maxLineW;
            slot.SpanX = maxX > minX ? (maxX - minX) : Mathf.Max(0.25f, maxLineW);
            slot.HalfWidthNorm = 0.5f * maxLineW;
            slot.HalfHeightNorm = 0.58f * Mathf.Max(1, row);
        }

        static AngularRect ComputeAngularRect(Vector3 eye, Vector3 fwd, Vector3 right, Vector3 up, Vector3 center, float widthM, float heightM)
        {
            Vector3 d = center - eye;
            float dist = Mathf.Max(0.5f, Vector3.Dot(d, fwd));
            float az = Mathf.Atan2(Vector3.Dot(d, right), dist);
            float el = Mathf.Atan2(Vector3.Dot(d, up), dist);
            float hw = 0.5f * widthM / dist;
            float hh = 0.5f * heightM / dist;
            return new AngularRect { AzMin = az - hw, AzMax = az + hw, ElMin = el - hh, ElMax = el + hh };
        }

        void OnFontTextureRebuilt(Font rebuiltFont)
        {
            if (rebuiltFont != activeFont) return;
            SyncFontAtlasTexture();
            for (int i = 0; i < MaxAmbientLabels; i++)
                if (ambientPool[i] != null) ambientPool[i].FontDirty = true;
            if (narrationHeadingSlot != null) narrationHeadingSlot.FontDirty = true;
            if (narrationEsSlot != null) narrationEsSlot.FontDirty = true;
            if (narrationTrSlot != null) narrationTrSlot.FontDirty = true;
        }

        void SyncFontAtlasTexture()
        {
            // Never mutate shared project asset Resources/Cosmos/Cosmos_MsdfLiveInk.mat;
            // dynamic font atlas texture is passed per draw call via MaterialPropertyBlock.
        }

        static void HideSlot(TextSlot slot)
        {
            if (slot == null) return;
            if (slot.Tether && slot.Tether.enabled) slot.Tether.enabled = false;
            if (slot.Root && slot.Root.activeSelf) slot.Root.SetActive(false);
        }

        void SetAllHidden()
        {
            visibleAmbientCount = 0;
            for (int i = 0; i < ambientPool.Length; i++) HideSlot(ambientPool[i]);
            HideSlot(narrationHeadingSlot);
            HideSlot(narrationEsSlot);
            HideSlot(narrationTrSlot);
        }

        void ReleaseSlot(TextSlot slot)
        {
            if (slot == null) return;
            if (slot.Mesh) ArtResources.Release(slot.Mesh);
        }

        void OnDestroy()
        {
            if (activeFont)
                Font.textureRebuilt -= OnFontTextureRebuilt;
            for (int i = 0; i < ambientPool.Length; i++) ReleaseSlot(ambientPool[i]);
            ReleaseSlot(narrationHeadingSlot);
            ReleaseSlot(narrationEsSlot);
            ReleaseSlot(narrationTrSlot);
            if (inkMaterial && inkMaterial.name.EndsWith("(RuntimeFallback)", StringComparison.Ordinal))
                ArtResources.Release(inkMaterial);
            if (tetherMaterial && tetherMaterial.name.EndsWith("(RuntimeFallback)", StringComparison.Ordinal))
                ArtResources.Release(tetherMaterial);
        }
    }
}
