// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;

namespace Maestro.AudioEvaluation
{
    internal sealed partial class OwnedLifecycle
    {
        AcousticFrame desiredScene;
        long sceneRevision, geometryRevision;
        Task simulation;
        PreparedGraph simulationGraph;
        Action simulationHook;
        internal long RequestedSceneRevision => sceneRevision;
        internal bool Updating => simulation != null;
        // Control-thread call. Latest snapshot wins; no unbounded update queue.
        internal long UpdateScene(AcousticFrame frame, Action afterSimulation = null)
        {
            if (closed)
                throw new InvalidOperationException("Closed audio owner");
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.SourceCount != desiredScene.SourceCount) throw new ArgumentException("Acoustic topology changed");
            sceneRevision++;
            if (!ReferenceEquals(desiredScene.Geometry, frame.Geometry))
                geometryRevision = sceneRevision;
            desiredScene = frame;
            simulationHook = afterSimulation;
            Active?.Present(frame, geometryRevision);
            return sceneRevision;
        }
        // One pose entry point: pure rotation changes decoding immediately;
        // translation also requests an updated propagation response.
        internal long UpdateListener(SteamAudio.CoordinateSpace3 pose)
        {
            if (closed)
                throw new InvalidOperationException("Closed audio owner");
            var next = desiredScene.WithListener(pose);
            var before = desiredScene.Listener.origin;
            if (pose.origin.x != before.x || pose.origin.y != before.y || pose.origin.z != before.z)
                return UpdateScene(next);
            desiredScene = next;
            Active?.Present(desiredScene, geometryRevision);
            return sceneRevision;
        }
        void ScheduleSimulation()
        {
            var graph = Active;
            if (closed || Failure != null || simulation != null || graph == null ||
                (sceneRevision <= graph.CompletedRevision && !graph.NeedsPcmResponse) ||
                graph.RenderedRevision < graph.CompletedRevision || !graph.TryEnter())
                return;
            if (sceneRevision <= graph.CompletedRevision)
                sceneRevision++;
            var resets = graph.SnapshotResetAcks();
            var frame = desiredScene;
            long revision = sceneRevision;
            var hook = simulationHook;
            simulationHook = null; // A retained diagnostic gate belongs to this update only.
            simulationGraph = graph;
            try
            {
                simulation = Task.Run(() =>
                                      {
                                          try
                                          {
                                              if (!graph.IsRetired)
                                              {
                                                  graph.Simulate(frame, revision, resets);
                                                  hook?.Invoke();
                                              }
                                          }
                                          finally
                                          {
                                              graph.Exit();
                                          }
                                      });
            }
            catch
            {
                simulationGraph = null;
                graph.Exit();
                throw;
            }
        }
        void FinishSimulation()
        {
            if (simulation == null || !simulation.IsCompleted)
                return;
            var finished = simulation;
            var graph = simulationGraph;
            simulation = null;
            simulationGraph = null;
            if (finished.IsFaulted)
            {
                var failure = finished.Exception.GetBaseException();
                if (closed || graph != Active)
                    RetiredFailure = failure;
                else
                {
                    // A partially applied geometry/input change cannot keep rendering
                    // as if it succeeded. Retire this graph and require explicit retry.
                    Failure = failure;
                    RetireActive();
                }
            }
            finished.Dispose();
        }
    }
}
