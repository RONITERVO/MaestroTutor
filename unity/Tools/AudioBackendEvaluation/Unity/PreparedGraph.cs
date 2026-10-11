// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Optional owned native graph. Its format is immutable; response updates have
// one worker and one DSP consumer. It is not the app's production audio pipeline.
using System;
using System.Runtime.InteropServices;
using System.Threading;
using SteamAudio;
using SVector = SteamAudio.Vector3;

namespace Maestro.AudioEvaluation
{
    internal sealed partial class PreparedGraph
    {
        const int MaximumVoices = AcousticFrame.MaximumSources;
        internal readonly int VoiceCount;
        readonly string[] voiceIds = new string[MaximumVoices];
        internal readonly int Rate, Frame;
        internal readonly long Generation;
        internal static int Opened, Closed, Diagnostics;
        // Root the reverse-P/Invoke delegate for every context, including a retired one.
        static readonly LogCallback Log = (level, message) =>
        {
            if (level == LogLevel.Warning || level == LogLevel.Error)
            {
                Interlocked.Increment(ref Diagnostics);
                System.Console.Error.WriteLine(message);
            }
        };
        readonly int[] playing = new int[MaximumVoices];
        readonly IntPtr[] sources = new IntPtr[MaximumVoices];
        readonly IntPtr[] reflections = new IntPtr[MaximumVoices];
        readonly IntPtr[] decoders = new IntPtr[MaximumVoices];
        readonly NativeDsp.Buffer[] inputs = new NativeDsp.Buffer[MaximumVoices];
        readonly NativeDsp.Buffer[] wet = new NativeDsp.Buffer[MaximumVoices];
        readonly NativeDsp.Buffer[] stereo = new NativeDsp.Buffer[MaximumVoices];
        readonly ReflectionEffectParams[] responses = new ReflectionEffectParams[MaximumVoices];
        readonly double[] phases = new double[MaximumVoices];
        readonly float[] signal, channel;
        readonly float[][] dryMix = new float [MaximumVoices][], reflectedMix = new float [MaximumVoices][];
        readonly PcmBinding[] renderedPcm = new PcmBinding[MaximumVoices];
        readonly bool[] renderedPcmAudible = new bool[MaximumVoices];
        internal Action BeforeOutput; // Isolated callback-boundary race probe.
        IntPtr context, scene, simulator, room, hrtf;
        int readers, retired, disposed, rendering;
        NativeDsp.DecodeParams decode;
        internal bool Disposed => Volatile.Read(ref disposed) != 0;
        int PlayingSlot(int index) => Volatile.Read(ref pcm[index]) is PcmBinding binding
                                          ? (binding.Voice?.Open == true ? 1 : 0)
                                          : Volatile.Read(ref playing[index]);
        internal int Playing { get { int count = 0; for (int i=0;i<VoiceCount;i++) count += PlayingSlot(i); return count; } }
        // Names used only by the original two-tone diagnostic fixtures. PCM
        // admission below accepts independent, bounded playback-instance IDs.
        internal static readonly string[] VoiceIds = { "maestro-voice", "world-voice" };

        PreparedGraph(int rate, int frame, long generation, AcousticFrame initial, long revision)
        {
            VoiceCount = initial.SourceCount;
            for (int i=0;i<Math.Min(VoiceCount, VoiceIds.Length);i++) voiceIds[i] = VoiceIds[i];
            Rate = rate;
            Frame = frame;
            Generation = generation;
            presentation = initial;
            completedRevision = revision;
            signal = new float[frame];
            channel = new float[frame];
            for (int i = 0; i < VoiceCount; i++)
            {
                dryMix[i] = new float[frame * 2];
                reflectedMix[i] = new float[frame * 2];
            }
        }
        static void Check(Error error)
        {
            if (error != Error.Success)
                throw new InvalidOperationException("Native audio creation: " + error);
        }
        static SVector V(float x, float y, float z) => new SVector { x = x, y = y, z = z };
        static CoordinateSpace3 Pose(float x, float y,
                                     float z) => new CoordinateSpace3 { right = V(1, 0, 0), up = V(0, 1, 0),
                                                                        ahead = V(0, 0, -1), origin = V(x, y, z) };

