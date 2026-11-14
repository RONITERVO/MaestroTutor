using System;
using UnityEngine;

namespace Maestro.Quest.Cosmos
{
    public enum CosmosLocale : byte
    {
        EsEs = 0,
        EnUs = 1,
        FiFi = 2,
    }

    public enum CosmosWaveform : byte
    {
        Sine = 0,
        Triangle = 1,
        Sawtooth = 2,
        Square = 3,
    }

    public enum CosmosLocomotionMode : byte
    {
        GroundedWalk = 0,
        FreeFlight = 1,
        AutoLanding = 2,
    }

    public enum CosmosBridgeKind : byte
    {
        ScaleDiveInward = 0,
        EmergenceHelix = 1,
        NestedSensoryLobe = 2,
        ScaleRiseOutward = 3,
        EmbeddedStellarArm = 4,
        HyperHorizonLotusPetal = 5,
        OuroborosReturn = 6,
    }

    public enum CosmosManifoldArchetype : byte
    {
        VolumetricChromaticPrism = 0,
        CymaticHarmonicHarp = 1,
        PlanetaryCutaway = 2,
        HeliocentricSystem = 3,
        SpiralGalaxy = 4,
        GravitationalFlowBasin = 5,
        VoronoiCosmicWeb = 6,
        DeepFieldSphericalShell = 7,
        HyperHorizonLotusWing = 8,
        QuantumSubatomicLattice = 9,
        EmergenceHelix = 10,
        ConnectomeCathedral = 11,
        OrbitalCivilizationWeb = 12,
        ExoplanetBiosphereBelt = 13,
    }

    public enum CosmosFocusTargetKind : byte
    {
        ExperienceCore = 0,
        PlanetBody = 1,
        DeepZone = 2,
        GalacticLandmark = 3,
        SensoryArtifact = 4,
        IslandSanctuary = 5,
        ContinuityBeacon = 6,
    }

    public static class CosmosLocaleExtensions
    {
        public static string Bcp47Code(this CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => "en-US",
            CosmosLocale.FiFi => "fi-FI",
            _ => "es-ES",
        };

        public static string ShortBadge(this CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => "EN",
            CosmosLocale.FiFi => "FI",
            _ => "ES",
        };
    }

    [Serializable]
    public struct CosmosScaleState
    {
        public const double MinScaleExp = -35.0;
        public const double MaxScaleExp = 30.0;
        public const double ColdStartScaleExp = 6.6;
        public const double WalkableScaleMin = 6.35;
        public const double WalkableScaleMax = 6.85;

        public double ScaleExp;
        public double TargetScaleExp;
        public float ScaleVelocity;
        public Vector3 VelocityLocal;
        public float SpeedMultiplier;
        public CosmosLocomotionMode Mode;
        public byte ActiveExperienceId;
        public byte ActiveIslandIndex;
        public bool VirtualModeActive;
        public float AltitudeAboveDais;

        public static CosmosScaleState CreateDefault() => new()
        {
            ScaleExp = ColdStartScaleExp,
            TargetScaleExp = ColdStartScaleExp,
            ScaleVelocity = 0f,
            VelocityLocal = Vector3.zero,
            SpeedMultiplier = 1f,
            Mode = CosmosLocomotionMode.GroundedWalk,
            ActiveExperienceId = 3,
            ActiveIslandIndex = 0,
            VirtualModeActive = false,
            AltitudeAboveDais = 0f,
        };

        public readonly bool IsWithinWalkableScale =>
            ScaleExp >= WalkableScaleMin && ScaleExp <= WalkableScaleMax;

        public static double MetersPerSceneUnit(double scaleExp) =>
            Math.Pow(10.0, scaleExp - ColdStartScaleExp);

        public void Clamp()
        {
            if (ScaleExp < MinScaleExp) ScaleExp = MinScaleExp;
            if (ScaleExp > MaxScaleExp) ScaleExp = MaxScaleExp;
            if (TargetScaleExp < MinScaleExp) TargetScaleExp = MinScaleExp;
            if (TargetScaleExp > MaxScaleExp) TargetScaleExp = MaxScaleExp;
            if (SpeedMultiplier < 1f) SpeedMultiplier = 1f;
            if (SpeedMultiplier > 3f) SpeedMultiplier = 3f;
        }
    }

