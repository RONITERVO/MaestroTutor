// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        static double BlockEnergy(float[] block)
        {
            double energy=0;
            foreach(float sample in block){Assert.IsTrue(float.IsFinite(sample));energy+=sample*(double)sample;}
            return energy;
        }
        [UnityTest] public IEnumerator PresentationPublishesGeometryRequirementBeforeConcurrentRead()
        {
            var owner=sink.Owner;var graph=owner.Active;
            var voice=Begin(0,dry:0);Queue(voice,440,chunks:40);
            yield return PcmReady(0);yield return Measure("atomic-before-publication");Tone(440);
            sink.BypassRendering=true;yield return new WaitForSecondsRealtime(.1f);
            var block=new float[graph.Frame*2];bool ready=false,rendered=false;int reader=0;
            int control=Thread.CurrentThread.ManagedThreadId;double now=AudioSettings.dspTime;
            graph.AfterPresentationPublished=()=>{
                // Publication is held on the control thread; a distinct consumer
                // reads readiness and runs the real native DSP before it returns.
                var read=Task.Run(()=>{reader=Thread.CurrentThread.ManagedThreadId;ready=graph.PcmWetReady(0);rendered=owner.Render(block,2,now);});
                Assert.IsTrue(read.Wait(TimeSpan.FromSeconds(5)),"Concurrent publication read deadline");
                read.GetAwaiter().GetResult();
            };
            try {owner.UpdateScene(Scene(room:false));}
            finally {graph.AfterPresentationPublished=null;sink.BypassRendering=false;}
            double energy=BlockEnergy(block);
            Debug.Log("ATOMIC_PUBLICATION control="+control+" reader="+reader+" ready="+ready+" rendered="+rendered+" energy="+energy);
            Assert.AreNotEqual(control,reader);Assert.IsTrue(rendered);
            Assert.IsFalse(ready,"New room publication must expose its new geometry requirement");
            Assert.That(block,Is.All.EqualTo(0),"The old room response cannot escape inside publication");
            yield return Updated(owner,owner.UpdateScene(Scene()));
            yield return PcmReady(0);yield return Measure("atomic-room-repaired");Tone(440);
            sink.BypassRendering=true;yield return new WaitForSecondsRealtime(.1f);now=AudioSettings.dspTime;
            graph.AfterPresentationPublished=()=>{
                var read=Task.Run(()=>{ready=graph.PcmWetReady(0);rendered=owner.Render(block,2,now);});
                Assert.IsTrue(read.Wait(TimeSpan.FromSeconds(5)));read.GetAwaiter().GetResult();
            };
            try {owner.UpdateListener(AcousticFrame.Pose(-1,0,0,(float)Math.PI));}
            finally {graph.AfterPresentationPublished=null;sink.BypassRendering=false;}
            Assert.IsTrue(ready&&rendered,"Pure orientation publication keeps the current geometry response");
            Assert.Greater(BlockEnergy(block),1e-10);Assert.AreSame(graph,owner.Active);Assert.IsTrue(voice.Open);
        }
        [UnityTest] public IEnumerator PresentationInvalidationDuringConcurrentRenderFencesWetSubmission()
        {
            var owner=sink.Owner;var graph=owner.Active;
            var voice=Begin(0,dry:0);Queue(voice,440,chunks:40);
            yield return PcmReady(0);yield return Measure("concurrent-before-submission");Tone(440);
            foreach(bool changeGeometry in new[]{true,false})
            {
                sink.BypassRendering=true;yield return new WaitForSecondsRealtime(.1f);
                using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
                int reader=0,control=Thread.CurrentThread.ManagedThreadId;var block=new float[graph.Frame*2];
                double now=AudioSettings.dspTime;
                graph.BeforeOutput=()=>{reader=Thread.CurrentThread.ManagedThreadId;entered.Set();if(!release.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Concurrent output gate");};
                var read=Task.Run(()=>owner.Render(block,2,now));
                try
                {
                    double end=Time.realtimeSinceStartupAsDouble+5;
                    while(!entered.IsSet&&!read.IsCompleted&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                    Assert.IsTrue(entered.IsSet,"DSP reached the final output boundary");
                    if(changeGeometry)owner.UpdateScene(Scene(room:false));else owner.SetGeometryReady(false);
                    release.Set();
                    while(!read.IsCompleted&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                    Assert.IsTrue(read.IsCompleted,"Concurrent renderer returned");Assert.IsTrue(read.GetAwaiter().GetResult());
                    Debug.Log("CONCURRENT_SUBMISSION geometry="+changeGeometry+" control="+control+" reader="+reader+" energy="+BlockEnergy(block));
                    Assert.AreNotEqual(control,reader);Assert.That(block,Is.All.EqualTo(0));
                }
                finally
                {
                    release.Set();graph.BeforeOutput=null;sink.BypassRendering=false;
                    _=read.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
                }
                owner.SetGeometryReady(true);yield return Updated(owner,owner.UpdateScene(Scene()));
                yield return PcmReady(0);yield return Measure("concurrent-repaired-"+changeGeometry);Tone(440);
            }
            Assert.AreSame(graph,owner.Active);Assert.IsTrue(voice.Open);
        }
    }
}
