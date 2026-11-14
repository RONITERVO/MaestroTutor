using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maestro.Quest.Cosmos
{
    /// <summary>
    /// Renders all non-walkable living HyperCosmos landmarks under "Cosmos Sky & Orbital Vista"
    /// outside RoomEditor and without Collider/Rigidbody/AcousticSurface, strictly within
    /// Standalone Meta Quest 3 BiRP draw budgets (&lt; 40 total draws across the cosmos scene).
    /// </summary>
    public sealed class CosmosLandmarksPresenter : MonoBehaviour
    {
        public const int MaxSpriteInstances = 768;
        private const int SolarInstanceCount = 12;   // 11 Solar bodies + Andromeda/CMB sphere accents
        private const int RingQuadInstanceCount = 3; // Saturn ring, Uranus ring, Sgr A* accretion
        private const int LotusPetalCount = 25;      // 25-petal Horizon Lotus crown

        private Transform _roomContent;
        private Transform _vistaRoot;
        private Transform _macroLayer;
        private Transform _microLayer;
        private Transform _skyDomeTx;
        private Transform _earthTx;
        private Transform _prismTx;
        private Transform _dnaTx;
        private Camera _camera;
        private CosmosArchipelagoBuilder _archipelago;

        private MeshRenderer _skyDomeRenderer;
        private MeshRenderer _earthRenderer;
        private MeshRenderer _auroraRenderer;
        private MeshRenderer _prismRenderer;
        private MeshRenderer _harpRenderer;
        private MeshRenderer _dnaRenderer;
        private MeshRenderer _setiBeaconRenderer;

        private Material _skyMat;
        private Material _earthMat;
        private Material _orbitalMat;
        private Material _orbitalAdditiveMat;
        private Material _auroraMat;
        private Material _beamMat;
        private Material _spriteMat;

        private Mesh _skyDomeMesh;
        private Mesh _sphereMesh;
        private Mesh _planarQuadMesh;
        private Mesh _auroraMesh;
        private Mesh _prismMesh;
        private Mesh _harpMesh;
        private Mesh _dnaMesh;
        private Mesh _beaconConeMesh;
        private Mesh _lotusPetalMesh;
        private Mesh _spriteQuadMesh;

        private readonly List<Mesh> _ownedMeshes = new(12);
        private readonly List<Material> _ownedMaterials = new(12);

        private MaterialPropertyBlock _skyMpb;
        private MaterialPropertyBlock _earthMpb;
        private MaterialPropertyBlock _auroraMpb;
        private MaterialPropertyBlock _beamMpb;
        private MaterialPropertyBlock _solarBatchMpb;
        private MaterialPropertyBlock _ringBatchMpb;
        private MaterialPropertyBlock _lotusBatchMpb;
        private MaterialPropertyBlock _spriteBatchMpb;
        private MaterialPropertyBlock _prismMpb;

        // Preallocated zero-GC instancing buffers
        private readonly Matrix4x4[] _solarMatrices = new Matrix4x4[SolarInstanceCount];
        private readonly Vector4[] _solarBaseColors = new Vector4[SolarInstanceCount];
        private readonly Vector4[] _solarBandColors = new Vector4[SolarInstanceCount];
        private readonly float[] _solarModes = new float[SolarInstanceCount];
        private readonly float[] _solarGlows = new float[SolarInstanceCount];

        private readonly Matrix4x4[] _ringMatrices = new Matrix4x4[RingQuadInstanceCount];
        private readonly Vector4[] _ringBaseColors = new Vector4[RingQuadInstanceCount];
        private readonly Vector4[] _ringBandColors = new Vector4[RingQuadInstanceCount];
        private readonly Vector4[] _ringTintColors = new Vector4[RingQuadInstanceCount];
        private readonly float[] _ringModes = new float[RingQuadInstanceCount];
        private readonly float[] _ringGlows = new float[RingQuadInstanceCount];

        private readonly Matrix4x4[] _lotusMatrices = new Matrix4x4[LotusPetalCount];
        private readonly Vector4[] _lotusBaseColors = new Vector4[LotusPetalCount];
        private readonly Vector4[] _lotusBandColors = new Vector4[LotusPetalCount];
        private readonly float[] _lotusModes = new float[LotusPetalCount];
        private readonly float[] _lotusGlows = new float[LotusPetalCount];

        private readonly Matrix4x4[] _spriteMatrices = new Matrix4x4[MaxSpriteInstances];
        private readonly Vector3[] _spriteBasePositions = new Vector3[MaxSpriteInstances];
        private readonly Quaternion[] _spriteBaseRotations = new Quaternion[MaxSpriteInstances];
        private readonly Vector3[] _spriteVelocities = new Vector3[MaxSpriteInstances];
        private readonly float[] _spriteBaseScales = new float[MaxSpriteInstances];
        private readonly byte[] _spriteLayerTag = new byte[MaxSpriteInstances]; // 0=stars,1=cosmicWeb,2=nebula,3=neural/micro
        private readonly Vector4[] _spriteColors = new Vector4[MaxSpriteInstances];
        private readonly float[] _spriteIntensities = new float[MaxSpriteInstances];
        private readonly float[] _spriteSoftness = new float[MaxSpriteInstances];
        private readonly float[] _spriteModes = new float[MaxSpriteInstances];
        private readonly float[] _spritePhases = new float[MaxSpriteInstances];
        private readonly float[] _spriteBillboards = new float[MaxSpriteInstances];

        private Vector3 _island1Origin;
        private Vector3 _island2Origin;
        private Vector3 _island3Origin;
        private Vector3 _island4Origin;
        private Vector3 _island5Origin;

        private bool _built;
        private bool _virtualModeActive;
        private bool _canInstance;

        public void Initialize(Transform roomContent, CosmosArchipelagoBuilder archipelago, Camera targetCamera = null)
        {
            if (roomContent == null)
            {
                return;
            }

            _archipelago = archipelago;
            _camera = targetCamera;

            if (_built && _roomContent == roomContent)
            {
                return;
            }

            ClearBuilt();
            _roomContent = roomContent;
            _canInstance = SystemInfo.supportsInstancing;
            CacheIslandOrigins();
            BuildSceneHierarchy();
            SeedSpriteField();
            SetVirtualModeActive(_virtualModeActive);
            _built = true;
        }

        public void SetVirtualModeActive(bool active)
        {
            _virtualModeActive = active;
            if (_vistaRoot != null && _vistaRoot.gameObject.activeSelf != active)
            {
                _vistaRoot.gameObject.SetActive(active);
            }
        }

        public void UpdateCosmosFrame(
            in CosmosScaleState scaleState,
            int highlightedTargetId,
            float timeSec,
            float focusPulse01)
        {
            if (!_built || !_virtualModeActive || _vistaRoot == null)
            {
                return;
            }

            Camera cam = _camera != null ? _camera : Camera.main;
            Vector3 eyeWorld = cam != null ? cam.transform.position : (_roomContent != null ? _roomContent.position : Vector3.zero);
            float farClip = cam != null ? Mathf.Max(24f, cam.farClipPlane * 0.78f) : 450f;
            float u = (float)scaleState.ScaleExp;

            // Keep Far-Shell Sky centered on active eye and synced to Room content rotation
            if (_skyDomeTx != null)
            {
                _skyDomeTx.position = eyeWorld;
                _skyDomeTx.rotation = _roomContent != null ? _roomContent.rotation : Quaternion.identity;
                float skyRadius = Mathf.Clamp(farClip, 22f, 920f);
                _skyDomeTx.localScale = new Vector3(skyRadius, skyRadius, skyRadius);
            }

            UpdateSkyShader(u, timeSec, scaleState.ActiveExperienceId, focusPulse01);
            UpdateScaleDiveLayers(u);
            UpdateEarthAndAurora(timeSec, focusPulse01);
            AnimateIslandLandmarks(timeSec, focusPulse01);
            DrawInstancedSolarAndRings(timeSec, u, focusPulse01);
            DrawInstancedHorizonLotus(timeSec, u, focusPulse01);
            DrawInstancedSpriteField(timeSec, u, focusPulse01);
        }

        private void CacheIslandOrigins()
        {
            _island1Origin = new Vector3(13.12f, 0.65f, 4.26f);
            _island2Origin = new Vector3(8.93f, 0.94f, -12.30f);
            _island3Origin = new Vector3(-12.84f, 0.50f, 4.17f);
            _island4Origin = new Vector3(-8.58f, 0.79f, -11.81f);
            _island5Origin = new Vector3(0.0f, 1.08f, 16.0f);

            if (_archipelago != null && _archipelago.Islands.Count >= 6)
            {
                _island1Origin = _archipelago.Islands[1].CenterLocal;
                _island2Origin = _archipelago.Islands[2].CenterLocal;
                _island3Origin = _archipelago.Islands[3].CenterLocal;
                _island4Origin = _archipelago.Islands[4].CenterLocal;
                _island5Origin = _archipelago.Islands[5].CenterLocal;
            }
        }

        private void BuildSceneHierarchy()
        {
            // Parent outside RoomEditor objects & strictly zero Colliders/Rigidbodies/Acoustics
            var rootGo = new GameObject("Cosmos Sky & Orbital Vista");
            _vistaRoot = rootGo.transform;
            _vistaRoot.SetParent(_roomContent, false);
            _vistaRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var macroGo = new GameObject("CosmosMacroVistaLayer");
            _macroLayer = macroGo.transform;
            _macroLayer.SetParent(_vistaRoot, false);

            var microGo = new GameObject("CosmosMicroQuantumLayer");
            _microLayer = microGo.transform;
            _microLayer.SetParent(_vistaRoot, false);

            ResolveMaterials();
            BuildMeshes();
            InitPropertyBlocks();

            // 1. Far-Shell Sky Dome
            var skyGo = new GameObject("FarShellSkyDome");
            _skyDomeTx = skyGo.transform;
            _skyDomeTx.SetParent(_vistaRoot, false);
            skyGo.AddComponent<MeshFilter>().sharedMesh = _skyDomeMesh;
            _skyDomeRenderer = skyGo.AddComponent<MeshRenderer>();
            _skyDomeRenderer.sharedMaterial = _skyMat;
            _skyDomeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _skyDomeRenderer.receiveShadows = false;

            // 2. Hero Earth & Aurora Borealis Curtains (NW Observatory quadrant)
            var earthGo = new GameObject("EarthHeroOrb");
            _earthTx = earthGo.transform;
            _earthTx.SetParent(_macroLayer, false);
            _earthTx.localPosition = new Vector3(-5.8f, 2.8f, 8.6f);
            _earthTx.localScale = new Vector3(2.55f, 2.55f, 2.55f);
            earthGo.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
            _earthRenderer = earthGo.AddComponent<MeshRenderer>();
            _earthRenderer.sharedMaterial = _earthMat;
            _earthRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _earthRenderer.receiveShadows = false;

            var auroraGo = new GameObject("EarthPolarAuroraCurtains");
            auroraGo.transform.SetParent(_earthTx, false);
            auroraGo.AddComponent<MeshFilter>().sharedMesh = _auroraMesh;
            _auroraRenderer = auroraGo.AddComponent<MeshRenderer>();
            _auroraRenderer.sharedMaterial = _auroraMat;
            _auroraRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _auroraRenderer.receiveShadows = false;

            // 3. Island 3: Rotating 3D HSL Bicone Prism (01 / 13 / 14) via Cosmos/OrbitalPlanet Mode 7
            var prismGo = new GameObject("HslBiconePrism");
            _prismTx = prismGo.transform;
            _prismTx.SetParent(_microLayer, false);
            _prismTx.localPosition = _island3Origin + new Vector3(-1.2f, 1.38f, 0.4f);
            prismGo.AddComponent<MeshFilter>().sharedMesh = _prismMesh;
            _prismRenderer = prismGo.AddComponent<MeshRenderer>();
            _prismRenderer.sharedMaterial = _orbitalMat;
            _prismRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _prismRenderer.receiveShadows = false;
            ApplyPrismProperties(0f, 0f);

            // 4. Island 4: 48-String Cymatic Harp + Chladni Ring (02) & DNA Double Helix (15..17)
            var harpGo = new GameObject("CymaticHarpAndChladni");
            harpGo.transform.SetParent(_macroLayer, false);
            harpGo.transform.localPosition = _island4Origin + new Vector3(-1.1f, 0.12f, -1.0f);
            harpGo.transform.localRotation = Quaternion.Euler(0f, 36f, 0f);
            harpGo.AddComponent<MeshFilter>().sharedMesh = _harpMesh;
            _harpRenderer = harpGo.AddComponent<MeshRenderer>();
            _harpRenderer.sharedMaterial = _beamMat;
            _harpRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _harpRenderer.receiveShadows = false;

            var dnaGo = new GameObject("BioDnaDoubleHelix");
            _dnaTx = dnaGo.transform;
            _dnaTx.SetParent(_microLayer, false);
            _dnaTx.localPosition = _island4Origin + new Vector3(0.45f, 1.45f, -1.75f);
            dnaGo.AddComponent<MeshFilter>().sharedMesh = _dnaMesh;
            _dnaRenderer = dnaGo.AddComponent<MeshRenderer>();
            _dnaRenderer.sharedMaterial = _beamMat;
            _dnaRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _dnaRenderer.receiveShadows = false;

            // 5. Island 5: SETI Prime Beacon Cone (22)
            var beaconGo = new GameObject("SetiPrimeBeaconCone");
            beaconGo.transform.SetParent(_macroLayer, false);
            beaconGo.transform.localPosition = _island5Origin + new Vector3(0f, 0.08f, 1.45f);
            beaconGo.AddComponent<MeshFilter>().sharedMesh = _beaconConeMesh;
            _setiBeaconRenderer = beaconGo.AddComponent<MeshRenderer>();
            _setiBeaconRenderer.sharedMaterial = _beamMat;
            _setiBeaconRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _setiBeaconRenderer.receiveShadows = false;
        }

        private void ResolveMaterials()
        {
            _skyMat = LoadOrFallback(CosmosShaderIds.ResFarShellSkyMat, CosmosShaderIds.ShaderFarShellSky, Color.black);
            _earthMat = LoadOrFallback(CosmosShaderIds.ResEarthHeroMat, CosmosShaderIds.ShaderEarthHero, new Color(0.18f, 0.50f, 0.92f, 1f));
            _orbitalMat = LoadOrFallback(CosmosShaderIds.ResOrbitalPlanetMat, CosmosShaderIds.ShaderOrbitalPlanet, new Color(0.85f, 0.72f, 0.45f, 1f));
            _auroraMat = LoadOrFallback(CosmosShaderIds.ResAuroralCurtainMat, CosmosShaderIds.ShaderAuroralCurtain, new Color(0.20f, 1.15f, 0.75f, 1f));
            _beamMat = LoadOrFallback(CosmosShaderIds.ResTetherBridgeBeamMat, CosmosShaderIds.ShaderTetherBridgeBeam, new Color(0.45f, 0.95f, 1.45f, 1f));
            _spriteMat = LoadOrFallback(CosmosShaderIds.ResSpriteFieldMat, CosmosShaderIds.ShaderSpriteField, Color.white);

            // Cloned additive two-sided variant of OrbitalPlanet for planar Ring (mode 2) & Kerr Black Hole (mode 3)
            if (_orbitalMat != null)
            {
                _orbitalAdditiveMat = new Material(_orbitalMat)
                {
                    name = "Cosmos_OrbitalRingsAdditive_Clone",
                    enableInstancing = true,
                    hideFlags = HideFlags.DontSave,
                };
                if (_orbitalAdditiveMat.HasProperty(CosmosShaderIds.CullMode)) _orbitalAdditiveMat.SetFloat(CosmosShaderIds.CullMode, 0f);
                if (_orbitalAdditiveMat.HasProperty(CosmosShaderIds.SrcBlend)) _orbitalAdditiveMat.SetFloat(CosmosShaderIds.SrcBlend, 1f);
                if (_orbitalAdditiveMat.HasProperty(CosmosShaderIds.DstBlend)) _orbitalAdditiveMat.SetFloat(CosmosShaderIds.DstBlend, 1f);
                if (_orbitalAdditiveMat.HasProperty(CosmosShaderIds.ZWrite)) _orbitalAdditiveMat.SetFloat(CosmosShaderIds.ZWrite, 0f);
                _orbitalAdditiveMat.renderQueue = (int)RenderQueue.Transparent + 10;
                _ownedMaterials.Add(_orbitalAdditiveMat);
            }
        }

        private Material LoadOrFallback(string resPath, string shaderName, Color fallbackColor)
        {
            Material mat = CosmosShaderIds.ResolveMaterial(resPath, shaderName, fallbackColor);
            if (mat != null)
            {
                mat.enableInstancing = true;
                if (mat.name.Contains("(RuntimeFallback)"))
                {
                    mat.hideFlags = HideFlags.DontSave;
                    _ownedMaterials.Add(mat);
                }
            }

            return mat;
        }

        private void InitPropertyBlocks()
        {
            _skyMpb = new MaterialPropertyBlock();
            _earthMpb = new MaterialPropertyBlock();
            _auroraMpb = new MaterialPropertyBlock();
            _beamMpb = new MaterialPropertyBlock();
            _solarBatchMpb = new MaterialPropertyBlock();
            _ringBatchMpb = new MaterialPropertyBlock();
            _lotusBatchMpb = new MaterialPropertyBlock();
            _spriteBatchMpb = new MaterialPropertyBlock();
            _prismMpb = new MaterialPropertyBlock();
        }

        private void BuildMeshes()
        {
            _skyDomeMesh = TrackMesh(CreateUvSphereMesh("CosmosSkyDome", 1f, 24, 14, inwardNormals: true));
            _sphereMesh = TrackMesh(CreateUvSphereMesh("CosmosHeroSphere", 0.5f, 28, 18, inwardNormals: false));
            _planarQuadMesh = TrackMesh(CreatePlanarUnitQuad("CosmosPlanarRingQuad", 1.0f));
            _spriteQuadMesh = TrackMesh(CreatePlanarUnitQuad("CosmosBillboardUnitQuad", 1.0f));
            _auroraMesh = TrackMesh(CreatePolarAuroraRibbonMesh());
            _prismMesh = TrackMesh(CreateHslBiconePrismMesh());
            _harpMesh = TrackMesh(CreateCymaticHarpAndChladniMesh());
            _dnaMesh = TrackMesh(CreateDnaDoubleHelixMesh());
            _beaconConeMesh = TrackMesh(CreateSetiBeaconConeMesh());
            _lotusPetalMesh = TrackMesh(CreateLotusPetalMesh());
        }

        private Mesh TrackMesh(Mesh mesh)
        {
            mesh.hideFlags = HideFlags.DontSave;
            _ownedMeshes.Add(mesh);
            return mesh;
        }

        private void SeedSpriteField()
        {
            // Deterministic PRNG for reproducible low-shimmer cosmos distribution (<= 768 total billboards)
            uint seed = 0x51A7F00Du;
            for (int i = 0; i < MaxSpriteInstances; i++)
            {
                float r0 = NextRand(ref seed);
                float r1 = NextRand(ref seed);
                float r2 = NextRand(ref seed);
                float r3 = NextRand(ref seed);
                _spriteBaseRotations[i] = Quaternion.identity;

                if (i < 420)
                {
                    // Layer 0: Deep-field stars around sky sphere & galactic Milky Way plane
                    _spriteLayerTag[i] = 0;
                    float theta = r0 * Mathf.PI * 2f;
                    bool inMilkyWayBand = i < 180;
                    float y = inMilkyWayBand ? (r1 - 0.5f) * 0.34f : (r1 * 2f - 1f);
                    float radHoriz = Mathf.Sqrt(Mathf.Max(0.01f, 1f - y * y));
                    float dist = 42f + r2 * 140f;
                    _spriteBasePositions[i] = new Vector3(Mathf.Cos(theta) * radHoriz, y, Mathf.Sin(theta) * radHoriz) * dist;
                    _spriteBaseScales[i] = 0.28f + r3 * 0.52f;
                    _spriteModes[i] = 0f; // AiryStar
                    _spriteBillboards[i] = 1f;
                    _spriteSoftness[i] = 0.45f;
                    _spriteIntensities[i] = inMilkyWayBand ? 1.15f : 0.85f;
                    _spriteColors[i] = Color.Lerp(new Color(0.62f, 0.80f, 1.25f, 1f), new Color(1.25f, 0.92f, 0.65f, 1f), r3);
                }
                else if (i < 580)
                {
                    // Layer 1: Island 2 Sgr A* spiral galaxy stars & Cosmic Web Voronoi nodes/filaments
                    _spriteLayerTag[i] = 1;
                    int idx = i - 420;
                    float armAngle = (idx % 4) * (Mathf.PI * 0.5f) + (idx / 160f) * 4.4f;
                    float armR = 0.35f + (idx / 160f) * 2.8f + (r0 - 0.5f) * 0.45f;
                    _spriteBasePositions[i] = _island2Origin + new Vector3(
                        1.1f + Mathf.Cos(armAngle) * armR,
                        1.45f + (r1 - 0.5f) * 0.85f + (idx / 160f) * 1.4f,
                        -1.2f + Mathf.Sin(armAngle) * armR);
                    bool isFilament = (i % 3) == 0;
                    _spriteBaseScales[i] = isFilament ? 0.42f : (0.14f + r2 * 0.22f);
                    _spriteModes[i] = isFilament ? 1f : 2f; // Filament or Node
                    _spriteBillboards[i] = 1f;
                    if (isFilament)
                    {
                        _spriteBaseRotations[i] = Quaternion.Euler(r0 * 180f, r1 * 360f, r2 * 180f);
                    }
                    _spriteSoftness[i] = 0.35f;
                    _spriteIntensities[i] = 1.1f;
                    _spriteColors[i] = idx < 80
                        ? new Color(0.55f, 0.85f, 1.45f, 1f)
                        : new Color(0.95f, 0.65f, 1.55f, 1f);
                }
                else if (i < 690)
                {
                    // Layer 2: Solar System Kuiper shards (Island 1) + Culture/SETI HexDyson swarm (Island 5)
                    _spriteLayerTag[i] = 2;
                    bool isDyson = i >= 635;
                    float ang = r0 * Mathf.PI * 2f;
                    if (!isDyson)
                    {
                        float orbitR = 2.0f + r1 * 1.4f;
                        _spriteBasePositions[i] = _island1Origin + new Vector3(1.6f + Mathf.Cos(ang) * orbitR, 1.95f + (r2 - 0.5f) * 0.35f, 1.2f + Mathf.Sin(ang) * orbitR);
                        _spriteModes[i] = 2f;
                        _spriteColors[i] = new Color(0.92f, 0.78f, 0.52f, 1f);
                    }
                    else
                    {
                        float r = 1.6f + r1 * 1.2f;
                        _spriteBasePositions[i] = _island5Origin + new Vector3(Mathf.Cos(ang) * r, 1.85f + (r2 - 0.5f) * 1.1f, 1.5f + Mathf.Sin(ang) * r);
                        _spriteModes[i] = 3f; // HexDyson
                        _spriteColors[i] = new Color(0.45f, 1.25f, 1.55f, 1f);
                    }

                    _spriteBaseScales[i] = 0.14f + r3 * 0.18f;
                    _spriteBillboards[i] = 1f;
                    _spriteSoftness[i] = 0.30f;
                    _spriteIntensities[i] = 1.0f;
                }
                else
                {
                    // Layer 3: Quantum subatomic orbitals (Island 3) + Mind/Life neural pulses (Island 4)
                    _spriteLayerTag[i] = 3;
                    bool isQuantum = i < 730;
                    float a = r0 * Mathf.PI * 2f;
                    float elev = (r1 - 0.5f) * 1.6f;
                    float rad = 0.65f + r2 * 1.15f;
                    Vector3 hub = isQuantum
                        ? _island3Origin + new Vector3(0.2f, 1.85f, -1.1f)
                        : _island4Origin + new Vector3(-0.6f, 2.15f, -2.0f);
                    _spriteBasePositions[i] = hub + new Vector3(Mathf.Cos(a) * rad, elev, Mathf.Sin(a) * rad);
                    _spriteBaseScales[i] = 0.12f + r3 * 0.16f;
                    _spriteModes[i] = isQuantum ? 4f : 2f; // OrbitalRing or Neural Node
                    _spriteBillboards[i] = 1f;
                    _spriteSoftness[i] = 0.25f;
                    _spriteIntensities[i] = 1.25f;
                    _spriteColors[i] = isQuantum
                        ? new Color(0.32f, 1.38f, 1.52f, 1f)
                        : new Color(1.35f, 0.65f, 1.48f, 1f);
                }

                _spriteVelocities[i] = new Vector3((r1 - 0.5f) * 0.22f, (r2 - 0.5f) * 0.15f, (r3 - 0.5f) * 0.22f);
                _spritePhases[i] = r0 * 6.28318f;
            }
        }

        private void UpdateSkyShader(float scaleU, float timeSec, byte activeExpId, float focusPulse01)
        {
            if (_skyDomeRenderer == null)
            {
                return;
            }

            CosmosExperienceMeta exp = CosmosCatalogData.GetExperience(activeExpId);
            Color accent = Color.Lerp(exp.HdrAccentLinear, Color.white, 0.15f * focusPulse01);

            _skyMpb.SetFloat(CosmosShaderIds.ScaleU, scaleU);
            _skyMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
            _skyMpb.SetFloat(CosmosShaderIds.StarIntensity, 1.15f);
            _skyMpb.SetFloat(CosmosShaderIds.MilkyWayGain, Mathf.Lerp(0.75f, 1.55f, Mathf.InverseLerp(4f, 22f, scaleU)));
            _skyMpb.SetColor(CosmosShaderIds.AccentColor, accent);
            _skyDomeRenderer.SetPropertyBlock(_skyMpb);
        }

        private void UpdateScaleDiveLayers(float scaleU)
        {
            // Scale-dive camera-relative visual exaggeration without ever modifying Room content scale
            float relU = scaleU - (float)CosmosScaleState.ColdStartScaleExp;
            float microBoost = Mathf.Clamp(Mathf.Pow(2.0f, Mathf.Clamp(-relU * 0.18f, -1.5f, 2.2f)), 0.35f, 4.2f);
            float macroBoost = Mathf.Clamp(Mathf.Pow(1.6f, Mathf.Clamp(relU * 0.12f, -1.2f, 2.4f)), 0.45f, 3.8f);

            if (_microLayer != null)
            {
                _microLayer.localScale = new Vector3(microBoost, microBoost, microBoost);
            }

            if (_macroLayer != null)
            {
                _macroLayer.localScale = new Vector3(macroBoost, macroBoost, macroBoost);
            }
        }

        private void UpdateEarthAndAurora(float timeSec, float focusPulse01)
        {
            if (_earthTx != null)
            {
                _earthTx.localRotation = Quaternion.Euler(23.4f, timeSec * 4.5f, 0f);
            }

            Vector3 sunDir = new Vector3(0.68f, 0.36f, 0.64f).normalized;
            if (_earthRenderer != null)
            {
                _earthMpb.SetVector(CosmosShaderIds.SunDir, sunDir);
                _earthMpb.SetFloat(CosmosShaderIds.CloudTime, timeSec * 0.35f);
                _earthMpb.SetFloat(CosmosShaderIds.CityGlow, 1.25f + 0.35f * focusPulse01);
                _earthMpb.SetColor(CosmosShaderIds.AtmosColor, new Color(0.24f, 0.78f, 1.48f, 1f));
                _earthMpb.SetFloat(CosmosShaderIds.FocusGlow, 0.35f + 0.65f * focusPulse01);
                _earthMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
                _earthRenderer.SetPropertyBlock(_earthMpb);
            }

            if (_auroraRenderer != null)
            {
                _auroraMpb.SetColor(CosmosShaderIds.ColorPrimary, new Color(0.14f, 1.48f, 0.72f, 1f));
                _auroraMpb.SetColor(CosmosShaderIds.ColorSecondary, new Color(1.22f, 0.28f, 1.45f, 1f));
                _auroraMpb.SetFloat(CosmosShaderIds.CurtainSpeed, 0.85f);
                _auroraMpb.SetFloat(CosmosShaderIds.Intensity, 1.15f + 0.45f * focusPulse01);
                _auroraMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
                _auroraMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
                _auroraRenderer.SetPropertyBlock(_auroraMpb);
            }
        }

        private void AnimateIslandLandmarks(float timeSec, float focusPulse01)
        {
            if (_prismTx != null)
            {
                _prismTx.localRotation = Quaternion.Euler(14f * Mathf.Sin(timeSec * 0.3f), timeSec * 18f, 0f);
                ApplyPrismProperties(timeSec, focusPulse01);
            }

            if (_dnaTx != null)
            {
                _dnaTx.localRotation = Quaternion.Euler(0f, timeSec * 22f, 10f);
            }

            if (_harpRenderer != null)
            {
                _beamMpb.SetColor(CosmosShaderIds.Color, new Color(0.45f, 1.25f, 1.55f, 1f));
                _beamMpb.SetFloat(CosmosShaderIds.PulseSpeed, 1.4f);
                _beamMpb.SetFloat(CosmosShaderIds.ActiveFocus, 0.45f + 0.55f * focusPulse01);
                _beamMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
                _beamMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
                _harpRenderer.SetPropertyBlock(_beamMpb);
            }

            if (_dnaRenderer != null)
            {
                _beamMpb.SetColor(CosmosShaderIds.Color, new Color(0.32f, 1.42f, 0.78f, 1f));
                _beamRendererSafeApply(_dnaRenderer, _beamMpb);
            }

            if (_setiBeaconRenderer != null)
            {
                _beamMpb.SetColor(CosmosShaderIds.Color, new Color(1.55f, 0.82f, 0.48f, 1f));
                _beamMpb.SetFloat(CosmosShaderIds.ActiveFocus, 0.55f + 0.45f * focusPulse01);
                _beamRendererSafeApply(_setiBeaconRenderer, _beamMpb);
            }
        }

        private static void _beamRendererSafeApply(MeshRenderer renderer, MaterialPropertyBlock block)
        {
            renderer.SetPropertyBlock(block);
        }

        private void ApplyPrismProperties(float timeSec, float focusPulse01)
        {
            if (_prismRenderer == null || _prismMpb == null)
            {
                return;
            }

            _prismMpb.Clear();
            _prismMpb.SetFloat(CosmosShaderIds.PlanetMode, 7f);
            _prismMpb.SetFloat(CosmosShaderIds.GlowGain, 1.10f + 0.40f * focusPulse01);
            _prismMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
            _prismMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
            _prismRenderer.SetPropertyBlock(_prismMpb);
        }

        private void DrawInstancedSolarAndRings(float timeSec, float scaleU, float focusPulse01)
        {
            if (_sphereMesh == null || _orbitalMat == null)
            {
                return;
            }

            Matrix4x4 macroWorld = _macroLayer != null ? _macroLayer.localToWorldMatrix : Matrix4x4.identity;
            Vector3 sunCenterLocal = _island1Origin + new Vector3(1.6f, 2.0f, 1.2f);
            Vector3 sunDir = new Vector3(0.68f, 0.36f, 0.64f).normalized;

            int bodyCount = Mathf.Min(SolarInstanceCount - 1, CosmosCatalogData.SolarBodies.Length);
            Vector3 saturnPosLocal = sunCenterLocal;
            Vector3 uranusPosLocal = sunCenterLocal;

            for (int i = 0; i < bodyCount; i++)
            {
                CosmosSolarBodyMeta b = CosmosCatalogData.SolarBodies[i];
                Vector3 localPos;
                float diam;
                float mode;

                if (i == 0)
                {
                    localPos = sunCenterLocal;
                    diam = 0.76f;
                    mode = 1f; // Sun
                }
                else
                {
                    float orbitR = 0.72f + b.NormalizedX * 2.55f;
                    float orbitSpeed = 0.45f / Mathf.Sqrt(orbitR);
                    float ang = timeSec * orbitSpeed + i * 0.95f;
                    localPos = sunCenterLocal + new Vector3(
                        Mathf.Cos(ang) * orbitR,
                        b.Lane * 0.8f,
                        Mathf.Sin(ang) * orbitR);
                    diam = Mathf.Clamp(b.VisualRadiusUnits * 0.011f, 0.11f, 0.42f);
                    mode = b.Id == "jupiter" || b.Id == "saturn" ? 0f : (b.Id == "uranus" || b.Id == "neptune" || b.Id == "pluto" ? 6f : 5f);
                    if (b.Id == "saturn") saturnPosLocal = localPos;
                    if (b.Id == "uranus") uranusPosLocal = localPos;
                }

                _solarMatrices[i] = macroWorld * Matrix4x4.TRS(localPos, Quaternion.Euler(0f, timeSec * 12f, 0f), new Vector3(diam, diam, diam));
                _solarBaseColors[i] = b.BaseColor;
                _solarBandColors[i] = b.BandColor;
                _solarModes[i] = mode;
                _solarGlows[i] = i == 0 ? 1.45f : (0.55f + 0.35f * focusPulse01);
            }

            // Instance 11: Andromeda tilted spiral disk halo over Island 2
            Vector3 androPosLocal = _island2Origin + new Vector3(2.2f, 2.35f, -2.1f);
            _solarMatrices[11] = macroWorld * Matrix4x4.TRS(androPosLocal, Quaternion.Euler(28f, timeSec * 8f, -18f), new Vector3(1.1f, 0.24f, 1.1f));
            _solarBaseColors[11] = new Vector4(0.75f, 0.72f, 1.38f, 1f);
            _solarBandColors[11] = new Vector4(1.35f, 1.05f, 0.72f, 1f);
            _solarModes[11] = 1f;
            _solarGlows[11] = 1.15f;

            _solarBatchMpb.Clear();
            _solarBatchMpb.SetFloatArray(CosmosShaderIds.PlanetMode, _solarModes);
            _solarBatchMpb.SetVectorArray(CosmosShaderIds.BaseColor, _solarBaseColors);
            _solarBatchMpb.SetVectorArray(CosmosShaderIds.BandColor, _solarBandColors);
            _solarBatchMpb.SetFloatArray(CosmosShaderIds.GlowGain, _solarGlows);
            _solarBatchMpb.SetVector(CosmosShaderIds.SunDir, sunDir);
            _solarBatchMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
            _solarBatchMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
            DrawInstancedOrSingle(_sphereMesh, _orbitalMat, _solarMatrices, SolarInstanceCount, _solarBatchMpb);

            // Planar [0,1]^2 Ring + Kerr Black Hole Accretion Quad Batch
            if (_planarQuadMesh != null && _orbitalAdditiveMat != null)
            {
                // 0: Saturn Cassini rings (mode 2)
                _ringMatrices[0] = macroWorld * Matrix4x4.TRS(saturnPosLocal, Quaternion.Euler(64f, 18f, timeSec * 6f), new Vector3(0.95f, 0.95f, 0.95f));
                _ringBaseColors[0] = new Vector4(0.92f, 0.80f, 0.56f, 1f);
                _ringBandColors[0] = new Vector4(0.70f, 0.62f, 0.45f, 1f);
                _ringTintColors[0] = new Vector4(1.15f, 0.98f, 0.72f, 1f);
                _ringModes[0] = 2f;
                _ringGlows[0] = 1.15f;

                // 1: Uranus tilted narrow ring (mode 2)
                _ringMatrices[1] = macroWorld * Matrix4x4.TRS(uranusPosLocal, Quaternion.Euler(8f, 35f, 82f), new Vector3(0.60f, 0.60f, 0.60f));
                _ringBaseColors[1] = new Vector4(0.55f, 0.90f, 1.05f, 1f);
                _ringBandColors[1] = new Vector4(0.35f, 0.68f, 0.85f, 1f);
                _ringTintColors[1] = new Vector4(0.62f, 0.95f, 1.15f, 1f);
                _ringModes[1] = 2f;
                _ringGlows[1] = 0.85f;

                // 2: Sgr A* Kerr Black Hole & Accretion Disk over Island 2 (mode 3)
                Vector3 sgrPosLocal = _island2Origin + new Vector3(1.1f, 1.55f, -1.2f);
                _ringMatrices[2] = macroWorld * Matrix4x4.TRS(sgrPosLocal, Quaternion.Euler(68f, timeSec * 14f, 12f), new Vector3(1.75f, 1.75f, 1.75f));
                _ringBaseColors[2] = new Vector4(1.48f, 0.85f, 0.35f, 1f);
                _ringBandColors[2] = new Vector4(1.55f, 0.48f, 0.22f, 1f);
                _ringTintColors[2] = new Vector4(1.65f, 1.25f, 0.72f, 1f);
                _ringModes[2] = 3f;
                _ringGlows[2] = 1.55f;

                _ringBatchMpb.Clear();
                _ringBatchMpb.SetFloatArray(CosmosShaderIds.PlanetMode, _ringModes);
                _ringBatchMpb.SetVectorArray(CosmosShaderIds.BaseColor, _ringBaseColors);
                _ringBatchMpb.SetVectorArray(CosmosShaderIds.BandColor, _ringBandColors);
                _ringBatchMpb.SetVectorArray(CosmosShaderIds.RingColor, _ringTintColors);
                _ringBatchMpb.SetFloatArray(CosmosShaderIds.GlowGain, _ringGlows);
                _ringBatchMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
                _ringBatchMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
                DrawInstancedOrSingle(_planarQuadMesh, _orbitalAdditiveMat, _ringMatrices, RingQuadInstanceCount, _ringBatchMpb);
            }
        }

        private void DrawInstancedHorizonLotus(float timeSec, float scaleU, float focusPulse01)
        {
            if (_lotusPetalMesh == null || _orbitalMat == null)
            {
                return;
            }

            Matrix4x4 macroWorld = _macroLayer != null ? _macroLayer.localToWorldMatrix : Matrix4x4.identity;
            Vector3 lotusOrigin = _island5Origin + new Vector3(0f, 2.75f, 2.1f);
            float bloom = 0.55f + 0.35f * Mathf.Clamp01((scaleU - 6.6f) * 0.08f) + 0.12f * Mathf.Sin(timeSec * 0.7f);

            for (int i = 0; i < LotusPetalCount; i++)
            {
                // Golden-angle phyllotaxis rosette matching 25 experiences accent palette
                float goldenAngle = i * 137.507764f + timeSec * 5f;
                int tier = i / 8;
                float tiltDeg = Mathf.Lerp(26f, 68f, bloom) - tier * 10f;
                float radius = 0.22f + tier * 0.22f;
                Quaternion rot = Quaternion.Euler(0f, goldenAngle, 0f) * Quaternion.Euler(tiltDeg, 0f, 0f);
                Vector3 petalOffset = rot * new Vector3(0f, radius * 0.35f, radius);
                float petalScale = 0.52f + tier * 0.14f;

                _lotusMatrices[i] = macroWorld * Matrix4x4.TRS(
                    lotusOrigin + petalOffset,
                    rot,
                    new Vector3(petalScale, petalScale, petalScale));

                Color acc = CosmosCatalogData.Experiences[i].HdrAccentLinear;
                _lotusBaseColors[i] = new Vector4(acc.r, acc.g, acc.b, 1f);
                _lotusBandColors[i] = new Vector4(acc.r * 0.5f, acc.g * 0.5f, acc.b * 0.5f, 1f);
                _lotusModes[i] = 4f; // Horizon Lotus mode
                _lotusGlows[i] = 0.85f + 0.45f * focusPulse01;
            }

            _lotusBatchMpb.Clear();
            _lotusBatchMpb.SetFloatArray(CosmosShaderIds.PlanetMode, _lotusModes);
            _lotusBatchMpb.SetVectorArray(CosmosShaderIds.BaseColor, _lotusBaseColors);
            _lotusBatchMpb.SetVectorArray(CosmosShaderIds.BandColor, _lotusBandColors);
            _lotusBatchMpb.SetFloatArray(CosmosShaderIds.GlowGain, _lotusGlows);
            _lotusBatchMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
            _lotusBatchMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);
            DrawInstancedOrSingle(_lotusPetalMesh, _orbitalMat, _lotusMatrices, LotusPetalCount, _lotusBatchMpb);
        }

        private void DrawInstancedSpriteField(float timeSec, float scaleU, float focusPulse01)
        {
            if (_spriteQuadMesh == null || _spriteMat == null)
            {
                return;
            }

            Matrix4x4 baseWorld = _vistaRoot != null ? _vistaRoot.localToWorldMatrix : Matrix4x4.identity;
            for (int i = 0; i < MaxSpriteInstances; i++)
            {
                Vector3 pos = _spriteBasePositions[i];
                float phase = _spritePhases[i] + timeSec * 0.65f;
                if (_spriteLayerTag[i] != 0)
                {
                    pos += _spriteVelocities[i] * Mathf.Sin(phase);
                }

                float s = _spriteBaseScales[i] * (0.92f + 0.14f * Mathf.Sin(phase * 1.4f + focusPulse01));
                Vector3 scaleVec = _spriteModes[i] == 1f
                    ? new Vector3(s * 0.22f, s * 2.1f, s)
                    : new Vector3(s, s, s);

                _spriteMatrices[i] = baseWorld * Matrix4x4.TRS(pos, _spriteBaseRotations[i], scaleVec);
            }

            _spriteBatchMpb.Clear();
            _spriteBatchMpb.SetVectorArray(CosmosShaderIds.Color, _spriteColors);
            _spriteBatchMpb.SetFloatArray(CosmosShaderIds.Intensity, _spriteIntensities);
            _spriteBatchMpb.SetFloatArray(CosmosShaderIds.Softness, _spriteSoftness);
            _spriteBatchMpb.SetFloatArray(CosmosShaderIds.SpriteMode, _spriteModes);
            _spriteBatchMpb.SetFloatArray(CosmosShaderIds.Phase, _spritePhases);
            _spriteBatchMpb.SetFloatArray(CosmosShaderIds.Billboard, _spriteBillboards);
            _spriteBatchMpb.SetFloat(CosmosShaderIds.TimeSec, timeSec);
            _spriteBatchMpb.SetFloat(CosmosShaderIds.VisibilityRealDepth, 0f);

            DrawInstancedOrSingle(_spriteQuadMesh, _spriteMat, _spriteMatrices, MaxSpriteInstances, _spriteBatchMpb);
        }

        private void DrawInstancedOrSingle(
            Mesh mesh,
            Material material,
            Matrix4x4[] matrices,
            int count,
            MaterialPropertyBlock mpb)
        {
            if (mesh == null || material == null || count <= 0)
            {
                return;
            }

            if (_canInstance && material.enableInstancing)
            {
                Graphics.DrawMeshInstanced(mesh, 0, material, matrices, count, mpb, ShadowCastingMode.Off, false, 0, null);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    Graphics.DrawMesh(mesh, matrices[i], material, 0, null, 0, mpb, false, false);
                }
            }
        }

        private static Mesh CreateUvSphereMesh(string meshName, float radius, int lonSeg, int latSeg, bool inwardNormals)
        {
            int vertCount = (lonSeg + 1) * (latSeg + 1);
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[lonSeg * latSeg * 6];

            int v = 0;
            for (int lat = 0; lat <= latSeg; lat++)
            {
                float v01 = lat / (float)latSeg;
                float phi = v01 * Mathf.PI;
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);

                for (int lon = 0; lon <= lonSeg; lon++)
                {
                    float u01 = lon / (float)lonSeg;
                    float theta = u01 * Mathf.PI * 2f;
                    Vector3 n = new Vector3(sinPhi * Mathf.Cos(theta), cosPhi, sinPhi * Mathf.Sin(theta));
                    verts[v] = n * radius;
                    normals[v] = inwardNormals ? -n : n;
                    uvs[v] = new Vector2(u01, 1f - v01);
                    v++;
                }
            }

            int t = 0;
            for (int lat = 0; lat < latSeg; lat++)
            {
                for (int lon = 0; lon < lonSeg; lon++)
                {
                    int i0 = lat * (lonSeg + 1) + lon;
                    int i1 = i0 + 1;
                    int i2 = i0 + (lonSeg + 1);
                    int i3 = i2 + 1;

                    if (inwardNormals)
                    {
                        tris[t++] = i0; tris[t++] = i1; tris[t++] = i2;
                        tris[t++] = i1; tris[t++] = i3; tris[t++] = i2;
                    }
                    else
                    {
                        tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                        tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                    }
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            return mesh;
        }

        private static Mesh CreatePlanarUnitQuad(string meshName, float size)
        {
            float h = size * 0.5f;
            var mesh = new Mesh { name = meshName };
            mesh.vertices = new[]
            {
                new Vector3(-h, -h, 0f),
                new Vector3( h, -h, 0f),
                new Vector3( h,  h, 0f),
                new Vector3(-h,  h, 0f),
            };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return mesh;
        }

        private static Mesh CreatePolarAuroraRibbonMesh()
        {
            const int segs = 36;
            var verts = new List<Vector3>(segs * 4 + 4);
            var uvs = new List<Vector2>(segs * 4 + 4);
            var tris = new List<int>(segs * 12);

            AppendPolarCrown(verts, uvs, tris, segs, +1f);
            AppendPolarCrown(verts, uvs, tris, segs, -1f);

            var mesh = new Mesh { name = "CosmosPolarAuroraCurtains" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendPolarCrown(
            List<Vector3> verts,
            List<Vector2> uvs,
            List<int> tris,
            int segs,
            float hemisphereSign)
        {
            int startIdx = verts.Count;
            const float ringPhi = 0.38f;
            float baseR = 0.535f * Mathf.Sin(ringPhi);
            float baseY = hemisphereSign * 0.535f * Mathf.Cos(ringPhi);
            float topR = baseR * 1.12f;
            float topY = hemisphereSign * (0.535f * Mathf.Cos(ringPhi) + 0.14f);

            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float ang = t * Mathf.PI * 2f;
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);
                verts.Add(new Vector3(c * baseR, baseY, s * baseR));
                verts.Add(new Vector3(c * topR, topY, s * topR));
                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));

                if (i > 0)
                {
                    int cur = startIdx + i * 2;
                    int prev = cur - 2;
                    tris.Add(prev); tris.Add(cur + 1); tris.Add(prev + 1);
                    tris.Add(prev); tris.Add(cur); tris.Add(cur + 1);
                    tris.Add(prev); tris.Add(prev + 1); tris.Add(cur + 1);
                    tris.Add(prev); tris.Add(cur + 1); tris.Add(cur);
                }
            }
        }

        private static Mesh CreateHslBiconePrismMesh()
        {
            const int segs = 24;
            var verts = new List<Vector3>(segs * 3 + 2);
            var colors = new List<Color>(segs * 3 + 2);
            var tris = new List<int>(segs * 12);

            verts.Add(new Vector3(0f, 0.85f, 0f));
            colors.Add(Color.white);
            verts.Add(new Vector3(0f, -0.85f, 0f));
            colors.Add(new Color(0.08f, 0.08f, 0.10f, 1f));

            int ringStart = verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float ang = t * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * 0.72f, 0f, Mathf.Sin(ang) * 0.72f));
                Color c = Color.HSVToRGB(t % 1f, 0.92f, 1f);
                c.a = 0.85f;
                colors.Add(c);
            }

            for (int i = 0; i < segs; i++)
            {
                int a = ringStart + i;
                int b = a + 1;
                tris.Add(0); tris.Add(b); tris.Add(a);
                tris.Add(1); tris.Add(a); tris.Add(b);
            }

            var mesh = new Mesh { name = "CosmosHslBiconePrism" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCymaticHarpAndChladniMesh()
        {
            var verts = new List<Vector3>(512);
            var uvs = new List<Vector2>(512);
            var colors = new List<Color>(512);
            var tris = new List<int>(1024);

            // 48 harmonic strings spanning MIDI 48..95 along an ascending harp frame
            const int stringCount = CosmosCatalogData.MidiCount;
            for (int i = 0; i < stringCount; i++)
            {
                float t = i / (float)(stringCount - 1);
                float x = (t - 0.5f) * 2.1f;
                float topY = 0.85f + (1f - t) * 1.15f;
                const float bottomY = 0.15f;
                const float halfW = 0.009f;

                Color c = Color.HSVToRGB((0.52f + t * 0.42f) % 1f, 0.85f, 1f);
                int idx = verts.Count;
                verts.Add(new Vector3(x - halfW, bottomY, 0f));
                verts.Add(new Vector3(x + halfW, bottomY, 0f));
                verts.Add(new Vector3(x + halfW, topY, 0f));
                verts.Add(new Vector3(x - halfW, topY, 0f));
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
            }

            // Chladni resonant figure ring beneath strings
            const int ringSegs = 32;
            int rStart = verts.Count;
            Color ringTint = new Color(0.45f, 1.35f, 1.55f, 1f);
            for (int i = 0; i <= ringSegs; i++)
            {
                float t = i / (float)ringSegs;
                float ang = t * Mathf.PI * 2f;
                float waveR = 0.88f + 0.12f * Mathf.Cos(5f * ang);
                float innerR = waveR - 0.04f;
                float outerR = waveR + 0.04f;
                verts.Add(new Vector3(Mathf.Cos(ang) * innerR, 0.06f, Mathf.Sin(ang) * innerR));
                verts.Add(new Vector3(Mathf.Cos(ang) * outerR, 0.06f, Mathf.Sin(ang) * outerR));
                uvs.Add(new Vector2(t, 0f)); uvs.Add(new Vector2(t, 1f));
                colors.Add(ringTint); colors.Add(ringTint);

                if (i > 0)
                {
                    int cur = rStart + i * 2;
                    int prev = cur - 2;
                    tris.Add(prev); tris.Add(cur + 1); tris.Add(prev + 1);
                    tris.Add(prev); tris.Add(cur); tris.Add(cur + 1);
                }
            }

            var mesh = new Mesh { name = "CosmosCymaticHarpAndChladni" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDnaDoubleHelixMesh()
        {
            const int steps = 28;
            var verts = new List<Vector3>(steps * 12);
            var uvs = new List<Vector2>(steps * 12);
            var colors = new List<Color>(steps * 12);
            var tris = new List<int>(steps * 24);

            Color strandA = new Color(0.32f, 1.42f, 0.72f, 1f);
            Color strandB = new Color(0.38f, 0.95f, 1.48f, 1f);
            Color rungCol = new Color(1.35f, 0.78f, 1.45f, 1f);

            const float radius = 0.34f;
            const float height = 1.65f;

            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps;
                float t1 = (i + 1) / (float)steps;
                float a0 = t0 * Mathf.PI * 4f;
                float a1 = t1 * Mathf.PI * 4f;
                float y0 = (t0 - 0.5f) * height;
                float y1 = (t1 - 0.5f) * height;

                Vector3 p0A = new Vector3(Mathf.Cos(a0) * radius, y0, Mathf.Sin(a0) * radius);
                Vector3 p1A = new Vector3(Mathf.Cos(a1) * radius, y1, Mathf.Sin(a1) * radius);
                Vector3 p0B = new Vector3(-p0A.x, y0, -p0A.z);
                Vector3 p1B = new Vector3(-p1A.x, y1, -p1A.z);

                AppendBeamSegment(verts, uvs, colors, tris, p0A, p1A, 0.024f, strandA);
                AppendBeamSegment(verts, uvs, colors, tris, p0B, p1B, 0.024f, strandB);
                if ((i & 1) == 0)
                {
                    AppendBeamSegment(verts, uvs, colors, tris, p0A, p0B, 0.016f, rungCol);
                }
            }

            var mesh = new Mesh { name = "CosmosDnaDoubleHelix" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateSetiBeaconConeMesh()
        {
            const int segs = 24;
            const float rBottom = 0.14f;
            const float rTop = 1.15f;
            const float height = 3.2f;

            var verts = new List<Vector3>((segs + 1) * 2);
            var uvs = new List<Vector2>((segs + 1) * 2);
            var colors = new List<Color>((segs + 1) * 2);
            var tris = new List<int>(segs * 12);

            Color amberCol = new Color(1.55f, 0.82f, 0.48f, 1f);
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float ang = t * Mathf.PI * 2f;
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);
                verts.Add(new Vector3(c * rBottom, 0f, s * rBottom));
                verts.Add(new Vector3(c * rTop, height, s * rTop));
                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));
                colors.Add(amberCol);
                colors.Add(amberCol * 0.45f);

                if (i > 0)
                {
                    int cur = i * 2;
                    int prev = cur - 2;
                    tris.Add(prev); tris.Add(cur + 1); tris.Add(prev + 1);
                    tris.Add(prev); tris.Add(cur); tris.Add(cur + 1);
                    tris.Add(prev); tris.Add(prev + 1); tris.Add(cur + 1);
                    tris.Add(prev); tris.Add(cur + 1); tris.Add(cur);
                }
            }

            var mesh = new Mesh { name = "CosmosSetiBeaconCone" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateLotusPetalMesh()
        {
            var mesh = new Mesh { name = "CosmosHorizonLotusPetal" };
            mesh.vertices = new[]
            {
                new Vector3( 0.00f, 0.00f, 0.00f),
                new Vector3(-0.26f, 0.42f, 0.10f),
                new Vector3( 0.00f, 0.96f, 0.24f),
                new Vector3( 0.26f, 0.42f, 0.10f),
                new Vector3( 0.00f, 0.44f, 0.02f),
            };
            mesh.uv = new[]
            {
                new Vector2(0.5f, 0.0f),
                new Vector2(0.0f, 0.45f),
                new Vector2(0.5f, 1.0f),
                new Vector2(1.0f, 0.45f),
                new Vector2(0.5f, 0.45f),
            };
            mesh.triangles = new[]
            {
                0, 1, 4,
                1, 2, 4,
                2, 3, 4,
                3, 0, 4,
                0, 4, 1,
                1, 4, 2,
                2, 4, 3,
                3, 4, 0,
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void AppendBeamSegment(
            List<Vector3> verts,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> tris,
            Vector3 a,
            Vector3 b,
            float halfWidth,
            Color tint)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f)
            {
                side = Vector3.right;
            }
            side = side.normalized * halfWidth;

            int idx = verts.Count;
            verts.Add(a - side);
            verts.Add(a + side);
            verts.Add(b + side);
            verts.Add(b - side);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            colors.Add(tint); colors.Add(tint); colors.Add(tint); colors.Add(tint);

            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
            tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
        }

        private static float NextRand(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / 16777215f;
        }

        private void ClearBuilt()
        {
            if (_vistaRoot != null)
            {
                Destroy(_vistaRoot.gameObject);
                _vistaRoot = null;
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
            _built = false;
        }

        private void OnDestroy()
        {
            ClearBuilt();
        }
    }
}