    public readonly struct CosmosZoneSoundRecipe
    {
        public readonly float BaseHz;
        public readonly float FilterHz;
        public readonly float NoiseAmount;
        public readonly float PulseRate;
        public readonly CosmosWaveform Waveform;

        public CosmosZoneSoundRecipe(
            float baseHz,
            float filterHz,
            float noiseAmount,
            float pulseRate,
            CosmosWaveform waveform)
        {
            BaseHz = baseHz;
            FilterHz = filterHz;
            NoiseAmount = noiseAmount;
            PulseRate = pulseRate;
            Waveform = waveform;
        }

        public static CosmosZoneSoundRecipe DefaultAmbient =>
            new(110f, 1800f, 0.04f, 0.14f, CosmosWaveform.Sine);
    }

    public readonly struct CosmosWordCue
    {
        public readonly ushort CharStart;
        public readonly ushort CharLength;
        public readonly float StartNormalized;
        public readonly float EndNormalized;
        public readonly uint StartMs;
        public readonly uint EndMs;

        public CosmosWordCue(
            ushort charStart,
            ushort charLength,
            float startNormalized,
            float endNormalized,
            uint startMs,
            uint endMs)
        {
            CharStart = charStart;
            CharLength = charLength;
            StartNormalized = startNormalized;
            EndNormalized = endNormalized;
            StartMs = startMs;
            EndMs = endMs;
        }
    }

    public readonly struct CosmosCueSpan
    {
        public readonly int Offset;
        public readonly int Count;

        public CosmosCueSpan(int offset, int count)
        {
            Offset = offset;
            Count = count;
        }
    }

    public readonly struct CosmosBilingualText
    {
        public readonly string Es;
        public readonly string En;
        public readonly string Fi;
        public readonly ushort DurationMsEs;
        public readonly ushort DurationMsEn;
        public readonly ushort DurationMsFi;

        public CosmosBilingualText(
            string es,
            string en,
            string fi,
            ushort durationMsEs,
            ushort durationMsEn,
            ushort durationMsFi)
        {
            Es = es;
            En = en;
            Fi = fi;
            DurationMsEs = durationMsEs;
            DurationMsEn = durationMsEn;
            DurationMsFi = durationMsFi;
        }

        public string GetText(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => En,
            CosmosLocale.FiFi => Fi,
            _ => Es,
        };

        public ushort GetDurationMs(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => DurationMsEn,
            CosmosLocale.FiFi => DurationMsFi,
            _ => DurationMsEs,
        };
    }

    public readonly struct CosmosExperienceMeta
    {
        public readonly byte Id;
        public readonly string Slug;
        public readonly byte SectorId;
        public readonly CosmosManifoldArchetype Archetype;
        public readonly string LabelEs;
        public readonly string LabelEn;
        public readonly string LabelFi;
        public readonly string TitleEs;
        public readonly string TitleEn;
        public readonly string TitleFi;
        public readonly double ScaleExpMin;
        public readonly double ScaleExpMax;
        public readonly double FocusScaleExp;
        public readonly float FocusRadiusMeters;
        public readonly byte ZoneCount;
        public readonly byte IslandIndex;
        public readonly Color HdrAccentLinear;
        public readonly CosmosZoneSoundRecipe RepresentativeSound;
        public readonly int FlagshipStartIndex;
        public readonly int FlagshipCount;

        public CosmosExperienceMeta(
            byte id,
            string slug,
            byte sectorId,
            CosmosManifoldArchetype archetype,
            string labelEs,
            string labelEn,
            string labelFi,
            string titleEs,
            string titleEn,
            string titleFi,
            double scaleExpMin,
            double scaleExpMax,
            double focusScaleExp,
            float focusRadiusMeters,
            byte zoneCount,
            byte islandIndex,
            Color hdrAccentLinear,
            CosmosZoneSoundRecipe representativeSound,
            int flagshipStartIndex,
            int flagshipCount)
        {
            Id = id;
            Slug = slug;
            SectorId = sectorId;
            Archetype = archetype;
            LabelEs = labelEs;
            LabelEn = labelEn;
            LabelFi = labelFi;
            TitleEs = titleEs;
            TitleEn = titleEn;
            TitleFi = titleFi;
            ScaleExpMin = scaleExpMin;
            ScaleExpMax = scaleExpMax;
            FocusScaleExp = focusScaleExp;
            FocusRadiusMeters = focusRadiusMeters;
            ZoneCount = zoneCount;
            IslandIndex = islandIndex;
            HdrAccentLinear = hdrAccentLinear;
            RepresentativeSound = representativeSound;
            FlagshipStartIndex = flagshipStartIndex;
            FlagshipCount = flagshipCount;
        }

        public string GetLabel(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => LabelEn,
            CosmosLocale.FiFi => LabelFi,
            _ => LabelEs,
        };

        public string GetTitle(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => TitleEn,
            CosmosLocale.FiFi => TitleFi,
            _ => TitleEs,
        };
    }

