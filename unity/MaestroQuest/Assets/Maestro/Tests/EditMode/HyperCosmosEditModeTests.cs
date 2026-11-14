// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Cosmos;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Maestro.Quest.Tests
{
    public sealed class HyperCosmosEditModeTests
    {
        readonly List<UnityEngine.Object> owned = new();

        T Own<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) owned.Add(obj);
            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i]) UnityEngine.Object.DestroyImmediate(owned[i]);
            }
            owned.Clear();
        }

        static void AssertFinite(float value, string label) =>
            Assert.That(float.IsFinite(value), Is.True, $"{label} must be finite (was {value})");

        static void AssertFinite(double value, string label) =>
            Assert.That(double.IsFinite(value), Is.True, $"{label} must be finite (was {value})");

        static void AssertFiniteColor(Color c, string label)
        {
            AssertFinite(c.r, label + ".r");
            AssertFinite(c.g, label + ".g");
            AssertFinite(c.b, label + ".b");
            AssertFinite(c.a, label + ".a");
            Assert.That(c.r, Is.GreaterThanOrEqualTo(0f), label + ".r");
            Assert.That(c.g, Is.GreaterThanOrEqualTo(0f), label + ".g");
            Assert.That(c.b, Is.GreaterThanOrEqualTo(0f), label + ".b");
        }

        static void AssertSoundRecipeValid(CosmosZoneSoundRecipe recipe, string label)
        {
            AssertFinite(recipe.BaseHz, label + ".BaseHz");
            AssertFinite(recipe.FilterHz, label + ".FilterHz");
            AssertFinite(recipe.NoiseAmount, label + ".NoiseAmount");
            AssertFinite(recipe.PulseRate, label + ".PulseRate");
            Assert.That(recipe.BaseHz, Is.InRange(20f, 4000f), label + ".BaseHz");
            Assert.That(recipe.FilterHz, Is.InRange(120f, 16000f), label + ".FilterHz");
            Assert.That(recipe.NoiseAmount, Is.InRange(0f, 1f), label + ".NoiseAmount");
            Assert.That(recipe.PulseRate, Is.InRange(0f, 40f), label + ".PulseRate");
            Assert.That(Enum.IsDefined(typeof(CosmosWaveform), recipe.Waveform), Is.True, label + ".Waveform");
        }

        static void AssertCueSpanValid(CosmosCueSpan span, string text, ushort durationMs, string label)
        {
            Assert.That(string.IsNullOrWhiteSpace(text), Is.False, label + " text must be non-empty");
            Assert.That(durationMs, Is.GreaterThan(0), label + " durationMs must be positive");
            Assert.That(span.Offset, Is.GreaterThanOrEqualTo(0), label + ".Offset");
            Assert.That(span.Count, Is.GreaterThan(0), label + ".Count");
            Assert.That(span.Offset + span.Count, Is.LessThanOrEqualTo(CosmosCatalogData.WordCues.Length), label + " exceeds WordCues");

            float prevEndNorm = 0f;
            uint prevEndMs = 0;
            int prevCharEnd = 0;
            for (int i = 0; i < span.Count; i++)
            {
                CosmosWordCue cue = CosmosCatalogData.WordCues[span.Offset + i];
                Assert.That(cue.CharLength, Is.GreaterThan(0), $"{label}[{i}].CharLength");
                Assert.That(cue.CharStart + cue.CharLength, Is.LessThanOrEqualTo(text.Length), $"{label}[{i}] char bounds");
                Assert.That(cue.CharStart, Is.GreaterThanOrEqualTo(prevCharEnd), $"{label}[{i}] char monotonic");
                AssertFinite(cue.StartNormalized, $"{label}[{i}].StartNormalized");
                AssertFinite(cue.EndNormalized, $"{label}[{i}].EndNormalized");
                Assert.That(cue.StartNormalized, Is.GreaterThanOrEqualTo(prevEndNorm - 1e-4f), $"{label}[{i}] norm monotonic");
                Assert.That(cue.EndNormalized, Is.GreaterThan(cue.StartNormalized), $"{label}[{i}] norm strictly positive width");
                Assert.That(cue.EndNormalized, Is.LessThanOrEqualTo(1.0001f), $"{label}[{i}].EndNormalized <= 1");
                Assert.That(cue.StartMs, Is.GreaterThanOrEqualTo(prevEndMs), $"{label}[{i}] StartMs monotonic");
                Assert.That(cue.EndMs, Is.GreaterThanOrEqualTo(cue.StartMs), $"{label}[{i}] EndMs >= StartMs");
                Assert.That(cue.EndMs, Is.LessThanOrEqualTo((uint)durationMs + 2u), $"{label}[{i}] EndMs <= durationMs");
                prevEndNorm = cue.EndNormalized;
                prevEndMs = cue.EndMs;
                prevCharEnd = cue.CharStart + cue.CharLength;
            }
        }

        [Test]
        public void CatalogDataCoversAll25ExperiencesAnd376ZonesWithEsEnFiParity()
        {
            Assert.That(CosmosCatalogData.ExperienceCount, Is.EqualTo(25));
            Assert.That(CosmosCatalogData.Experiences.Length, Is.EqualTo(25));
            Assert.That(CosmosCatalogData.TotalCatalogZones, Is.EqualTo(376));
            Assert.That(CosmosCatalogData.ContinuityEdgeCount, Is.EqualTo(25));
            Assert.That(CosmosCatalogData.ContinuityEdges.Length, Is.EqualTo(25));
            Assert.That(CosmosCatalogData.FlagshipZones.Length, Is.GreaterThan(0));
            Assert.That(CosmosCatalogData.WordCues.Length, Is.GreaterThan(0));
            Assert.That(CosmosCatalogData.SolarBodies.Length, Is.GreaterThan(0));
            Assert.That(CosmosCatalogData.GalacticLandmarks.Length, Is.GreaterThan(0));
            Assert.That(CosmosCatalogData.MusicPitches.Length, Is.EqualTo(12));
            Assert.That(CosmosCatalogData.MusicTimbres.Length, Is.GreaterThan(0));
            Assert.That(CosmosCatalogData.ColorSamples.Length, Is.GreaterThan(0));

            Assert.That(CosmosCatalogData.TryGetExperience(0, out _), Is.False);
            Assert.That(CosmosCatalogData.TryGetExperience(26, out _), Is.False);

            int totalZoneCount = 0;
            for (byte id = 1; id <= CosmosCatalogData.ExperienceCount; id++)
            {
                Assert.That(CosmosCatalogData.TryGetExperience(id, out var meta), Is.True, $"Experience {id} missing");
                Assert.That(meta.Id, Is.EqualTo(id));
                Assert.That(string.IsNullOrWhiteSpace(meta.Slug), Is.False, $"Experience {id} Slug");
                Assert.That(string.IsNullOrWhiteSpace(meta.LabelEs), Is.False, $"Experience {id} LabelEs");
                Assert.That(string.IsNullOrWhiteSpace(meta.LabelEn), Is.False, $"Experience {id} LabelEn");
                Assert.That(string.IsNullOrWhiteSpace(meta.LabelFi), Is.False, $"Experience {id} LabelFi");
                Assert.That(string.IsNullOrWhiteSpace(meta.TitleEs), Is.False, $"Experience {id} TitleEs");
                Assert.That(string.IsNullOrWhiteSpace(meta.TitleEn), Is.False, $"Experience {id} TitleEn");
                Assert.That(string.IsNullOrWhiteSpace(meta.TitleFi), Is.False, $"Experience {id} TitleFi");
                Assert.That(Enum.IsDefined(typeof(CosmosManifoldArchetype), meta.Archetype), Is.True);

                AssertFinite(meta.ScaleExpMin, $"Experience {id} ScaleExpMin");
                AssertFinite(meta.ScaleExpMax, $"Experience {id} ScaleExpMax");
                AssertFinite(meta.FocusScaleExp, $"Experience {id} FocusScaleExp");
                AssertFinite(meta.FocusRadiusMeters, $"Experience {id} FocusRadiusMeters");
                Assert.That(meta.ScaleExpMin, Is.LessThanOrEqualTo(meta.ScaleExpMax));
                Assert.That(meta.FocusRadiusMeters, Is.GreaterThan(0f));
                Assert.That(meta.ZoneCount, Is.GreaterThan(0), $"Experience {id} ZoneCount");
                Assert.That(meta.IslandIndex, Is.LessThan((byte)6), $"Experience {id} IslandIndex");

                AssertFiniteColor(meta.HdrAccentLinear, $"Experience {id} HdrAccentLinear");
                AssertSoundRecipeValid(meta.RepresentativeSound, $"Experience {id} RepresentativeSound");

                CosmosCatalogData.GetFlagshipZones(id, out int start, out int count);
                Assert.That(start, Is.EqualTo(meta.FlagshipStartIndex));
                Assert.That(count, Is.EqualTo(meta.FlagshipCount));
                if (id <= 2)
                {
                    // Experiences 01 (Chromatic Prism) & 02 (Musical Spectrum) are continuous manifolds
                    // backed by ColorSamples and MusicTimbres/MusicPitches rather than discrete FlagshipZones.
                    Assert.That(meta.ZoneCount, Is.EqualTo(1), $"Continuous Experience {id} ZoneCount");
                }
                else
                {
                    Assert.That(count, Is.GreaterThan(0), $"Experience {id} must have flagship zones");
                    Assert.That(start, Is.GreaterThanOrEqualTo(0));
                    Assert.That(start + count, Is.LessThanOrEqualTo(CosmosCatalogData.FlagshipZones.Length));
                    Assert.That(CosmosCatalogData.TryGetPrimaryZoneForExperience(id, out var primaryZone), Is.True);
                    Assert.That(primaryZone.ExperienceId, Is.EqualTo(id));
                    Assert.That(CosmosCatalogData.TryGetZone(id, primaryZone.ZoneIndex, out var lookedUp), Is.True);
                    Assert.That(lookedUp.ZoneId, Is.EqualTo(primaryZone.ZoneId));
                    totalZoneCount += meta.ZoneCount;
                }
            }

            Assert.That(totalZoneCount, Is.EqualTo(CosmosCatalogData.TotalCatalogZones));

            for (int i = 0; i < CosmosCatalogData.ContinuityEdges.Length; i++)
            {
                CosmosContinuityEdge edge = CosmosCatalogData.ContinuityEdges[i];
                Assert.That(edge.FromExperienceId, Is.InRange((byte)1, (byte)CosmosCatalogData.ExperienceCount));
                Assert.That(edge.ToExperienceId, Is.InRange((byte)1, (byte)CosmosCatalogData.ExperienceCount));
                Assert.That(Enum.IsDefined(typeof(CosmosBridgeKind), edge.Kind), Is.True);
            }

            for (int i = 0; i < CosmosCatalogData.ColorSamples.Length; i++)
            {
                CosmosColorSample cs = CosmosCatalogData.ColorSamples[i];
                Assert.That(string.IsNullOrWhiteSpace(cs.LabelEs), Is.False);
                Assert.That(string.IsNullOrWhiteSpace(cs.LabelEn), Is.False);
                Assert.That(string.IsNullOrWhiteSpace(cs.LabelFi), Is.False);
                AssertFiniteColor(cs.LinearColor, $"ColorSample[{i}]");
            }
        }

        [Test]
        public void CatalogAudioRecipesAndSyncVoiceCuesAreValidAcrossAllFlagshipZones()
        {
            for (int i = 0; i < CosmosCatalogData.FlagshipZones.Length; i++)
            {
                CosmosZoneEntry z = CosmosCatalogData.FlagshipZones[i];
                string tag = $"FlagshipZone[{i}](exp={z.ExperienceId},z={z.ZoneIndex})";
                Assert.That(z.ExperienceId, Is.InRange((byte)1, (byte)CosmosCatalogData.ExperienceCount), tag);
                Assert.That(string.IsNullOrWhiteSpace(z.ZoneId), Is.False, tag);
                AssertSoundRecipeValid(z.Sound, tag + ".Sound");

                Assert.That(string.IsNullOrWhiteSpace(z.Name.Es), Is.False, tag + ".Name.Es");
                Assert.That(string.IsNullOrWhiteSpace(z.Name.En), Is.False, tag + ".Name.En");
                Assert.That(string.IsNullOrWhiteSpace(z.Name.Fi), Is.False, tag + ".Name.Fi");
                Assert.That(string.IsNullOrWhiteSpace(z.Metric.Es), Is.False, tag + ".Metric.Es");
                Assert.That(string.IsNullOrWhiteSpace(z.Metric.En), Is.False, tag + ".Metric.En");
                Assert.That(string.IsNullOrWhiteSpace(z.Metric.Fi), Is.False, tag + ".Metric.Fi");
                Assert.That(string.IsNullOrWhiteSpace(z.Fact.Es), Is.False, tag + ".Fact.Es");
                Assert.That(string.IsNullOrWhiteSpace(z.Fact.En), Is.False, tag + ".Fact.En");
                Assert.That(string.IsNullOrWhiteSpace(z.Fact.Fi), Is.False, tag + ".Fact.Fi");

                AssertCueSpanValid(z.NameCuesEs, z.Name.Es, z.Name.DurationMsEs, tag + ".NameCuesEs");
                AssertCueSpanValid(z.NameCuesEn, z.Name.En, z.Name.DurationMsEn, tag + ".NameCuesEn");
                AssertCueSpanValid(z.NameCuesFi, z.Name.Fi, z.Name.DurationMsFi, tag + ".NameCuesFi");
                AssertCueSpanValid(z.MetricCuesEs, z.Metric.Es, z.Metric.DurationMsEs, tag + ".MetricCuesEs");
                AssertCueSpanValid(z.MetricCuesEn, z.Metric.En, z.Metric.DurationMsEn, tag + ".MetricCuesEn");
                AssertCueSpanValid(z.MetricCuesFi, z.Metric.Fi, z.Metric.DurationMsFi, tag + ".MetricCuesFi");
                AssertCueSpanValid(z.FactCuesEs, z.Fact.Es, z.Fact.DurationMsEs, tag + ".FactCuesEs");
                AssertCueSpanValid(z.FactCuesEn, z.Fact.En, z.Fact.DurationMsEn, tag + ".FactCuesEn");
                AssertCueSpanValid(z.FactCuesFi, z.Fact.Fi, z.Fact.DurationMsFi, tag + ".FactCuesFi");
            }

            Assert.That(CosmosCatalogData.ResolveMidiFrequency(69), Is.EqualTo(440f).Within(1e-4f));
            for (int midi = CosmosCatalogData.MidiMin; midi < CosmosCatalogData.MidiMin + CosmosCatalogData.MidiCount; midi++)
            {
                float hz = CosmosCatalogData.ResolveMidiFrequency(midi);
                AssertFinite(hz, $"MIDI {midi}");
                Assert.That(hz, Is.GreaterThan(20f).And.LessThan(20000f));
                var pitch = CosmosCatalogData.GetMusicPitch(midi);
                Assert.That(string.IsNullOrWhiteSpace(pitch.Symbol), Is.False);
            }

            for (int i = 0; i < CosmosCatalogData.MusicTimbres.Length; i++)
            {
                CosmosMusicTimbre timbre = CosmosCatalogData.MusicTimbres[i];
                Assert.That(Enum.IsDefined(typeof(CosmosWaveform), timbre.Waveform), Is.True);
                Assert.That(timbre.FilterHz, Is.InRange(120f, 16000f));
                Assert.That(timbre.MaxAmplitude, Is.InRange(0.01f, 1.5f));
                Assert.That(string.IsNullOrWhiteSpace(timbre.LabelEs), Is.False);
                Assert.That(string.IsNullOrWhiteSpace(timbre.LabelEn), Is.False);
                Assert.That(string.IsNullOrWhiteSpace(timbre.LabelFi), Is.False);
            }
        }

        [Test]
        public void CatalogLookupsAllocateZeroManagedMemory()
        {
            // Warm up static ctor and JIT before measuring allocations.
            CosmosCatalogData.TryGetExperience(3, out _);
            CosmosCatalogData.TryFindExperienceForScale(6.6, out var warmupExp);
            CosmosCatalogData.TryGetZone(3, 2, out var warmupZone);
            CosmosCatalogData.GetWordCues(warmupZone.FactCuesEn, out _, out _, out _);
            CosmosCatalogData.ResolveMidiFrequency(69);
            CosmosCatalogData.GetMusicPitch(60);
            CosmosCatalogData.ResolveNearestColorSample(210f, 80f, 55f);
            CosmosCatalogData.HslToPrismLocal(120f, 70f, 50f);
            CosmosCatalogData.HslToLinear(120f, 70f, 50f);
            _ = warmupExp.GetTitle(CosmosLocale.FiFi);

            long bytes = ManagedAllocations.Measure(() =>
            {
                for (int i = 0; i < 64; i++)
                {
                    byte id = (byte)((i % CosmosCatalogData.ExperienceCount) + 1);
                    CosmosCatalogData.TryGetExperience(id, out var exp);
                    CosmosCatalogData.GetExperience(id);
                    CosmosCatalogData.TryFindExperienceForScale(-30.0 + i, out _);
                    CosmosCatalogData.GetFlagshipZones(id, out _, out _);
                    CosmosCatalogData.TryGetPrimaryZoneForExperience(id, out var zone);
                    CosmosCatalogData.TryGetZone(id, zone.ZoneIndex, out _);
                    CosmosCatalogData.GetWordCues(zone.NameCuesEs, out _, out _, out _);
                    CosmosCatalogData.ResolveMidiFrequency(48 + (i % 48));
                    CosmosCatalogData.GetMusicPitch(48 + i);
                    CosmosCatalogData.ResolveNearestColorSample((i * 17f) % 360f, 80f, 52f);
                    CosmosCatalogData.HslToPrismLocal((i * 19f) % 360f, 75f, 50f);
                    CosmosCatalogData.HslToLinear((i * 23f) % 360f, 85f, 50f);
                    _ = exp.GetLabel(CosmosLocale.EnUs);
                }
            });

            Assert.That(bytes, Is.Zero, $"Catalog lookup loop must allocate 0 bytes on managed heap (allocated {bytes})");
        }

        [Test]
        public void FlightControllerMathMatchesTauDampingStickShapingThrottleAndPinchFloor()
        {
            Assert.That(CosmosFlightController.DefaultInertialTauSec, Is.EqualTo(0.18).Within(1e-12));
            Assert.That(CosmosFlightController.MinPinchSpanM, Is.EqualTo(0.05).Within(1e-12));

            // Exact tau = 0.18s single-step & dt-subdivision invariance check
            double oneTau = CosmosFlightController.ExponentialTauDamp(0.0, 10.0, 0.18, 0.18);
            Assert.That(oneTau, Is.EqualTo(10.0 * (1.0 - Math.Exp(-1.0))).Within(1e-10));

            double stepped = 2.0;
            for (int i = 0; i < 36; i++)
            {
                stepped = CosmosFlightController.ExponentialTauDamp(stepped, -6.0, 0.18, 0.01);
            }
            double analytical = CosmosFlightController.ExponentialTauDamp(2.0, -6.0, 0.18, 0.36);
            Assert.That(stepped, Is.EqualTo(analytical).Within(1e-9), "Exponential damping must be frame-rate invariant");

            Vector3 vecDamped = CosmosFlightController.ExponentialTauDamp(new Vector3(1f, -2f, 4f), new Vector3(5f, 6f, -4f), 0.18, 0.18);
            double factor = 1.0 - Math.Exp(-1.0);
            Assert.That(vecDamped.x, Is.EqualTo((float)(1.0 + 4.0 * factor)).Within(1e-5f));
            Assert.That(vecDamped.y, Is.EqualTo((float)(-2.0 + 8.0 * factor)).Within(1e-5f));
            Assert.That(vecDamped.z, Is.EqualTo((float)(4.0 - 8.0 * factor)).Within(1e-5f));

            // Stick axis shaping: 0.06 deadzone & 1.6 exponent
            Assert.That(CosmosFlightController.ShapeStickAxis(0.0), Is.EqualTo(0.0));
            Assert.That(CosmosFlightController.ShapeStickAxis(0.059), Is.EqualTo(0.0));
            Assert.That(CosmosFlightController.ShapeStickAxis(-0.06), Is.EqualTo(0.0));
            Assert.That(CosmosFlightController.ShapeStickAxis(0.06), Is.EqualTo(0.0));
            Assert.That(CosmosFlightController.ShapeStickAxis(1.0), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(CosmosFlightController.ShapeStickAxis(-1.0), Is.EqualTo(-1.0).Within(1e-12));
            double expectedMid = Math.Pow((0.53 - 0.06) / 0.94, 1.6);
            Assert.That(CosmosFlightController.ShapeStickAxis(0.53), Is.EqualTo(expectedMid).Within(1e-10));
            Assert.That(CosmosFlightController.ShapeStickAxis(-0.53), Is.EqualTo(-expectedMid).Within(1e-10));

            // Magnetic approach throttle: monotonically increases up to maxSpeed, clamped at MagneticMinSpeed
            double nearThrottle = CosmosFlightController.MagneticApproachThrottle(0.01, 4.0);
            double midThrottle = CosmosFlightController.MagneticApproachThrottle(3.0, 4.0);
            double farThrottle = CosmosFlightController.MagneticApproachThrottle(40.0, 4.0);
            Assert.That(nearThrottle, Is.EqualTo(CosmosFlightController.MagneticMinSpeed).Within(1e-12));
            Assert.That(midThrottle, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(farThrottle, Is.EqualTo(4.0).Within(1e-12));
            Assert.That(nearThrottle, Is.LessThan(midThrottle));
            Assert.That(midThrottle, Is.LessThanOrEqualTo(farThrottle));

            // Two-hand pinch MIN_PINCH_SPAN_M = 0.05m singularity guard & doubling span test
            CosmosPinchStepResult collapsedPinch = CosmosFlightController.ComputeTwoHandPinchStep(
                new Vector3(-0.005f, 0f, 0.5f), new Vector3(0.005f, 0f, 0.5f),
                new Vector3(-0.01f, 0f, 0.5f), new Vector3(0.01f, 0f, 0.5f));
            AssertFinite(collapsedPinch.DeltaU, "Collapsed pinch DeltaU");
            Assert.That(collapsedPinch.DeltaU, Is.EqualTo(0.0).Within(1e-12), "Both spans < 0.05m must clamp cleanly to MinPinchSpanM");

            CosmosPinchStepResult expandPinch = CosmosFlightController.ComputeTwoHandPinchStep(
                new Vector3(-0.10f, 0f, 0.5f), new Vector3(0.10f, 0f, 0.5f),
                new Vector3(-0.20f, 0f, 0.5f), new Vector3(0.20f, 0f, 0.5f));
            Assert.That(expandPinch.SpanRatio, Is.EqualTo(2.0f).Within(1e-5f));
            Assert.That(expandPinch.DeltaU, Is.EqualTo(-Math.Log10(2.0)).Within(1e-9));
        }

        [Test]
        public void FlightControllerHomeSplineReturnsSmoothlyToTargetAcrossShortestYaw()
        {
            Vector3 startPos = new Vector3(11.5f, 7.2f, -13.4f);
            Vector3 targetPos = new Vector3(0f, 0f, 0f);
            CosmosHomeSplineState spline = CosmosFlightController.CreateHomeSpline(
                startPos,
                startYawDeg: 170f,
                startScaleExp: 18.0,
                targetPos,
                targetYawDeg: -170f,
                targetScaleExp: CosmosFlightController.ColdStartScaleExp,
                isEarthHome: true);

            Assert.That(spline.Active, Is.True);
            Assert.That(spline.DurationSec, Is.GreaterThan(0.35).And.LessThanOrEqualTo(4.2));

            spline.Evaluate(0.0, out Vector3 p0, out float y0, out double u0, out bool fin0);
            Assert.That(fin0, Is.False);
            Assert.That(Vector3.Distance(p0, startPos), Is.LessThan(1e-4f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(y0, 170f)), Is.LessThan(1e-3f));
            Assert.That(u0, Is.EqualTo(18.0).Within(1e-6));

            // Midpoint crosses +/-180 deg shortest arc (never wraps back through 0 deg!)
            spline.Evaluate(spline.DurationSec * 0.5, out Vector3 pMid, out float yMid, out double uMid, out bool finMid);
            Assert.That(finMid, Is.False);
            AssertFinite(pMid.y, "Mid Y");
            AssertFinite(uMid, "Mid ScaleExp");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yMid, 180f)), Is.LessThan(1e-2f), "Shortest yaw arc must traverse +/-180 deg");

            spline.Evaluate(spline.DurationSec + 0.05, out Vector3 pEnd, out float yEnd, out double uEnd, out bool finEnd);
            Assert.That(finEnd, Is.True);
            Assert.That(Vector3.Distance(pEnd, targetPos), Is.LessThan(1e-4f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yEnd, -170f)), Is.LessThan(1e-3f));
            Assert.That(uEnd, Is.EqualTo(CosmosFlightController.ColdStartScaleExp).Within(1e-6));
        }

        [Test]
        public void NarrationFsmCoversLifecycleHysteresisSwitchMarginCancelFadeAndZeroAlloc()
        {
            CosmosFsmConfig cfg = CosmosFsmConfig.Default;
            Assert.That(cfg.SettlingEndMs, Is.EqualTo(200f).Within(1e-4f));
            Assert.That(cfg.LockDwellMs, Is.EqualTo(900f).Within(1e-4f));
            Assert.That(cfg.ExitConeMultiplier, Is.EqualTo(1.35f).Within(1e-4f));
            Assert.That(cfg.CandidateSwitchMargin, Is.EqualTo(1.25f).Within(1e-4f));
            Assert.That(cfg.CancelFadeMs, Is.EqualTo(120f).Within(1e-4f));

            var fsm = new CosmosNarrationFsm(cfg);
            var cands = new CosmosFocusCandidate[2];
            cands[0] = new CosmosFocusCandidate
            {
                EntityId = 101,
                DirCam = Vector3.forward,
                AngularRadiusDeg = 2.4f,
                SalienceTier = 1.0f,
                VisibilityWeight = 1.0f,
                ExperienceId = 3,
                ZoneIndex = 2,
                FeatureIndex = 0
            };

            Assert.That(fsm.State.Kind, Is.EqualTo(CosmosFsmStateKind.Moving));

            // 0..200ms -> Settling
            CosmosFsmOutput o1 = fsm.Step(Vector3.forward, 0f, 0f, 0.10f, cands, 1, false);
            Assert.That(o1.State.Kind, Is.EqualTo(CosmosFsmStateKind.Settling));
            Assert.That(o1.HasStartNarrationTarget, Is.False);

            // 200..900ms -> Locked
            CosmosFsmOutput o2 = fsm.Step(Vector3.forward, 0f, 0f, 0.20f, cands, 1, false);
            Assert.That(o2.State.Kind, Is.EqualTo(CosmosFsmStateKind.Locked));
            Assert.That(o2.DwellProgress, Is.EqualTo(300f / 900f).Within(0.02f));

            // >= 900ms -> Narrating + StartNarrationTarget emitted once
            CosmosFsmOutput o3 = fsm.Step(Vector3.forward, 0f, 0f, 0.65f, cands, 1, false);
            Assert.That(o3.State.Kind, Is.EqualTo(CosmosFsmStateKind.Narrating));
            Assert.That(o3.HasStartNarrationTarget, Is.True);
            Assert.That(o3.StartNarrationTarget.EntityId, Is.EqualTo(101UL));

            // narrationFinished -> Complete
            CosmosFsmOutput o4 = fsm.Step(Vector3.forward, 0f, 0f, 0.05f, cands, 1, narrationFinished: true);
            Assert.That(o4.State.Kind, Is.EqualTo(CosmosFsmStateKind.Complete));

            // 120ms cancel fade when breaking out of Narrating via motion
            fsm.Reset();
            fsm.Step(Vector3.forward, 0f, 0f, 0.50f, cands, 1, false);
            fsm.Step(Vector3.forward, 0f, 0f, 0.50f, cands, 1, false);
            Assert.That(fsm.State.Kind, Is.EqualTo(CosmosFsmStateKind.Narrating));

            CosmosFsmOutput cancel0 = fsm.Step(Vector3.forward, 0.50f, 0f, 0.01f, cands, 1, false);
            Assert.That(cancel0.State.Kind, Is.EqualTo(CosmosFsmStateKind.Moving));
            Assert.That(cancel0.ShouldCancelAudio, Is.True);
            Assert.That(cancel0.CancelFadeRemainingMs, Is.EqualTo(120f).Within(0.1f));
            Assert.That(cancel0.CancelFadeAlpha, Is.EqualTo(1f).Within(0.01f));

            CosmosFsmOutput cancelHalf = fsm.Step(Vector3.forward, 0.50f, 0f, 0.06f, cands, 1, false);
            Assert.That(cancelHalf.CancelFadeRemainingMs, Is.EqualTo(60f).Within(1f));
            Assert.That(cancelHalf.CancelFadeAlpha, Is.EqualTo(0.5f).Within(0.02f));

            CosmosFsmOutput cancelDone = fsm.Step(Vector3.forward, 0.50f, 0f, 0.07f, cands, 1, false);
            Assert.That(cancelDone.CancelFadeRemainingMs, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(cancelDone.CancelFadeAlpha, Is.EqualTo(0f).Within(1e-4f));

            // 1.35x tremor hysteresis cone: entry is 2.4 deg, exit is 2.4 * 1.35 = 3.24 deg.
            // Ramp slowly (< ExitRayAngularDegPerSec = 5.5 deg/s) so velocity guard doesn't trip first.
            fsm.Reset();
            fsm.Step(Vector3.forward, 0f, 0f, 0.22f, cands, 1, false);
            Assert.That(fsm.State.Kind, Is.EqualTo(CosmosFsmStateKind.Locked));

            for (int i = 1; i <= 25; i++)
            {
                float yaw = 2.85f * (i / 25f);
                fsm.Step(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, 0f, 0f, 0.025f, cands, 1, false);
            }
            Assert.That(fsm.State.Kind, Is.Not.EqualTo(CosmosFsmStateKind.Moving),
                "2.85 deg slow drift is inside 2.4 * 1.35 = 3.24 deg hysteresis exit cone");

            for (int i = 1; i <= 20; i++)
            {
                float yaw = 2.85f + 1.15f * (i / 20f);
                fsm.Step(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, 0f, 0f, 0.025f, cands, 1, false);
            }
            Assert.That(fsm.State.Kind, Is.EqualTo(CosmosFsmStateKind.Moving),
                "Moving ray well past 3.24 deg exit cone must release target");

            // 1.25x switch margin: challenger score < 1.25x incumbent keeps incumbent; >= 1.25x switches
            fsm.Reset();
            cands[0] = new CosmosFocusCandidate
            {
                EntityId = 101,
                DirCam = Vector3.forward,
                AngularRadiusDeg = 2.4f,
                SalienceTier = 1.0f,
                VisibilityWeight = 1.0f,
                ExperienceId = 3,
                ZoneIndex = 2,
                FeatureIndex = 0
            };
            fsm.Step(Vector3.forward, 0f, 0f, 0.30f, cands, 1, false);
            Assert.That(fsm.State.Target.EntityId, Is.EqualTo(101UL));

            // Slight competitor (score 1.15 < 1.25 * 1.0) must NOT switch
            cands[1] = new CosmosFocusCandidate
            {
                EntityId = 202,
                DirCam = Vector3.forward,
                AngularRadiusDeg = 2.4f,
                SalienceTier = 1.15f,
                VisibilityWeight = 1.0f,
                ExperienceId = 4,
                ZoneIndex = 3,
                FeatureIndex = 0
            };
            fsm.Step(Vector3.forward, 0f, 0f, 0.05f, cands, 2, false);
            Assert.That(fsm.State.Target.EntityId, Is.EqualTo(101UL), "Competitor < 1.25x score margin must not steal focus");

            // Stronger competitor (score 1.40 >= 1.25 * 1.0) switches cleanly
            cands[1].SalienceTier = 1.40f;
            fsm.Step(Vector3.forward, 0f, 0f, 0.05f, cands, 2, false);
            Assert.That(fsm.State.Target.EntityId, Is.EqualTo(202UL), "Competitor >= 1.25x score margin should switch target");

            // Zero managed allocations on steady-state Step
            long alloc = ManagedAllocations.Measure(() =>
            {
                for (int i = 0; i < 64; i++)
                {
                    fsm.Step(Vector3.forward, 0f, 0f, 0.016f, cands, 2, false);
                }
            });
            Assert.That(alloc, Is.Zero, $"CosmosNarrationFsm.Step allocated {alloc} bytes");
        }

        [Test]
        public void NarrationMemoryDeduplicatesOnlyCompletedHeadingAndSharedContextWithinActiveRegion()
        {
            var memory = new CosmosNarrationMemory();
            var segs = new[]
            {
                new CosmosMemorySegmentInput
                {
                    Id = "heading-solar",
                    Role = CosmosMemoryRole.RegionHeading,
                    Display = "Sistema Solar",
                    Narration = "El Sistema Solar",
                    Translation = "The Solar System"
                },
                new CosmosMemorySegmentInput
                {
                    Id = "ctx-rocky",
                    Role = CosmosMemoryRole.SharedContext,
                    Display = "Planeta rocoso",
                    Narration = "Planeta rocoso",
                    Translation = "Rocky planet"
                },
                new CosmosMemorySegmentInput
                {
                    Id = "fact-unique",
                    Role = null,
                    Display = "Marte",
                    Narration = "Es un desierto frío.",
                    Translation = "It is a cold desert."
                }
            };

            CosmosMemoryPlanState plan1 = memory.BeginPlan("solar-system", segs, "en-US");
            Assert.That(plan1.Skipped, Is.EqualTo(new[] { false, false, false }));

            // Partial completion (ES only) must NOT mark pair completed
            Assert.That(memory.MarkPairCompleted(plan1, 0, true, false), Is.False);
            Assert.That(memory.RememberedCount, Is.Zero);

            // Full ES + EN pair completion IS remembered for RegionHeading and SharedContext, never Role == null
            Assert.That(memory.MarkPairCompleted(plan1, 0, true, true), Is.True);
            Assert.That(memory.MarkPairCompleted(plan1, 1, true, true), Is.True);
            Assert.That(memory.MarkPairCompleted(plan1, 2, true, true), Is.False);
            Assert.That(memory.RememberedCount, Is.EqualTo(2));

            CosmosMemoryPlanState plan2 = memory.BeginPlan("solar-system", segs, "en-US");
            Assert.That(plan2.Skipped, Is.EqualTo(new[] { true, true, false }));

            // Switching locale must miss previous locale hash (never reuse stale EN skip for FI!)
            CosmosMemoryPlanState planFi = memory.BeginPlan("solar-system", segs, "fi-FI");
            Assert.That(planFi.Skipped, Is.EqualTo(new[] { false, false, false }));

            // InvalidatePending rejects older plan generation
            memory.InvalidatePending();
            Assert.That(memory.MarkPairCompleted(planFi, 0, true, true), Is.False);

            // Switching region clears remembered pairs
            memory.SetRegion("milky-way");
            Assert.That(memory.RememberedCount, Is.Zero);
        }

        [Test]
        public void ArchipelagoBuilderDescriptorsSatisfyWalkableRadiusSlopeAndRiserLimits()
        {
            var host = Own(new GameObject("ArchipelagoDescriptorCheck"));
            var builder = host.AddComponent<CosmosArchipelagoBuilder>();
            builder.Initialize(host.transform);

            Assert.That(builder.Islands.Count, Is.EqualTo(6));
            Assert.That(builder.Bridges.Count, Is.EqualTo(5));

            CosmosIslandDescriptor dais = builder.Islands[0];
            Assert.That(dais.CenterLocal, Is.EqualTo(Vector3.zero));
            Assert.That(dais.TopY, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(dais.WalkableRadius, Is.GreaterThanOrEqualTo(4.2f));

            for (int i = 0; i < builder.Islands.Count; i++)
            {
                CosmosIslandDescriptor isl = builder.Islands[i];
                float radialDist = new Vector2(isl.CenterLocal.x, isl.CenterLocal.z).magnitude;
                Assert.That(radialDist, Is.LessThanOrEqualTo(CosmosArchipelagoBuilder.MaxArchipelagoRadius),
                    $"Island {isl.Name} center must remain within 22m radius");
                Assert.That(RoomViewpoint.ValidPosition(isl.CenterLocal), Is.True,
                    $"Island {isl.Name} must satisfy RoomViewpoint.ValidPosition <= 25m");
                Assert.That(isl.WalkableRadius, Is.GreaterThanOrEqualTo(2.0f));
            }

            for (int i = 0; i < builder.Bridges.Count; i++)
            {
                CosmosBridgeDescriptor br = builder.Bridges[i];
                Assert.That(br.FromIsland, Is.EqualTo((byte)0));
                Assert.That(br.ToIsland, Is.InRange((byte)1, (byte)5));
                Assert.That(br.StepCount, Is.GreaterThan(0));
                Assert.That(br.StepRiseMeters, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.08f),
                    $"Bridge {br.BridgeIndex} riser {br.StepRiseMeters}m exceeds 0.08m cap");
                Assert.That(br.SlopeDegrees, Is.GreaterThan(0f).And.LessThanOrEqualTo(15.0f),
                    $"Bridge {br.BridgeIndex} slope {br.SlopeDegrees} deg exceeds 15 deg cap");
                Assert.That(br.WidthMeters, Is.GreaterThanOrEqualTo(1.2f));
            }

            Assert.That(builder.AllWalkableColliders.Count, Is.GreaterThan(0));
            for (int i = 0; i < builder.AllWalkableColliders.Count; i++)
            {
                BoxCollider col = builder.AllWalkableColliders[i];
                Assert.That(col, Is.Not.Null);
                Assert.That(col.gameObject.layer, Is.EqualTo(RoomPhysicsLayers.Environment));
                Assert.That(col.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(col.GetComponentInParent<Rigidbody>(), Is.Null);
                Assert.That(col.GetComponent<RoomWalkableSurface>(), Is.Not.Null);
            }
        }

        [Test]
        public void AllNineCosmosResourceMaterialsAndShadersLoadAndCompileCleanly()
        {
            var expected = new (string ResourcePath, string ShaderName, bool MustSupportInstancing)[]
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

            for (int i = 0; i < expected.Length; i++)
            {
                var item = expected[i];
                Material mat = Resources.Load<Material>(item.ResourcePath);
                Assert.That(mat, Is.Not.Null, $"Missing material at Resources/{item.ResourcePath}.mat");
                Assert.That(mat.shader, Is.Not.Null, $"{item.ResourcePath} has null shader");
                Assert.That(mat.shader.name, Is.EqualTo(item.ShaderName), $"{item.ResourcePath} shader mismatch");
                Assert.That(mat.shader.isSupported, Is.True, $"Shader {item.ShaderName} is not supported");
                if (item.MustSupportInstancing)
                {
                    Assert.That(mat.enableInstancing, Is.True, $"Material {item.ResourcePath} must enable GPU instancing");
                }
#if UNITY_EDITOR
                Assert.That(ShaderUtil.ShaderHasError(mat.shader), Is.False,
                    $"Shader {item.ShaderName} reported compilation errors in ShaderUtil");
#endif
            }
        }
    }
}
