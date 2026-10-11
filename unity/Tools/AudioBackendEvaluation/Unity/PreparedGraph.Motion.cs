// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using SteamAudio;

namespace Maestro.AudioEvaluation
{
    internal sealed partial class PreparedGraph
    {
        AcousticFrame presentation;
        long completedRevision, renderedRevision = -1, requiredRevision;
        int simulating, simulations;
        internal long CompletedRevision => Volatile.Read(ref completedRevision);
        internal long RenderedRevision => Volatile.Read(ref renderedRevision);
        internal int Simulations => Volatile.Read(ref simulations);
        internal bool IsRetired => Volatile.Read(ref retired) != 0;
        internal void Present(AcousticFrame frame, long geometryRevision)
        {
            Volatile.Write(ref presentation, frame);
            Volatile.Write(ref requiredRevision, geometryRevision);
        }
        // Exactly one worker owns scene/source input mutation. It holds a lease
        // throughout the call. DSP owns only effects and the native IR consumer.
        internal void Simulate(AcousticFrame frame, long revision)
        {
            if (Interlocked.CompareExchange(ref simulating, 1, 0) != 0)
                throw new InvalidOperationException("Overlapping acoustic simulation");
            try
            {
                if (frame.Room && room == IntPtr.Zero)
                    AddRoom();
                else if (!frame.Room && room != IntPtr.Zero)
                {
                    API.iplStaticMeshRemove(room, scene);
                    API.iplStaticMeshRelease(ref room);
                }
                API.iplSceneCommit(scene);
                API.iplSimulatorCommit(simulator);
                for (int i = 0; i < VoiceCount; i++)
                {
                    var input = new SimulationInputs { flags = SimulationFlags.Reflections,
                                                       source = i == 0 ? frame.First : frame.Second,
                                                       occlusionType = OcclusionType.Raycast,
                                                       numOcclusionSamples = 8,
                                                       occlusionRadius = .1f,
                                                       numTransmissionRays = 1,
                                                       hybridReverbTransitionTime = .125f,
                                                       hybridReverbOverlapPercent = .25f,
                                                       reverbScaleLow = 1,
                                                       reverbScaleMid = 1,
                                                       reverbScaleHigh = 1 };
                    API.iplSourceSetInputs(sources[i], SimulationFlags.Reflections, ref input);
                }
                var shared = new SimulationSharedInputs {
                    listener = frame.Listener, numRays = 1024, numBounces = 8,
                    duration = .25f,           order = 1,      irradianceMinDistance = .1f
                };
                API.iplSimulatorSetSharedInputs(simulator, SimulationFlags.Reflections, ref shared);
                API.iplSimulatorRunReflections(simulator);
                // Do not call GetOutputs while simulation and DSP overlap. The
                // initial IR pointer remains source-owned; order/length/format are
                // immutable. Pinned 4.8.1 commits and crossfades its triple buffer.
                Interlocked.Increment(ref simulations);
                Volatile.Write(ref completedRevision, revision);
            }
            finally
            {
                Volatile.Write(ref simulating, 0);
            }
        }
    }
}