    public readonly struct CosmosZoneEntry
    {
        public readonly byte ExperienceId;
        public readonly byte ZoneIndex;
        public readonly string ZoneId;
        public readonly Color SrgbColor;
        public readonly CosmosZoneSoundRecipe Sound;
        public readonly CosmosBilingualText Name;
        public readonly CosmosBilingualText Metric;
        public readonly CosmosBilingualText Fact;
        public readonly CosmosCueSpan NameCuesEs;
        public readonly CosmosCueSpan NameCuesEn;
        public readonly CosmosCueSpan NameCuesFi;
        public readonly CosmosCueSpan MetricCuesEs;
        public readonly CosmosCueSpan MetricCuesEn;
        public readonly CosmosCueSpan MetricCuesFi;
        public readonly CosmosCueSpan FactCuesEs;
        public readonly CosmosCueSpan FactCuesEn;
        public readonly CosmosCueSpan FactCuesFi;

        public CosmosZoneEntry(
            byte experienceId,
            byte zoneIndex,
            string zoneId,
            Color srgbColor,
            CosmosZoneSoundRecipe sound,
            CosmosBilingualText name,
            CosmosBilingualText metric,
            CosmosBilingualText fact,
            CosmosCueSpan nameCuesEs,
            CosmosCueSpan nameCuesEn,
            CosmosCueSpan nameCuesFi,
            CosmosCueSpan metricCuesEs,
            CosmosCueSpan metricCuesEn,
            CosmosCueSpan metricCuesFi,
            CosmosCueSpan factCuesEs,
            CosmosCueSpan factCuesEn,
            CosmosCueSpan factCuesFi)
        {
            ExperienceId = experienceId;
            ZoneIndex = zoneIndex;
            ZoneId = zoneId;
            SrgbColor = srgbColor;
            Sound = sound;
            Name = name;
            Metric = metric;
            Fact = fact;
            NameCuesEs = nameCuesEs;
            NameCuesEn = nameCuesEn;
            NameCuesFi = nameCuesFi;
            MetricCuesEs = metricCuesEs;
            MetricCuesEn = metricCuesEn;
            MetricCuesFi = metricCuesFi;
            FactCuesEs = factCuesEs;
            FactCuesEn = factCuesEn;
            FactCuesFi = factCuesFi;
        }

        public CosmosCueSpan GetNameCues(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => NameCuesEn,
            CosmosLocale.FiFi => NameCuesFi,
            _ => NameCuesEs,
        };

        public CosmosCueSpan GetMetricCues(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => MetricCuesEn,
            CosmosLocale.FiFi => MetricCuesFi,
            _ => MetricCuesEs,
        };

        public CosmosCueSpan GetFactCues(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => FactCuesEn,
            CosmosLocale.FiFi => FactCuesFi,
            _ => FactCuesEs,
        };
    }

    public readonly struct CosmosContinuityEdge
    {
        public readonly byte FromExperienceId;
        public readonly byte ToExperienceId;
        public readonly CosmosBridgeKind Kind;

        public CosmosContinuityEdge(byte fromExperienceId, byte toExperienceId, CosmosBridgeKind kind)
        {
            FromExperienceId = fromExperienceId;
            ToExperienceId = toExperienceId;
            Kind = kind;
        }
    }

    public readonly struct CosmosSolarBodyMeta
    {
        public readonly string Id;
        public readonly byte ZoneIndex;
        public readonly float NormalizedX;
        public readonly float Lane;
        public readonly float VisualRadiusUnits;
        public readonly Color BaseColor;
        public readonly Color BandColor;
        public readonly bool HasRings;

        public CosmosSolarBodyMeta(
            string id,
            byte zoneIndex,
            float normalizedX,
            float lane,
            float visualRadiusUnits,
            Color baseColor,
            Color bandColor,
            bool hasRings)
        {
            Id = id;
            ZoneIndex = zoneIndex;
            NormalizedX = normalizedX;
            Lane = lane;
            VisualRadiusUnits = visualRadiusUnits;
            BaseColor = baseColor;
            BandColor = bandColor;
            HasRings = hasRings;
        }
    }

