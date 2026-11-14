using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maestro.Quest.Cosmos
{
    /// <summary>
    /// Builds and manages the walkable floating Cosmos Archipelago under Room content:
    /// a flush obsidian-crystalline Observatory Dais at y = 0.0 (r >= 4.2m), 5 thematic
    /// islands within r <= 20m (&lt; 22m &lt; 25m), stepped walkable crystalline tether bridges
    /// (slope <= 15 deg, riser <= 0.08m, width >= 1.2m), and glowing tether beam ribbons.
    /// Activated strictly when virtual mode is active so Maestro default/MR acoustics and
    /// RoomEditor documents remain completely unaffected.
    /// </summary>
    public sealed class CosmosArchipelagoBuilder : MonoBehaviour
    {
        public const float MaxArchipelagoRadius = 22.0f;
        public const float CentralDaisRadius = 4.30f;
        public const float StepRiserMeters = 0.072f;
        public const float BridgeWidthMeters = 1.56f;
        private const float SlabThickness = 0.24f;
        private const float StepThickness = 0.18f;

        private readonly List<BoxCollider> _walkableColliders = new(80);
        private readonly List<RoomWalkableSurface> _walkableSurfaces = new(80);
        private readonly List<CosmosIslandDescriptor> _islands = new(6);
        private readonly List<CosmosBridgeDescriptor> _bridges = new(5);
        private readonly List<CosmosFocusTarget> _focusTargets = new(36);
        private readonly List<Mesh> _ownedMeshes = new(12);
        private readonly List<Material> _ownedMaterials = new(4);

        private GameObject _archipelagoRoot;
        private Transform _roomContent;
        private Material _islandMaterial;
        private Material _beamMaterial;
        private MeshRenderer _beamRenderer;
        private MaterialPropertyBlock _sharedMpb;
        private bool _built;
        private bool _virtualModeActive;

        public bool VirtualModeActive => _virtualModeActive;
        public IReadOnlyList<CosmosIslandDescriptor> Islands => _islands;
        public IReadOnlyList<CosmosBridgeDescriptor> Bridges => _bridges;
        public IReadOnlyList<BoxCollider> AllWalkableColliders => _walkableColliders;
        public IReadOnlyList<CosmosFocusTarget> AllFocusTargets => _focusTargets;
        public Transform ArchipelagoTransform => _archipelagoRoot != null ? _archipelagoRoot.transform : null;

        public void Initialize(Transform roomContent)
        {
            if (roomContent == null)
            {
                return;
            }

            if (_built && _roomContent == roomContent)
            {
                return;
            }

            ClearBuilt();
            _roomContent = roomContent;
            BuildAll();
            SetVirtualModeActive(_virtualModeActive);
        }

        public void SetVirtualModeActive(bool active)
        {
            _virtualModeActive = active;
            if (!_built && active && _roomContent != null)
            {
                BuildAll();
            }

            if (_archipelagoRoot == null)
            {
                return;
            }

            if (_archipelagoRoot.activeSelf != active)
            {
                _archipelagoRoot.SetActive(active);
            }

            for (int i = 0; i < _walkableColliders.Count; i++)
            {
                BoxCollider box = _walkableColliders[i];
                if (box != null)
                {
                    box.enabled = active;
                }
            }

            if (active)
            {
                for (int i = 0; i < _walkableSurfaces.Count && i < _walkableColliders.Count; i++)
                {
                    RoomWalkableSurface surface = _walkableSurfaces[i];
                    BoxCollider box = _walkableColliders[i];
                    if (surface != null && box != null)
                    {
                        surface.Publish(box);
                    }
                }
            }
        }

        public void UpdateVisualPulse(float timeSec, float activeBridgeFocus01)
        {
            if (!_virtualModeActive || _beamRenderer == null)
            {
                return;
            }

            _sharedMpb ??= new MaterialPropertyBlock();
            _beamRenderer.GetPropertyBlock(_sharedMpb);
            _sharedMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
            _sharedMpb.SetFloat(CosmosShaderIds.ActiveFocus, activeBridgeFocus01);
            _beamRenderer.SetPropertyBlock(_sharedMpb);
        }

        public bool FindNearestLandingSurface(
            Vector3 contentLocalPos,
            out Vector3 landingPoint,
            out Vector3 landingNormal)
        {
            return FindNearestLandingSurface(contentLocalPos, out landingPoint, out landingNormal, out _);
        }

        public bool FindNearestLandingSurface(
            Vector3 contentLocalPos,
            out Vector3 landingPoint,
            out Vector3 landingNormal,
            out byte islandIndex)
        {
            landingPoint = Vector3.zero;
            landingNormal = Vector3.up;
            islandIndex = 0;

            if (_islands.Count == 0)
            {
                return false;
            }

            // Prefer walkable colliders when active for exact step/platform landing
            if (_virtualModeActive && _walkableColliders.Count > 0)
            {
                float bestDistSq = float.MaxValue;
                Vector3 bestPoint = Vector3.zero;
                bool found = false;

                Vector3 worldQuery = _roomContent != null
                    ? _roomContent.TransformPoint(contentLocalPos)
                    : contentLocalPos;

                for (int i = 0; i < _walkableColliders.Count; i++)
                {
                    BoxCollider box = _walkableColliders[i];
                    if (box == null || !box.enabled || !box.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Transform boxTx = box.transform;
                    Vector3 boxSpaceQuery = boxTx.InverseTransformPoint(worldQuery);
                    float safeHx = Mathf.Max(0f, box.size.x * 0.5f - 0.25f);
                    float safeHz = Mathf.Max(0f, box.size.z * 0.5f - 0.25f);
                    Vector3 clampedBoxLocal = new Vector3(
                        Mathf.Clamp(boxSpaceQuery.x - box.center.x, -safeHx, safeHx) + box.center.x,
                        box.center.y + box.size.y * 0.5f,
                        Mathf.Clamp(boxSpaceQuery.z - box.center.z, -safeHz, safeHz) + box.center.z);

                    Vector3 safeWorld = boxTx.TransformPoint(clampedBoxLocal);
                    Vector3 closestLocal = _roomContent != null
                        ? _roomContent.InverseTransformPoint(safeWorld)
                        : safeWorld;

                    float dx = closestLocal.x - contentLocalPos.x;
                    float dz = closestLocal.z - contentLocalPos.z;
                    float dy = closestLocal.y - contentLocalPos.y;
                    float distSq = dx * dx + dz * dz + dy * dy * 0.25f;

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestPoint = closestLocal;
                        found = true;
                    }
                }

                if (found)
                {
                    landingPoint = bestPoint;
                    islandIndex = FindClosestIslandIndex(bestPoint);
                    return true;
                }
            }

            // Analytical island fallback when archipelago is inactive
            byte bestIsland = FindClosestIslandIndex(contentLocalPos);
            CosmosIslandDescriptor island = _islands[bestIsland];
            Vector2 delta = new Vector2(contentLocalPos.x - island.CenterLocal.x, contentLocalPos.z - island.CenterLocal.z);
            float maxR = Mathf.Max(0.2f, island.WalkableRadius - 0.35f);
            if (delta.sqrMagnitude > maxR * maxR)
            {
                delta = delta.normalized * maxR;
            }

            landingPoint = new Vector3(island.CenterLocal.x + delta.x, island.TopY, island.CenterLocal.z + delta.y);
            islandIndex = bestIsland;
            return true;
        }

        private byte FindClosestIslandIndex(Vector3 localPos)
        {
            byte best = 0;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < _islands.Count; i++)
            {
                Vector3 c = _islands[i].CenterLocal;
                float dx = c.x - localPos.x;
                float dz = c.z - localPos.z;
                float d = dx * dx + dz * dz;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    best = _islands[i].IslandIndex;
                }
            }

            return best;
        }

        private void BuildAll()
        {
            if (_roomContent == null)
            {
                return;
            }

            _archipelagoRoot = new GameObject("Cosmos Archipelago");
            _archipelagoRoot.transform.SetParent(_roomContent, false);
            _archipelagoRoot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _archipelagoRoot.transform.localScale = Vector3.one;

            ResolveMaterials();
            PopulateIslandDescriptors();
            BuildObservatoryDais();
            BuildFloatingIslands();
            BuildTetherBridgesAndRibbons();
            BuildFocusTargets();
            _built = true;
        }

        private void ResolveMaterials()
        {
            _islandMaterial = CosmosShaderIds.ResolveMaterial(
                CosmosShaderIds.ResCrystallineIslandMat,
                CosmosShaderIds.ShaderCrystallineIsland,
                new Color(0.03f, 0.05f, 0.09f, 1f));
            TrackOwnedFallbackMaterial(_islandMaterial);

            _beamMaterial = CosmosShaderIds.ResolveMaterial(
                CosmosShaderIds.ResTetherBridgeBeamMat,
                CosmosShaderIds.ShaderTetherBridgeBeam,
                new Color(0.28f, 0.72f, 1.15f, 1f));
            TrackOwnedFallbackMaterial(_beamMaterial);
        }

        private void TrackOwnedFallbackMaterial(Material mat)
        {
            if (mat != null && mat.name.Contains("(RuntimeFallback)") && !_ownedMaterials.Contains(mat))
            {
                mat.hideFlags = HideFlags.DontSave;
                _ownedMaterials.Add(mat);
            }
        }

        private void PopulateIslandDescriptors()
        {
            _islands.Clear();

            // Island 0: Central Earth Observatory Dais flush at y = 0.0 (r >= 4.2m)
            _islands.Add(new CosmosIslandDescriptor(
                0,
                "ObservatoryDais",
                "Observatorio Tierra Central",
                "Central Earth Observatory Dais",
                "Keskus-Maa-observatorio",
                Vector3.zero,
                0.0f,
                CentralDaisRadius,
                CentralDaisRadius + 0.35f,
                new Color(0.24f, 0.78f, 1.38f, 1f),
                3, 4, 18));

            // 5 thematic islands in a sweeping pentagonal crown within r <= 19.9m (< 22m < 25m)
            AddThematicIsland(1, "SolarSystemTerrace", "Terraza del Sistema Solar", "Solar System & Rings Terrace", "Aurinkokunnan terassi", 72f, 13.8f, 9, 2.65f, new Color(1.45f, 0.92f, 0.34f, 1f), 4, 3, 20);
            AddThematicIsland(2, "GalacticCosmicWebOverlook", "Mirador Galáctico y Red Cósmica", "Galactic & Cosmic Web Overlook", "Galaktinen näköalatasanne", 144f, 15.2f, 13, 2.70f, new Color(0.75f, 0.72f, 1.48f, 1f), 5, 6, 8);
            AddThematicIsland(3, "QuantumColorPrismSpire", "Aguja Cuántica y Prisma Cromático", "Quantum & Color Prism Spire", "Kvantti- ja väriprismahuippu", -72f, 13.5f, 7, 2.65f, new Color(1.28f, 0.50f, 1.55f, 1f), 1, 13, 14);
            AddThematicIsland(4, "LifeMindHarpSanctuary", "Santuario de Vida, Mente y Arpa", "Life, Mind & Harmonic Harp Sanctuary", "Elämän, mielen ja harpun pyhättö", -144f, 14.6f, 11, 2.65f, new Color(0.32f, 1.38f, 0.88f, 1f), 2, 16, 17);
            AddThematicIsland(5, "CivilizationSetiHorizonLotus", "Horizonte Civilización, SETI y Loto", "Civilization, SETI Contact & Horizon Lotus", "Sivilisaatio, SETI ja Horisonttilootus", 0f, 16.0f, 15, 2.75f, new Color(1.45f, 1.25f, 0.88f, 1f), 22, 25, 19);
        }

        private void AddThematicIsland(
            byte index,
            string name,
            string titleEs,
            string titleEn,
            string titleFi,
            float azimuthDeg,
            float radialDistanceMeters,
            int bridgeStepRiseCount,
            float walkableApothemMeters,
            Color accentLinear,
            byte exp0,
            byte exp1,
            byte exp2)
        {
            float rad = azimuthDeg * Mathf.Deg2Rad;
            float topY = bridgeStepRiseCount * StepRiserMeters;
            Vector3 center = new Vector3(
                Mathf.Sin(rad) * radialDistanceMeters,
                topY,
                Mathf.Cos(rad) * radialDistanceMeters);

            float cornerR = radialDistanceMeters + walkableApothemMeters * 1.4142135f;
            if (!RoomViewpoint.ValidPosition(new Vector3(center.x, 0f, center.z)) || cornerR > MaxArchipelagoRadius)
            {
                radialDistanceMeters = Mathf.Min(radialDistanceMeters, MaxArchipelagoRadius - walkableApothemMeters * 1.45f);
                center = new Vector3(Mathf.Sin(rad) * radialDistanceMeters, topY, Mathf.Cos(rad) * radialDistanceMeters);
            }

            _islands.Add(new CosmosIslandDescriptor(
                index,
                name,
                titleEs,
                titleEn,
                titleFi,
                center,
                topY,
                walkableApothemMeters,
                walkableApothemMeters * 1.15f,
                accentLinear,
                exp0,
                exp1,
                exp2));
        }

        private void BuildObservatoryDais()
        {
            CosmosIslandDescriptor dais = _islands[0];
            CreateOctagonWalkablePlatform(
                "ObservatoryDais",
                dais.CenterLocal,
                Quaternion.identity,
                dais.WalkableRadius,
                dais.AccentLinear,
                isCentralDais: true);
        }

        private void BuildFloatingIslands()
        {
            for (int i = 1; i < _islands.Count; i++)
            {
                CosmosIslandDescriptor island = _islands[i];
                Vector3 radialDir = new Vector3(island.CenterLocal.x, 0f, island.CenterLocal.z).normalized;
                Quaternion facing = Quaternion.LookRotation(radialDir, Vector3.up);
                CreateOctagonWalkablePlatform(
                    $"Island_{island.IslandIndex}_{island.Name}",
                    island.CenterLocal,
                    facing,
                    island.WalkableRadius,
                    island.AccentLinear,
                    isCentralDais: false);
            }
        }

        private void CreateOctagonWalkablePlatform(
            string goName,
            Vector3 topCenter,
            Quaternion platformRotation,
            float apothemMeters,
            Color rimColor,
            bool isCentralDais)
        {
            var root = new GameObject(goName);
            root.transform.SetParent(_archipelagoRoot.transform, false);
            root.transform.SetLocalPositionAndRotation(topCenter, platformRotation);
            root.layer = RoomPhysicsLayers.Environment;

            // Four rotated strips (0, 45, 90, 135 deg) form an exact regular octagon floor
            // flush at local y = 0.0f matching CrystalDeckVisual without corner overhang.
            float side = apothemMeters * 2f;
            float stripWidth = side * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            Vector3 stripSize = new Vector3(stripWidth, SlabThickness, side);
            for (int stripIndex = 0; stripIndex < 4; stripIndex++)
            {
                int angleDeg = stripIndex * 45;
                CreateWalkableBoxChild(
                    root.transform,
                    $"WalkableBox_{angleDeg:D3}",
                    Vector3.zero,
                    Quaternion.Euler(0f, angleDeg, 0f),
                    stripSize);
            }

            // Single crystalline visual slab slightly recessed -0.002m under walkable plane to avoid z-fighting
            Mesh slabMesh = BuildOctagonCrystalSlabMesh(apothemMeters, SlabThickness, isCentralDais);
            _ownedMeshes.Add(slabMesh);

            var visualGo = new GameObject("CrystalDeckVisual");
            visualGo.transform.SetParent(root.transform, false);
            visualGo.transform.localPosition = new Vector3(0f, -0.002f, 0f);
            var mf = visualGo.AddComponent<MeshFilter>();
            mf.sharedMesh = slabMesh;

            var mr = visualGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _islandMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(CosmosShaderIds.Color, new Color(0.015f, 0.022f, 0.040f, 1f));
            mpb.SetColor(CosmosShaderIds.RimColor, rimColor);
            mpb.SetFloat(CosmosShaderIds.RimPower, isCentralDais ? 3.4f : 2.8f);
            mpb.SetFloat(CosmosShaderIds.GridScale, isCentralDais ? 2.4f : 3.0f);
            mpb.SetFloat(CosmosShaderIds.WorldLighting, 0.35f);
            mpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
            mr.SetPropertyBlock(mpb);
        }

        private void CreateWalkableBoxChild(
            Transform parent,
            string childName,
            Vector3 topCenterLocal,
            Quaternion localRot,
            Vector3 boxSize)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = topCenterLocal;
            go.transform.localRotation = localRot;
            go.layer = RoomPhysicsLayers.Environment;

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -boxSize.y * 0.5f, 0f);
            box.size = new Vector3(
                Mathf.Max(0.05f, boxSize.x),
                Mathf.Max(0.05f, boxSize.y),
                Mathf.Max(0.05f, boxSize.z));
            box.isTrigger = false;

            var surface = go.AddComponent<RoomWalkableSurface>();
            _walkableColliders.Add(box);
            _walkableSurfaces.Add(surface);
        }

        private void BuildTetherBridgesAndRibbons()
        {
            _bridges.Clear();
            var bridgesParent = new GameObject("WalkableTetherBridges");
            bridgesParent.transform.SetParent(_archipelagoRoot.transform, false);
            bridgesParent.layer = RoomPhysicsLayers.Environment;

            // Combine all 5 bridges' step visuals and all 10 tether spline ribbons into 2 low-draw meshes
            var stepVerts = new List<Vector3>(2048);
            var stepNormals = new List<Vector3>(2048);
            var stepUvs = new List<Vector2>(2048);
            var stepTris = new List<int>(4096);

            var ribbonVerts = new List<Vector3>(1024);
            var ribbonUvs = new List<Vector2>(1024);
            var ribbonColors = new List<Color>(1024);
            var ribbonTris = new List<int>(2048);

            for (int i = 1; i < _islands.Count; i++)
            {
                CosmosIslandDescriptor island = _islands[i];
                Vector3 radialDir = new Vector3(island.CenterLocal.x, 0f, island.CenterLocal.z).normalized;
                Quaternion facing = Quaternion.LookRotation(radialDir, Vector3.up);

                float rStart = CentralDaisRadius - 0.12f;
                float rEnd = new Vector2(island.CenterLocal.x, island.CenterLocal.z).magnitude - (island.WalkableRadius - 0.16f);
                float span = Mathf.Max(2.0f, rEnd - rStart);

                int riserCount = Mathf.Max(2, Mathf.RoundToInt(island.TopY / StepRiserMeters));
                float actualRiser = island.TopY / riserCount;
                int treadCount = riserCount - 1; // Step 1..treadCount bridge between y=0 and island.TopY
                float pitch = span / treadCount;
                float treadLen = pitch + 0.08f; // 8 cm front-to-back overlap avoids hairline collider gaps
                float slopeDeg = Mathf.Atan2(actualRiser, pitch) * Mathf.Rad2Deg;

                Vector3 startTop = radialDir * rStart;
                Vector3 endTop = radialDir * rEnd + Vector3.up * island.TopY;

                _bridges.Add(new CosmosBridgeDescriptor(
                    (byte)(i - 1),
                    0,
                    island.IslandIndex,
                    startTop,
                    endTop,
                    BridgeWidthMeters,
                    treadCount,
                    actualRiser,
                    slopeDeg,
                    island.AccentLinear));

                for (int s = 1; s <= treadCount; s++)
                {
                    float rCenter = rStart + (s - 0.5f) * pitch;
                    Vector3 stepTopPos = radialDir * rCenter + Vector3.up * (s * actualRiser);
                    Vector3 boxSize = new Vector3(BridgeWidthMeters, StepThickness, treadLen);

                    CreateWalkableBoxChild(
                        bridgesParent.transform,
                        $"Bridge_{island.IslandIndex}_Step_{s}",
                        stepTopPos,
                        facing,
                        boxSize);

                    AppendBoxToMesh(
                        stepVerts, stepNormals, stepUvs, stepTris,
                        stepTopPos - Vector3.up * 0.002f,
                        facing,
                        new Vector3(BridgeWidthMeters - 0.02f, 0.09f, treadLen - 0.02f));
                }

                // Append two additive spline tether ribbons flanking left & right bridge edges (no colliders)
                Color ribbonTint = island.AccentLinear;
                ribbonTint.a = 1f;
                AppendBridgeRibbon(ribbonVerts, ribbonUvs, ribbonColors, ribbonTris, startTop, endTop, facing, -(BridgeWidthMeters * 0.5f + 0.09f), ribbonTint);
                AppendBridgeRibbon(ribbonVerts, ribbonUvs, ribbonColors, ribbonTris, startTop, endTop, facing, +(BridgeWidthMeters * 0.5f + 0.09f), ribbonTint);
            }

            // Combined steps visual mesh (1 draw call)
            var stepsVisualMesh = new Mesh { name = "CosmosBridgeStepsCombined", hideFlags = HideFlags.DontSave };
            stepsVisualMesh.SetVertices(stepVerts);
            stepsVisualMesh.SetNormals(stepNormals);
            stepsVisualMesh.SetUVs(0, stepUvs);
            stepsVisualMesh.SetTriangles(stepTris, 0);
            stepsVisualMesh.RecalculateBounds();
            _ownedMeshes.Add(stepsVisualMesh);

            var stepsGo = new GameObject("BridgeStepsVisual");
            stepsGo.transform.SetParent(bridgesParent.transform, false);
            stepsGo.AddComponent<MeshFilter>().sharedMesh = stepsVisualMesh;
            var stepsMr = stepsGo.AddComponent<MeshRenderer>();
            stepsMr.sharedMaterial = _islandMaterial;
            stepsMr.shadowCastingMode = ShadowCastingMode.Off;
            stepsMr.receiveShadows = false;
            var stepsMpb = new MaterialPropertyBlock();
            stepsMpb.SetColor(CosmosShaderIds.Color, new Color(0.020f, 0.035f, 0.065f, 1f));
            stepsMpb.SetColor(CosmosShaderIds.RimColor, new Color(0.42f, 0.95f, 1.45f, 1f));
            stepsMpb.SetFloat(CosmosShaderIds.RimPower, 2.6f);
            stepsMpb.SetFloat(CosmosShaderIds.GridScale, 3.6f);
            stepsMpb.SetFloat(CosmosShaderIds.WorldLighting, 0.30f);
            stepsMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
            stepsMr.SetPropertyBlock(stepsMpb);

            // Combined glowing tether spline beams mesh (1 draw call)
            var ribbonsMesh = new Mesh { name = "CosmosTetherRibbonsCombined", hideFlags = HideFlags.DontSave };
            ribbonsMesh.SetVertices(ribbonVerts);
            ribbonsMesh.SetUVs(0, ribbonUvs);
            ribbonsMesh.SetColors(ribbonColors);
            ribbonsMesh.SetTriangles(ribbonTris, 0);
            ribbonsMesh.RecalculateNormals();
            ribbonsMesh.RecalculateBounds();
            _ownedMeshes.Add(ribbonsMesh);

            var beamGo = new GameObject("TetherBridgeBeamsVisual");
            beamGo.transform.SetParent(bridgesParent.transform, false);
            beamGo.AddComponent<MeshFilter>().sharedMesh = ribbonsMesh;
            _beamRenderer = beamGo.AddComponent<MeshRenderer>();
            _beamRenderer.sharedMaterial = _beamMaterial;
            _beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _beamRenderer.receiveShadows = false;
            var beamMpb = new MaterialPropertyBlock();
            beamMpb.SetColor(CosmosShaderIds.Color, new Color(0.35f, 0.88f, 1.45f, 1f));
            beamMpb.SetFloat(CosmosShaderIds.PulseSpeed, 1.25f);
            beamMpb.SetFloat(CosmosShaderIds.ActiveFocus, 0.35f);
            beamMpb.SetFloat(CosmosShaderIds.TimeSec, 0f);
            _beamRenderer.SetPropertyBlock(beamMpb);
        }

        private void BuildFocusTargets()
        {
            _focusTargets.Clear();
            int nextId = 1;

            // Hero Earth Sphere off the NW quadrant (clear of all bridges & starter objects)
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.PlanetBody, 3, 2, 0,
                new Vector3(-5.8f, 2.8f, 8.6f), 4.2f, 0.95f);

            // Solar System planets along sunlit Eastern sky arc above Island 1
            CosmosIslandDescriptor island1 = _islands[1];
            Vector3 sunPos = island1.CenterLocal + new Vector3(1.6f, 2.0f, 1.2f);
            AddCustomTarget(ref nextId, CosmosFocusTargetKind.PlanetBody, 4, 0, 1, "solar-sun",
                "Sol", "Sun", "Aurinko", sunPos, 2.6f, 11.2, 0.92f,
                CosmosCatalogData.GetExperience(4).HdrAccentLinear, 0);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.PlanetBody, 4, 3, 1,
                island1.CenterLocal + new Vector3(-0.2f, 1.45f, -0.4f), 2.0f, 0.82f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.PlanetBody, 4, 7, 1,
                island1.CenterLocal + new Vector3(1.8f, 1.65f, -0.7f), 2.4f, 0.90f);

            // Island 2: Galactic & Cosmic Web Exps 05..10 + Great Attractor
            CosmosIslandDescriptor island2 = _islands[2];
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.GalacticLandmark, 5, 10, 2,
                island2.CenterLocal + new Vector3(1.1f, 1.55f, -1.2f), 2.8f, 0.92f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.GalacticLandmark, 6, 6, 2,
                island2.CenterLocal + new Vector3(2.2f, 2.35f, -2.1f), 2.6f, 0.85f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.GalacticLandmark, 7, 7, 2,
                island2.CenterLocal + new Vector3(0.4f, 2.75f, -2.5f), 2.6f, 0.82f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 8, 2, 2,
                island2.CenterLocal + new Vector3(-1.3f, 2.0f, -1.5f), 2.8f, 0.80f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 9, 8, 2,
                island2.CenterLocal + new Vector3(-1.9f, 3.1f, -2.4f), 3.0f, 0.78f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 10, 7, 2,
                island2.CenterLocal + new Vector3(0.8f, 3.6f, -3.2f), 3.0f, 0.76f);

            // Island 3: Colors Prism (01), Matter & Energy (13), Spacetime (14)
            CosmosIslandDescriptor island3 = _islands[3];
            CosmosExperienceMeta exp01 = CosmosCatalogData.GetExperience(1);
            AddCustomTarget(ref nextId, CosmosFocusTargetKind.SensoryArtifact, 1, 0, 3, exp01.Slug,
                exp01.LabelEs, exp01.LabelEn, exp01.LabelFi,
                island3.CenterLocal + new Vector3(-1.2f, 1.40f, 0.4f), 2.2f, exp01.FocusScaleExp, 0.92f,
                exp01.HdrAccentLinear, -1);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 13, 3, 3,
                island3.CenterLocal + new Vector3(0.2f, 1.85f, -1.1f), 2.4f, 0.88f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 14, 7, 3,
                island3.CenterLocal + new Vector3(-1.6f, 2.45f, 1.4f), 2.4f, 0.86f);

            // Island 4: Music Cymatic Harp (02), Complexity (15), Life DNA (16), Mind (17)
            CosmosIslandDescriptor island4 = _islands[4];
            CosmosExperienceMeta exp02 = CosmosCatalogData.GetExperience(2);
            AddCustomTarget(ref nextId, CosmosFocusTargetKind.SensoryArtifact, 2, 0, 4, exp02.Slug,
                exp02.LabelEs, exp02.LabelEn, exp02.LabelFi,
                island4.CenterLocal + new Vector3(-1.1f, 1.40f, -1.0f), 2.2f, exp02.FocusScaleExp, 0.92f,
                exp02.HdrAccentLinear, -1);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 15, 6, 4,
                island4.CenterLocal + new Vector3(-2.1f, 1.90f, -0.2f), 2.2f, 0.80f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 16, 7, 4,
                island4.CenterLocal + new Vector3(0.4f, 1.65f, -1.7f), 2.4f, 0.90f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 17, 9, 4,
                island4.CenterLocal + new Vector3(-0.6f, 2.35f, -2.1f), 2.4f, 0.86f);

            // Island 5: Culture (18), Tech (19), Futures (20), Cosmic Life (21), Contact/SETI (22),
            // and Hyper-Horizon Lotus Wings (11, 12, 23, 24, 25)
            CosmosIslandDescriptor island5 = _islands[5];
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 18, 13, 5,
                island5.CenterLocal + new Vector3(-1.5f, 1.45f, 0.8f), 2.2f, 0.80f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 19, 16, 5,
                island5.CenterLocal + new Vector3(1.5f, 1.45f, 0.8f), 2.2f, 0.82f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 20, 16, 5,
                island5.CenterLocal + new Vector3(-2.0f, 2.20f, 1.8f), 2.2f, 0.78f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ExperienceCore, 21, 13, 5,
                island5.CenterLocal + new Vector3(2.0f, 2.20f, 1.8f), 2.2f, 0.82f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ContinuityBeacon, 22, 12, 5,
                island5.CenterLocal + new Vector3(0.0f, 1.75f, 1.5f), 2.6f, 0.94f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ContinuityBeacon, 11, 4, 5,
                island5.CenterLocal + new Vector3(-1.4f, 3.10f, 2.4f), 2.5f, 0.78f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ContinuityBeacon, 12, 6, 5,
                island5.CenterLocal + new Vector3(-0.7f, 3.30f, 2.6f), 2.5f, 0.78f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ContinuityBeacon, 23, 8, 5,
                island5.CenterLocal + new Vector3(0.7f, 3.30f, 2.6f), 2.5f, 0.78f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ContinuityBeacon, 24, 24, 5,
                island5.CenterLocal + new Vector3(1.4f, 3.10f, 2.4f), 2.5f, 0.80f);
            AddExperienceTarget(ref nextId, CosmosFocusTargetKind.ContinuityBeacon, 25, 24, 5,
                island5.CenterLocal + new Vector3(0.0f, 2.85f, 2.1f), 3.0f, 0.96f);
        }

        private void AddExperienceTarget(
            ref int nextId,
            CosmosFocusTargetKind kind,
            byte expId,
            byte preferredZoneIndex,
            byte islandIndex,
            Vector3 localPos,
            float focusRadius,
            float salience)
        {
            CosmosExperienceMeta exp = CosmosCatalogData.GetExperience(expId);
            string labelEs = exp.LabelEs;
            string labelEn = exp.LabelEn;
            string labelFi = exp.LabelFi;
            CosmosZoneSoundRecipe sound = exp.RepresentativeSound;
            int zoneRecIdx = FindZoneRecordIndex(expId, preferredZoneIndex);

            if (zoneRecIdx >= 0)
            {
                CosmosZoneEntry z = CosmosCatalogData.FlagshipZones[zoneRecIdx];
                labelEs = z.Name.Es;
                labelEn = z.Name.En;
                labelFi = z.Name.Fi;
                sound = z.Sound;
            }

            AddCustomTarget(
                ref nextId,
                kind,
                expId,
                preferredZoneIndex,
                islandIndex,
                exp.Slug,
                labelEs,
                labelEn,
                labelFi,
                localPos,
                focusRadius,
                exp.FocusScaleExp,
                salience,
                exp.HdrAccentLinear,
                zoneRecIdx,
                sound);
        }

        private void AddCustomTarget(
            ref int nextId,
            CosmosFocusTargetKind kind,
            byte expId,
            byte zoneIndex,
            byte islandIndex,
            string slug,
            string labelEs,
            string labelEn,
            string labelFi,
            Vector3 localPos,
            float focusRadius,
            double focusScaleExp,
            float salience,
            Color accent,
            int zoneRecIdx,
            CosmosZoneSoundRecipe? customSound = null)
        {
            CosmosZoneSoundRecipe sound = customSound ?? CosmosCatalogData.GetExperience(expId).RepresentativeSound;
            _focusTargets.Add(new CosmosFocusTarget(
                nextId++,
                kind,
                expId,
                zoneIndex,
                islandIndex,
                slug,
                labelEs,
                labelEn,
                labelFi,
                localPos,
                focusRadius,
                focusScaleExp,
                salience,
                accent,
                sound,
                zoneRecIdx));
        }

        private static int FindZoneRecordIndex(byte expId, byte zoneIndex)
        {
            CosmosCatalogData.GetFlagshipZones(expId, out int start, out int count);
            int bestIdx = -1;
            int bestDelta = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                int delta = Mathf.Abs(CosmosCatalogData.FlagshipZones[idx].ZoneIndex - zoneIndex);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    bestIdx = idx;
                }
            }

            return bestIdx;
        }

        private static Mesh BuildOctagonCrystalSlabMesh(float apothemMeters, float thickness, bool chamferUndercut)
        {
            // Regular octagon top (y=0) & tapered obsidian crystal belly (y=-thickness)
            float topR = apothemMeters / Mathf.Cos(22.5f * Mathf.Deg2Rad);
            float bottomR = chamferUndercut ? topR * 0.76f : topR * 0.64f;
            const int sides = 8;

            var verts = new List<Vector3>(sides * 4 + 2);
            var normals = new List<Vector3>(sides * 4 + 2);
            var uvs = new List<Vector2>(sides * 4 + 2);
            var tris = new List<int>(sides * 12);

            // Top cap (0 = center, 1..8 = perimeter)
            verts.Add(Vector3.zero);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                float angle = (i + 0.5f) * (2f * Mathf.PI / sides);
                float x = Mathf.Cos(angle) * topR;
                float z = Mathf.Sin(angle) * topR;
                verts.Add(new Vector3(x, 0f, z));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f + x / (2f * topR), 0.5f + z / (2f * topR)));
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris.Add(0);
                tris.Add(1 + next);
                tris.Add(1 + i);
            }

            // Crystal faceted skirt walls
            int skirtStart = verts.Count;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i + 0.5f) * (2f * Mathf.PI / sides);
                float a1 = (i + 1.5f) * (2f * Mathf.PI / sides);
                float amid = 0.5f * (a0 + a1);
                Vector3 n = new Vector3(Mathf.Cos(amid), -0.35f, Mathf.Sin(amid)).normalized;

                Vector3 t0 = new Vector3(Mathf.Cos(a0) * topR, 0f, Mathf.Sin(a0) * topR);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * topR, 0f, Mathf.Sin(a1) * topR);
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * bottomR, -thickness, Mathf.Sin(a0) * bottomR);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * bottomR, -thickness, Mathf.Sin(a1) * bottomR);

                int idx = verts.Count;
                verts.Add(t0); verts.Add(t1); verts.Add(b1); verts.Add(b0);
                normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
                uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0f, 0f));

                tris.Add(idx + 0); tris.Add(idx + 1); tris.Add(idx + 2);
                tris.Add(idx + 0); tris.Add(idx + 2); tris.Add(idx + 3);
            }

            // Underside inverted pyramid tip for zero open holes from below in free flight
            int apexIdx = verts.Count;
            float bellyDrop = chamferUndercut ? -thickness * 1.45f : -thickness * 2.25f;
            verts.Add(new Vector3(0f, bellyDrop, 0f));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                int bIdx = skirtStart + i * 4;
                tris.Add(apexIdx);
                tris.Add(bIdx + 3);
                tris.Add(bIdx + 2);
            }

            var mesh = new Mesh { name = chamferUndercut ? "CosmosDaisCrystal" : "CosmosIslandCrystal", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendBoxToMesh(
            List<Vector3> verts,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> tris,
            Vector3 topCenter,
            Quaternion rot,
            Vector3 size)
        {
            float hx = size.x * 0.5f;
            float hy = size.y;
            float hz = size.z * 0.5f;
            Vector3 origin = topCenter;

            // Top + 4 vertical faces (no bottom face needed; steps float cleanly or keep 5 faces)
            AddQuad(verts, normals, uvs, tris, origin, rot, Vector3.up,
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz));
            AddQuad(verts, normals, uvs, tris, origin, rot, Vector3.back,
                new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, -hz));
            AddQuad(verts, normals, uvs, tris, origin, rot, Vector3.forward,
                new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz));
            AddQuad(verts, normals, uvs, tris, origin, rot, Vector3.left,
                new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz));
            AddQuad(verts, normals, uvs, tris, origin, rot, Vector3.right,
                new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz));
            AddQuad(verts, normals, uvs, tris, origin, rot, Vector3.down,
                new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz));
        }

        private static void AddQuad(
            List<Vector3> verts,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> tris,
            Vector3 origin,
            Quaternion rot,
            Vector3 localNormal,
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int idx = verts.Count;
            Vector3 wn = rot * localNormal;
            verts.Add(origin + rot * p0);
            verts.Add(origin + rot * p1);
            verts.Add(origin + rot * p2);
            verts.Add(origin + rot * p3);
            normals.Add(wn); normals.Add(wn); normals.Add(wn); normals.Add(wn);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
            tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
        }

        private static void AppendBridgeRibbon(
            List<Vector3> verts,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> tris,
            Vector3 startTop,
            Vector3 endTop,
            Quaternion facing,
            float lateralOffsetX,
            Color tint)
        {
            const int segments = 14;
            Vector3 sideOffset = facing * new Vector3(lateralOffsetX, 0f, 0f);
            Vector3 halfRibbonUp = new Vector3(0f, 0.045f, 0f);

            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                Vector3 center = Vector3.Lerp(startTop, endTop, t);
                float arcLift = 0.22f + Mathf.Sin(t * Mathf.PI) * 0.26f;
                center.y += arcLift;
                center += sideOffset;

                verts.Add(center - halfRibbonUp);
                verts.Add(center + halfRibbonUp);
                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));
                colors.Add(tint);
                colors.Add(tint);

                if (s > 0)
                {
                    int cur = verts.Count - 2;
                    int prev = cur - 2;
                    // Double-sided spline ribbon triangles so visible from either side
                    tris.Add(prev); tris.Add(cur + 1); tris.Add(prev + 1);
                    tris.Add(prev); tris.Add(cur); tris.Add(cur + 1);

                    tris.Add(prev); tris.Add(prev + 1); tris.Add(cur + 1);
                    tris.Add(prev); tris.Add(cur + 1); tris.Add(cur);
                }
            }
        }

        private void ClearBuilt()
        {
            if (_archipelagoRoot != null)
            {
                Destroy(_archipelagoRoot);
                _archipelagoRoot = null;
            }

            for (int i = 0; i < _ownedMeshes.Count; i++)
            {
                if (_ownedMeshes[i] != null)
                {
                    Destroy(_ownedMeshes[i]);
                }
            }

            _ownedMeshes.Clear();

            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                {
                    Destroy(_ownedMaterials[i]);
                }
            }

            _ownedMaterials.Clear();
            _walkableColliders.Clear();
            _walkableSurfaces.Clear();
            _islands.Clear();
            _bridges.Clear();
            _focusTargets.Clear();
            _beamRenderer = null;
            _built = false;
        }

        private void OnDestroy()
        {
            ClearBuilt();
        }
    }
}