        // Initial construction owns every resource before publication. Later updates
        // retain a worker lease and use the pinned SDK's single-consumer IR handoff.
        internal static PreparedGraph Build(int rate, int frame, long generation, Action prepared = null,
                                            AcousticFrame initial = null, long revision = 0)
        {
            var graph = new PreparedGraph(rate, frame, generation, initial ?? AcousticFrame.Default, revision);
            Interlocked.Increment(ref Opened);
            try
            {
                graph.Prepare();
                prepared?.Invoke();
                return graph;
            }
            catch
            {
                graph.Retire();
                graph.TryDispose();
                throw;
            }
        }
        void Prepare()
        {
            var settings = new ContextSettings { version = Constants.kVersion, simdLevel = SIMDLevel.SSE2,
                                                 flags = ContextFlags.Validation, logCallback = Log };
            Check(API.iplContextCreate(ref settings, out context));
            var geometry = new SceneSettings { type = SceneType.Default };
            Check(API.iplSceneCreate(context, ref geometry, out scene));
            var simulation = new SimulationSettings { flags = SimulationFlags.Reflections,
                                                      sceneType = SceneType.Default,
                                                      reflectionType = ReflectionEffectType.Convolution,
                                                      maxNumOcclusionSamples = 8,
                                                      maxNumRays = 1024,
                                                      numDiffuseSamples = 64,
                                                      maxDuration = .25f,
                                                      maxOrder = 1,
                                                      maxNumSources = VoiceCount,
                                                      numThreads = 1,
                                                      rayBatchSize = 1,
                                                      numVisSamples = 8,
                                                      samplingRate = Rate,
                                                      frameSize = Frame };
            Check(API.iplSimulatorCreate(context, ref simulation, out simulator));
            API.iplSimulatorSetScene(simulator, scene);
            var sourceSettings = new SourceSettings { flags = SimulationFlags.Reflections };
            for (int i = 0; i < VoiceCount; i++)
            {
                Check(API.iplSourceCreate(simulator, ref sourceSettings, out sources[i]));
                API.iplSourceAdd(sources[i], simulator);
            }
            Simulate(presentation, completedRevision);
            var audio = new SteamAudio.AudioSettings { samplingRate = Rate, frameSize = Frame };
            var hrtfSettings = new HRTFSettings { type = HRTFType.Default, volume = 1 };
            Check(API.iplHRTFCreate(context, ref audio, ref hrtfSettings, out hrtf));
            PreparePcm(ref audio);
            decode = new NativeDsp.DecodeParams { order = 1, hrtf = hrtf, orientation = Pose(-1, 0, 0),
                                                  binaural = Bool.True };
            for (int i = 0; i < VoiceCount; i++)
            {
                var output = new SimulationOutputs();
                API.iplSourceGetOutputs(sources[i], SimulationFlags.Reflections, ref output);
                if (output.reflections.ir == IntPtr.Zero)
                    throw new InvalidOperationException("No prepared IR");
                responses[i] = output.reflections;
                responses[i].type = ReflectionEffectType.Convolution;
                responses[i].numChannels = 4;
                responses[i].irSize = Rate / 4;
                var effect = new NativeDsp.ReflectionSettings { type = ReflectionEffectType.Convolution, channels = 4,
                                                                irSize = Rate / 4 };
                Check(NativeDsp.iplReflectionEffectCreate(context, ref audio, ref effect, out reflections[i]));
                var decoder =
                    new NativeDsp.DecodeSettings { layout = new NativeDsp.Layout { type = 1 }, hrtf = hrtf, order = 1 };
                Check(NativeDsp.iplAmbisonicsDecodeEffectCreate(context, ref audio, ref decoder, out decoders[i]));
                Check(NativeDsp.iplAudioBufferAllocate(context, 1, Frame, ref inputs[i]));
                Check(NativeDsp.iplAudioBufferAllocate(context, 4, Frame, ref wet[i]));
                Check(NativeDsp.iplAudioBufferAllocate(context, 2, Frame, ref stereo[i]));
            }
        }
        void AddRoom()
        {
            // Pinned input arrays survive the call; the native mesh owns its copy.
            var vertices = new[] { V(-3, -3, -3), V(3, -3, -3), V(3, 3, -3), V(-3, 3, -3),
                                   V(-3, -3, 3),  V(3, -3, 3),  V(3, 3, 3),  V(-3, 3, 3) };
            var triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4,
                                    3, 7, 6, 3, 6, 2, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5 };
            var materials = new[] { .2f, .2f, .2f, .5f, .01f, .01f, .01f };
            var indices = new int[12];
            GCHandle a = default, b = default, c = default, d = default;
            try
            {
                a = GCHandle.Alloc(vertices, GCHandleType.Pinned);
                b = GCHandle.Alloc(triangles, GCHandleType.Pinned);
                c = GCHandle.Alloc(materials, GCHandleType.Pinned);
                d = GCHandle.Alloc(indices, GCHandleType.Pinned);
                var mesh = new StaticMeshSettings { numVertices = 8,
                                                    numTriangles = 12,
                                                    numMaterials = 1,
                                                    vertices = a.AddrOfPinnedObject(),
                                                    triangles = b.AddrOfPinnedObject(),
                                                    materials = c.AddrOfPinnedObject(),
                                                    materialIndices = d.AddrOfPinnedObject() };
                Check(API.iplStaticMeshCreate(scene, ref mesh, out room));
                API.iplStaticMeshAdd(room, scene);
            }
            finally
            {
                if (a.IsAllocated)
                    a.Free();
                if (b.IsAllocated)
                    b.Free();
                if (c.IsAllocated)
                    c.Free();
                if (d.IsAllocated)
                    d.Free();
            }
        }
        internal bool TryEnter()
        {
            Interlocked.Increment(ref readers);
            if (Volatile.Read(ref retired) == 0)
                return true;
            Interlocked.Decrement(ref readers);
            return false;
        }
        internal void Exit() => Interlocked.Decrement(ref readers);
        internal void Retire()
        {
            Interlocked.Exchange(ref retired, 1);
            RetirePcm();
        }
        internal bool SetPlaying(string id, bool value)
        {
            int index = FindSlot(id);
            if (index < 0 || Volatile.Read(ref retired) != 0 || Volatile.Read(ref pcm[index]) != null)
                return false;
            Volatile.Write(ref playing[index], value ? 1 : 0);
            return true;
        }
        // Control thread owns slot identity. DSP observes the immutable binding,
        // including its reset serial, rather than looking up a mutable string.
        int FindSlot(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128) return -1;
            for (int i=0;i<VoiceCount;i++) if (voiceIds[i] == id) return i;
            return -1;
        }
        int ClaimSlot(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || IsRetired) return -1;
            int existing = FindSlot(id);
            if (existing >= 0) return existing;
            for (int i=0;i<VoiceCount;i++) if (PlayingSlot(i) == 0)
            { voiceIds[i] = id; return i; }
            return -1;
        }
        // Caller holds a read lease. Only one consumer may mutate DSP history.
        // Mismatched callbacks produce silence, never process old-size buffers.
        internal bool Render(float[] output, int channels, double dspTime)
        {
            if (channels != 2 || output.Length != Frame * 2 || Interlocked.Exchange(ref rendering, 1) != 0)
                return false;
            try
            {
                long revision = Volatile.Read(ref completedRevision);
                var parameters = decode;
                var frame = Volatile.Read(ref presentation);
                parameters.orientation = frame.Listener;
                long geometryRevision = Volatile.Read(ref requiredRevision);
                bool geometryReady = revision >= geometryRevision;
                bool responsesConsumed = true;
                for (int voice = 0; voice < VoiceCount; voice++)
                {
                    Array.Clear(dryMix[voice], 0, dryMix[voice].Length);
                    Array.Clear(reflectedMix[voice], 0, reflectedMix[voice].Length);
                    var binding = Volatile.Read(ref pcm[voice]);
                    renderedPcm[voice] = binding;
                    if (binding != null)
                    {
                        responsesConsumed &= RenderPcm(voice, binding, frame, geometryReady, geometryRevision,
                                                       ref parameters, dryMix[voice], dspTime);
                        continue;
                    }
                    bool audible = Volatile.Read(ref playing[voice]) != 0;
                    // Consume every published IR even while a voice is muted.
                    // Otherwise the SDK's pending response can prevent newer writes.
                    // Muted voices still receive zero input and never mix their tails.
                    double step = 2 * Math.PI * (440 + 220 * voice) / Rate;
                    for (int i = 0; i < Frame; i++)
                    {
                        signal[i] = audible ? .03f * (float)Math.Sin(phases[voice]) : 0;
                        phases[voice] = (phases[voice] + step) % (2 * Math.PI);
                    }
                    Marshal.Copy(signal, 0, Marshal.ReadIntPtr(inputs[voice].data), Frame);
                    NativeDsp.iplReflectionEffectApply(reflections[voice], ref responses[voice], ref inputs[voice],
                                                       ref wet[voice], IntPtr.Zero);
                    NativeDsp.iplAmbisonicsDecodeEffectApply(decoders[voice], ref parameters, ref wet[voice],
                                                             ref stereo[voice]);
                    if (!audible || !geometryReady)
                        continue;
                    for (int c = 0; c < 2; c++)
                    {
                        Marshal.Copy(Marshal.ReadIntPtr(stereo[voice].data, c * IntPtr.Size), channel, 0, Frame);
                        for (int i = 0; i < Frame; i++)
                            reflectedMix[voice][2 * i + c] += channel[i];
                    }
                }
                // This acknowledges a callback that began after all native writes
                // for this revision. No second producer may run before it.
                BeforeOutput?.Invoke();
                bool wetCurrent = revision >= Volatile.Read(ref requiredRevision);
                for (int voice = 0; voice < VoiceCount; voice++)
                {
                    var binding = renderedPcm[voice];
                    // Stop/replacement can arrive after DSP but before submission.
                    // It discards only that voice's block; unrelated voices continue.
                    if (binding != Volatile.Read(ref pcm[voice]) || binding != null && binding.Voice?.Open != true)
                        continue;
                    bool audible = binding == null ? Volatile.Read(ref playing[voice]) != 0 : renderedPcmAudible[voice];
                    if (audible)
                        for (int i = 0; i < output.Length; i++)
                            output[i] += dryMix[voice][i] + (wetCurrent ? reflectedMix[voice][i] : 0);
                    if (binding != null && renderedPcmFinal[voice])
                        binding.Voice.SubmitRendererEnd(dspTime + (double)Frame / Rate);
                }
                if (responsesConsumed)
                    Volatile.Write(ref renderedRevision, revision);
                return true;
            }
            finally
            {
                Volatile.Write(ref rendering, 0);
            }
        }
        // Only the owner calls this, after the preparation Task completed.
        // Retirement is permanent, so a late reader can only decline the lease.
        internal bool TryDispose()
        {
            if (Volatile.Read(ref retired) == 0 || Volatile.Read(ref readers) != 0)
                return false;
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return true;
            for (int i = 0; i < VoiceCount; i++)
            {
                if (direct[i] != IntPtr.Zero)
                    NativeDsp.iplBinauralEffectRelease(ref direct[i]);
                if (decoders[i] != IntPtr.Zero)
                    NativeDsp.iplAmbisonicsDecodeEffectRelease(ref decoders[i]);
                if (reflections[i] != IntPtr.Zero)
                    NativeDsp.iplReflectionEffectRelease(ref reflections[i]);
                if (stereo[i].data != IntPtr.Zero)
                    NativeDsp.iplAudioBufferFree(context, ref stereo[i]);
                if (wet[i].data != IntPtr.Zero)
                    NativeDsp.iplAudioBufferFree(context, ref wet[i]);
                if (inputs[i].data != IntPtr.Zero)
                    NativeDsp.iplAudioBufferFree(context, ref inputs[i]);
                if (sources[i] != IntPtr.Zero)
                {
                    API.iplSourceRemove(sources[i], simulator);
                    API.iplSourceRelease(ref sources[i]);
                }
            }
            if (hrtf != IntPtr.Zero)
                API.iplHRTFRelease(ref hrtf);
            if (simulator != IntPtr.Zero)
            {
                API.iplSimulatorCommit(simulator);
                API.iplSimulatorRelease(ref simulator);
            }
            if (room != IntPtr.Zero)
            {
                API.iplStaticMeshRemove(room, scene);
                API.iplStaticMeshRelease(ref room);
            }
            if (scene != IntPtr.Zero)
            {
                API.iplSceneCommit(scene);
                API.iplSceneRelease(ref scene);
            }
            if (context != IntPtr.Zero)
                API.iplContextRelease(ref context);
            Interlocked.Increment(ref Closed);
            return true;
        }
    }
}
