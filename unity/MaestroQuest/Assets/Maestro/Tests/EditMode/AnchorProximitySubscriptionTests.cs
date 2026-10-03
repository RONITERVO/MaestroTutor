// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class AnchorProximitySubscriptionTests
    {
        sealed class Probe:IProgramAnchorProbe {
            public int Reads,Disposals;public bool Valid=true;public AnchorProximitySample Sample=new(Vector3.right,Vector3.zero,true);
            public bool Read(out AnchorProximitySample sample,out string error){Reads++;sample=Sample;error=Valid?null:"Anchor replaced";return Valid;}
            public void Dispose(){Disposals++;}
            public void Set(float distance,bool available=true,uint revision=0,uint holderRevision=0)=>Sample=new(Vector3.right*distance,Vector3.zero,available,revision,holderRevision);
        }
        sealed class World:IProgramEventWorld,IProgramAnchorWorld {
            public readonly Probe Probe=new();public bool TryPosition(string id,out Vector3 p){p=default;return true;}
            public IProgramAnchorProbe OpenAnchorProbe(string target,JObject holder,Vector3 offset,bool physics)=>Probe;
        }
        static JObject Args(string initial="baseline",string transition="either"){var a=(JObject)BehaviourCatalog.Event("object.anchor.proximity.changed").ToJson()["example"].DeepClone();a["radius"]=.2;a["hysteresis"]=.05;a["initial"]=initial;a["transition"]=transition;return a;}
        [Test] public void InitialBaselineAndHysteresisObserveMovingAnchorWithoutRepeatedEntries(){
            var w=new World();using var watch=new AnchorProximitySubscription(w,Args(),0);w.Probe.Set(.19f);
            Assert.That(watch.Poll(.051f,out var value,out var fields,out var error),Is.True);Assert.That(error,Is.Null);Assert.That((bool)fields["inside"],Is.True);Assert.That(value.Text,Is.EqualTo(new string('0',32)));Assert.That(BehaviourCatalog.Event("object.anchor.proximity.changed").ValidFields(fields),Is.True);
            w.Probe.Set(.23f);Assert.That(watch.Poll(.102f,out _,out _,out _),Is.False);w.Probe.Set(.26f);Assert.That(watch.Poll(.153f,out _,out fields,out _),Is.True);Assert.That((bool)fields["inside"],Is.False);
        }
        [Test] public void ReportWaitsForFirstSampleAndAppliesTransitionFilter(){
            var w=new World();w.Probe.Set(.1f);using var watch=new AnchorProximitySubscription(w,Args("report","enter"),0);
            Assert.That(watch.Poll(.049f,out _,out _,out _),Is.False);Assert.That(watch.Poll(.051f,out _,out var fields,out _),Is.True);Assert.That((bool)fields["inside"],Is.True);Assert.That(watch.Poll(.102f,out _,out _,out _),Is.False);
        }
        [TestCase("gap")] [TestCase("target")] [TestCase("holder")] [TestCase("unavailable")]
        public void InterruptedObservationRebaselinesWithoutFabricatedEntry(string cause){
            var w=new World();using var watch=new AnchorProximitySubscription(w,Args(),0);float at=.051f;
            if(cause=="unavailable"){w.Probe.Set(.1f,false);Assert.That(watch.Poll(at,out _,out _,out _),Is.False);at=.102f;}
            if(cause=="gap")at=1;
            w.Probe.Set(.1f,revision:cause=="target"?1u:0,holderRevision:cause=="holder"?1u:0);
            Assert.That(watch.Poll(at,out _,out _,out _),Is.False);w.Probe.Set(.3f,revision:cause=="target"?1u:0,holderRevision:cause=="holder"?1u:0);
            Assert.That(watch.Poll(at+.051f,out _,out var fields,out _),Is.True);Assert.That((bool)fields["inside"],Is.False);
        }
        [Test] public void SamplingAndDisposalAreBoundedAndFailuresNeverInventEvents(){
            var w=new World();var watch=new AnchorProximitySubscription(w,Args(),0);for(int i=0;i<100;i++)Assert.That(watch.Poll(i*.0001f,out _,out _,out _),Is.False);Assert.That(w.Probe.Reads,Is.EqualTo(1));
            w.Probe.Valid=false;Assert.That(watch.Poll(.06f,out _,out _,out var error),Is.False);Assert.That(error,Does.Contain("replaced"));watch.Dispose();watch.Dispose();Assert.That(watch.Poll(2,out _,out _,out _),Is.False);Assert.That(w.Probe.Reads,Is.EqualTo(2));Assert.That(w.Probe.Disposals,Is.EqualTo(1));
            w=new World();w.Probe.Sample=new(Vector3.one*float.NaN,Vector3.zero,true);Assert.Throws<ProgramFault>(()=>new AnchorProximitySubscription(w,Args(),0));Assert.That(w.Probe.Disposals,Is.EqualTo(1));
        }
        [Test] public void SharedContractBindsTypedAnchorFieldsButNeverSelectorsOrUndeclaredActionResources(){
            var p=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-anchor-zone.json")));Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out var error),Is.True,error);
            var wait=p["functions"][0]["body"][0];wait["bindings"]["holder.revision"]=new JObject {["value"]=1};Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out error),Is.True,error);wait["bindings"]["holder.kind"]=new JObject {["value"]="object"};Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False);
            var call=(JObject)p["functions"][0]["body"][1]["then"][0]["arguments"];call["reach"]["radius"]=0;Assert.That(BehaviourCatalog.TryCall("object.hold",1,call,out _,out _),Is.False);call["reach"]["radius"]=.1;Assert.That(BehaviourCatalog.TryCall("object.hold",1,call,out var hold,out error),Is.True,error);Assert.That(hold.Resources,Is.EquivalentTo(new[]{new string('0',32),new string('1',32)}));
        }
    }
}
