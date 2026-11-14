// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Maestro.Quest.Cosmos
{
    public enum CosmosFsmStateKind
    {
        Moving = 0,
        Settling = 1,
        Locked = 2,
        Narrating = 3,
        Complete = 4
    }

    public struct CosmosFocusCandidate
    {
        public ulong EntityId;
        public Vector3 DirCam;
        public float AngularRadiusDeg;
        public float SalienceTier;
        public float VisibilityWeight;
        public byte ExperienceId;
        public ushort ZoneIndex;
        public ushort FeatureIndex;

        public readonly float Score(float thetaRad, float coneRad)
        {
            float ratio = thetaRad / Mathf.Max(1e-5f, coneRad);
            return Mathf.Max(0f, SalienceTier) * Mathf.Exp(-(ratio * ratio)) * Mathf.Max(0f, VisibilityWeight);
        }
    }

    public readonly struct CosmosTargetIdentity : IEquatable<CosmosTargetIdentity>
    {
        public readonly ulong EntityId;
        public readonly byte ExperienceId;
        public readonly ushort ZoneIndex;
        public readonly ushort FeatureIndex;

        public CosmosTargetIdentity(ulong entityId, byte experienceId, ushort zoneIndex, ushort featureIndex)
        {
            EntityId = entityId;
            ExperienceId = experienceId;
            ZoneIndex = zoneIndex;
            FeatureIndex = featureIndex;
        }

        public static CosmosTargetIdentity FromCandidate(in CosmosFocusCandidate candidate) =>
            new CosmosTargetIdentity(candidate.EntityId, candidate.ExperienceId, candidate.ZoneIndex, candidate.FeatureIndex);

        public bool Equals(CosmosTargetIdentity other) =>
            EntityId == other.EntityId
            && ExperienceId == other.ExperienceId
            && ZoneIndex == other.ZoneIndex
            && FeatureIndex == other.FeatureIndex;

        public override bool Equals(object obj) => obj is CosmosTargetIdentity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(EntityId, ExperienceId, ZoneIndex, FeatureIndex);

        public static bool operator ==(CosmosTargetIdentity left, CosmosTargetIdentity right) => left.Equals(right);
        public static bool operator !=(CosmosTargetIdentity left, CosmosTargetIdentity right) => !left.Equals(right);
    }

    [Serializable]
    public struct CosmosFsmConfig
    {
        public float RayTauSec;
        public float EntryConeDeg;
        public float ExitConeMultiplier;
        public float CandidateSwitchMargin;
        public float SettlingEndMs;
        public float LockDwellMs;
        public float CancelFadeMs;
        public float EnterLinearSpeed;
        public float ExitLinearSpeed;
        public float EnterScaleSpeed;
        public float ExitScaleSpeed;
        public float EnterRayAngularDegPerSec;
        public float ExitRayAngularDegPerSec;

        public static CosmosFsmConfig Default => new CosmosFsmConfig
        {
            RayTauSec = 0.080f,
            EntryConeDeg = 2.4f,
            ExitConeMultiplier = 1.35f,
            CandidateSwitchMargin = 1.25f,
            SettlingEndMs = 200.0f,
            LockDwellMs = 900.0f,
            CancelFadeMs = 120.0f,
            EnterLinearSpeed = 0.06f,
            ExitLinearSpeed = 0.09f,
            EnterScaleSpeed = 0.08f,
            ExitScaleSpeed = 0.12f,
            EnterRayAngularDegPerSec = 3.5f,
            ExitRayAngularDegPerSec = 5.5f
        };
    }

    public struct CosmosFsmState
    {
        public CosmosFsmStateKind Kind;
        public bool HasTarget;
        public CosmosTargetIdentity Target;
        public float ElapsedMs;
        public float ActiveMs;

        public static CosmosFsmState Moving => new CosmosFsmState { Kind = CosmosFsmStateKind.Moving };
        public static CosmosFsmState Settling(CosmosTargetIdentity target, float elapsedMs) => new CosmosFsmState { Kind = CosmosFsmStateKind.Settling, HasTarget = true, Target = target, ElapsedMs = elapsedMs };
        public static CosmosFsmState Locked(CosmosTargetIdentity target, float elapsedMs) => new CosmosFsmState { Kind = CosmosFsmStateKind.Locked, HasTarget = true, Target = target, ElapsedMs = elapsedMs };
        public static CosmosFsmState Narrating(CosmosTargetIdentity target, float activeMs) => new CosmosFsmState { Kind = CosmosFsmStateKind.Narrating, HasTarget = true, Target = target, ActiveMs = activeMs };
        public static CosmosFsmState Complete(CosmosTargetIdentity target) => new CosmosFsmState { Kind = CosmosFsmStateKind.Complete, HasTarget = true, Target = target };
    }

    public struct CosmosFsmOutput
    {
        public CosmosFsmState State;
        public float DwellProgress;
        public Vector3 SmoothedRayDirCam;
        public float RayAngularDegPerSec;
        public bool HasStartNarrationTarget;
        public CosmosTargetIdentity StartNarrationTarget;
        public bool ShouldCancelAudio;
        public float CancelFadeRemainingMs;
        public float CancelFadeAlpha;
    }

    /// <summary>
    /// 1:1 Rest-to-Learn dwell narration FSM:
    /// Moving -> Settling (0..200ms) -> Locked (200..900ms) -> Narrating -> Complete,
    /// with 80ms ray low-pass tremor tolerance, 1.35x exit cone hysteresis, 1.25x switch margin, and 120ms cancel fade.
    /// </summary>
    public sealed class CosmosNarrationFsm
    {
        public CosmosFsmConfig Config;
        public CosmosFsmState State { get; private set; } = CosmosFsmState.Moving;
        public Vector3 SmoothedRay => smoothedRay;

        bool rayInitialized;
        Vector3 smoothedRay = Vector3.forward;
        float rayAngularDegPerSec;
        float cancelFadeRemainingMs;

        public CosmosNarrationFsm() : this(CosmosFsmConfig.Default) { }

        public CosmosNarrationFsm(CosmosFsmConfig config)
        {
            Config = config;
        }

        public void Reset()
        {
            State = CosmosFsmState.Moving;
            rayInitialized = false;
            smoothedRay = Vector3.forward;
            rayAngularDegPerSec = 0f;
            cancelFadeRemainingMs = 0f;
        }

        public CosmosFsmOutput Step(
            Vector3 rawRayDirCam,
            float linearSpeed,
            float scaleSpeed,
            float dtSec,
            CosmosFocusCandidate[] candidates,
            int candidateCount,
            bool narrationFinished)
        {
            int count = candidates == null ? 0 : Mathf.Clamp(candidateCount, 0, candidates.Length);
            return StepInternal(rawRayDirCam, linearSpeed, scaleSpeed, dtSec, candidates, count, narrationFinished);
        }

        public CosmosFsmOutput Step(
            Vector3 rawRayDirCam,
            float linearSpeed,
            float scaleSpeed,
            float dtSec,
            IReadOnlyList<CosmosFocusCandidate> candidates,
            bool narrationFinished)
        {
            float dt = Mathf.Max(0f, dtSec);
            float dtMs = dt * 1000f;
            UpdateFilterAndFade(rawRayDirCam, dt, dtMs);

            bool shouldCancelAudio = false;
            if (!EvaluateStillness(linearSpeed, scaleSpeed))
            {
                TriggerCancelIfActive(ref shouldCancelAudio);
                State = CosmosFsmState.Moving;
                return BuildOutput(false, default, shouldCancelAudio);
            }

            int curIdx = -1;
            float curScore = 0f;
            int bestIdx = -1;
            float bestScore = float.NegativeInfinity;

            int count = candidates?.Count ?? 0;
            bool hasCurTarget = State.HasTarget;
            CosmosTargetIdentity curTarget = State.Target;

            for (int i = 0; i < count; i++)
            {
                CosmosFocusCandidate c = candidates[i];
                float theta = RayAngleRad(smoothedRay, c.DirCam);
                float entryDeg = Mathf.Max(Config.EntryConeDeg, c.AngularRadiusDeg);
                float entryRad = entryDeg * Mathf.Deg2Rad;
                float exitRad = entryDeg * Config.ExitConeMultiplier * Mathf.Deg2Rad;

                if (hasCurTarget && curIdx < 0 && CosmosTargetIdentity.FromCandidate(in c) == curTarget && theta <= exitRad)
                {
                    curIdx = i;
                    curScore = c.Score(theta, entryRad);
                }

                if (theta <= entryRad)
                {
                    float score = c.Score(theta, entryRad);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = i;
                    }
                }
            }

            return AdvanceFromMatches(
                candidates != null && curIdx >= 0 ? candidates[curIdx] : default,
                curIdx >= 0,
                curScore,
                candidates != null && bestIdx >= 0 ? candidates[bestIdx] : default,
                bestIdx >= 0,
                bestScore,
                dtMs,
                narrationFinished,
                ref shouldCancelAudio);
        }

        CosmosFsmOutput StepInternal(
            Vector3 rawRayDirCam,
            float linearSpeed,
            float scaleSpeed,
            float dtSec,
            CosmosFocusCandidate[] candidates,
            int count,
            bool narrationFinished)
        {
            float dt = Mathf.Max(0f, dtSec);
            float dtMs = dt * 1000f;
            UpdateFilterAndFade(rawRayDirCam, dt, dtMs);

            bool shouldCancelAudio = false;
            if (!EvaluateStillness(linearSpeed, scaleSpeed))
            {
                TriggerCancelIfActive(ref shouldCancelAudio);
                State = CosmosFsmState.Moving;
                return BuildOutput(false, default, shouldCancelAudio);
            }

            int curIdx = -1;
            float curScore = 0f;
            int bestIdx = -1;
            float bestScore = float.NegativeInfinity;

            bool hasCurTarget = State.HasTarget;
            CosmosTargetIdentity curTarget = State.Target;

            for (int i = 0; i < count; i++)
            {
                ref readonly CosmosFocusCandidate c = ref candidates[i];
                float theta = RayAngleRad(smoothedRay, c.DirCam);
                float entryDeg = Mathf.Max(Config.EntryConeDeg, c.AngularRadiusDeg);
                float entryRad = entryDeg * Mathf.Deg2Rad;
                float exitRad = entryDeg * Config.ExitConeMultiplier * Mathf.Deg2Rad;

                if (hasCurTarget && curIdx < 0 && CosmosTargetIdentity.FromCandidate(in c) == curTarget && theta <= exitRad)
                {
                    curIdx = i;
                    curScore = c.Score(theta, entryRad);
                }

                if (theta <= entryRad)
                {
                    float score = c.Score(theta, entryRad);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = i;
                    }
                }
            }

            return AdvanceFromMatches(
                curIdx >= 0 ? candidates[curIdx] : default,
                curIdx >= 0,
                curScore,
                bestIdx >= 0 ? candidates[bestIdx] : default,
                bestIdx >= 0,
                bestScore,
                dtMs,
                narrationFinished,
                ref shouldCancelAudio);
        }

        CosmosFsmOutput AdvanceFromMatches(
            in CosmosFocusCandidate curCandidate,
            bool hasCurMatch,
            float curScore,
            in CosmosFocusCandidate altCandidate,
            bool hasBestEntry,
            float bestScore,
            float dtMs,
            bool narrationFinished,
            ref bool shouldCancelAudio)
        {
            CosmosTargetIdentity chosenTarget = default;
            bool hasChosen = false;
            bool targetChanged = false;

            if (hasCurMatch && hasBestEntry)
            {
                var altId = CosmosTargetIdentity.FromCandidate(in altCandidate);
                var curId = CosmosTargetIdentity.FromCandidate(in curCandidate);
                if (altId != curId && bestScore >= curScore * Config.CandidateSwitchMargin)
                {
                    chosenTarget = altId;
                    targetChanged = true;
                }
                else
                {
                    chosenTarget = curId;
                    targetChanged = false;
                }
                hasChosen = true;
            }
            else if (hasCurMatch)
            {
                chosenTarget = CosmosTargetIdentity.FromCandidate(in curCandidate);
                hasChosen = true;
                targetChanged = false;
            }
            else if (hasBestEntry)
            {
                chosenTarget = CosmosTargetIdentity.FromCandidate(in altCandidate);
                hasChosen = true;
                targetChanged = State.HasTarget;
            }

            if (!hasChosen)
            {
                TriggerCancelIfActive(ref shouldCancelAudio);
                State = CosmosFsmState.Moving;
                return BuildOutput(false, default, shouldCancelAudio);
            }

            bool hasStart = false;
            CosmosTargetIdentity startTarget = default;

            if (targetChanged)
            {
                TriggerCancelIfActive(ref shouldCancelAudio);
                float nextElapsed = Mathf.Min(dtMs, Config.SettlingEndMs);
                State = CosmosFsmState.Settling(chosenTarget, nextElapsed);
            }
            else
            {
                switch (State.Kind)
                {
                    case CosmosFsmStateKind.Moving:
                        State = DwellStateForMs(chosenTarget, dtMs, ref hasStart, ref startTarget);
                        break;
                    case CosmosFsmStateKind.Settling:
                    case CosmosFsmStateKind.Locked:
                        State = DwellStateForMs(State.Target, State.ElapsedMs + dtMs, ref hasStart, ref startTarget);
                        break;
                    case CosmosFsmStateKind.Narrating:
                        State = narrationFinished
                            ? CosmosFsmState.Complete(State.Target)
                            : CosmosFsmState.Narrating(State.Target, State.ActiveMs + dtMs);
                        break;
                    case CosmosFsmStateKind.Complete:
                        break;
                }
            }

            return BuildOutput(hasStart, startTarget, shouldCancelAudio);
        }

        void UpdateFilterAndFade(Vector3 rawRayDirCam, float dtSec, float dtMs)
        {
            if (cancelFadeRemainingMs > 0f)
                cancelFadeRemainingMs = Mathf.Max(0f, cancelFadeRemainingMs - dtMs);

            Vector3 rawDir = rawRayDirCam.sqrMagnitude > 1e-8f ? rawRayDirCam.normalized : Vector3.forward;
            if (!rayInitialized)
            {
                smoothedRay = rawDir;
                rayInitialized = true;
                rayAngularDegPerSec = 0f;
                return;
            }

            Vector3 prevRay = smoothedRay;
            float alpha = dtSec > 0f && Config.RayTauSec > 0f ? 1f - Mathf.Exp(-dtSec / Config.RayTauSec) : 1f;
            Vector3 blended = prevRay + (rawDir - prevRay) * alpha;
            smoothedRay = blended.sqrMagnitude >= 0.25f ? blended.normalized : rawDir;

            float cosStep = Mathf.Clamp(Vector3.Dot(prevRay, smoothedRay), -1f, 1f);
            float stepDeg = Mathf.Acos(cosStep) * Mathf.Rad2Deg;
            rayAngularDegPerSec = dtSec > 1e-6f ? stepDeg / dtSec : 0f;
        }

        bool EvaluateStillness(float linearSpeed, float scaleSpeed)
        {
            float absScale = Mathf.Abs(scaleSpeed);
            if (State.Kind != CosmosFsmStateKind.Moving)
            {
                return linearSpeed < Config.ExitLinearSpeed
                    && absScale < Config.ExitScaleSpeed
                    && rayAngularDegPerSec < Config.ExitRayAngularDegPerSec;
            }
            return linearSpeed < Config.EnterLinearSpeed
                && absScale < Config.EnterScaleSpeed
                && rayAngularDegPerSec < Config.EnterRayAngularDegPerSec;
        }

        void TriggerCancelIfActive(ref bool shouldCancelAudio)
        {
            if (State.Kind == CosmosFsmStateKind.Locked || State.Kind == CosmosFsmStateKind.Narrating)
            {
                shouldCancelAudio = State.Kind == CosmosFsmStateKind.Narrating;
                cancelFadeRemainingMs = Config.CancelFadeMs;
            }
        }

        CosmosFsmState DwellStateForMs(CosmosTargetIdentity target, float elapsedMs, ref bool hasStart, ref CosmosTargetIdentity startTarget)
        {
            if (elapsedMs < Config.SettlingEndMs) return CosmosFsmState.Settling(target, elapsedMs);
            if (elapsedMs < Config.LockDwellMs) return CosmosFsmState.Locked(target, elapsedMs);
            hasStart = true;
            startTarget = target;
            return CosmosFsmState.Narrating(target, elapsedMs - Config.LockDwellMs);
        }

        CosmosFsmOutput BuildOutput(bool hasStart, CosmosTargetIdentity startTarget, bool shouldCancelAudio)
        {
            float lockMs = Mathf.Max(1f, Config.LockDwellMs);
            float dwell = State.Kind switch
            {
                CosmosFsmStateKind.Moving => 0f,
                CosmosFsmStateKind.Settling => Mathf.Clamp01(State.ElapsedMs / lockMs),
                CosmosFsmStateKind.Locked => Mathf.Clamp01(State.ElapsedMs / lockMs),
                _ => 1f
            };
            float fadeAlpha = Config.CancelFadeMs > 0f ? Mathf.Clamp01(cancelFadeRemainingMs / Config.CancelFadeMs) : 0f;
            return new CosmosFsmOutput
            {
                State = State,
                DwellProgress = dwell,
                SmoothedRayDirCam = smoothedRay,
                RayAngularDegPerSec = rayAngularDegPerSec,
                HasStartNarrationTarget = hasStart,
                StartNarrationTarget = startTarget,
                ShouldCancelAudio = shouldCancelAudio,
                CancelFadeRemainingMs = cancelFadeRemainingMs,
                CancelFadeAlpha = fadeAlpha
            };
        }

        static float RayAngleRad(Vector3 a, Vector3 b)
        {
            if (b.sqrMagnitude <= 1e-8f) return Mathf.PI;
            float dot = Mathf.Clamp(Vector3.Dot(a, b.normalized), -1f, 1f);
            return Mathf.Acos(dot);
        }
    }

    public enum CosmosMemoryRole
    {
        RegionHeading = 0,
        SharedContext = 1
    }

    public struct CosmosMemorySegmentInput
    {
        public string Id;
        public CosmosMemoryRole? Role;
        public string Display;
        public string Narration;
        public string Translation;
    }

    public readonly struct CosmosRememberedEntry : IEquatable<CosmosRememberedEntry>
    {
        public readonly CosmosMemoryRole Role;
        public readonly ulong TextHash;

        public CosmosRememberedEntry(CosmosMemoryRole role, ulong textHash)
        {
            Role = role;
            TextHash = textHash;
        }

        public bool Equals(CosmosRememberedEntry other) => Role == other.Role && TextHash == other.TextHash;
        public override bool Equals(object obj) => obj is CosmosRememberedEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Role, TextHash);
        public static bool operator ==(CosmosRememberedEntry left, CosmosRememberedEntry right) => left.Equals(right);
        public static bool operator !=(CosmosRememberedEntry left, CosmosRememberedEntry right) => !left.Equals(right);
    }

    public sealed class CosmosMemoryPlanState
    {
        public ulong Generation;
        public string RegionId;
        public bool[] Skipped;
        public (bool Valid, string Id, CosmosRememberedEntry Entry)[] EligibleEntries;
    }

    /// <summary>
    /// Scoped bilingual shared-context skip memory matching HyperCosmosVR memory.rs / Spanish-Quick-Apps.
    /// ONLY RegionHeading and SharedContext are deduplicated within a contiguous active region.
    /// </summary>
    public sealed class CosmosNarrationMemory
    {
        string currentRegion;
        ulong generation;
        readonly Dictionary<string, CosmosRememberedEntry> heard = new Dictionary<string, CosmosRememberedEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, int> scratchIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        public string CurrentRegion => currentRegion;
        public ulong Generation => generation;
        public int RememberedCount => heard.Count;

        public bool TryGetRemembered(string segmentId, out CosmosRememberedEntry entry)
        {
            if (string.IsNullOrWhiteSpace(segmentId)) { entry = default; return false; }
            return heard.TryGetValue(segmentId.Trim(), out entry);
        }

        public void Reset()
        {
            currentRegion = null;
            heard.Clear();
            unchecked { generation++; }
        }

        public void InvalidatePending()
        {
            unchecked { generation++; }
        }

        public void SetRegion(string regionId)
        {
            string next = string.IsNullOrWhiteSpace(regionId) ? null : regionId.Trim();
            if (!string.Equals(currentRegion, next, StringComparison.Ordinal))
            {
                currentRegion = next;
                heard.Clear();
                unchecked { generation++; }
            }
        }

        public static string RoleString(CosmosMemoryRole role) =>
            role == CosmosMemoryRole.RegionHeading ? "region-heading" : "shared-context";

        public static ulong ComputeSegmentTextHash(
            CosmosMemoryRole role,
            string display,
            string narration,
            CosmosLocale locale,
            string translation) =>
            ComputeSegmentTextHash(role, display, narration, locale.Bcp47Code(), translation);

        public static ulong ComputeSegmentTextHash(
            CosmosMemoryRole role,
            string display,
            string narration,
            string localeCode,
            string translation)
        {
            const ulong FnvOffset = 0xcbf29ce484222325UL;
            const ulong FnvPrime = 0x0100000001b3UL;

            ulong h = FnvOffset;
            FeedTrimmedUtf8(ref h, RoleString(role), FnvPrime);
            FeedTrimmedUtf8(ref h, display, FnvPrime);
            FeedTrimmedUtf8(ref h, narration, FnvPrime);
            FeedTrimmedUtf8(ref h, localeCode, FnvPrime);
            FeedTrimmedUtf8(ref h, translation, FnvPrime);
            return h;
        }

        public CosmosMemoryPlanState BeginPlan(
            string regionId,
            IReadOnlyList<CosmosMemorySegmentInput> segments,
            string localeCode)
        {
            int count = segments?.Count ?? 0;
            string normRegion = string.IsNullOrWhiteSpace(regionId) ? string.Empty : regionId.Trim();
            if (normRegion.Length == 0)
            {
                Reset();
                return new CosmosMemoryPlanState
                {
                    Generation = generation,
                    RegionId = string.Empty,
                    Skipped = new bool[count],
                    EligibleEntries = new (bool, string, CosmosRememberedEntry)[count]
                };
            }

            SetRegion(normRegion);
            scratchIdCounts.Clear();

            var rawValid = new bool[count];
            var rawIds = new string[count];
            var rawEntries = new CosmosRememberedEntry[count];

            for (int i = 0; i < count; i++)
            {
                CosmosMemorySegmentInput seg = segments[i];
                string id = string.IsNullOrWhiteSpace(seg.Id) ? string.Empty : seg.Id.Trim();
                string disp = string.IsNullOrWhiteSpace(seg.Display) ? string.Empty : seg.Display.Trim();
                string narr = string.IsNullOrWhiteSpace(seg.Narration) ? string.Empty : seg.Narration.Trim();
                string tr = string.IsNullOrWhiteSpace(seg.Translation) ? string.Empty : seg.Translation.Trim();

                if (seg.Role.HasValue && id.Length > 0 && disp.Length > 0 && narr.Length > 0 && tr.Length > 0)
                {
                    scratchIdCounts[id] = scratchIdCounts.TryGetValue(id, out int prevCount) ? prevCount + 1 : 1;
                    rawValid[i] = true;
                    rawIds[i] = id;
                    rawEntries[i] = new CosmosRememberedEntry(
                        seg.Role.Value,
                        ComputeSegmentTextHash(seg.Role.Value, disp, narr, localeCode, tr));
                }
            }

            var skipped = new bool[count];
            var eligible = new (bool Valid, string Id, CosmosRememberedEntry Entry)[count];

            for (int i = 0; i < count; i++)
            {
                if (rawValid[i] && scratchIdCounts.TryGetValue(rawIds[i], out int c) && c == 1)
                {
                    string id = rawIds[i];
                    CosmosRememberedEntry entry = rawEntries[i];
                    if (heard.TryGetValue(id, out CosmosRememberedEntry prev))
                    {
                        if (prev == entry)
                        {
                            skipped[i] = true;
                            eligible[i] = (true, id, entry);
                            continue;
                        }
                        heard.Remove(id);
                    }
                    skipped[i] = false;
                    eligible[i] = (true, id, entry);
                }
            }

            return new CosmosMemoryPlanState
            {
                Generation = generation,
                RegionId = normRegion,
                Skipped = skipped,
                EligibleEntries = eligible
            };
        }

        public bool MarkPairCompleted(
            CosmosMemoryPlanState state,
            int segmentIndex,
            bool spanishDone,
            bool translationDone)
        {
            if (state == null || !(spanishDone && translationDone)) return false;
            if (state.Generation != generation) return false;
            if (!string.Equals(currentRegion, state.RegionId, StringComparison.Ordinal)) return false;
            if (state.EligibleEntries == null || (uint)segmentIndex >= (uint)state.EligibleEntries.Length) return false;

            var slot = state.EligibleEntries[segmentIndex];
            if (!slot.Valid) return false;
            heard[slot.Id] = slot.Entry;
            return true;
        }

        static void FeedTrimmedUtf8(ref ulong h, string text, ulong prime)
        {
            string trimmed = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();
            if (trimmed.Length > 0)
            {
                int maxBytes = Encoding.UTF8.GetMaxByteCount(trimmed.Length);
                if (maxBytes <= 512)
                {
                    Span<byte> buf = stackalloc byte[maxBytes];
                    int written = Encoding.UTF8.GetBytes(trimmed.AsSpan(), buf);
                    for (int i = 0; i < written; i++)
                    {
                        h ^= buf[i];
                        h *= prime;
                    }
                }
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(trimmed);
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        h ^= bytes[i];
                        h *= prime;
                    }
                }
            }
            h ^= 0xff;
            h *= prime;
        }
    }
}
