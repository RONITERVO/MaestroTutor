// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class RectangularContainerTests {
        sealed class CurrentContainerFacts:IProgramFacts,IProgramFactQueries {
            public int Reads;public bool Available=true;public RoomContainer Container;
            public bool TryRead(string name,out ProgramValue value){value=default;return false;}
            public bool TryRead(string name,int version,JObject args,out ProgramValue value){
                Reads++;Assert.That(name,Is.EqualTo("object.container"));Assert.That(version,Is.EqualTo(1));Assert.That((string)args["target"],Is.EqualTo(new string('a',32)));
                value=ProgramValue.Literal(new JObject{["revision"]=42,["configured"]=true,["definition"]=ContainerCapability.Definition(Container,true)},BehaviourCatalog.Fact(name).Type);return Available;
            }
        }
        [TestCase(false,true)] [TestCase(true,true)] [TestCase(true,false)]
        public void SharedCurrentRecordProgramUsesOneFreshFactOrFailsBeforeTheAction(bool rectangular,bool available){
            var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/current-container-program.json"));
            Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);
            var container=rectangular?Box():new RoomContainer();container.amountMl=123.456;
            var facts=new CurrentContainerFacts{Container=container,Available=available};var machine=new ProgramMachine(program,facts);
            Assert.That(machine.Advance(out var action,256),Is.EqualTo(available?ProgramYield.Action:ProgramYield.Failed),machine.Error);Assert.That(facts.Reads,Is.EqualTo(1));
            if(!available){Assert.That(action,Is.Null);return;}
            Assert.That((int)action.Arguments["revision"],Is.EqualTo(42));Assert.That(JToken.DeepEquals(action.Arguments["definition"],ContainerCapability.Definition(container,true)),Is.True);
            var decoded=ContainerCapability.ReadDefinition((JObject)action.Arguments["definition"]);Assert.That(decoded.Validate(out error),Is.True,error);Assert.That(decoded.IsRectangular,Is.EqualTo(rectangular));Assert.That(decoded.amountMl,Is.EqualTo(123.456));
        }
        static RoomContainer Box()=>new(){version=2,rectangle=new(){width=1.2f,depth=.8f},height=.3f,capacityMl=10000,amountMl=8000};
        [Test] public void RectangleNeedsItsOwnVersionAndCopiedDimensionsAreIndependent(){
            var c=Box();Assert.That(c.Validate(out var error),Is.True,error);var copy=c.Copy();copy.rectangle.width=.9f;Assert.That(c.rectangle.width,Is.EqualTo(1.2f));
            c.version=1;Assert.That(c.Validate(out _),Is.False);c.version=2;c.rectangle=null;Assert.That(c.Validate(out _),Is.False);
            c=Box();c.rectangle.depth=float.NaN;Assert.That(c.Validate(out _),Is.False);
            var data=ContainerCapability.Definition(Box());Assert.That(ContainerCapability.ReadDefinition(data).version,Is.EqualTo(2));data["rectangle"]=null;Assert.That(ContainerCapability.ReadDefinition(data).version,Is.EqualTo(1));
            var legacy=ContainerCapability.Definition(new RoomContainer());Assert.That(legacy.ContainsKey("rectangle"),Is.False);
        }
        [Test] public void RectangularContentsCannotHideInAnOlderRoomOrSavedComponent(){
            var c=Box();var room=new RoomDocument{version=18,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('1',32),kind=RoomObjectKind.Block,containers=new[]{c}}}};
            Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;Assert.That(room.Validate(out var error),Is.True,error);
            var saved=JObject.Parse(JsonUtility.ToJson(c));saved["version"]=1;Assert.That(CapabilityArguments.Validate(saved,ContainerCapability.SavedSchema(),out _),Is.False);
        }
        [Test] public void BoxPlaneVolumeIsSymmetricMonotoneAndAccurateNearAxisAlignment(){
            foreach(var n in new[]{Vector3.up,Vector3.right,Vector3.forward,new Vector3(1,1,1).normalized,new Vector3(.1f,.91f,.33f).normalized,new Vector3(1,1e-8f,1e-12f).normalized}){
                var c=Box();double extent=c.HorizontalExtent(n)+c.height*.5*Math.Abs(n.y),prior=0;
                for(int i=0;i<=100;i++){
                    double level=-extent+2*extent*i/100,f=ContainerFlowGeometry.FractionBelow(c,n,level);
                    Assert.That(f,Is.InRange(prior-1e-12,1),n+" "+level);prior=f;
                    Assert.That(f+ContainerFlowGeometry.FractionBelow(c,n,-level),Is.EqualTo(1).Within(1e-10));
                }
                foreach(double fraction in new[]{.001,.05,.25,.5,.8,.99,.999}){
                    float level=ContainerFlowGeometry.Level(c,n,fraction);Assert.That(ContainerFlowGeometry.FractionBelow(c,n,level),Is.EqualTo(fraction).Within(1e-5));
                }
            }
            // Unit cube x+y+z < 1 has tetrahedral volume 1/6.
            Assert.That(ContainerFlowGeometry.BoxFractionBelow(Vector3.one,1,1,1,-.5),Is.EqualTo(1d/6).Within(1e-12));
        }
        [Test] public void BoxVolumeAgreesWithIndependentVerticalColumnIntegration(){
            var c=Box();const int samples=160;
            foreach(var n in new[]{new Vector3(.2f,.8f,.5f).normalized,new Vector3(.9f,.3f,.4f).normalized,new Vector3(.5f,-.6f,.2f).normalized})foreach(float level in new[]{-.2f,-.05f,.08f,.2f}){
                double total=0;
                for(int x=0;x<samples;x++)for(int z=0;z<samples;z++){
                    double px=((x+.5)/samples-.5)*c.rectangle.width,pz=((z+.5)/samples-.5)*c.rectangle.depth;
                    double py=(level-n.x*px-n.z*pz)/n.y;
                    double f=Math.Clamp((py+c.height*.5)/c.height,0,1);total+=n.y>0?f:1-f;
                }
                Assert.That(ContainerFlowGeometry.FractionBelow(c,n,level),Is.EqualTo(total/(samples*samples)).Within(.0001));
            }
        }
        [Test] public void RectanglePourLipAndReceiverUseTheActualFootprint(){
            var go=new GameObject("Rectangle flow geometry");try{
                var c=Box();Assert.That(ContainerFlowGeometry.Excess(c,Quaternion.identity,Vector3.up),Is.Zero.Within(1e-8));
                double prior=0;foreach(int angle in new[]{0,10,20,45,80,90,120,180}){double excess=ContainerFlowGeometry.Excess(c,Quaternion.Euler(0,0,angle),Vector3.up);Assert.That(excess,Is.InRange(prior-1e-8,c.amountMl));prior=excess;}
                Assert.That(prior,Is.EqualTo(c.amountMl));go.transform.SetPositionAndRotation(new Vector3(2,1,3),Quaternion.Euler(0,35,0));go.transform.localScale=Vector3.one*2;
                Vector3 World(float x,float y,float z)=>go.transform.TransformPoint(new Vector3(x,y,z));
                Assert.That(ContainerFlowGeometry.Enters(c,go.transform,World(.5f,.6f,.3f),World(.5f,0,.3f),Vector3.up,out var fraction),Is.True);Assert.That(fraction,Is.EqualTo(.5).Within(.0001));
                Assert.That(ContainerFlowGeometry.Enters(c,go.transform,World(.61f,.6f,0),World(.61f,0,0),Vector3.up,out _),Is.False);
                Assert.That(ContainerFlowGeometry.Enters(c,go.transform,World(0,.6f,.41f),World(0,0,.41f),Vector3.up,out _),Is.False);
                go.transform.rotation=Quaternion.Euler(20,35,30);var lip=ContainerFlowGeometry.Lip(c,go.transform,Vector3.up,out _);var local=go.transform.InverseTransformPoint(lip);
                Assert.That(local.y,Is.EqualTo(c.height).Within(1e-5));Assert.That(Mathf.Abs(local.x),Is.EqualTo(c.rectangle.width*.5).Within(1e-5));Assert.That(Mathf.Abs(local.z),Is.EqualTo(c.rectangle.depth*.5).Within(1e-5));
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void RectangularImmersionUsesCompleteCavitiesAndTransformedOpenings(){
            var donor=new GameObject("Rectangular donor");var receiver=new GameObject("Dipping vessel");try{
                var source=Box();source.height=.5f;source.amountMl=9000;
                var target=new RoomContainer{radius=.08f,height=.12f};receiver.transform.position=new Vector3(.4f,.05f,.2f);
                Assert.That(ContainerScoopingGeometry.TryContact(source,donor.transform,target,receiver.transform,Vector3.up,out var contact),Is.True);Assert.That(contact.Path(0,out var from,out var to),Is.True);Assert.That(from.y,Is.GreaterThan(to.y));
                receiver.transform.position=new Vector3(.55f,.05f,.2f);Assert.That(ContainerScoopingGeometry.TryContact(source,donor.transform,target,receiver.transform,Vector3.up,out _),Is.False,"Cylinder crosses the rectangular wall");
                target=new RoomContainer{version=2,rectangle=new(){width=.5f,depth=.14f},height=.1f};receiver.transform.position=new Vector3(.3f,.05f,0);
                Assert.That(ContainerScoopingGeometry.TryContact(source,donor.transform,target,receiver.transform,Vector3.up,out _),Is.True,"A long rectangular scoop must not be approximated by a circle");
                receiver.transform.rotation=Quaternion.Euler(0,0,70);Assert.That(ContainerScoopingGeometry.TryContact(source,donor.transform,target,receiver.transform,Vector3.up,out _),Is.False,"A tilted receiving corner crosses the floor");
                receiver.transform.rotation=Quaternion.identity;receiver.transform.position=new Vector3(0,.05f,0);source=new RoomContainer{radius=.5f,height=.5f,amountMl=240};
                Assert.That(ContainerScoopingGeometry.TryContact(source,donor.transform,target,receiver.transform,Vector3.up,out _),Is.True,"Rectangular receivers also work in cylinders");
            }finally{UnityEngine.Object.DestroyImmediate(donor);UnityEngine.Object.DestroyImmediate(receiver);}
        }
    }
}
