// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using UnityEngine;

namespace Maestro.Quest.Cosmos
{
    public enum CosmosWristBeadKind : byte
    {
        HomeAnchor = 0,
        LocaleToggle = 1,
        WalkFlyToggle = 2,
        SpeedPearl = 3
    }

    public enum CosmosWristSummonMode : byte
    {
        Hidden = 0,
        VrForearmPalmUp = 1,
        DesktopHeld = 2,
        DesktopPinnedTab = 3
    }

    public enum CosmosWristActionKind : byte
    {
        None = 0,
        SetSpeedMultiplier = 1,
        SetLocale = 2,
        ToggleWalkFly = 3,
        TriggerEarthHome = 4
    }

    public readonly struct CosmosWristAction
    {
        public readonly CosmosWristActionKind Kind;
        public readonly float SpeedMultiplier;
        public readonly int SpeedBeadIndex;
        public readonly CosmosLocale Locale;
        public readonly Vector3 WorldPos;

        public CosmosWristAction(CosmosWristActionKind kind, float speedMultiplier, int speedBeadIndex, CosmosLocale locale, Vector3 worldPos)
        {
            Kind = kind;
            SpeedMultiplier = speedMultiplier;
            SpeedBeadIndex = speedBeadIndex;
            Locale = locale;
            WorldPos = worldPos;
        }
    }

    public struct CosmosWristBeadState
    {
        public int Id;
        public CosmosWristBeadKind Kind;
        public string Label;
        public float SpeedValue;
        public CosmosLocale Locale;
        public Vector3 LocalPos;
        public Vector3 WorldPos;
        public float Radius;
        public float HoverT;
        public bool Selected;
        public Color HdrColor;
    }

    /// <summary>
    /// Diegetic 3D proximal left-forearm Orbital Resonance Ring (r = 7.5 cm, 22.5 cm proximal past Wrist
    /// keeping >= 12 cm clearance from PalmRecoveryButton). Carries 9 speed pearls (1.0x..3.0x @ 0.25x),
    /// EN/FI locale pearls, Walk/Fly toggle pearl, and central Earth Home anchor pearl.
    /// </summary>
    public sealed class CosmosWristRing : MonoBehaviour
    {
        public const float RingOuterRadiusMeters = 0.075f;        // 7.5 cm
        public const float InnerLocaleRadiusMeters = 0.044f;      // 4.4 cm
        public const float BottomToggleRadiusMeters = 0.056f;     // 5.6 cm
        public const float ForearmProximalOffsetMeters = 0.225f;  // 22.5 cm proximal past Wrist (>=12cm clear of PalmRecoveryButton)
        public const float ForearmSurfaceLiftMeters = 0.055f;
        public const float FingerPokeCooldownSec = 0.28f;

        public static readonly float[] PlaybackRates =
        {
            1.00f, 1.25f, 1.50f, 1.75f, 2.00f, 2.25f, 2.50f, 2.75f, 3.00f
        };

        public static readonly string[] PlaybackRateLabels =
        {
            "1.0x", "1.25x", "1.5x", "1.75x", "2.0x", "2.25x", "2.5x", "2.75x", "3.0x"
        };

        public const int TotalBeadCount = 13; // 0:Home, 1:EN, 2:FI, 3:Walk/Fly, 4..12:9 speed pearls

        readonly CosmosWristBeadState[] beads = new CosmosWristBeadState[TotalBeadCount];
        readonly MeshRenderer[] beadRenderers = new MeshRenderer[TotalBeadCount];
        readonly Transform[] beadTransforms = new Transform[TotalBeadCount];
        MaterialPropertyBlock propBlock;
        Material beadMaterial;
        MeshRenderer ringBandRenderer;
        Transform ringRoot;
        Camera viewerCamera;

        CosmosWristSummonMode summonMode = CosmosWristSummonMode.Hidden;
        bool desktopTabPinned;
        float openAlpha;
        int selectedRateIndex; // 0 => 1.0x
        CosmosLocale selectedLocale = CosmosLocale.EnUs;
        bool freeFlightActive;
        int hoveredBeadId = -1;
        float fingerPokeCooldown;
        bool ringCapturesSelect;
        Vector3 anchorPos;
        Quaternion anchorRot = Quaternion.identity;

