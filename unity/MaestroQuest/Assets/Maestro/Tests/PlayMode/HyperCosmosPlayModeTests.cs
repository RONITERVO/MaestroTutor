// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Cosmos;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Maestro.Quest.Tests
{
    public sealed class HyperCosmosPlayModeTests
    {
        readonly List<UnityEngine.Object> owned = new();
        readonly List<string> tempDirs = new();
        readonly List<WorkspaceHost> trackedWorkspaces = new();
        AudioListener[] priorListeners;
        bool[] priorListenerStates;

        T Own<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) owned.Add(obj);
            return obj;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Shader.SetGlobalFloat("_MaestroLightingEnabled", 0f);
            Shader.SetGlobalFloat("_MaestroLocalLightingEnabled", 0f);
            Shader.SetGlobalFloat("_MaestroFogDensity", 0f);

            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i]) UnityEngine.Object.Destroy(owned[i]);
            }
            owned.Clear();

            yield return null;

            for (int i = 0; i < trackedWorkspaces.Count; i++)
            {
                WorkspaceHost ws = trackedWorkspaces[i];
                if (ReferenceEquals(ws, null)) continue;
                double deadline = Time.realtimeSinceStartupAsDouble + 10.0;
                while (!ws.Retirement.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                }
                Assert.That(ws.Retirement.IsCompleted, Is.True, "WorkspaceHost retirement did not complete before directory cleanup");
            }
            trackedWorkspaces.Clear();

            if (priorListeners != null)
            {
                for (int i = 0; i < priorListeners.Length; i++)
                {
                    if (priorListeners[i]) priorListeners[i].enabled = priorListenerStates[i];
                }
                priorListeners = null;
                priorListenerStates = null;
            }

            for (int i = 0; i < tempDirs.Count; i++)
            {
                if (Directory.Exists(tempDirs[i]))
                {
                    Directory.Delete(tempDirs[i], true);
                }
            }
            tempDirs.Clear();
        }

        MaestroRoom SpawnMaestroRoom(
            out GameObject roomGo,
            out WorkspaceHost workspace,
            out Camera cam,
            out VirtualRoomView view,
            out HyperCosmosWorld cosmos)
        {
            priorListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            priorListenerStates = new bool[priorListeners.Length];
            for (int i = 0; i < priorListeners.Length; i++)
            {
                priorListenerStates[i] = priorListeners[i].enabled;
                priorListeners[i].enabled = false;
            }

            string dir = Path.Combine(Application.temporaryCachePath, "hypercosmos-pm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            tempDirs.Add(dir);

            roomGo = Own(new GameObject("Maestro HyperCosmos Test Room"));
            roomGo.SetActive(false);
            MaestroRoom room = roomGo.AddComponent<MaestroRoom>();
#if UNITY_EDITOR
            room.ProbeWorkspaceDirectory = dir;
#endif
            roomGo.SetActive(true);

            workspace = roomGo.GetComponent<WorkspaceHost>();
            Assert.That(workspace, Is.Not.Null);
            trackedWorkspaces.Add(workspace);
            Assert.That(workspace.Ready, Is.True, workspace.Status);

            cam = roomGo.GetComponentInChildren<Camera>(true);
            view = roomGo.GetComponent<VirtualRoomView>();
            cosmos = roomGo.GetComponentInChildren<HyperCosmosWorld>(true);
            Assert.That(cam, Is.Not.Null);
            Assert.That(view, Is.Not.Null);
            Assert.That(cosmos, Is.Not.Null);
            return room;
        }

        static Transform FindRoomContent(GameObject roomGo, HyperCosmosWorld cosmos)
        {
            if (cosmos != null && cosmos.Archipelago != null && cosmos.Archipelago.ArchipelagoTransform != null)
                return cosmos.Archipelago.ArchipelagoTransform.parent;
            return roomGo.transform.Find("Room content");
        }

        static (string ResPath, string ShaderName, bool Instanced)[] AllNineCosmosShaders() => new[]
        {
            (CosmosShaderIds.ResFarShellSkyMat, CosmosShaderIds.ShaderFarShellSky, false),
            (CosmosShaderIds.ResEarthHeroMat, CosmosShaderIds.ShaderEarthHero, false),
            (CosmosShaderIds.ResOrbitalPlanetMat, CosmosShaderIds.ShaderOrbitalPlanet, true),
            (CosmosShaderIds.ResAuroralCurtainMat, CosmosShaderIds.ShaderAuroralCurtain, false),
            (CosmosShaderIds.ResTetherBridgeBeamMat, CosmosShaderIds.ShaderTetherBridgeBeam, false),
            (CosmosShaderIds.ResCrystallineIslandMat, CosmosShaderIds.ShaderCrystallineIsland, false),
            (CosmosShaderIds.ResSpriteFieldMat, CosmosShaderIds.ShaderSpriteField, true),
            (CosmosShaderIds.ResMsdfLiveInkMat, CosmosShaderIds.ShaderMsdfLiveInk, false),
            (CosmosShaderIds.ResWristBeadMat, CosmosShaderIds.ShaderWristBead, false),
        };

        [UnityTest]
        public IEnumerator MaestroRoomOledBackdropFarClipAndPassthroughColliderInvariantsHold()
        {
            SpawnMaestroRoom(out GameObject roomGo, out WorkspaceHost workspace, out Camera cam, out VirtualRoomView view, out HyperCosmosWorld cosmos);
            string[] beforeObjectIds = workspace.Current.Editor.Snapshot().objects.Select(x => x.id).ToArray();
            Assert.That(beforeObjectIds, Is.SupersetOf(new[] { "book", "maestro" }));

            Assert.That(cam.farClipPlane, Is.GreaterThanOrEqualTo(1000f), "Far shell dome requires farClipPlane >= 1000");

            // Passthrough default before entering virtual cosmos: archipelago colliders must stay disabled
            Transform archipelagoTx = cosmos.Archipelago.ArchipelagoTransform;
            Assert.That(archipelagoTx, Is.Not.Null);
            Assert.That(cosmos.VirtualModeActive, Is.False);
            foreach (BoxCollider col in cosmos.Archipelago.AllWalkableColliders)
            {
                Assert.That(col.enabled, Is.False, $"Collider {col.name} must be disabled at BackdropOpacity == 0");
            }

            // Enter virtual cosmos (BackdropOpacity == 1 -> OLED black (0,0,0,1))
            Assert.That(view.Enter(), Is.True, "VirtualRoomView.Enter should succeed");
            yield return null;

            Assert.That(view.BackdropOpacity, Is.EqualTo(1f));
            Assert.That(view.Active, Is.True);
            Assert.That(cam.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(cam.backgroundColor.r, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(cam.backgroundColor.g, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(cam.backgroundColor.b, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(cam.backgroundColor.a, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(cosmos.VirtualModeActive, Is.True);
            Assert.That(archipelagoTx.gameObject.activeInHierarchy, Is.True);

            // D2 regression: Island 3 HslBiconePrism must use bundled Cosmos/* shader (not strippable Sprites/Default)
            MeshRenderer prismRenderer = roomGo.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(r => r.gameObject.name == "HslBiconePrism");
            Assert.That(prismRenderer, Is.Not.Null, "Expected HslBiconePrism MeshRenderer under CosmosLandmarksPresenter");
            Assert.That(prismRenderer.sharedMaterial, Is.Not.Null);
            Assert.That(prismRenderer.sharedMaterial.shader, Is.Not.Null);
            Assert.That(prismRenderer.sharedMaterial.shader.name, Does.StartWith("Cosmos/"),
                "HslBiconePrism must use a Resources-backed Cosmos/* shader so IL2CPP Quest builds cannot strip it");
            Assert.That(prismRenderer.sharedMaterial.shader.name, Is.Not.EqualTo("Sprites/Default"));

            Assert.That(cosmos.Archipelago.AllWalkableColliders.Count, Is.GreaterThan(0));
            foreach (BoxCollider col in cosmos.Archipelago.AllWalkableColliders)
            {
                Assert.That(col.enabled, Is.True, $"Collider {col.name} must be enabled in virtual mode");
                Assert.That(col.gameObject.activeInHierarchy, Is.True);
            }

            // Exit back to MR passthrough (BackdropOpacity == 0): ALL "Cosmos Archipelago" colliders disabled!
            Assert.That(view.SetPresentation(0f, true), Is.True);
            yield return null;

            Assert.That(view.BackdropOpacity, Is.EqualTo(0f));
            Assert.That(cosmos.VirtualModeActive, Is.False);
            Collider[] archipelagoColliders = archipelagoTx.GetComponentsInChildren<Collider>(true);
            Assert.That(archipelagoColliders.Length, Is.GreaterThan(0));
            foreach (Collider col in archipelagoColliders)
            {
                Assert.That(col.enabled, Is.False, $"Passthrough Collider {col.name} must be disabled when BackdropOpacity == 0");
                Assert.That(col.gameObject.activeInHierarchy, Is.False, $"Passthrough GO {col.name} must be inactive when BackdropOpacity == 0");
            }

            // Verify WorkspaceDefaults.Snapshot & live workspace keep exact Codex starter object invariants ["book", "maestro"]
            var defaultsSnapshot = WorkspaceDefaults.Snapshot(null);
            JObject defaultRoom = JObject.Parse(System.Text.Encoding.UTF8.GetString(defaultsSnapshot.Documents[RoomStorage.FileName]));
            Assert.That(defaultRoom["objects"].Select(x => (string)x["id"]), Is.EquivalentTo(new[] { "book", "maestro" }));
            Assert.That(workspace.Current.Editor.Snapshot().objects.Select(x => x.id), Is.EquivalentTo(beforeObjectIds));
        }

        [UnityTest]
        public IEnumerator AllIslandsAndTetherBridgeStepsPassRoomGroundQueryWalkableSupport()
        {
            SpawnMaestroRoom(out GameObject roomGo, out _, out _, out VirtualRoomView view, out HyperCosmosWorld cosmos);
            Transform content = FindRoomContent(roomGo, cosmos);
            Assert.That(content, Is.Not.Null);

            Assert.That(view.Enter(), Is.True);
            yield return null;
            Physics.SyncTransforms();

            Assert.That(cosmos.Archipelago.Islands.Count, Is.EqualTo(6));
            Assert.That(cosmos.Archipelago.Bridges.Count, Is.EqualTo(5));

            var ground = new RoomGroundQuery();
            ground.Capture(content, includeScan: false);

            // Structural physics check across every walkable collider
            foreach (BoxCollider box in cosmos.Archipelago.AllWalkableColliders)
            {
                Assert.That(box, Is.Not.Null);
                Assert.That(box.enabled, Is.True);
                Assert.That(box.isTrigger, Is.False);
                Assert.That(box.gameObject.layer, Is.EqualTo(RoomPhysicsLayers.Environment),
                    $"Collider {box.name} must be on Environment layer 8");
                Assert.That(box.GetComponent<Rigidbody>(), Is.Null, $"Walkable {box.name} must have zero Rigidbody");
                Assert.That(box.GetComponentInParent<Rigidbody>(), Is.Null);
                var walkable = box.GetComponent<RoomWalkableSurface>();
                Assert.That(walkable, Is.Not.Null, $"Walkable {box.name} missing RoomWalkableSurface");
                Assert.That(walkable.Available, Is.True);
            }

            // Every floating island (and central ObservatoryDais) supports footprints at center and perimeter
            Vector3[] offsets =
            {
                Vector3.zero,
                Vector3.forward,
                Vector3.back,
                Vector3.left,
                Vector3.right
            };

            foreach (CosmosIslandDescriptor island in cosmos.Archipelago.Islands)
            {
                float insetR = Mathf.Max(0.25f, island.WalkableRadius * 0.55f);
                foreach (Vector3 dir in offsets)
                {
                    Vector3 sampleLocal = island.CenterLocal + dir * (dir == Vector3.zero ? 0f : insetR);
                    Assert.That(RoomViewpoint.ValidPosition(sampleLocal), Is.True,
                        $"Island {island.Name} point {sampleLocal} exceeds RoomViewpoint <= 25m");

                    Vector3 sampleWorld = content.TransformPoint(sampleLocal);
                    bool supported = ground.Support(sampleWorld, 0.20f, 0.35f, 0.35f, out Vector3 hitPoint, out Vector3 hitNormal, out string err);
                    Assert.That(supported, Is.True, $"Island {island.Name} sample {sampleLocal} failed RoomGroundQuery.Support: {err}");
                    Assert.That(Vector3.Angle(hitNormal, Vector3.up), Is.LessThanOrEqualTo(15f));
                    Assert.That(hitPoint.y, Is.EqualTo(sampleWorld.y).Within(0.05f));
                }
            }

            // Every single tether bridge step collider passes RoomGroundQuery.Support within <= 25m
            Transform bridgesRoot = cosmos.Archipelago.ArchipelagoTransform.Find("WalkableTetherBridges");
            Assert.That(bridgesRoot, Is.Not.Null);
            BoxCollider[] stepColliders = bridgesRoot.GetComponentsInChildren<BoxCollider>(false);
            Assert.That(stepColliders.Length, Is.GreaterThanOrEqualTo(5 * 2), "Expected stepped bridge colliders for all 5 bridges");

            foreach (BoxCollider stepBox in stepColliders)
            {
                Vector3 stepWorldTop = stepBox.transform.position;
                Vector3 stepLocalTop = content.InverseTransformPoint(stepWorldTop);
                Assert.That(RoomViewpoint.ValidPosition(stepLocalTop), Is.True,
                    $"Bridge step {stepBox.name} at {stepLocalTop} exceeds RoomViewpoint <= 25m");

                bool supported = ground.Support(stepWorldTop, 0.20f, 0.25f, 0.25f, out Vector3 hitPoint, out Vector3 hitNormal, out string err);
                Assert.That(supported, Is.True, $"Bridge step {stepBox.name} at {stepLocalTop} failed RoomGroundQuery.Support: {err}");
                Assert.That(Vector3.Angle(hitNormal, Vector3.up), Is.LessThanOrEqualTo(15f));
                Assert.That(hitPoint.y, Is.EqualTo(stepWorldTop.y).Within(0.03f));
            }
        }

        [UnityTest]
        public IEnumerator GroundedWalkFreeFlightScaleDiveAndAutoLandingPreserveWorldMotionAndSnapshot()
        {
            SpawnMaestroRoom(out GameObject roomGo, out WorkspaceHost workspace, out Camera cam, out VirtualRoomView view, out HyperCosmosWorld cosmos);
            string[] beforeObjectIds = workspace.Current.Editor.Snapshot().objects.Select(x => x.id).ToArray();
            Assert.That(beforeObjectIds, Is.SupersetOf(new[] { "book", "maestro" }));
            Transform content = FindRoomContent(roomGo, cosmos);
            RoomPhysicsWorld physics = roomGo.GetComponent<RoomPhysicsWorld>();
            var motion = new RoomWorldMotion(content, physics);

            cam.transform.position = new Vector3(0f, 1.55f, 0f);
            cam.transform.rotation = Quaternion.identity;

            Assert.That(view.Enter(), Is.True);
            yield return null;

            CosmosFlightController flight = cosmos.Flight;
            Assert.That(flight.Mode, Is.EqualTo(CosmosLocomotionMode.GroundedWalk));
            Assert.That(flight.ScaleState.ScaleExp, Is.EqualTo(CosmosFlightController.ColdStartScaleExp).Within(1e-6));
            Assert.That(motion.Ready, Is.True);

            // 1. Lift off from GroundedWalk into 6DoF FreeFlight
            var ascendInput = new CosmosInputSnapshot
            {
                DesktopMove = new Vector3(0f, 1f, 0f),
                RightStick = new Vector2(0.8f, -0.7f)
            };
            for (int i = 0; i < 36; i++)
            {
                flight.Step(1f / 60f, in ascendInput, focusDistanceMeters: 8f);
                var frame = new RoomFrame(content);
                Assert.That(frame.MetresPerUnit, Is.EqualTo(1f).Within(1e-5f), "RoomWorldMotion requires MetresPerUnit == 1 every frame");
                Assert.That(Vector3.Distance(content.up, Vector3.up), Is.LessThan(1e-5f), "content.up must stay Vector3.up");
            }

            Assert.That(flight.Mode, Is.EqualTo(CosmosLocomotionMode.FreeFlight));
            Assert.That(flight.FlightLocalPosition.y, Is.GreaterThan(0.25f));
            Assert.That(flight.ShouldFreezeViewpointWrites, Is.True);

            // 2. Two-hand pinch scale-dive + twist past 180 deg yaw
            Vector3 head = cam.transform.position;
            CosmosInputSnapshot pinchStart = new CosmosInputSnapshot
            {
                LeftTracked = true,
                RightTracked = true,
                LeftPinching = true,
                RightPinching = true,
                LeftPinchPos = head + new Vector3(-0.12f, -0.15f, 0.40f),
                RightPinchPos = head + new Vector3(0.12f, -0.15f, 0.40f)
            };
            flight.Step(1f / 60f, in pinchStart);

            double scaleBeforePinch = flight.ScaleState.ScaleExp;
            CosmosInputSnapshot pinchZoomIn = pinchStart;
            pinchZoomIn.LeftPinchPos = head + new Vector3(-0.24f, -0.15f, 0.40f);
            pinchZoomIn.RightPinchPos = head + new Vector3(0.24f, -0.15f, 0.40f);
            flight.Step(1f / 60f, in pinchZoomIn);
            Assert.That(flight.ScaleState.ScaleExp, Is.LessThan(scaleBeforePinch), "Widening pinch span must dive scale exponent");
            Assert.That(Vector3.Distance(content.up, Vector3.up), Is.LessThan(1e-5f));

            // 3. Auto-land back onto nearest island & verify walkable support + uncorrupted RoomFrame
            CosmosFocusTarget islandTarget = cosmos.Archipelago.AllFocusTargets[0];
            flight.FlyToFocusTarget(in islandTarget);
            for (int i = 0; i < 60 && flight.IsHomeSplineActive; i++)
            {
                CosmosInputSnapshot idle = default;
                flight.Step(0.05f, in idle);
            }

            // Yaw around past 180 deg before auto-landing
            CosmosInputSnapshot yawSpin = new CosmosInputSnapshot { RightStick = new Vector2(-1f, 0f) };
            for (int i = 0; i < 60; i++)
            {
                flight.Step(0.05f, in yawSpin);
            }

            // D4 regression: non-identity camera yaw (90 deg) before Auto-Landing must not snap content yaw via RestoreViewpoint
            cam.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            float maxLandingStepAngleDeg = 0f;
            float completionStepAngleDeg = -1f;

            flight.RequestAutoLanding();
            Assert.That(flight.Mode, Is.EqualTo(CosmosLocomotionMode.AutoLanding));
            for (int i = 0; i < 120 && flight.Mode != CosmosLocomotionMode.GroundedWalk; i++)
            {
                Quaternion prevRot = content.rotation;
                CosmosInputSnapshot idle = default;
                flight.Step(0.05f, in idle);
                float stepAngleDeg = Quaternion.Angle(prevRot, content.rotation);
                maxLandingStepAngleDeg = Mathf.Max(maxLandingStepAngleDeg, stepAngleDeg);
                if (flight.Mode == CosmosLocomotionMode.GroundedWalk)
                {
                    completionStepAngleDeg = stepAngleDeg;
                }

                var stepFrame = new RoomFrame(content);
                Assert.That(stepFrame.MetresPerUnit, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(Vector3.Distance(content.up, Vector3.up), Is.LessThan(1e-5f));
            }

            Assert.That(flight.Mode, Is.EqualTo(CosmosLocomotionMode.GroundedWalk));
            Assert.That(completionStepAngleDeg, Is.GreaterThanOrEqualTo(0f).And.LessThan(2f),
                "D4 regression: Auto-Landing RestoreViewpoint transition must preserve continuous content orientation at 90 deg head yaw");
            Assert.That(maxLandingStepAngleDeg, Is.LessThan(10f));
            Assert.That(flight.ScaleState.ScaleExp, Is.EqualTo(CosmosFlightController.ColdStartScaleExp).Within(1e-4));
            Assert.That(flight.ShouldFreezeViewpointWrites, Is.False);
            Assert.That(new RoomFrame(content).MetresPerUnit, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Vector3.Distance(content.up, Vector3.up), Is.LessThan(1e-5f));

            // Landed position must be walkable under RoomGroundQuery
            Physics.SyncTransforms();
            var ground = new RoomGroundQuery();
            ground.Capture(content, includeScan: false);
            Vector3 landedWorld = content.TransformPoint(flight.FlightLocalPosition);
            Assert.That(ground.Support(landedWorld, 0.20f, 0.4f, 0.4f, out _, out Vector3 normal, out string err), Is.True,
                $"Auto-landed pose {flight.FlightLocalPosition} must have walkable support: {err}");
            Assert.That(Vector3.Angle(normal, Vector3.up), Is.LessThanOrEqualTo(15f));

            // WorkspaceDefaults.Snapshot and current workspace starter entities remain unpolluted
            var snapshot = WorkspaceDefaults.Snapshot(null);
            JObject doc = JObject.Parse(System.Text.Encoding.UTF8.GetString(snapshot.Documents[RoomStorage.FileName]));
            Assert.That(doc["objects"].Select(x => (string)x["id"]), Is.EquivalentTo(new[] { "book", "maestro" }));
            Assert.That(workspace.Current.Editor.Snapshot().objects.Select(x => x.id), Is.EquivalentTo(beforeObjectIds));
        }

        [UnityTest]
        public IEnumerator AllNineCosmosMaterialsRenderOffscreenAndSupportStereoInstancingWithZeroErrors()
        {
            var shaderSpecs = AllNineCosmosShaders();

            var quadGo = Own(GameObject.CreatePrimitive(PrimitiveType.Quad));
            quadGo.layer = 31;
            var renderer = quadGo.GetComponent<MeshRenderer>();

            var camGo = Own(new GameObject("CosmosShaderRenderCam"));
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << 31;
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.transform.position = new Vector3(0f, 0f, -2f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var rt = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readTex = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
            RenderTexture prevActive = RenderTexture.active;
            var mpb = new MaterialPropertyBlock();

            try
            {
                for (int i = 0; i < shaderSpecs.Length; i++)
                {
                    var spec = shaderSpecs[i];
                    Material mat = Resources.Load<Material>(spec.ResPath);
                    Assert.That(mat, Is.Not.Null, $"Missing Resources material {spec.ResPath}");
                    Assert.That(mat.shader, Is.Not.Null);
                    Assert.That(mat.shader.name, Is.EqualTo(spec.ShaderName));
                    Assert.That(mat.shader.isSupported, Is.True, $"Shader {spec.ShaderName} unsupported");
                    if (spec.Instanced)
                    {
                        Assert.That(mat.enableInstancing, Is.True, $"{spec.ResPath} must support GPU instancing");
                    }

#if UNITY_EDITOR
                    Assert.That(ShaderUtil.ShaderHasError(mat.shader), Is.False,
                        $"ShaderUtil reported error on {spec.ShaderName}");
                    string shaderAssetPath = AssetDatabase.GetAssetPath(mat.shader);
                    Assert.That(string.IsNullOrEmpty(shaderAssetPath), Is.False);
                    string shaderSrc = File.ReadAllText(shaderAssetPath);
                    Assert.That(shaderSrc, Does.Contain("UNITY_VERTEX_OUTPUT_STEREO"),
                        $"Shader {spec.ShaderName} missing Single-Pass Stereo output macro");
                    if (spec.Instanced)
                    {
                        Assert.That(shaderSrc, Does.Contain("multi_compile_instancing"),
                            $"Instanced shader {spec.ShaderName} missing #pragma multi_compile_instancing");
                    }
#endif

                    mpb.Clear();
                    mpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
                    renderer.sharedMaterial = mat;
                    renderer.SetPropertyBlock(mpb);

                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    readTex.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                    readTex.Apply();
                    Color px = readTex.GetPixel(16, 16);
                    Assert.That(float.IsFinite(px.r) && float.IsFinite(px.g) && float.IsFinite(px.b) && float.IsFinite(px.a),
                        Is.True, $"Non-finite rendered pixel for shader {spec.ShaderName}");
                }
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(readTex);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator WristRingMaintainsAtLeast12CmClearanceFromBothPalmRecoveryButtons()
        {
            SpawnMaestroRoom(out GameObject roomGo, out _, out Camera cam, out VirtualRoomView view, out HyperCosmosWorld cosmos);

            Assert.That(view.Enter(), Is.True);
            yield return null;

            PalmRecoveryButton[] palmButtons = roomGo.GetComponentsInChildren<PalmRecoveryButton>(true);
            Assert.That(palmButtons.Length, Is.EqualTo(2), "Expected both left and right PalmRecoveryButtons in MaestroRoom");
            BoxCollider leftCol = palmButtons[0].GetComponent<BoxCollider>();
            BoxCollider rightCol = palmButtons[1].GetComponent<BoxCollider>();
            Assert.That(leftCol, Is.Not.Null);
            Assert.That(rightCol, Is.Not.Null);

            Transform trackingSpace = cam.transform.parent;
            Assert.That(trackingSpace, Is.Not.Null);
            CosmosWristRing ring = cosmos.WristRing;
            Assert.That(ring.BeadCount, Is.EqualTo(13));

            cam.transform.position = new Vector3(0f, 1.55f, 0f);
            cam.transform.rotation = Quaternion.identity;

            // Test multiple realistic arm/wrist orientations (palm-up near chest, extended forward, side watch gaze)
            var scenarios = new (Vector3 HeadPos, Vector3 LeftWristWorld, Vector3 LeftPalmWorld, Vector3 RightPalmWorld, Vector3 ProximalDir, Vector3 PalmNormal, CosmosLocomotionMode Mode)[]
            {
                (
                    new Vector3(0f, 1.55f, 0f),
                    new Vector3(-0.20f, 1.28f, 0.34f),
                    new Vector3(-0.20f, 1.30f, 0.41f),
                    new Vector3(0.20f, 1.30f, 0.41f),
                    new Vector3(0f, -0.34f, -0.94f).normalized,
                    new Vector3(0.32f, 0.65f, -0.69f).normalized,
                    CosmosLocomotionMode.FreeFlight
                ),
                (
                    new Vector3(0f, 1.55f, 0f),
                    new Vector3(-0.24f, 1.24f, 0.46f),
                    new Vector3(-0.24f, 1.27f, 0.53f),
                    new Vector3(0.24f, 1.27f, 0.53f),
                    new Vector3(-0.10f, -0.45f, -0.89f).normalized,
                    new Vector3(0.36f, 0.72f, -0.59f).normalized,
                    CosmosLocomotionMode.GroundedWalk
                ),
                (
                    new Vector3(0f, 1.55f, 0f),
                    new Vector3(-0.16f, 1.34f, 0.30f),
                    new Vector3(-0.15f, 1.36f, 0.37f),
                    new Vector3(0.22f, 1.28f, 0.42f),
                    new Vector3(-0.22f, -0.32f, -0.92f).normalized,
                    new Vector3(0.26f, 0.62f, -0.74f).normalized,
                    CosmosLocomotionMode.FreeFlight
                )
            };

            for (int s = 0; s < scenarios.Length; s++)
            {
                var sc = scenarios[s];
                cam.transform.position = sc.HeadPos;

                Vector3 leftPalmTracking = trackingSpace.InverseTransformPoint(sc.LeftPalmWorld);
                Vector3 rightPalmTracking = trackingSpace.InverseTransformPoint(sc.RightPalmWorld);

                var snap = new CosmosInputSnapshot
                {
                    LeftTracked = true,
                    RightTracked = true,
                    LeftUsingHand = true,
                    HasLeftWrist = true,
                    HasLeftPalm = true,
                    LeftWristPos = sc.LeftWristWorld,
                    LeftPalmPos = sc.LeftPalmWorld,
                    LeftPalmNormal = sc.PalmNormal,
                    LeftForearmProximalDir = sc.ProximalDir,
                    LeftWristRot = Quaternion.LookRotation(-sc.ProximalDir, sc.PalmNormal)
                };

                // Run several rapid steps while keeping SetPalmPose active before Update() clears it
                for (int step = 0; step < 8; step++)
                {
                    palmButtons[0].SetPalmPose(new Pose(leftPalmTracking, Quaternion.identity));
                    palmButtons[1].SetPalmPose(new Pose(rightPalmTracking, Quaternion.identity));
                    Physics.SyncTransforms();
                    ring.Step(0.05f, in snap, sc.Mode);
                }

                Assert.That(leftCol.enabled, Is.True);
                Assert.That(rightCol.enabled, Is.True);
                Assert.That(ring.SummonMode, Is.EqualTo(CosmosWristSummonMode.VrForearmPalmUp),
                    $"Scenario {s} should summon VrForearmPalmUp");

                Vector3 closestLeft = leftCol.ClosestPoint(ring.AnchorWorldPosition);
                Vector3 closestRight = rightCol.ClosestPoint(ring.AnchorWorldPosition);
                float leftClearance = ring.ComputeMinimumClearanceFromPoint(closestLeft);
                float rightClearance = ring.ComputeMinimumClearanceFromPoint(closestRight);

                Assert.That(leftClearance, Is.GreaterThanOrEqualTo(0.12f),
                    $"Scenario {s}: Left PalmRecoveryButton clearance {leftClearance:F3}m < 0.12m (12cm)");
                Assert.That(rightClearance, Is.GreaterThanOrEqualTo(0.12f),
                    $"Scenario {s}: Right PalmRecoveryButton clearance {rightClearance:F3}m < 0.12m (12cm)");

                for (int b = 0; b < ring.BeadCount; b++)
                {
                    CosmosWristBeadState bead = ring.GetBead(b);
                    Vector3 beadLeftPt = leftCol.ClosestPoint(bead.WorldPos);
                    Vector3 beadRightPt = rightCol.ClosestPoint(bead.WorldPos);
                    float dLeft = Vector3.Distance(bead.WorldPos, beadLeftPt) - bead.Radius;
                    float dRight = Vector3.Distance(bead.WorldPos, beadRightPt) - bead.Radius;
                    Assert.That(dLeft, Is.GreaterThanOrEqualTo(0.12f),
                        $"Scenario {s}, Bead {b} ({bead.Label}) too close to Left PalmRecoveryButton ({dLeft:F3}m)");
                    Assert.That(dRight, Is.GreaterThanOrEqualTo(0.12f),
                        $"Scenario {s}, Bead {b} ({bead.Label}) too close to Right PalmRecoveryButton ({dRight:F3}m)");
                }
            }
        }

        [UnityTest]
        public IEnumerator AudioEngineAllocatesAtMost6SourcesDisablesAcousticsDucksAndSilencesCleanly()
        {
            SpawnMaestroRoom(out GameObject roomGo, out _, out _, out _, out HyperCosmosWorld cosmos);
            CosmosAudioEngine audio = cosmos.Audio;
            Assert.That(audio, Is.Not.Null);

            yield return null;

            Assert.That(audio.PooledAudioSourceCount, Is.LessThanOrEqualTo(CosmosAudioEngine.MaxPooledSources));
            Assert.That(CosmosAudioEngine.MaxPooledSources, Is.EqualTo(6));
            AudioSource[] sources = audio.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources.Length, Is.LessThanOrEqualTo(6));
            Assert.That(sources.Length, Is.GreaterThanOrEqualTo(1));
            foreach (AudioSource src in sources)
            {
                // D1 regression: Meta XR Audio spatialization must run post-OnAudioFilterRead
                Assert.That(src.spatializePostEffects, Is.EqualTo(src.spatialize),
                    $"AudioSource {src.name} spatializePostEffects must match spatialize for Meta XR HRTF");
            }

            // Strictly preserve ProductionAcousticsTests invariants
            Assert.That(roomGo.GetComponentsInChildren<RoomAudioOutput>(true), Is.Empty);
            Assert.That(roomGo.GetComponentsInChildren<RoomAcousticMapScheduler>(true), Is.Empty);
            foreach (var spatializer in audio.GetComponentsInChildren<MetaXRAudioSource>(true))
            {
                Assert.That(spatializer.EnableAcoustics, Is.False, "CosmosAudioEngine must keep EnableAcoustics == false");
            }

            // Exercise DSP synth crossfade (slot 0 -> 1 -> 0) & -9 dB speech ducking regardless of headless batchmode driver
            audio.SetZone(110f, 1800f, 0.05f, 0.2f, (int)CosmosWaveform.Sine, 3);
            var pcm = new float[2048];
            for (int b = 0; b < 32; b++)
            {
                Array.Clear(pcm, 0, pcm.Length);
                audio.AmbientSynth.MixBlock(pcm, 1024, 2, narrationActive: true, sampleRate: 48000);
            }
            Assert.That(audio.CrossfadeProgress, Is.EqualTo(1f).Within(1e-3f));

            audio.SetZone(220f, 2400f, 0.08f, 0.25f, (int)CosmosWaveform.Triangle, 4);
            for (int b = 0; b < 32; b++)
            {
                Array.Clear(pcm, 0, pcm.Length);
                audio.AmbientSynth.MixBlock(pcm, 1024, 2, narrationActive: true, sampleRate: 48000);
            }
            Assert.That(audio.CrossfadeProgress, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(audio.ActiveDuckGain, Is.LessThan(0.42f), "Active narration should duck ambient toward -9 dB (~0.355)");
            for (int i = 0; i < pcm.Length; i++)
            {
                Assert.That(float.IsFinite(pcm[i]), Is.True);
            }

            audio.TriggerLockChime(Vector3.forward * 2f);
            Array.Clear(pcm, 0, pcm.Length);
            audio.ChimeSynth.MixBlock(pcm, 1024, 2, 48000);
            float chimePeak = 0f;
            for (int i = 0; i < pcm.Length; i++)
            {
                Assert.That(float.IsFinite(pcm[i]), Is.True);
                chimePeak = Mathf.Max(chimePeak, Mathf.Abs(pcm[i]));
            }
            Assert.That(chimePeak, Is.GreaterThan(0.001f));

            // Recover duck gain when narration ends
            for (int b = 0; b < 32; b++)
            {
                Array.Clear(pcm, 0, pcm.Length);
                audio.AmbientSynth.MixBlock(pcm, 1024, 2, narrationActive: false, sampleRate: 48000);
            }
            Assert.That(audio.ActiveDuckGain, Is.GreaterThan(0.92f));

            // Focus loss, pause, and destroy silence all pooled AudioSources
            audio.SendMessage("OnApplicationFocus", false);
            Assert.That(audio.IsMuted, Is.True);
            foreach (AudioSource src in sources)
            {
                Assert.That(src.mute, Is.True);
                Assert.That(src.isPlaying, Is.False);
            }

            audio.SendMessage("OnApplicationFocus", true);
            Assert.That(audio.IsMuted, Is.False);

            audio.SendMessage("OnApplicationPause", true);
            Assert.That(audio.IsMuted, Is.True);
            foreach (AudioSource src in sources)
            {
                Assert.That(src.mute, Is.True);
                Assert.That(src.isPlaying, Is.False);
            }

            UnityEngine.Object.DestroyImmediate(audio.gameObject);
            yield return null;
        }
    }
}
