// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AudioSettings = UnityEngine.AudioSettings;
namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        GameObject LeaseScope()
        {
            var scope=new GameObject("Application PCM scope");scope.transform.SetParent(emitter.transform,false);
            scope.AddComponent<PcmOutputBackend>();return scope;
        }
        static void QueueInput(SpeechPcmStream stream,int frequency,int chunks=20)
        {
            var samples=new short[4800];
            for(int i=0;i<samples.Length;i++)samples[i]=(short)(900*Math.Sin(2*Math.PI*frequency*i/24000));
            for(int n=1;n<=chunks;n++)Assert.IsTrue(stream.TryWrite(n,samples,AudioSettings.dspTime,out var issue),issue);
        }
        static PcmOutputLease Lease(Transform parent,SpeechPcmStream input,bool spatial=true)
            =>PcmOutputLease.Open(parent,"Application speech or world sound",input,spatial,1,96,1,15);
        [UnityTest]
        public IEnumerator PcmLeasePlayerEntryUsesApplicationBoundaryAndDrainsNativeOwnership()
        {
            var task=PcmLeasePlayerProbe.Run();while(!task.IsCompleted)yield return null;
            Assert.IsFalse(task.IsFaulted,task.Exception?.ToString());var result=task.Result;
            Assert.AreEqual("passed",result.status,result.issue);Assert.AreEqual(1,result.completedVoices);
            Assert.AreEqual(4800,result.submitted);Assert.AreEqual(4800,result.played);Assert.IsTrue(result.drained);
            Assert.Greater(result.energy,1e-6);Assert.AreEqual(1,result.graphs);Assert.AreEqual(0,result.nativeDiagnostics);
            Debug.Log("NATIVE_LEASE_PLAYER_PROBE "+JsonUtility.ToJson(result));
        }
        [UnityTest]
        public IEnumerator PcmLeaseSealedQueueStartsOnlyOnRequestAndCompletesActualOutput()
        {
            var scope=LeaseScope();using var adapter=new PcmLeaseAdapter(scope.GetComponent<PcmOutputBackend>(),sink.Owner);
            var input=FreshStream();QueueInput(input,440,1);Assert.IsTrue(input.TryComplete(1,out _));
            using var lease=Lease(scope.transform,input);lease.SetPose(new Vector3(1,0,0),Quaternion.identity);
            yield return PcmReady(0);
            lease.SetPaused(false);yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(0,input.Read(AudioSettings.dspTime+60).Played,"Unpause before Start must not consume prequeued input");
            lease.SetPaused(true);lease.Start();yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(0,input.Read(AudioSettings.dspTime+60).Played,"Start must preserve a requested pause");
            capture.Clear();lease.SetPaused(false);
            double end=Time.realtimeSinceStartupAsDouble+10;
            while(!lease.IsDrained(AudioSettings.dspTime)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.IsTrue(lease.IsDrained(AudioSettings.dspTime));var captured=capture.Read();
            Assert.Greater(captured.blocks,0);Assert.IsTrue(captured.finite);Assert.Greater(Energy(captured.samples),1e-8);
            Assert.AreEqual(4800,input.Read(AudioSettings.dspTime).Played);Assert.IsTrue(lease.Available);
            Assert.IsNull(lease.Source,"The central renderer needs no dummy per-voice AudioSource");
            lease.Dispose();Assert.IsTrue(input.Read(AudioSettings.dspTime).Closed);Assert.IsFalse(lease.IsDrained(AudioSettings.dspTime+60));
        }
        [UnityTest]
        public IEnumerator PcmLeaseCompletionWaitsForNativeTailAndFinalOutputClock()
        {
            var scope=LeaseScope();using var adapter=new PcmLeaseAdapter(scope.GetComponent<PcmOutputBackend>(),sink.Owner);
            var input=FreshStream();QueueInput(input,440,1);Assert.IsTrue(input.TryComplete(1,out _));
            using var lease=Lease(scope.transform,input);lease.SetPose(new Vector3(1,0,0),Quaternion.identity);
            yield return PcmReady(0);
            sink.BypassRendering=true;yield return new WaitForSecondsRealtime(.1f);
            lease.Start();var graph=sink.Owner.Active;
            var block=new float[graph.Frame*2];double time=AudioSettings.dspTime;
            int count=0;while(!input.Read(time).SourceDrained&&count++<200){Assert.IsTrue(sink.Owner.Render(block,2,time));time+=(double)graph.Frame/graph.Rate;}
            Assert.IsTrue(input.Read(time).SourceDrained);Assert.IsFalse(lease.IsDrained(time+60),"Consumed input cannot invent renderer completion");
            double energy=0;count=0;
            graph.BeforeOutput=()=>Assert.IsFalse(lease.IsDrained(time+60),"No completion before the last output submission");
            try{while(!lease.IsDrained(time+60)&&count++<200){Assert.IsTrue(sink.Owner.Render(block,2,time));energy+=Energy(block);time+=(double)graph.Frame/graph.Rate;}}
            finally{graph.BeforeOutput=null;}
            Assert.Less(count,200);Assert.Greater(energy,1e-8,"A real native reflection tail must survive the application lease");
            Assert.IsFalse(lease.IsDrained(time));Assert.IsTrue(lease.IsDrained(time+PcmOutputLease.DeviceTail+.1));
            Assert.AreEqual(4800,input.Read(time+1).Played);sink.BypassRendering=false;
        }
        [UnityTest]
        public IEnumerator PcmLeaseAndDirectAliasesCannotCloseOrStealEachOthersInputs()
        {
            var owner=sink.Owner;var scope=LeaseScope();using var adapter=new PcmLeaseAdapter(scope.GetComponent<PcmOutputBackend>(),owner);
            var directInput=FreshStream();var direct=owner.BeginPcm("independent",owner.Generation,directInput,.02);Queue(direct,660,40);
            Assert.Throws<InvalidOperationException>(()=>Lease(scope.transform,directInput));Assert.IsTrue(direct.Open);
            var input=FreshStream();using var lease=Lease(scope.transform,input);QueueInput(input,440,40);lease.Start();
            Assert.Throws<InvalidOperationException>(()=>owner.BeginPcm("alias",owner.Generation,input,.02));
            Assert.IsFalse(input.Read(AudioSettings.dspTime).Closed);Assert.IsFalse(directInput.Read(AudioSettings.dspTime).Closed);
            yield return PcmReady(0);yield return PcmReady(1);yield return Measure("actual-lease-and-direct-owner");Tone(440);Tone(660);
            lease.Dispose();yield return Measure("lease-stopped-direct-owner-continues");Tone(660);
            Assert.Less(MeasuredPower(440),MeasuredPower(660)*.001);Assert.IsTrue(direct.Open);
        }
        [UnityTest]
        public IEnumerator PcmLeasePoseGainAndPauseReachNativeOutputWithoutStoppingPeer()
        {
            var owner=sink.Owner;owner.UpdateScene(new AcousticFrame(false,AcousticFrame.Pose(0,0,0),AcousticFrame.Pose(1,0,0),AcousticFrame.Pose(0,0,-1)));
            var scope=LeaseScope();using var adapter=new PcmLeaseAdapter(scope.GetComponent<PcmOutputBackend>(),owner);
            var anchor=new GameObject("Moving mouth");anchor.transform.SetParent(scope.transform,false);anchor.transform.position=new Vector3(1,0,0);
            var input=FreshStream();using var lease=Lease(anchor.transform,input);QueueInput(input,440,40);lease.Start();
            var peerInput=FreshStream();using var peer=Lease(scope.transform,peerInput,false);QueueInput(peerInput,660,40);peer.Start();
            yield return PcmReady(0);yield return PcmReady(1);yield return Measure("lease-parent-right");Tone(440);Tone(660);
            double full=StereoPower(440),other=StereoPower(660),before=MeasuredPower(440)/Power(measuredRight,AudioSettings.outputSampleRate,440);
            lease.SetGain(.25f);yield return Measure("lease-quarter-gain");
            Assert.That(StereoPower(440)/full,Is.EqualTo(.0625).Within(.008));Assert.That(StereoPower(660)/other,Is.EqualTo(1).Within(.05));
            lease.SetGain(1);anchor.transform.SetPositionAndRotation(new Vector3(-1,0,0),Quaternion.Euler(0,180,0));
            yield return Measure("lease-parent-left");double after=MeasuredPower(440)/Power(measuredRight,AudioSettings.outputSampleRate,440);
            Assert.Less(Math.Log(before)*Math.Log(after),0);Assert.Greater(Math.Abs(Math.Log(before)),.1);
            lease.SetPaused(true);yield return new WaitForSecondsRealtime(.15f);long played=input.Read(AudioSettings.dspTime).Played;
            long peerPlayed=peerInput.Read(AudioSettings.dspTime).Played;yield return Measure("lease-paused-peer-continues");Tone(660);
            Assert.AreEqual(played,input.Read(AudioSettings.dspTime).Played);Assert.Greater(peerInput.Read(AudioSettings.dspTime).Played,peerPlayed);
            Assert.Less(MeasuredPower(440),MeasuredPower(660)*.001);
            lease.SetPaused(false);yield return Measure("lease-resumed");Tone(440);Tone(660);
            UnityEngine.Object.Destroy(anchor);yield return null;Assert.IsFalse(lease.Available);Assert.IsTrue(input.Read(AudioSettings.dspTime).Closed);
            yield return Measure("lease-anchor-lost-peer-continues");Tone(660);Assert.IsTrue(peer.Available);
        }
        [UnityTest]
        public IEnumerator PcmLeaseScopeAndSelectionTeardownLeaveBorrowedGraphAndOtherVoiceAlive()
        {
            var owner=sink.Owner;var scope=LeaseScope();var backend=scope.GetComponent<PcmOutputBackend>();
            using var adapter=new PcmLeaseAdapter(backend,owner);var direct=Begin(0);Queue(direct,660,40);
            var input=FreshStream();using var lease=Lease(scope.transform,input);QueueInput(input,440,40);lease.Start();
            yield return Measure("lease-before-scope-disable");Tone(440);Tone(660);backend.enabled=false;
            Assert.IsFalse(lease.Available);Assert.IsTrue(input.Read(AudioSettings.dspTime).Closed);
            yield return Measure("lease-scope-disabled-direct-remains");Tone(660);Assert.IsTrue(direct.Open);
            backend.enabled=true;Assert.IsFalse(lease.Available);var fresh=FreshStream();using var replacement=Lease(scope.transform,fresh);
            QueueInput(fresh,880,20);replacement.Start();yield return Measure("lease-replacement");Tone(880);Tone(660);
            adapter.Dispose();Assert.IsFalse(replacement.Available);Assert.IsTrue(fresh.Read(AudioSettings.dspTime).Closed);
            Assert.IsNotNull(owner.Active);yield return Measure("selection-disposed-graph-borrower-remains");Tone(660);
            var rejected=FreshStream();Assert.Throws<InvalidOperationException>(()=>Lease(scope.transform,rejected));Assert.IsTrue(rejected.Read(AudioSettings.dspTime).Closed);
        }
        [UnityTest]
        public IEnumerator PcmLeaseDeviceResetInvalidatesOldPlaybackWithoutReplayingIt()
        {
            var scope=LeaseScope();using var adapter=new PcmLeaseAdapter(scope.GetComponent<PcmOutputBackend>(),sink.Owner);
            var input=FreshStream();using var lease=Lease(scope.transform,input);QueueInput(input,440,40);lease.Start();
            yield return Measure("lease-before-device-reset");Tone(440);long generation=sink.Owner.Generation;
            Assert.IsTrue(AudioSettings.Reset(AudioSettings.GetConfiguration()));yield return Ready(sink.Owner);
            Assert.Greater(sink.Owner.Generation,generation);Assert.IsFalse(lease.Available);Assert.IsTrue(input.Read(AudioSettings.dspTime).Closed);
            Assert.IsFalse(lease.IsDrained(AudioSettings.dspTime+60));Assert.Throws<InvalidOperationException>(()=>lease.Start());
            yield return Measure("lease-reset-no-replay");Silence();var fresh=FreshStream();using var replacement=Lease(scope.transform,fresh);
            QueueInput(fresh,880,20);replacement.Start();lease.Dispose();yield return Measure("new-lease-after-reset");Tone(880);
            Assert.IsTrue(replacement.Available);Assert.IsFalse(fresh.Read(AudioSettings.dspTime).Closed);
        }
    }
}
