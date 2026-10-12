// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class PcmBackendTests
    {
        sealed class Output : IPcmOutput
        {
            internal readonly SpeechPcmStream Stream;
            internal bool Disposed,Started,Drained,FailDispose;
            internal Vector3 Position;internal Quaternion Rotation;internal float Gain;
            internal Output(SpeechPcmStream stream){Stream=stream;}
            public bool Available=>!Disposed;
            public AudioSource Source=>null;
            public void Start()=>Started=true;
            public void SetPose(Vector3 position,Quaternion rotation){Position=position;Rotation=rotation;}
            public void SetPaused(bool value)=>Stream.SetPaused(value);
            public void SetGain(float value)=>Gain=value;
            public bool IsDrained(double now)=>Drained;
            public void Dispose(){if(Disposed)return;Disposed=true;if(FailDispose)throw new InvalidOperationException("synthetic renderer disposal failure");}
        }
        GameObject root;readonly List<PcmOutputLease> leases=new();
        [SetUp] public void Setup(){root=new GameObject("PCM backend scope test");}
        [UnityTearDown] public IEnumerator Cleanup()
        {foreach(var lease in leases)lease.Dispose();leases.Clear();if(root)UnityEngine.Object.Destroy(root);yield return null;}
        PcmOutputLease Open(Transform parent,SpeechPcmStream stream=null)
        {var lease=PcmOutputLease.Open(parent,"Test voice",stream??new SpeechPcmStream(0,0),true,.7f,96,.5f,15);leases.Add(lease);return lease;}
        [Test] public void AliasAcrossScopesCannotCloseOrReadmitTheExistingInput()
        {
            int calls=0;var backend=root.AddComponent<PcmOutputBackend>();backend.Configure((request,stream)=>{calls++;return new Output(stream);});
            var first=Open(root.transform);Assert.Throws<InvalidOperationException>(()=>Open(root.transform,first.Stream));
            Assert.That(calls,Is.EqualTo(1));Assert.That(first.Available,Is.True);Assert.That(first.Stream.Read(0).Closed,Is.False);
            var foreign=new SpeechPcmStream(0,0);Assert.That(foreign.TryClaimOutput(new object()),Is.True);
            Assert.Throws<InvalidOperationException>(()=>Open(root.transform,foreign));Assert.That(foreign.Read(0).Closed,Is.False);foreign.Close();
        }
        [Test] public void MissingOrDisabledSelectedBackendNeverFallsBackToAnotherRenderer()
        {
            var backend=root.AddComponent<PcmOutputBackend>();
            foreach(bool disabled in new[]{false,true}){
                if(disabled){backend.Configure((request,stream)=>new Output(stream));backend.enabled=false;}
                var stream=new SpeechPcmStream(0,0);Assert.Throws<InvalidOperationException>(()=>Open(root.transform,stream));
                Assert.That(stream.Read(0).Closed,Is.True);Assert.That(root.GetComponentsInChildren<AudioSource>(true),Is.Empty);
            }
        }
        [Test] public void NearestRoomScopeStopsOnlyItsOwnVoicesAndCannotReviveThem()
        {
            var outer=root.AddComponent<PcmOutputBackend>();outer.Configure((request,stream)=>new Output(stream));
            var child=new GameObject("Independent nested scope");child.transform.SetParent(root.transform,false);
            var inner=child.AddComponent<PcmOutputBackend>();inner.Configure((request,stream)=>new Output(stream));
            var first=Open(root.transform);var second=Open(child.transform);inner.enabled=false;
            Assert.That(second.Available,Is.False);Assert.That(second.Stream.Read(0).Closed,Is.True);
            Assert.That(first.Available,Is.True);Assert.That(first.Stream.Read(0).Closed,Is.False);
            inner.enabled=true;Assert.That(second.Available,Is.False);Assert.That(Open(child.transform).Available,Is.True);
        }
        [Test] public void StaleSelectionHandleCannotStopAReplacementAndLiveSelectionCannotChange()
        {
            var backend=root.AddComponent<PcmOutputBackend>();var old=backend.Configure((request,stream)=>new Output(stream));var first=Open(root.transform);
            Assert.Throws<InvalidOperationException>(()=>backend.Configure((request,stream)=>new Output(stream)));
            first.Dispose();var current=backend.Configure((request,stream)=>new Output(stream));var second=Open(root.transform);
            old.Dispose();Assert.That(second.Available,Is.True);Assert.That(second.Stream.Read(0).Closed,Is.False);
            current.Dispose();Assert.That(second.Available,Is.False);Assert.That(second.Stream.Read(0).Closed,Is.True);
        }
        [Test] public void SourceAndRendererMustBothDrainAndControlsReachTheSelectedOutput()
        {
            Output output=null;var backend=root.AddComponent<PcmOutputBackend>();backend.Configure((request,stream)=>output=new Output(stream));
            var lease=Open(root.transform);lease.Start();Assert.That(output.Started,Is.True);
            lease.SetPose(new Vector3(2,3,4),Quaternion.Euler(0,35,0));Assert.That(output.Position,Is.EqualTo(new Vector3(2,3,4)));
            lease.SetGain(.3f);Assert.That(output.Gain,Is.EqualTo(.3f));lease.SetPaused(true);Assert.That(lease.Stream.IsPaused,Is.True);lease.SetPaused(false);
            Assert.That(lease.Stream.TryWrite(1,new short[4800],0,out var error),Is.True,error);Assert.That(lease.Stream.TryComplete(1,out error),Is.True,error);
            output.Drained=true;Assert.That(lease.IsDrained(2),Is.False,"A renderer cannot invent source consumption");
            output.Drained=false;Assert.That(lease.Stream.Render(new float[4800],1,24000,0),Is.True);Assert.That(lease.Stream.Read(2).SourceDrained,Is.True);
            Assert.That(lease.IsDrained(2),Is.False,"Source consumption cannot invent downstream completion");output.Drained=true;Assert.That(lease.IsDrained(2),Is.True);
        }
        [Test] public void ChangedSelectionDuringFactoryAdmissionReleasesOnlyTheRejectedOutput()
        {
            var backend=root.AddComponent<PcmOutputBackend>();Output rejected=null;
            backend.Configure((request,stream)=>{rejected=new Output(stream);backend.Configure((next,input)=>new Output(input));return rejected;});
            var source=new SpeechPcmStream(0,0);Assert.Throws<InvalidOperationException>(()=>Open(root.transform,source));
            Assert.That(rejected.Disposed,Is.True);Assert.That(source.Read(0).Closed,Is.True);Assert.That(Open(root.transform).Available,Is.True);
        }
        [Test] public void BackendTeardownClosesEveryInputEvenWhenOneRendererDisposalFails()
        {
            var backend=root.AddComponent<PcmOutputBackend>();int index=0;
            backend.Configure((request,stream)=>new Output(stream){FailDispose=index++==0});
            var first=Open(root.transform);var second=Open(root.transform);
            LogAssert.Expect(LogType.Exception,new Regex("InvalidOperationException: synthetic renderer disposal failure"));backend.enabled=false;
            Assert.That(first.Stream.Read(0).Closed&&second.Stream.Read(0).Closed,Is.True);
            Assert.That(first.Available||second.Available,Is.False);
        }
    }
}