    public readonly struct CosmosGalacticLandmarkMeta
    {
        public readonly byte ExperienceId;
        public readonly string Id;
        public readonly byte ZoneIndex;
        public readonly float NormalizedP;
        public readonly float Lane;
        public readonly float VisualSize;

        public CosmosGalacticLandmarkMeta(
            byte experienceId,
            string id,
            byte zoneIndex,
            float normalizedP,
            float lane,
            float visualSize)
        {
            ExperienceId = experienceId;
            Id = id;
            ZoneIndex = zoneIndex;
            NormalizedP = normalizedP;
            Lane = lane;
            VisualSize = visualSize;
        }
    }

    public readonly struct CosmosMusicTimbre
    {
        public readonly byte Index;
        public readonly string LabelEs;
        public readonly string LabelEn;
        public readonly string LabelFi;
        public readonly CosmosWaveform Waveform;
        public readonly float MaxAmplitude;
        public readonly float DetuneCents;
        public readonly float FilterHz;
        public readonly float AttackSec;
        public readonly float ReleaseSec;

        public CosmosMusicTimbre(
            byte index,
            string labelEs,
            string labelEn,
            string labelFi,
            CosmosWaveform waveform,
            float maxAmplitude,
            float detuneCents,
            float filterHz,
            float attackSec,
            float releaseSec)
        {
            Index = index;
            LabelEs = labelEs;
            LabelEn = labelEn;
            LabelFi = labelFi;
            Waveform = waveform;
            MaxAmplitude = maxAmplitude;
            DetuneCents = detuneCents;
            FilterHz = filterHz;
            AttackSec = attackSec;
            ReleaseSec = releaseSec;
        }

        public string GetLabel(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => LabelEn,
            CosmosLocale.FiFi => LabelFi,
            _ => LabelEs,
        };
    }

    public readonly struct CosmosMusicPitch
    {
        public readonly byte PitchIndex;
        public readonly string LatinEs;
        public readonly string Symbol;

        public CosmosMusicPitch(byte pitchIndex, string latinEs, string symbol)
        {
            PitchIndex = pitchIndex;
            LatinEs = latinEs;
            Symbol = symbol;
        }
    }

    public readonly struct CosmosColorSample
    {
        public readonly float HueDeg;
        public readonly float SatPct;
        public readonly float LightPct;
        public readonly string LabelEs;
        public readonly string LabelEn;
        public readonly string LabelFi;
        public readonly Color LinearColor;
        public readonly Vector3 PrismLocal;

        public CosmosColorSample(
            float hueDeg,
            float satPct,
            float lightPct,
            string labelEs,
            string labelEn,
            string labelFi,
            Color linearColor,
            Vector3 prismLocal)
        {
            HueDeg = hueDeg;
            SatPct = satPct;
            LightPct = lightPct;
            LabelEs = labelEs;
            LabelEn = labelEn;
            LabelFi = labelFi;
            LinearColor = linearColor;
            PrismLocal = prismLocal;
        }

        public string GetLabel(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => LabelEn,
            CosmosLocale.FiFi => LabelFi,
            _ => LabelEs,
        };
    }

    public readonly struct CosmosFocusTarget
    {
        public readonly int TargetId;
        public readonly CosmosFocusTargetKind Kind;
        public readonly byte ExperienceId;
        public readonly byte ZoneIndex;
        public readonly byte IslandIndex;
        public readonly string Slug;
        public readonly string LabelEs;
        public readonly string LabelEn;
        public readonly string LabelFi;
        public readonly Vector3 LocalPosition;
        public readonly float FocusRadius;
        public readonly double FocusScaleExp;
        public readonly float Salience;
        public readonly Color AccentLinear;
        public readonly CosmosZoneSoundRecipe Sound;
        public readonly int FlagshipZoneRecordIndex;

        public CosmosFocusTarget(
            int targetId,
            CosmosFocusTargetKind kind,
            byte experienceId,
            byte zoneIndex,
            byte islandIndex,
            string slug,
            string labelEs,
            string labelEn,
            string labelFi,
            Vector3 localPosition,
            float focusRadius,
            double focusScaleExp,
            float salience,
            Color accentLinear,
            CosmosZoneSoundRecipe sound,
            int flagshipZoneRecordIndex)
        {
            TargetId = targetId;
            Kind = kind;
            ExperienceId = experienceId;
            ZoneIndex = zoneIndex;
            IslandIndex = islandIndex;
            Slug = slug;
            LabelEs = labelEs;
            LabelEn = labelEn;
            LabelFi = labelFi;
            LocalPosition = localPosition;
            FocusRadius = focusRadius;
            FocusScaleExp = focusScaleExp;
            Salience = salience;
            AccentLinear = accentLinear;
            Sound = sound;
            FlagshipZoneRecordIndex = flagshipZoneRecordIndex;
        }

        public string GetLabel(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => LabelEn,
            CosmosLocale.FiFi => LabelFi,
            _ => LabelEs,
        };
    }

