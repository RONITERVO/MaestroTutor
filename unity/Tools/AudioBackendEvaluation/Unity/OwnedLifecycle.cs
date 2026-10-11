// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Optional experiment: one control thread, one preparation worker, one audio consumer.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Maestro.AudioEvaluation
{
    internal sealed partial class OwnedLifecycle
    {
        PreparedGraph active, retiring;
        Task<PreparedGraph> pending;
        int requestedRate, requestedFrame;
        long generation, attempted;
        bool closed;
        Action prepareHook;
        internal Exception Failure { get; private set; }
        internal Exception RetiredFailure { get; private set; }
        internal long Generation => generation;
        internal PreparedGraph Active => Volatile.Read(ref active);
        internal bool Drained => Active == null && retiring == null && pending == null && simulation == null;
        internal int InterruptedVoices { get; private set; }
        internal int StalePreparations { get; private set; }

        internal OwnedLifecycle(int voiceCapacity = 2) { desiredScene = AcousticFrame.CreateDefault(voiceCapacity); }
        internal int VoiceCapacity => desiredScene.SourceCount;

        // Control-thread API. Coalesce resets instead of accumulating workers/contexts.
        internal void Configure(int rate, int frame, Action prepared = null)
        {
            if (closed)
                throw new InvalidOperationException("Closed audio owner");
            if (rate < 24000 || rate > 192000 || frame < 64 || frame > 8192 || (frame & (frame - 1)) != 0)
                throw new ArgumentOutOfRangeException("Unsupported evaluation format");
            RetireActive();
            generation++;
            requestedRate = rate;
            requestedFrame = frame;
            prepareHook = prepared;
            Failure = null;
        }
        void RetireActive()
        {
            var old = Interlocked.Exchange(ref active, null);
            if (old == null)
                return;
            if (retiring != null)
                throw new InvalidOperationException("Retirement ownership overlap");
            InterruptedVoices += old.Playing;
            old.Retire();
            retiring = old;
        }
        internal bool Play(string id, long commandGeneration, bool play = true)
        {
            var graph = Active;
            return !closed && commandGeneration == generation && graph != null && graph.SetPlaying(id, play);
        }
        internal void Close()
        {
            if (closed)
                return;
            closed = true;
            generation++;
            RetireActive();
        }
        internal void Tick()
        {
            FinishSimulation();
            if (retiring != null && simulation == null && retiring.TryDispose())
                retiring = null;
            if (pending != null && pending.IsCompleted)
            {
                var finished = pending;
                pending = null;
                if (finished.IsFaulted)
                {
                    var failure = finished.Exception.GetBaseException();
                    if (closed || attempted != generation)
                    {
                        RetiredFailure = failure;
                        StalePreparations++;
                    }
                    else
                        Failure = failure;
                }
                else
                {
                    var graph = finished.Result;
                    if (closed || graph.Generation != generation)
                    {
                        graph.Retire();
                        graph.TryDispose();
                        StalePreparations++;
                    }
                    else
                    {
                        graph.Present(desiredScene, geometryRevision);
                        Volatile.Write(ref active, graph);
                    }
                }
                finished.Dispose();
            }
            if (!closed && active == null && retiring == null && pending == null && attempted != generation)
            {
                attempted = generation;
                int rate = requestedRate, frame = requestedFrame;
                long next = generation;
                Action hook = prepareHook;
                var initial = desiredScene;
                long revision = sceneRevision;
                pending = Task.Run(() => PreparedGraph.Build(rate, frame, next, hook, initial, revision));
            }
            ScheduleSimulation();
        }
        internal bool Render(float[] samples, int channels, double dspTime = 0)
        {
            Array.Clear(samples, 0, samples.Length);
            var graph = Active;
            if (graph == null || !graph.TryEnter())
                return false;
            try
            {
                bool rendered = graph.Render(samples, channels, dspTime);
                // Do not publish a buffer from an interrupted generation.
                if (graph != Active)
                {
                    Array.Clear(samples, 0, samples.Length);
                    return false;
                }
                return rendered;
            }
            finally
            {
                graph.Exit();
            }
        }
    }
}