        public CosmosWristSummonMode SummonMode => summonMode;
        public float OpenAlpha => openAlpha;
        public bool IsOpenOrOpening => openAlpha > 0.05f;
        public bool CapturesPointer => IsOpenOrOpening && hoveredBeadId >= 0;
        public float ActiveSpeedMultiplier => PlaybackRates[selectedRateIndex];
        public int ActiveSpeedBeadIndex => selectedRateIndex;
        public CosmosLocale SelectedLocale => selectedLocale;
        public Vector3 AnchorWorldPosition => anchorPos;
        public Quaternion AnchorWorldRotation => anchorRot;
        public int BeadCount => TotalBeadCount;

        public CosmosWristBeadState GetBead(int index) => beads[Mathf.Clamp(index, 0, TotalBeadCount - 1)];

        public float ComputeMinimumClearanceFromPoint(Vector3 palmColliderClosestPoint)
        {
            float minDist = Vector3.Distance(anchorPos, palmColliderClosestPoint);
            for (int i = 0; i < TotalBeadCount; i++)
            {
                float d = Vector3.Distance(beads[i].WorldPos, palmColliderClosestPoint) - beads[i].Radius;
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        internal void Initialize(Camera camera, CosmosLocale initialLocale = CosmosLocale.EnUs)
        {
            viewerCamera = camera;
            selectedLocale = initialLocale == CosmosLocale.FiFi ? CosmosLocale.FiFi : CosmosLocale.EnUs;
            propBlock ??= new MaterialPropertyBlock();
            beadMaterial = CosmosShaderIds.ResolveMaterial(
                CosmosShaderIds.ResWristBeadMat,
                CosmosShaderIds.ShaderWristBead,
                new Color(0.25f, 0.72f, 1.0f, 0.85f));

            if (!ringRoot)
            {
                var rootGo = new GameObject("CosmosWristRingAnchor");
                rootGo.transform.SetParent(transform, false);
                ringRoot = rootGo.transform;

                var bandGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bandGo.name = "WristBand";
                bandGo.transform.SetParent(ringRoot, false);
                bandGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bandGo.transform.localScale = new Vector3(RingOuterRadiusMeters * 2.08f, 0.0025f, RingOuterRadiusMeters * 2.08f);
                ArtResources.Release(bandGo.GetComponent<Collider>());
                ringBandRenderer = bandGo.GetComponent<MeshRenderer>();
                ringBandRenderer.sharedMaterial = beadMaterial;

                InitBeadSlots();
                ApplyLocalVisuals(0f);
                SetVisualVisible(false);
            }
        }

        internal CosmosWristAction Step(float dtSec, in CosmosInputSnapshot input, CosmosLocomotionMode locomotionMode)
        {
            if (!ringRoot) Initialize(viewerCamera);
            float dt = Mathf.Clamp(dtSec, 0f, 0.1f);
            if (fingerPokeCooldown > 0f) fingerPokeCooldown = Mathf.Max(0f, fingerPokeCooldown - dt);
            freeFlightActive = locomotionMode != CosmosLocomotionMode.GroundedWalk;

            CosmosWristAction action = default;

            // Direct keyboard shortcuts ([ / ] speed, L locale, Tab pin).
            if (input.DesktopTabJustPressed)
                desktopTabPinned = !desktopTabPinned;

            if (input.DesktopSpeedDownJustPressed)
            {
                selectedRateIndex = Mathf.Max(0, selectedRateIndex - 1);
                action = new CosmosWristAction(
                    CosmosWristActionKind.SetSpeedMultiplier,
                    PlaybackRates[selectedRateIndex],
                    selectedRateIndex,
                    selectedLocale,
                    beads[4 + selectedRateIndex].WorldPos);
            }
            else if (input.DesktopSpeedUpJustPressed)
            {
                selectedRateIndex = Mathf.Min(PlaybackRates.Length - 1, selectedRateIndex + 1);
                action = new CosmosWristAction(
                    CosmosWristActionKind.SetSpeedMultiplier,
                    PlaybackRates[selectedRateIndex],
                    selectedRateIndex,
                    selectedLocale,
                    beads[4 + selectedRateIndex].WorldPos);
            }
            else if (input.DesktopLocaleToggleJustPressed)
            {
                selectedLocale = selectedLocale == CosmosLocale.EnUs ? CosmosLocale.FiFi : CosmosLocale.EnUs;
                action = new CosmosWristAction(
                    CosmosWristActionKind.SetLocale,
                    PlaybackRates[selectedRateIndex],
                    selectedRateIndex,
                    selectedLocale,
                    beads[selectedLocale == CosmosLocale.EnUs ? 1 : 2].WorldPos);
            }

            // Compute proximal left-forearm world placement (with safe camera fallback for Desktop).
            Vector3 headPos = viewerCamera ? viewerCamera.transform.position : Vector3.up * 1.55f;
            Vector3 headFwd = viewerCamera ? viewerCamera.transform.forward : Vector3.forward;
            Vector3 headUp = viewerCamera ? viewerCamera.transform.up : Vector3.up;
            Quaternion headRot = viewerCamera ? viewerCamera.transform.rotation : Quaternion.identity;

            bool vrHasWrist = input.HasLeftWrist;
            if (vrHasWrist)
            {
                Vector3 proxDir = input.LeftForearmProximalDir.sqrMagnitude > 1e-6f
                    ? input.LeftForearmProximalDir.normalized
                    : -(input.LeftWristRot * Vector3.forward);
                Vector3 palmNormal = input.LeftPalmNormal.sqrMagnitude > 1e-6f
                    ? input.LeftPalmNormal.normalized
                    : Vector3.up;

                anchorPos = input.LeftWristPos
                          + proxDir * ForearmProximalOffsetMeters
                          + palmNormal * ForearmSurfaceLiftMeters;

                Vector3 lookToHead = (headPos - anchorPos);
                if (lookToHead.sqrMagnitude < 1e-5f) lookToHead = -headFwd;
                lookToHead.Normalize();
                Vector3 ringUp = Vector3.ProjectOnPlane(headUp, lookToHead);
                if (ringUp.sqrMagnitude < 1e-4f) ringUp = Vector3.Cross(lookToHead, proxDir);
                anchorRot = Quaternion.LookRotation(-lookToHead, ringUp.sqrMagnitude > 1e-4f ? ringUp.normalized : Vector3.up);
            }
            else
            {
                anchorPos = headPos + headFwd * 0.56f - headUp * 0.10f;
                anchorRot = headRot;
            }

            bool vrPalmSummon = vrHasWrist && IsVrPalmOrWatchGaze(input.LeftPalmNormal, headPos, input.LeftWristPos, anchorPos, headFwd, headUp);
            if (vrPalmSummon)
                summonMode = CosmosWristSummonMode.VrForearmPalmUp;
            else if (input.DesktopRightMouseHeld || input.DesktopRKeyHeld)
                summonMode = CosmosWristSummonMode.DesktopHeld;
            else if (desktopTabPinned)
                summonMode = CosmosWristSummonMode.DesktopPinnedTab;
            else
                summonMode = CosmosWristSummonMode.Hidden;

            float targetAlpha = summonMode == CosmosWristSummonMode.Hidden ? 0f : 1f;
            float alphaStep = dt > 0f ? 1f - Mathf.Exp(-dt / 0.065f) : 1f;
            openAlpha = Mathf.Lerp(openAlpha, targetAlpha, alphaStep);
            if (targetAlpha == 0f && openAlpha < 0.01f) openAlpha = 0f;
            else if (targetAlpha == 1f && openAlpha > 0.99f) openAlpha = 1f;

            ApplyLocalVisuals(dt);

            bool visible = openAlpha > 0.02f;
            SetVisualVisible(visible);
            if (!visible)
            {
                hoveredBeadId = -1;
                ringCapturesSelect = false;
                return action;
            }

            if (action.Kind == CosmosWristActionKind.None)
            {
                action = EvaluateHoverAndSelection(in input);
            }
            return action;
        }

        public static bool IsVrPalmOrWatchGaze(
            Vector3 palmNormalWorld,
            Vector3 headPos,
            Vector3 wristPos,
            Vector3 ringAnchorPos,
            Vector3 headForward,
            Vector3 headUp)
        {
            Vector3 toHead = headPos - wristPos;
            float wristDist = toHead.magnitude;
            if (wristDist > 0.85f || wristDist < 0.12f) return false;
            Vector3 toHeadDir = toHead / wristDist;

            if (palmNormalWorld.sqrMagnitude > 0.25f)
            {
                Vector3 pn = palmNormalWorld.normalized;
                Vector3 hu = headUp.sqrMagnitude > 0.25f ? headUp.normalized : Vector3.up;
                if (Vector3.Dot(pn, toHeadDir) > 0.52f && Vector3.Dot(pn, hu) > 0.18f)
                    return true;
            }

            Vector3 toRing = ringAnchorPos - headPos;
            float ringDist = toRing.magnitude;
            if (ringDist > 0.12f && ringDist < 0.72f)
            {
                float angle = Vector3.Angle(headForward, toRing / ringDist);
                if (angle < 22f) return true;
            }
            return false;
        }

        void InitBeadSlots()
        {
            // 0: Central Earth Home anchor pearl
            SetupBeadSlot(0, "HomePearl", CosmosWristBeadKind.HomeAnchor, "HOME", 0f, selectedLocale, new Color(0.35f, 1.15f, 2.0f, 1f));
            // 1..2: EN / FI locale pearls
            SetupBeadSlot(1, "LocaleEnPearl", CosmosWristBeadKind.LocaleToggle, "EN", 0f, CosmosLocale.EnUs, new Color(0.45f, 1.45f, 1.85f, 1f));
            SetupBeadSlot(2, "LocaleFiPearl", CosmosWristBeadKind.LocaleToggle, "FI", 0f, CosmosLocale.FiFi, new Color(0.85f, 1.10f, 2.05f, 1f));
            // 3: Walk / Fly mode toggle pearl
            SetupBeadSlot(3, "WalkFlyPearl", CosmosWristBeadKind.WalkFlyToggle, "FLY", 0f, selectedLocale, new Color(0.30f, 1.85f, 1.15f, 1f));
            // 4..12: 9 Speed pearls (1.0x .. 3.0x @ 0.25x)
            for (int i = 0; i < PlaybackRates.Length; i++)
            {
                int slot = 4 + i;
                float t = i / (float)(PlaybackRates.Length - 1);
                Color col = Color.Lerp(new Color(0.28f, 0.90f, 1.85f, 1f), new Color(1.95f, 1.35f, 0.35f, 1f), t);
                SetupBeadSlot(slot, $"SpeedPearl_{PlaybackRateLabels[i]}", CosmosWristBeadKind.SpeedPearl, PlaybackRateLabels[i], PlaybackRates[i], selectedLocale, col);
            }
        }

        void SetupBeadSlot(int idx, string goName, CosmosWristBeadKind kind, string label, float speedVal, CosmosLocale locale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = goName;
            go.transform.SetParent(ringRoot, false);
            ArtResources.Release(go.GetComponent<Collider>());
            beadTransforms[idx] = go.transform;
            beadRenderers[idx] = go.GetComponent<MeshRenderer>();
            beadRenderers[idx].sharedMaterial = beadMaterial;

            beads[idx] = new CosmosWristBeadState
            {
                Id = idx,
                Kind = kind,
                Label = label,
                SpeedValue = speedVal,
                Locale = locale,
                HdrColor = color
            };
        }

        void ApplyLocalVisuals(float dt)
        {
            if (ringRoot)
                ringRoot.SetPositionAndRotation(anchorPos, anchorRot);

            float scale = Mathf.Clamp(openAlpha, 0.05f, 1f);
            float outerR = RingOuterRadiusMeters * scale;
            float innerR = InnerLocaleRadiusMeters * scale;
            float bottomR = BottomToggleRadiusMeters * scale;

            if (ringBandRenderer)
            {
                ringBandRenderer.transform.localScale = new Vector3(outerR * 2.06f, 0.002f * scale, outerR * 2.06f);
                ApplyBeadShaderProps(ringBandRenderer, new Color(0.10f, 0.14f, 0.22f, 0.55f * openAlpha), 0.15f * openAlpha);
            }

            // 0: HOME
            beads[0].LocalPos = Vector3.zero;
            beads[0].Radius = 0.017f * scale;
            beads[0].Selected = false;

            // 1..2: EN / FI on inner flanking arc (-0.58 rad, +0.58 rad, lower hemisphere)
            beads[1].LocalPos = new Vector3(Mathf.Sin(-0.58f) * innerR, -Mathf.Cos(-0.58f) * innerR, 0f);
            beads[1].Radius = 0.014f * scale;
            beads[1].Selected = selectedLocale == CosmosLocale.EnUs;

            beads[2].LocalPos = new Vector3(Mathf.Sin(0.58f) * innerR, -Mathf.Cos(0.58f) * innerR, 0f);
            beads[2].Radius = 0.014f * scale;
            beads[2].Selected = selectedLocale == CosmosLocale.FiFi;

            // 3: Walk/Fly toggle pearl at straight bottom (0, -bottomR, 0)
            beads[3].LocalPos = new Vector3(0f, -bottomR, 0f);
            beads[3].Radius = 0.015f * scale;
            beads[3].Selected = freeFlightActive;
            beads[3].Label = freeFlightActive ? "WALK" : "FLY";
            beads[3].HdrColor = freeFlightActive ? new Color(1.85f, 1.15f, 0.25f, 1f) : new Color(0.30f, 1.85f, 1.25f, 1f);

            // 4..12: 9 Speed pearls along upper 200-deg arc (-100 deg to +100 deg) at radius = 7.5 cm
            float arcSpanRad = 200f * Mathf.Deg2Rad;
            for (int i = 0; i < PlaybackRates.Length; i++)
            {
                int slot = 4 + i;
                float t = i / (float)(PlaybackRates.Length - 1);
                float theta = -0.5f * arcSpanRad + t * arcSpanRad;
                beads[slot].LocalPos = new Vector3(Mathf.Sin(theta) * outerR, Mathf.Cos(theta) * outerR, 0f);
                beads[slot].Radius = (i == selectedRateIndex ? 0.0135f : 0.0105f) * scale;
                beads[slot].Selected = i == selectedRateIndex;
            }

            float hoverRate = dt > 0f ? 1f - Mathf.Exp(-dt / 0.07f) : 1f;
            for (int i = 0; i < TotalBeadCount; i++)
            {
                float targetHover = hoveredBeadId == i ? 1f : 0f;
                beads[i].HoverT = Mathf.Lerp(beads[i].HoverT, targetHover, hoverRate);
                beads[i].WorldPos = anchorPos + anchorRot * beads[i].LocalPos;

                Transform bt = beadTransforms[i];
                if (bt)
                {
                    bt.localPosition = beads[i].LocalPos;
                    float visualScale = beads[i].Radius * 2f * (1f + 0.24f * beads[i].HoverT);
                    bt.localScale = new Vector3(visualScale, visualScale, visualScale);
                }

                MeshRenderer mr = beadRenderers[i];
                if (mr)
                {
                    float glow = (beads[i].Selected ? 1.0f : 0.35f) + 0.65f * beads[i].HoverT;
                    Color c = beads[i].HdrColor;
                    c.a = openAlpha;
                    ApplyBeadShaderProps(mr, c, glow * openAlpha);
                }
            }
        }

        CosmosWristAction EvaluateHoverAndSelection(in CosmosInputSnapshot input)
        {
            int nextHover = -1;
            float bestMetric = float.MaxValue;

            // Direct VR index-finger poke check.
            if (input.HasRightFingerTip)
            {
                for (int i = 0; i < TotalBeadCount; i++)
                {
                    float d = Vector3.Distance(input.RightFingerTipPos, beads[i].WorldPos);
                    if (d <= beads[i].Radius + 0.016f && d < bestMetric)
                    {
                        bestMetric = d;
                        nextHover = i;
                    }
                }
                if (nextHover >= 0 && fingerPokeCooldown <= 0f)
                {
                    fingerPokeCooldown = FingerPokeCooldownSec;
                    hoveredBeadId = nextHover;
                    return TriggerBead(nextHover);
                }
            }

            // Pointer ray-sphere intersection (XR controller ray or Desktop cursor).
            if (input.HasPointerRay && input.PointerDir.sqrMagnitude > 1e-6f)
            {
                Vector3 ro = input.PointerOrigin;
                Vector3 rd = input.PointerDir.normalized;
                bestMetric = float.MaxValue;
                for (int i = 0; i < TotalBeadCount; i++)
                {
                    float hitRadius = beads[i].Radius * 1.45f;
                    Vector3 oc = beads[i].WorldPos - ro;
                    float proj = Vector3.Dot(oc, rd);
                    if (proj < 0.02f || proj > 2.5f) continue;
                    float perpSq = oc.sqrMagnitude - proj * proj;
                    if (perpSq <= hitRadius * hitRadius && perpSq < bestMetric)
                    {
                        bestMetric = perpSq;
                        nextHover = i;
                    }
                }
            }

            hoveredBeadId = nextHover;
            ringCapturesSelect = hoveredBeadId >= 0;

            if (ringCapturesSelect && input.PointerSelectJustPressed)
            {
                return TriggerBead(hoveredBeadId);
            }

            return default;
        }

        CosmosWristAction TriggerBead(int beadIndex)
        {
            ref readonly var bead = ref beads[beadIndex];
            switch (bead.Kind)
            {
                case CosmosWristBeadKind.HomeAnchor:
                    return new CosmosWristAction(CosmosWristActionKind.TriggerEarthHome, ActiveSpeedMultiplier, selectedRateIndex, selectedLocale, bead.WorldPos);

                case CosmosWristBeadKind.LocaleToggle:
                    selectedLocale = bead.Locale;
                    return new CosmosWristAction(CosmosWristActionKind.SetLocale, ActiveSpeedMultiplier, selectedRateIndex, selectedLocale, bead.WorldPos);

                case CosmosWristBeadKind.WalkFlyToggle:
                    return new CosmosWristAction(CosmosWristActionKind.ToggleWalkFly, ActiveSpeedMultiplier, selectedRateIndex, selectedLocale, bead.WorldPos);

                case CosmosWristBeadKind.SpeedPearl:
                    selectedRateIndex = Mathf.Clamp(beadIndex - 4, 0, PlaybackRates.Length - 1);
                    return new CosmosWristAction(CosmosWristActionKind.SetSpeedMultiplier, PlaybackRates[selectedRateIndex], selectedRateIndex, selectedLocale, bead.WorldPos);

                default:
                    return default;
            }
        }

        void SetVisualVisible(bool visible)
        {
            if (ringBandRenderer && ringBandRenderer.enabled != visible)
                ringBandRenderer.enabled = visible;
            for (int i = 0; i < beadRenderers.Length; i++)
            {
                if (beadRenderers[i] && beadRenderers[i].enabled != visible)
                    beadRenderers[i].enabled = visible;
            }
        }

        void ApplyBeadShaderProps(Renderer rend, Color color, float activeGlow)
        {
            propBlock.Clear();
            propBlock.SetColor(CosmosShaderIds.Color, color);
            propBlock.SetFloat(CosmosShaderIds.ActiveGlow, activeGlow);
            propBlock.SetColor(CosmosShaderIds.RimColor, new Color(0.55f, 0.85f, 1.0f, color.a));
            propBlock.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
            rend.SetPropertyBlock(propBlock);
        }

        public void SetFreeFlightActive(bool active) => freeFlightActive = active;

        public void SetLocale(CosmosLocale locale)
        {
            if (locale == CosmosLocale.EnUs || locale == CosmosLocale.FiFi)
                selectedLocale = locale;
        }

        void OnDestroy()
        {
            // RuntimeFallback material created only if Resources/Cosmos/Cosmos_WristBead wasn't baked yet;
            // ArtResources safely ignores project asset materials.
            if (beadMaterial && beadMaterial.name.EndsWith("(RuntimeFallback)", StringComparison.Ordinal))
                ArtResources.Release(beadMaterial);
        }
    }
}