    public readonly struct CosmosIslandDescriptor
    {
        public readonly byte IslandIndex;
        public readonly string Name;
        public readonly string TitleEs;
        public readonly string TitleEn;
        public readonly string TitleFi;
        public readonly Vector3 CenterLocal;
        public readonly float TopY;
        public readonly float WalkableRadius;
        public readonly float OuterRadius;
        public readonly Color AccentLinear;
        public readonly byte PrimaryExp0;
        public readonly byte PrimaryExp1;
        public readonly byte PrimaryExp2;

        public CosmosIslandDescriptor(
            byte islandIndex,
            string name,
            string titleEs,
            string titleEn,
            string titleFi,
            Vector3 centerLocal,
            float topY,
            float walkableRadius,
            float outerRadius,
            Color accentLinear,
            byte primaryExp0,
            byte primaryExp1,
            byte primaryExp2)
        {
            IslandIndex = islandIndex;
            Name = name;
            TitleEs = titleEs;
            TitleEn = titleEn;
            TitleFi = titleFi;
            CenterLocal = centerLocal;
            TopY = topY;
            WalkableRadius = walkableRadius;
            OuterRadius = outerRadius;
            AccentLinear = accentLinear;
            PrimaryExp0 = primaryExp0;
            PrimaryExp1 = primaryExp1;
            PrimaryExp2 = primaryExp2;
        }

        public string GetTitle(CosmosLocale locale) => locale switch
        {
            CosmosLocale.EnUs => TitleEn,
            CosmosLocale.FiFi => TitleFi,
            _ => TitleEs,
        };
    }

    public readonly struct CosmosBridgeDescriptor
    {
        public readonly byte BridgeIndex;
        public readonly byte FromIsland;
        public readonly byte ToIsland;
        public readonly Vector3 StartTopLocal;
        public readonly Vector3 EndTopLocal;
        public readonly float WidthMeters;
        public readonly int StepCount;
        public readonly float StepRiseMeters;
        public readonly float SlopeDegrees;
        public readonly Color AccentLinear;

        public CosmosBridgeDescriptor(
            byte bridgeIndex,
            byte fromIsland,
            byte toIsland,
            Vector3 startTopLocal,
            Vector3 endTopLocal,
            float widthMeters,
            int stepCount,
            float stepRiseMeters,
            float slopeDegrees,
            Color accentLinear)
        {
            BridgeIndex = bridgeIndex;
            FromIsland = fromIsland;
            ToIsland = toIsland;
            StartTopLocal = startTopLocal;
            EndTopLocal = endTopLocal;
            WidthMeters = widthMeters;
            StepCount = stepCount;
            StepRiseMeters = stepRiseMeters;
            SlopeDegrees = slopeDegrees;
            AccentLinear = accentLinear;
        }
    }

    public static class CosmosShaderIds
    {
        public const string ShaderFarShellSky = "Cosmos/FarShellSky";
        public const string ShaderEarthHero = "Cosmos/EarthHero";
        public const string ShaderOrbitalPlanet = "Cosmos/OrbitalPlanet";
        public const string ShaderAuroralCurtain = "Cosmos/AuroralCurtain";
        public const string ShaderTetherBridgeBeam = "Cosmos/TetherBridgeBeam";
        public const string ShaderCrystallineIsland = "Cosmos/CrystallineIsland";
        public const string ShaderSpriteField = "Cosmos/SpriteField";
        public const string ShaderMsdfLiveInk = "Cosmos/MsdfLiveInk";
        public const string ShaderWristBead = "Cosmos/WristBead";

