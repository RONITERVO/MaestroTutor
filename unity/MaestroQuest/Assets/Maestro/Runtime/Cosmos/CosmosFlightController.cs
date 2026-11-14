// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Cosmos
{
    public struct CosmosPinchStepResult
    {
        public double DeltaU;
        public Vector3 CamLocalDisplacementOld;
        public float DeltaYawRad;
        public Vector3 MidpointCam;
        public float SpanRatio;
    }

    public struct CosmosHomeSplineState
    {
        public bool Active;
        public bool IsEarthHome;
        public double ElapsedSec;
        public double DurationSec;
        public Vector3 StartLocalPos;
        public Vector3 MidLocalPos;
        public Vector3 TargetLocalPos;
        public float StartYawDeg;
        public float TargetYawDeg;
        public double StartScaleExp;
        public double MidScaleExp;
        public double TargetScaleExp;

        public void Evaluate(double nextElapsedSec, out Vector3 posLocal, out float yawDeg, out double scaleExp, out bool finished)
        {
            ElapsedSec = nextElapsedSec;
            double dur = DurationSec > 0.05 ? DurationSec : 0.05;
            double rawT = Math.Clamp(ElapsedSec / dur, 0.0, 1.0);
            finished = rawT >= 1.0;

            // Quintic Hermite smootherstep (C2 continuous acceleration at endpoints).
            double s = rawT * rawT * rawT * (rawT * (rawT * 6.0 - 15.0) + 10.0);
            double oneMinus = 1.0 - s;

            // Quadratic Bezier blend through mid-arc apex so distant cross-scale jumps arc outward smoothly.
            posLocal = (float)(oneMinus * oneMinus) * StartLocalPos
                     + (float)(2.0 * oneMinus * s) * MidLocalPos
                     + (float)(s * s) * TargetLocalPos;

            scaleExp = oneMinus * oneMinus * StartScaleExp
                     + 2.0 * oneMinus * s * MidScaleExp
                     + s * s * TargetScaleExp;

            yawDeg = Mathf.LerpAngle(StartYawDeg, TargetYawDeg, (float)s);
            if (finished) Active = false;
        }
    }

    /// <summary>
    /// Scale-invariant 6DoF HyperCosmosVR flight controller ported from crates/cosmos_nav/src/flight.rs.
    /// Layers seamlessly over VirtualRoomView without ever scaling or tilting Room content
    /// (preserving RoomWorldMotion.FrameReady: MetresPerUnit == 1, content.up == Vector3.up).
    /// </summary>
    public sealed class CosmosFlightController : MonoBehaviour
    {
        public const double ColdStartScaleExp = CosmosScaleState.ColdStartScaleExp; // 6.6
        public const double DefaultInertialTauSec = 0.18;
        public const double MinPinchSpanM = 0.05;
        public const double MagneticMinSpeed = 0.05;
        public const double MinScaleExp = CosmosScaleState.MinScaleExp; // -35.0
        public const double MaxScaleExp = CosmosScaleState.MaxScaleExp; // +30.0
        public const float GroundedMaxRadiusMeters = 22.0f;
        public const float WalkableScaleTolerance = 0.25f;

        [Header("Flight Tuning (HyperCosmosVR 1:1)")]
        public double InertialTauSec = DefaultInertialTauSec;
        public float MaxLinearSpeed = 4.5f;
        public float MaxAngularSpeedRad = 2.6f;
        public float MaxScaleSpeed = 6.0f;
        public float ScrollScaleStep = 0.22f;
        public float BoostMultiplier = 2.5f;
        public float StickDeadzone = 0.06f;

        Transform content;
        Transform trackingOrigin;
        Camera viewerCamera;
        VirtualRoomView virtualView;
        MovementControls movementControls;
        WorkspaceViewpoint workspaceViewpoint;
        BookControllerInput controllerInput;
        CosmosArchipelagoBuilder archipelago;
        RoomPhysicsWorld roomPhysics;
        RoomWorldMotion flightMotion;

        CosmosScaleState scaleState = CosmosScaleState.CreateDefault();
        CosmosHomeSplineState homeSpline;

        bool pinchActive;
        Vector3 prevLeftPinchCam;
        Vector3 prevRightPinchCam;

        Vector3 flightLocalPos;
        float flightYawDeg;
        float desktopPitchDeg;
        float desktopYawDeg;
        bool flightInitialized;
        bool wasUserWalkingBeforeFlight;
        bool xrHeadTracked;

        public CosmosScaleState ScaleState => scaleState;
        public CosmosLocomotionMode Mode => scaleState.Mode;
        public Vector3 FlightLocalPosition => flightLocalPos;
        public float FlightYawDeg => flightYawDeg;
        public bool IsHomeSplineActive => homeSpline.Active;

        /// <summary>
        /// Protects RoomViewpoint persistence when soaring in 6DoF, past r=22m, or away from Earth scale u=6.6.
        /// </summary>
        public bool ShouldFreezeViewpointWrites
        {
            get
            {
                if (!scaleState.VirtualModeActive) return false;
                if (scaleState.Mode != CosmosLocomotionMode.GroundedWalk) return true;
                float rSq = flightLocalPos.x * flightLocalPos.x + flightLocalPos.z * flightLocalPos.z;
                if (rSq > GroundedMaxRadiusMeters * GroundedMaxRadiusMeters) return true;
                return Math.Abs(scaleState.ScaleExp - ColdStartScaleExp) > WalkableScaleTolerance;
            }
        }

        // =====================================================================
        // Pure Static HyperCosmosVR Math Helpers (zero-scene testable)
        // =====================================================================

        public static double ExponentialTauDamp(double current, double target, double tauSec, double dtSec)
        {
            if (dtSec <= 0.0) return current;
            if (tauSec <= 0.0) return target;
            double alpha = 1.0 - Math.Exp(-dtSec / tauSec);
            return current + (target - current) * alpha;
        }

        public static Vector3 ExponentialTauDamp(Vector3 current, Vector3 target, double tauSec, double dtSec)
        {
            return new Vector3(
                (float)ExponentialTauDamp(current.x, target.x, tauSec, dtSec),
                (float)ExponentialTauDamp(current.y, target.y, tauSec, dtSec),
                (float)ExponentialTauDamp(current.z, target.z, tauSec, dtSec));
        }

        public static double MagneticApproachThrottle(double distanceRenderUnits, double maxSpeed)
        {
            double hi = Math.Max(maxSpeed, MagneticMinSpeed);
            double d = double.IsFinite(distanceRenderUnits) ? Math.Max(0.0, distanceRenderUnits) : hi * 3.0;
            return Math.Clamp(d / 3.0, MagneticMinSpeed, hi);
        }

        public static double CloseUpStrafeThrottle(double distanceRenderUnits, double maxSpeed)
        {
            const double minStrafe = 0.08;
            double hi = Math.Max(maxSpeed, minStrafe);
            double d = double.IsFinite(distanceRenderUnits) ? Math.Max(0.0, distanceRenderUnits) : hi * 1.5;
            return Math.Clamp(d / 1.5, minStrafe, hi);
        }

        public static double ShapeStickAxis(double raw, double deadzone = 0.06)
        {
            double clamped = Math.Clamp(raw, -1.0, 1.0);
            double abs = Math.Abs(clamped);
            double dz = Math.Clamp(deadzone, 0.0, 0.5);
            if (abs <= dz) return 0.0;
            double norm = Math.Clamp((abs - dz) / (1.0 - dz), 0.0, 1.0);
            return Math.Sign(clamped) * Math.Pow(norm, 1.6);
        }

        public static CosmosPinchStepResult ComputeTwoHandPinchStep(
            Vector3 prevLeftCam,
            Vector3 prevRightCam,
            Vector3 curLeftCam,
            Vector3 curRightCam)
        {
            double dPrev = Math.Max(Vector3.Distance(prevLeftCam, prevRightCam), MinPinchSpanM);
            double dCur = Math.Max(Vector3.Distance(curLeftCam, curRightCam), MinPinchSpanM);
            Vector3 midPrev = 0.5f * (prevLeftCam + prevRightCam);
            Vector3 midCur = 0.5f * (curLeftCam + curRightCam);

            double spanRatio = dCur / dPrev;
            double deltaU = -Math.Log10(spanRatio);
            float invZoom = (float)(1.0 / spanRatio);
            Vector3 displacementOld = midPrev - midCur * invZoom;

            Vector2 vPrev = new Vector2(prevRightCam.x - prevLeftCam.x, prevRightCam.z - prevLeftCam.z);
            Vector2 vCur = new Vector2(curRightCam.x - curLeftCam.x, curRightCam.z - curLeftCam.z);
            float deltaYawRad = 0f;
            if (vPrev.magnitude >= (float)MinPinchSpanM && vCur.magnitude >= (float)MinPinchSpanM)
            {
                Vector3 a = new Vector3(vPrev.x, 0f, vPrev.y).normalized;
                Vector3 b = new Vector3(vCur.x, 0f, vCur.y).normalized;
                deltaYawRad = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad;
            }

            return new CosmosPinchStepResult
            {
                DeltaU = deltaU,
                CamLocalDisplacementOld = displacementOld,
                DeltaYawRad = deltaYawRad,
                MidpointCam = midCur,
                SpanRatio = (float)spanRatio
            };
        }

        public static CosmosHomeSplineState CreateHomeSpline(
            Vector3 startLocalPos,
            float startYawDeg,
            double startScaleExp,
            Vector3 targetLocalPos,
            float targetYawDeg,
            double targetScaleExp,
            bool isEarthHome = true,
            double? durationOverrideSec = null)
        {
            double du = Math.Abs(startScaleExp - targetScaleExp);
            double dur = durationOverrideSec.HasValue
                ? Math.Clamp(durationOverrideSec.Value, 0.35, 4.2)
                : Math.Clamp(1.4 + 0.08 * du, 0.35, 4.2);

            double midU = startScaleExp;
            if (du > 3.0)
            {
                midU = startScaleExp > targetScaleExp
                    ? 0.5 * (startScaleExp + targetScaleExp)
                    : Math.Max(targetScaleExp + 1.8, startScaleExp + 2.0);
                midU = Math.Clamp(midU, MinScaleExp, MaxScaleExp);
            }
            else
            {
                midU = 0.5 * (startScaleExp + targetScaleExp);
            }

            Vector3 midPos = Vector3.Lerp(startLocalPos, targetLocalPos, 0.5f);
            midPos.y = Mathf.Max(midPos.y, Mathf.Max(startLocalPos.y, targetLocalPos.y) + (du > 1.5 ? 2.2f : 0.4f));

            return new CosmosHomeSplineState
            {
                Active = true,
                IsEarthHome = isEarthHome,
                ElapsedSec = 0.0,
                DurationSec = dur,
                StartLocalPos = startLocalPos,
                MidLocalPos = midPos,
                TargetLocalPos = targetLocalPos,
                StartYawDeg = startYawDeg,
                TargetYawDeg = targetYawDeg,
                StartScaleExp = startScaleExp,
                MidScaleExp = midU,
                TargetScaleExp = targetScaleExp
            };
        }

        // =====================================================================
        // Runtime Initialization & Maestro Integration
        // =====================================================================

        internal void Initialize(
            Transform roomContent,
            Transform physicalOrigin,
            Camera camera,
            VirtualRoomView view,
            MovementControls controls,
            WorkspaceViewpoint viewpoint,
            BookControllerInput input,
            CosmosArchipelagoBuilder builder)
        {
            content = roomContent;
            trackingOrigin = physicalOrigin;
            viewerCamera = camera;
            virtualView = view;
            movementControls = controls;
            workspaceViewpoint = viewpoint;
            controllerInput = input;
            archipelago = builder;
            if (content && view)
            {
                roomPhysics = view.GetComponent<RoomPhysicsWorld>();
                flightMotion = roomPhysics ? new RoomWorldMotion(content, roomPhysics) : null;
            }
            SyncLocalFromRoomContent();
            flightInitialized = true;
        }

        internal void BindSession(MovementControls controls, WorkspaceViewpoint viewpoint)
        {
            movementControls = controls;
            workspaceViewpoint = viewpoint;
            ApplyViewpointFreezeState();
        }

        public void SetVirtualModeActive(bool active)
        {
            if (scaleState.VirtualModeActive == active) return;
            scaleState.VirtualModeActive = active;
            if (!active)
            {
                homeSpline.Active = false;
                pinchActive = false;
                scaleState.VelocityLocal = Vector3.zero;
                scaleState.ScaleVelocity = 0f;
                scaleState.ScaleExp = ColdStartScaleExp;
                scaleState.TargetScaleExp = ColdStartScaleExp;
                scaleState.Mode = CosmosLocomotionMode.GroundedWalk;
                scaleState.AltitudeAboveDais = 0f;
                if (workspaceViewpoint && !workspaceViewpoint.enabled)
                    workspaceViewpoint.enabled = true;
            }
            else
            {
                SyncLocalFromRoomContent();
            }
        }

        public void SetHeadTrackedState(bool tracked) => xrHeadTracked = tracked;

        public void SetSpeedMultiplier(float multiplier)
        {
            scaleState.SpeedMultiplier = Mathf.Clamp(multiplier, 1f, 3f);
        }

        public void ToggleWalkFly()
        {
            if (scaleState.Mode == CosmosLocomotionMode.GroundedWalk)
                EnterFreeFlight(liftOffBoost: true);
            else
                RequestAutoLanding();
        }

        public void TriggerEarthHome()
        {
            homeSpline = CreateHomeSpline(
                flightLocalPos,
                flightYawDeg,
                scaleState.ScaleExp,
                new Vector3(0f, 0f, 0f),
                0f,
                ColdStartScaleExp,
                isEarthHome: true);
            scaleState.Mode = CosmosLocomotionMode.AutoLanding;
            pinchActive = false;
            ApplyViewpointFreezeState();
        }

        public void FlyToFocusTarget(in CosmosFocusTarget target)
        {
            if (!scaleState.VirtualModeActive) return;
            PrepareFlightMode();
            Vector3 approachOffset = new Vector3(0f, Mathf.Clamp(target.FocusRadius * 0.35f, 0.25f, 1.8f), -Mathf.Clamp(target.FocusRadius * 2.1f, 1.4f, 6.0f));
            homeSpline = CreateHomeSpline(
                flightLocalPos,
                flightYawDeg,
                scaleState.ScaleExp,
                target.LocalPosition + approachOffset,
                flightYawDeg,
                target.FocusScaleExp,
                isEarthHome: false);
            scaleState.ActiveExperienceId = target.ExperienceId;
            scaleState.ActiveIslandIndex = target.IslandIndex;
            scaleState.Mode = CosmosLocomotionMode.FreeFlight;
            ApplyViewpointFreezeState();
        }

        public void RequestAutoLanding()
        {
            Vector3 landPos = new Vector3(0f, 0f, 0f);
            byte islandIdx = 0;
            if (archipelago != null && archipelago.FindNearestLandingSurface(flightLocalPos, out var foundPos, out _, out byte nearestIslandIdx))
            {
                landPos = foundPos;
                islandIdx = nearestIslandIdx;
            }
            homeSpline = CreateHomeSpline(
                flightLocalPos,
                flightYawDeg,
                scaleState.ScaleExp,
                landPos,
                flightYawDeg,
                ColdStartScaleExp,
                isEarthHome: false,
                durationOverrideSec: 0.95);
            scaleState.ActiveIslandIndex = islandIdx;
            scaleState.Mode = CosmosLocomotionMode.AutoLanding;
            pinchActive = false;
            ApplyViewpointFreezeState();
        }

        internal void Step(float dtSec, in CosmosInputSnapshot input, float focusDistanceMeters = -1f, bool ringCapturesRightButton = false)
        {
            if (!flightInitialized || !scaleState.VirtualModeActive) return;
            float dt = Mathf.Clamp(dtSec, 0f, 0.1f);

            if (input.DesktopHomeJustPressed)
            {
                TriggerEarthHome();
            }

            bool twoHandPinch = input.LeftTracked && input.RightTracked && (input.LeftPinching || input.LeftGrip) && (input.RightPinching || input.RightGrip);
            bool hasManualFlightInput = twoHandPinch
                || Mathf.Abs(input.DesktopScrollY) > 0.01f
                || Mathf.Abs(input.DesktopMove.y) > 0.01f
                || (input.DesktopMove.sqrMagnitude > 0.01f && scaleState.Mode != CosmosLocomotionMode.GroundedWalk)
                || Mathf.Abs((float)ShapeStickAxis(input.RightStick.y, StickDeadzone)) > 0.12f;

            if (hasManualFlightInput && homeSpline.Active)
            {
                homeSpline.Active = false;
                if (scaleState.Mode == CosmosLocomotionMode.AutoLanding)
                    scaleState.Mode = CosmosLocomotionMode.FreeFlight;
            }

            // Auto-ascend seamlessly into FreeFlight upon vertical/scale-dive gestures.
            if (scaleState.Mode == CosmosLocomotionMode.GroundedWalk)
            {
                SyncLocalFromRoomContent();
                bool wantsAscend = twoHandPinch
                    || input.DesktopMove.y > 0.1f
                    || Mathf.Abs(input.DesktopScrollY) > 0.01f
                    || Math.Abs(ShapeStickAxis(input.RightStick.y, StickDeadzone)) > 0.25;
                if (wantsAscend)
                {
                    EnterFreeFlight(liftOffBoost: input.DesktopMove.y > 0.1f && !twoHandPinch);
                }
                else
                {
                    // Desktop WASD / mouse-look grounded navigation over Cosmos Archipelago.
                    StepDesktopGroundedLookAndWalk(dt, in input);
                    ApplyViewpointFreezeState();
                    return;
                }
            }

            if (homeSpline.Active)
            {
                homeSpline.Evaluate(homeSpline.ElapsedSec + dt, out var splinePos, out var splineYaw, out var splineU, out bool finished);
                flightLocalPos = splinePos;
                flightYawDeg = splineYaw;
                scaleState.ScaleExp = Math.Clamp(splineU, MinScaleExp, MaxScaleExp);
                scaleState.TargetScaleExp = scaleState.ScaleExp;
                scaleState.VelocityLocal = Vector3.zero;
                scaleState.ScaleVelocity = 0f;
                scaleState.AltitudeAboveDais = flightLocalPos.y;
                ApplyFlightTransformToRoomContent();

                if (finished && scaleState.Mode == CosmosLocomotionMode.AutoLanding)
                {
                    CompleteLandingToGroundedWalk();
                }
                ApplyViewpointFreezeState();
                return;
            }

            // Active FreeFlight / two-hand world pinch.
            StepFreeFlight(dt, in input, focusDistanceMeters, twoHandPinch, ringCapturesRightButton);
            ApplyViewpointFreezeState();
        }

        void StepDesktopGroundedLookAndWalk(float dt, in CosmosInputSnapshot input)
        {
            if (viewerCamera && !xrHeadTracked && input.DesktopLookActive)
            {
                desktopYawDeg += input.DesktopLookDelta.x * 0.18f;
                desktopPitchDeg = Mathf.Clamp(desktopPitchDeg - input.DesktopLookDelta.y * 0.18f, -82f, 82f);
                viewerCamera.transform.localRotation = Quaternion.Euler(desktopPitchDeg, desktopYawDeg, 0f);
            }

            if (virtualView && (Mathf.Abs(input.DesktopMove.x) > 0.01f || Mathf.Abs(input.DesktopMove.z) > 0.01f))
            {
                Transform camT = viewerCamera ? viewerCamera.transform : transform;
                Vector3 fwd = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3 moveDir = (fwd * input.DesktopMove.z + right * input.DesktopMove.x);
                if (moveDir.sqrMagnitude > 1e-4f) moveDir.Normalize();
                float speed = (input.DesktopSprint ? 3.2f : 1.65f) * scaleState.SpeedMultiplier;
                Vector3 delta = moveDir * Mathf.Min(speed * dt, 0.08f);
                delta.y = 0f;
                virtualView.Move(delta);
                SyncLocalFromRoomContent();
            }
        }

        void StepFreeFlight(float dt, in CosmosInputSnapshot input, float focusDistanceMeters, bool twoHandPinch, bool ringCapturesRightButton)
        {
            Transform camT = viewerCamera ? viewerCamera.transform : transform;
            if (twoHandPinch)
            {
                Vector3 curL = camT.InverseTransformPoint(input.LeftPinchPos);
                Vector3 curR = camT.InverseTransformPoint(input.RightPinchPos);
                if (!pinchActive)
                {
                    pinchActive = true;
                    prevLeftPinchCam = curL;
                    prevRightPinchCam = curR;
                    scaleState.VelocityLocal = Vector3.zero;
                    scaleState.ScaleVelocity = 0f;
                    return;
                }

                CosmosPinchStepResult step = ComputeTwoHandPinchStep(prevLeftPinchCam, prevRightPinchCam, curL, curR);
                prevLeftPinchCam = curL;
                prevRightPinchCam = curR;

                double nextU = Math.Clamp(scaleState.ScaleExp + step.DeltaU, MinScaleExp, MaxScaleExp);
                double actualDu = nextU - scaleState.ScaleExp;
                scaleState.ScaleExp = nextU;
                scaleState.TargetScaleExp = nextU;

                Vector3 worldDisp = camT.TransformVector(step.CamLocalDisplacementOld);
                Vector3 localDisp = content ? content.InverseTransformVector(worldDisp) : worldDisp;
                flightLocalPos += localDisp;
                flightYawDeg += step.DeltaYawRad * Mathf.Rad2Deg;

                if (dt > 1e-5f)
                {
                    scaleState.ScaleVelocity = (float)(actualDu / dt);
                    scaleState.VelocityLocal = localDisp / dt;
                }
                scaleState.AltitudeAboveDais = flightLocalPos.y;
                ApplyFlightTransformToRoomContent();
                return;
            }
            pinchActive = false;

            // Mouse wheel focal zoom (handles both Windows +-120 detents and normalized +-1 notches).
            if (Mathf.Abs(input.DesktopScrollY) > 0.01f)
            {
                float rawNotches = Mathf.Abs(input.DesktopScrollY) > 10f ? input.DesktopScrollY / 120f : input.DesktopScrollY;
                float notches = Mathf.Clamp(rawNotches, -6f, 6f);
                double rawDu = -notches * ScrollScaleStep * scaleState.SpeedMultiplier;
                double clampedU = Math.Clamp(scaleState.ScaleExp + rawDu, MinScaleExp, MaxScaleExp);
                double actualDu = clampedU - scaleState.ScaleExp;
                scaleState.ScaleExp = clampedU;
                scaleState.TargetScaleExp = clampedU;
                double zoomFactor = Math.Pow(10.0, -actualDu);
                float pivotDist = focusDistanceMeters > 0.05f ? focusDistanceMeters : 2.5f;
                Vector3 pivotWorld = camT.forward * pivotDist * (float)(1.0 - 1.0 / zoomFactor);
                flightLocalPos += content ? content.InverseTransformVector(pivotWorld) : pivotWorld;
            }

            // Look rotation (Desktop non-XR pitch/yaw + VR right stick yaw).
            if (viewerCamera && !xrHeadTracked && input.DesktopLookActive)
            {
                desktopYawDeg += input.DesktopLookDelta.x * 0.18f;
                desktopPitchDeg = Mathf.Clamp(desktopPitchDeg - input.DesktopLookDelta.y * 0.18f, -82f, 82f);
                viewerCamera.transform.localRotation = Quaternion.Euler(desktopPitchDeg, desktopYawDeg, 0f);
            }

            float yawAxis = (float)ShapeStickAxis(input.RightStick.x, StickDeadzone);
            if (Mathf.Abs(yawAxis) > 0.001f)
            {
                flightYawDeg -= yawAxis * MaxAngularSpeedRad * Mathf.Rad2Deg * dt;
            }

            // Magnetic approach & close-up strafe auto-throttle.
            float speedMul = scaleState.SpeedMultiplier * (input.DesktopSprint ? BoostMultiplier : 1f);
            double dist = focusDistanceMeters > 0.05f ? focusDistanceMeters : 100.0;
            float maxFwd = (float)MagneticApproachThrottle(dist, MaxLinearSpeed * speedMul);
            float maxStrafe = (float)CloseUpStrafeThrottle(dist, MaxLinearSpeed * speedMul);

            float moveRight = (float)ShapeStickAxis(input.LeftStick.x, StickDeadzone) + input.DesktopMove.x;
            float moveFwd = (float)ShapeStickAxis(input.LeftStick.y, StickDeadzone) + input.DesktopMove.z;
            float moveUp = input.DesktopMove.y + (input.RightPrimary ? 0.65f : 0f) - (input.LeftPrimary ? 0.65f : 0f);

            Vector3 fwdWorld = camT.forward;
            Vector3 rightWorld = camT.right;
            Vector3 worldTargetVel = rightWorld * (Mathf.Clamp(moveRight, -1f, 1f) * maxStrafe)
                                   + Vector3.up * (Mathf.Clamp(moveUp, -1f, 1f) * maxStrafe)
                                   + fwdWorld * (Mathf.Clamp(moveFwd, -1f, 1f) * (moveFwd >= 0f ? maxFwd : MaxLinearSpeed * speedMul));

            // Single-hand pointer trigger thrust along pointer direction.
            if (input.RightTrigger && !ringCapturesRightButton && input.HasPointerRay)
            {
                worldTargetVel += input.PointerDir.normalized * maxFwd;
            }

            Vector3 localTargetVel = content ? content.InverseTransformVector(worldTargetVel) : worldTargetVel;
            double targetScaleVel = -ShapeStickAxis(input.RightStick.y, StickDeadzone) * MaxScaleSpeed * scaleState.SpeedMultiplier;

            scaleState.VelocityLocal = ExponentialTauDamp(scaleState.VelocityLocal, localTargetVel, InertialTauSec, dt);
            scaleState.ScaleVelocity = (float)ExponentialTauDamp(scaleState.ScaleVelocity, targetScaleVel, InertialTauSec, dt);

            flightLocalPos += scaleState.VelocityLocal * dt;
            double nextScale = Math.Clamp(scaleState.ScaleExp + scaleState.ScaleVelocity * dt, MinScaleExp, MaxScaleExp);
            scaleState.ScaleExp = nextScale;
            scaleState.TargetScaleExp = nextScale;
            scaleState.AltitudeAboveDais = flightLocalPos.y;

            // Gentle ground floor check & auto-land assist when descending right above a walkable island terrace.
            if (Math.Abs(scaleState.ScaleExp - ColdStartScaleExp) <= WalkableScaleTolerance
                && scaleState.VelocityLocal.y < -0.15f
                && archipelago != null
                && archipelago.FindNearestLandingSurface(flightLocalPos, out var surfacePos, out _, out byte landIslandIdx))
            {
                float walkableR = landIslandIdx < archipelago.Islands.Count
                    ? archipelago.Islands[landIslandIdx].WalkableRadius
                    : 4.3f;
                float horizDist = Vector2.Distance(new Vector2(flightLocalPos.x, flightLocalPos.z), new Vector2(surfacePos.x, surfacePos.z));
                float heightAbove = flightLocalPos.y - surfacePos.y;
                if (horizDist <= walkableR + 0.5f && heightAbove <= 0.12f && heightAbove >= -0.35f)
                {
                    flightLocalPos.y = surfacePos.y;
                    scaleState.AltitudeAboveDais = surfacePos.y;
                    scaleState.ActiveIslandIndex = landIslandIdx;
                    CompleteLandingToGroundedWalk();
                    return;
                }
            }

            ApplyFlightTransformToRoomContent();
        }

        void PrepareFlightMode()
        {
            if (movementControls && movementControls.UserEnabled)
            {
                wasUserWalkingBeforeFlight = true;
                movementControls.ToggleUser();
            }
            if (workspaceViewpoint && workspaceViewpoint.enabled)
                workspaceViewpoint.enabled = false;
        }

        void EnterFreeFlight(bool liftOffBoost)
        {
            PrepareFlightMode();
            scaleState.Mode = CosmosLocomotionMode.FreeFlight;
            if (liftOffBoost && flightLocalPos.y < 0.08f)
                scaleState.VelocityLocal = new Vector3(0f, 1.15f, 0f);
        }

        void CompleteLandingToGroundedWalk()
        {
            homeSpline.Active = false;
            pinchActive = false;
            scaleState.VelocityLocal = Vector3.zero;
            scaleState.ScaleVelocity = 0f;
            scaleState.ScaleExp = ColdStartScaleExp;
            scaleState.TargetScaleExp = ColdStartScaleExp;
            scaleState.Mode = CosmosLocomotionMode.GroundedWalk;
            scaleState.AltitudeAboveDais = flightLocalPos.y;

            if (virtualView && flightLocalPos.x * flightLocalPos.x + flightLocalPos.z * flightLocalPos.z <= GroundedMaxRadiusMeters * GroundedMaxRadiusMeters)
            {
                // VirtualRoomView.RestoreViewpoint applies AngleAxis(heading - vp.yaw, up),
                // so pass heading + flightYawDeg to preserve current world yaw (-flightYawDeg) seamlessly.
                float heading = ComputeViewerHeadingDeg();
                var vp = new RoomViewpoint
                {
                    active = true,
                    position = flightLocalPos,
                    yaw = Mathf.DeltaAngle(0f, heading + flightYawDeg)
                };
                if (!virtualView.RestoreViewpoint(vp, out _))
                    ApplyFlightTransformToRoomContent();
            }
            else
            {
                ApplyFlightTransformToRoomContent();
            }

            if (wasUserWalkingBeforeFlight && movementControls && !movementControls.UserEnabled)
            {
                wasUserWalkingBeforeFlight = false;
                movementControls.ToggleUser();
            }
            ApplyViewpointFreezeState();
        }

        float ComputeViewerHeadingDeg()
        {
            if (!viewerCamera) return 0f;
            Vector3 forward = Vector3.ProjectOnPlane(viewerCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f || !float.IsFinite(forward.x) || !float.IsFinite(forward.z))
                return 0f;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        void SyncLocalFromRoomContent()
        {
            if (!content || !viewerCamera) return;
            var frame = new RoomFrame(content);
            Vector3 viewerHead = viewerCamera.transform.position;
            float footY = trackingOrigin ? trackingOrigin.position.y : 0f;
            Vector3 footWorld = new Vector3(viewerHead.x, footY, viewerHead.z);
            flightLocalPos = frame.PointToRoom(footWorld);
            flightYawDeg = Mathf.DeltaAngle(0f, -content.eulerAngles.y);
            scaleState.AltitudeAboveDais = flightLocalPos.y;
        }

        bool ApplyFlightTransformToRoomContent()
        {
            if (!content || !viewerCamera) return false;
            Vector3 head = viewerCamera.transform.position;
            float footY = trackingOrigin ? trackingOrigin.position.y : 0f;
            Vector3 foot = new Vector3(head.x, footY, head.z);

            // Preserve RoomWorldMotion invariants: MetresPerUnit == 1 & content.up == Vector3.up ALWAYS.
            Quaternion rot = Quaternion.AngleAxis(-flightYawDeg, Vector3.up);
            Vector3 targetContentPos = foot - rot * flightLocalPos;

            if (flightMotion != null && flightMotion.SetPose(targetContentPos, rot, out _))
                return true;

            if (roomPhysics && roomPhysics.Running)
            {
                // Never bypass RoomWorldMotion BodyPose transfers while live RoomPhysicsWorld runs.
                SyncLocalFromRoomContent();
                scaleState.VelocityLocal = Vector3.zero;
                return false;
            }

            content.SetPositionAndRotation(targetContentPos, rot);
            Physics.SyncTransforms();
            return true;
        }

        void ApplyViewpointFreezeState()
        {
            if (!workspaceViewpoint) return;
            bool shouldBeEnabled = !ShouldFreezeViewpointWrites;
            if (workspaceViewpoint.enabled != shouldBeEnabled)
                workspaceViewpoint.enabled = shouldBeEnabled;
        }
    }
}
