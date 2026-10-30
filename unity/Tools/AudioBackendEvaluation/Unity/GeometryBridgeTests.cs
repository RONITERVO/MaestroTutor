// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        sealed class PublishedSurface : IAcousticGeometry
        {
            public bool Active {get;set;}=true;
            public bool HasGeometry {get;set;}=true;
            public bool Scanned {get;set;}
            public bool ReflectionBoundary {get;set;}=true;
            public int Revision {get;set;}
            public Matrix4x4 Pose {get;set;}=Matrix4x4.identity;
            public string Issue {get;set;}
            internal int Reads;internal bool Fail;internal Action OnRead;
            internal readonly List<int> ReadFrames=new();
            public bool Read(out float[] points,out int[] indices,out string issue)
            {
                Reads++;ReadFrames.Add(Time.frameCount);OnRead?.Invoke();
                points=new float[]{-3,-3,3,3,-3,3,3,3,3,-3,3,3,-3,-3,-3,3,-3,-3,3,3,-3,-3,3,-3};
                indices=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                issue=Fail?"Boundary not ready":null;return !Fail;
            }
        }
        sealed class Publication
        {
            internal bool Available=true;internal int Pending;
            internal readonly List<(AcousticGeometryKey key,PublishedSurface surface)> Inputs=new();
            internal AcousticGeometryKey Add(PublishedSurface surface)
            {var key=new AcousticGeometryKey();Inputs.Add((key,surface));return key;}
            internal AcousticGeometryCatalog Read()
            {
                var result=new AcousticGeometryBinding[Inputs.Count];
                for(int i=0;i<result.Length;i++)result[i]=new AcousticGeometryBinding(Inputs[i].key,Inputs[i].surface);
                return new AcousticGeometryCatalog(Available,Pending,result);
            }
        }
        IEnumerator BridgeReady(GeometryBridge bridge,bool held=false)
        {
            double end=Time.realtimeSinceStartupAsDouble+15;
            do
            {
                bridge.Poll();
                var graph=sink.Owner.Active;
                if(!bridge.Preparing&&graph!=null&&(held||graph.CompletedRevision>=sink.Owner.RequestedSceneRevision))
                {Assert.IsNull(sink.Owner.Failure);yield break;}
                yield return null;
            }while(Time.realtimeSinceStartupAsDouble<end);
            Assert.Fail("Geometry publication deadline: "+bridge.Issue);
        }
        [UnityTest] public IEnumerator GeometryBridgeReusesMeshesAcrossMovementAndOwnerTransfer()
        {
            var source=new Publication();var surface=new PublishedSurface();var key=source.Add(surface);
            using var bridge=new GeometryBridge(sink.Owner,source.Read);
            yield return BridgeReady(bridge);Assert.IsNull(bridge.Issue);
            var graph=sink.Owner.Active;int meshes=PreparedGraph.MeshesOpened,instances=PreparedGraph.InstancesOpened;
            Assert.IsTrue(sink.Owner.Play(PreparedGraph.VoiceIds[0],sink.Owner.Generation));
            yield return Measure("published-room");Tone(440);
            surface.Pose=Matrix4x4.Translate(Vector3.right*200);
            yield return BridgeReady(bridge);yield return Measure("published-room-moved");
            Assert.Less(MeasuredPower(440),1e-16);Assert.AreEqual(1,surface.Reads,"Pose changes do not reread meshes");
            var retained=new PublishedSurface();source.Inputs[0]=(key,retained);
            yield return BridgeReady(bridge);yield return Measure("published-retained-owner");Tone(440);
            Assert.AreEqual(1,retained.Reads);Assert.AreEqual(meshes,PreparedGraph.MeshesOpened);
            Assert.AreEqual(instances,PreparedGraph.InstancesOpened,"Owner transfer retains exact placement identity");
            Assert.AreSame(graph,sink.Owner.Active);
        }
        [UnityTest] public IEnumerator GeometryBridgeFencesPendingAndUnavailableBoundaries()
        {
            var source=new Publication();source.Add(new PublishedSurface());
            using var bridge=new GeometryBridge(sink.Owner,source.Read);
            yield return BridgeReady(bridge);
            var dry=Begin(0,dry:1,wet:0);var wet=Begin(1,dry:0,wet:1);Queue(dry,440,chunks:40);Queue(wet,660,chunks:40);
            yield return PcmReady(0);yield return PcmReady(1);yield return Measure("publication-complete");Tone(440);Tone(660);
            source.Pending=1;yield return BridgeReady(bridge,true);
            yield return Measure("publication-missing-wall");Tone(440);Assert.AreEqual(1,bridge.OmittedBoundaries);
            // A finite DFT of the continuing dry tone can leak into 660 Hz.
            // Mute only that dry voice, then require the entire buffer to be zero.
            Assert.IsTrue(sink.Owner.SetPcmGain(PreparedGraph.VoiceIds[0],dry,0));
            int simulations=sink.Owner.Active.Simulations;yield return Measure("publication-still-pending");Assert.That(measured,Is.All.EqualTo(0));Assert.AreEqual(simulations,sink.Owner.Active.Simulations);
            Assert.IsTrue(sink.Owner.SetPcmGain(PreparedGraph.VoiceIds[0],dry,1));
            source.Pending=0;yield return BridgeReady(bridge);yield return Measure("publication-repaired");Tone(660);
            source.Available=false;bridge.Poll();yield return Measure("publication-unavailable");Tone(440);
            Assert.IsTrue(sink.Owner.SetPcmGain(PreparedGraph.VoiceIds[0],dry,0));
            yield return Measure("publication-unavailable-wet-only");Assert.That(measured,Is.All.EqualTo(0));
            Assert.IsTrue(sink.Owner.SetPcmGain(PreparedGraph.VoiceIds[0],dry,1));
            source.Available=true;yield return BridgeReady(bridge);yield return Measure("publication-owner-returned");Tone(660);
        }
        [UnityTest] public IEnumerator GeometryBridgeCancelsChangedDefinitionsWithinSharedBudget()
        {
            var source=new Publication();var surfaces=new List<PublishedSurface>();
            for(int i=0;i<12;i++){var s=new PublishedSurface{Pose=Matrix4x4.Translate(Vector3.right*i*10)};surfaces.Add(s);source.Add(s);}
            var window=new RoomPreparationBudget.Window();var other=new RoomPreparationBudget(()=>{},window);
            var units=new List<object>{null,null,null,null};var otherWork=other.Run(units);
            using var bridge=new GeometryBridge(sink.Owner,source.Read,window);
            bridge.Poll();Assert.IsTrue(bridge.Preparing);Assert.AreEqual(0,surfaces[0].Reads,"Existing work owns the shared frame budget");
            source.Inputs.Clear();source.Add(new PublishedSurface());
            yield return BridgeReady(bridge);Assert.IsNull(bridge.Issue);Assert.IsTrue(otherWork.IsCompleted);
            int abandoned=0;foreach(var s in surfaces)abandoned+=s.Reads;Assert.AreEqual(0,abandoned,"Cancelled snapshot cannot read old owners after its yield");
            int nativeMeshes=PreparedGraph.MeshesOpened;
            var replacement=source.Inputs[0].surface;replacement.Pose=Matrix4x4.Translate(Vector3.right*2);
            yield return BridgeReady(bridge);Assert.AreEqual(nativeMeshes,PreparedGraph.MeshesOpened);
        }
        [UnityTest] public IEnumerator GeometryBridgeCachesFailureUntilRepairOrRetry()
        {
            var source=new Publication();var surface=new PublishedSurface{Fail=true};source.Add(surface);
            using var bridge=new GeometryBridge(sink.Owner,source.Read);
            yield return BridgeReady(bridge,true);Assert.AreEqual(1,bridge.OmittedBoundaries);Assert.AreEqual(1,surface.Reads);
            for(int i=0;i<8;i++){bridge.Poll();yield return null;}
            Assert.AreEqual(1,surface.Reads,"Stable failures cannot keep rereading the same input");
            surface.Fail=false;bridge.Retry();yield return BridgeReady(bridge);Assert.AreEqual(2,surface.Reads);Assert.IsNull(bridge.Issue);
            surface.Fail=true;surface.Revision++;yield return BridgeReady(bridge,true);Assert.AreEqual(3,surface.Reads);
            surface.Fail=false;surface.Revision++;yield return BridgeReady(bridge);Assert.IsNull(bridge.Issue);
        }
        [UnityTest] public IEnumerator GeometryBridgeStopsOldWetOutputAtSubmission()
        {
            var source=new Publication();source.Add(new PublishedSurface());
            using var bridge=new GeometryBridge(sink.Owner,source.Read);
            yield return BridgeReady(bridge);var voice=Begin(0,dry:0);Queue(voice,440);yield return PcmReady(0);
            yield return Measure("publication-before-boundary-loss");Tone(440);
            sink.BypassRendering=true;yield return new WaitForSecondsRealtime(.1f);
            var graph=sink.Owner.Active;var block=new float[graph.Frame*2];
            graph.BeforeOutput=()=>{source.Pending=1;bridge.Poll();};
            try{Assert.IsTrue(sink.Owner.Render(block,2,AudioSettings.dspTime));}
            finally{graph.BeforeOutput=null;sink.BypassRendering=false;}
            foreach(float value in block)Assert.AreEqual(0,value,"An admitted old wet block cannot escape boundary invalidation");
            source.Pending=0;yield return BridgeReady(bridge);yield return Measure("publication-after-boundary-return");Tone(440);
        }
        [UnityTest] public IEnumerator GeometryBridgeBoundsAcquisitionAndKeepsScanReservation()
        {
            var source=new Publication{Pending=1};var inputs=new List<PublishedSurface>();
            for(int i=0;i<385;i++){var input=new PublishedSurface();inputs.Add(input);source.Add(input);}
            var scanned=new PublishedSurface{Scanned=true};source.Add(scanned);
            int opened=PreparedGraph.MeshesOpened;
            using var bridge=new GeometryBridge(sink.Owner,source.Read);
            yield return BridgeReady(bridge,true);
            Assert.AreEqual(1,bridge.Omitted);Assert.AreEqual(2,bridge.OmittedBoundaries);
            Assert.AreEqual(0,inputs[384].Reads,"Over-capacity input cannot allocate an unbounded staging mesh");
            Assert.AreEqual(1,scanned.Reads,"Virtual saturation cannot consume the scan reservation");
            var perFrame=new Dictionary<int,int>();
            foreach(var input in inputs)foreach(int frame in input.ReadFrames)
                perFrame[frame]=perFrame.TryGetValue(frame,out var count)?count+1:1;
            foreach(int frame in scanned.ReadFrames)perFrame[frame]=perFrame.TryGetValue(frame,out var count)?count+1:1;
            foreach(int count in perFrame.Values)Assert.LessOrEqual(count,RoomPreparationBudget.MaximumStepsPerFrame);
            source.Inputs.RemoveAt(0);yield return BridgeReady(bridge,true);
            Assert.AreEqual(0,bridge.Omitted);Assert.AreEqual(1,inputs[384].Reads,"Freed capacity admits the previously refused input");
            Assert.AreEqual(1,scanned.Reads);Assert.AreEqual(opened,PreparedGraph.MeshesOpened,"An incomplete boundary set stays outside native simulation");
        }
        [UnityTest] public IEnumerator GeometryBridgeCloseCancelsPreparationAndResetStaysHeld()
        {
            var source=new Publication();var surface=new PublishedSurface();source.Add(surface);
            var window=new RoomPreparationBudget.Window{Frame=Time.frameCount,Steps=4,Started=System.Diagnostics.Stopwatch.GetTimestamp()};
            var bridge=new GeometryBridge(sink.Owner,source.Read,window);bridge.Poll();Assert.IsTrue(bridge.Preparing);
            bridge.Dispose();yield return null;yield return null;Assert.AreEqual(0,surface.Reads);
            var owner=sink.Owner;var graph=owner.Active;owner.Configure(graph.Rate,graph.Frame);yield return Ready(owner);
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0],owner.Generation));yield return Measure("publication-closed-reset");
            Assert.Less(MeasuredPower(440),1e-16,"Reset cannot bypass the closed geometry publication");
        }
    }
}