        public const string ResFarShellSkyMat = "Cosmos/Cosmos_FarShellSky";
        public const string ResEarthHeroMat = "Cosmos/Cosmos_EarthHero";
        public const string ResOrbitalPlanetMat = "Cosmos/Cosmos_OrbitalPlanet";
        public const string ResAuroralCurtainMat = "Cosmos/Cosmos_AuroralCurtain";
        public const string ResTetherBridgeBeamMat = "Cosmos/Cosmos_TetherBridgeBeam";
        public const string ResCrystallineIslandMat = "Cosmos/Cosmos_CrystallineIsland";
        public const string ResSpriteFieldMat = "Cosmos/Cosmos_SpriteField";
        public const string ResMsdfLiveInkMat = "Cosmos/Cosmos_MsdfLiveInk";
        public const string ResWristBeadMat = "Cosmos/Cosmos_WristBead";

        public static readonly int ScaleU = Shader.PropertyToID("_ScaleU");
        public static readonly int TimeSec = Shader.PropertyToID("_TimeSec");
        public static readonly int StarIntensity = Shader.PropertyToID("_StarIntensity");
        public static readonly int MilkyWayGain = Shader.PropertyToID("_MilkyWayGain");
        public static readonly int AccentColor = Shader.PropertyToID("_AccentColor");

        public static readonly int SunDir = Shader.PropertyToID("_SunDir");
        public static readonly int CloudTime = Shader.PropertyToID("_CloudTime");
        public static readonly int CityGlow = Shader.PropertyToID("_CityGlow");
        public static readonly int AtmosColor = Shader.PropertyToID("_AtmosColor");
        public static readonly int FocusGlow = Shader.PropertyToID("_FocusGlow");

        public static readonly int PlanetMode = Shader.PropertyToID("_PlanetMode");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BandColor = Shader.PropertyToID("_BandColor");
        public static readonly int RingColor = Shader.PropertyToID("_RingColor");
        public static readonly int GlowGain = Shader.PropertyToID("_GlowGain");
        public static readonly int CullMode = Shader.PropertyToID("_CullMode");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        public static readonly int ColorPrimary = Shader.PropertyToID("_ColorPrimary");
        public static readonly int ColorSecondary = Shader.PropertyToID("_ColorSecondary");
        public static readonly int CurtainSpeed = Shader.PropertyToID("_CurtainSpeed");
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");

        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int PulseSpeed = Shader.PropertyToID("_PulseSpeed");
        public static readonly int ActiveFocus = Shader.PropertyToID("_ActiveFocus");

        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int GridScale = Shader.PropertyToID("_GridScale");
        public static readonly int WorldLighting = Shader.PropertyToID("_WorldLighting");
        public static readonly int VisibilityRealDepth = Shader.PropertyToID("_VisibilityRealDepth");

        public static readonly int Softness = Shader.PropertyToID("_Softness");
        public static readonly int SpriteMode = Shader.PropertyToID("_SpriteMode");
        public static readonly int Phase = Shader.PropertyToID("_Phase");
        public static readonly int Billboard = Shader.PropertyToID("_Billboard");

        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int HaloColor = Shader.PropertyToID("_HaloColor");
        public static readonly int RevealRange = Shader.PropertyToID("_RevealRange");
        public static readonly int GlyphIndex = Shader.PropertyToID("_GlyphIndex");
        public static readonly int GlyphCount = Shader.PropertyToID("_GlyphCount");
        public static readonly int SdfMode = Shader.PropertyToID("_SdfMode");
        public static readonly int PxRange = Shader.PropertyToID("_PxRange");
        public static readonly int HaloWidth = Shader.PropertyToID("_HaloWidth");
        public static readonly int NibColor = Shader.PropertyToID("_NibColor");
        public static readonly int PhraseBounds = Shader.PropertyToID("_PhraseBounds");

        public static readonly int ActiveGlow = Shader.PropertyToID("_ActiveGlow");

        internal static Material ResolveMaterial(string resourcePath, string shaderName, Color fallbackColor)
        {
            Material loaded = Resources.Load<Material>(resourcePath);
            if (loaded != null)
            {
                return loaded;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            var mat = new Material(shader)
            {
                name = shaderName + " (RuntimeFallback)",
                enableInstancing = true,
            };

            if (mat.HasProperty(Color))
            {
                mat.SetColor(Color, fallbackColor);
            }

            if (mat.HasProperty(BaseColor))
            {
                mat.SetColor(BaseColor, fallbackColor);
            }

            return mat;
        }
    }
}
