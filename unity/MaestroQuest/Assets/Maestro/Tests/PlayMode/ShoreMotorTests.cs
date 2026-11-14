// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed class ShoreMotorTests {
        GameObject root;RoomItem lake;HeightFieldView view;RoomHeightField field;RoomContainer contents;
        RoomGroundQuery ground;readonly RoomUprightBody body=new(.06f,.3f);
        RoomShoreMotor motor;LiquidMediumGeometry water;float floatY;
        readonly List<(Vector3 foot,bool floating)> trace=new();
        const int Mask=1<<RoomPhysicsLayers.Environment;
        [SetUp]public void Setup(){
            root=new GameObject("Shore motor native fixture");root.transform.position=new(8,-2,3);
            lake=root.AddComponent<RoomItem>();view=root.AddComponent<HeightFieldView>();
            field=new RoomHeightField{cells=16,width=4,depth=2,maxHeight=.5f,heights=new float[289]};
            for(int z=0;z<=16;z++)for(int x=0;x<=16;x++)field.heights[z*17+x]=.5f*(1-x/16f);
            Assert.That(view.Apply(new[]{field}),Is.True);
            contents=new RoomContainer{version=3,rectangle=new(){width=4,depth=2},height=.5f,capacityMl=TerrainLiquidGeometry.Capacity(field,.5f)*1000000};
            SetLevel(.3f);ground=new();ground.Capture(root.transform);motor=new();trace.Clear();Physics.SyncTransforms();
        }
        void SetLevel(float localY){
            contents.capacityMl=TerrainLiquidGeometry.Capacity(field,.5f)*1000000;
            contents.amountMl=new TerrainLiquidGeometry(field,.5f).VolumeBelow(Vector3.up,localY)*1000000;
            water=new("lake",lake,contents,Vector3.up,null);
            Assert.That(water.SurfaceCapsuleFoot(body,.6f,out floatY,out var error),Is.True,error);
        }
        Vector3 Point(float x,float y)=>root.transform.TransformPoint(new Vector3(x,y,0));
        bool Allowed(Vector3 from,Vector3 to,bool floating,out string error){
            error=null;return !floating||water.SurfaceCapsuleCavity(from,to,body,out error);
        }
        bool Step(Vector3 from,Vector3 planar,bool floating,out RoomShoreMotor.Step step,out string error,int mask=Mask,Func<Vector3,bool> permitted=null)=>
            motor.Travel(ground,from,planar,.025f,body,floatY,floating,mask,_=>true,permitted??(_=>true),Allowed,out step,out error);
        [UnityTearDown]public IEnumerator Teardown(){motor.Clear();ground.Clear();UnityEngine.Object.Destroy(root);yield return null;}
        [UnityTest]public IEnumerator ShoreMotorWalksIntoWaterAndReturnsAlongTheSameNativeSlope(){
            var current=Point(-1.5f,.4375f);bool floating=false;var start=current;
            foreach(float target in new[]{1.6f,-1.5f}){
                for(int i=0;i<180&&Mathf.Abs(current.x-(root.transform.position.x+target))>.002f;i++){
                    var toward=Vector3.right*Mathf.Clamp(root.transform.position.x+target-current.x,-.025f,.025f);
                    Assert.That(Step(current,toward,floating,out var next,out var error),Is.True,error+" from "+current);
                    Assert.That(Vector3.Distance(current,next.Foot),Is.LessThan(.105f),"No teleport at the boundary");
                    current=next.Foot;floating=next.Floating;trace.Add((current,floating));
                    yield return null;
                }
                Assert.That(current.x,Is.EqualTo(root.transform.position.x+target).Within(.003f));
                if(target>0){Assert.That(floating,Is.True);Assert.That(current.y,Is.EqualTo(floatY).Within(.005f));}
            }
            Assert.That(floating,Is.False);Assert.That(Vector3.Distance(current,start),Is.LessThan(.006f));
            Assert.That(trace.Exists(x=>x.floating),Is.True);Assert.That(trace.Exists(x=>!x.floating),Is.True);
        }
        [UnityTest]public IEnumerator ShoreMotorStationaryBuoyantBodyDoesNotMistakeItsFootForTheShallowBed(){
            // Choose actual ground 5 cm below the floating foot, inside the
            // ground motor's support band. Standing must remain buoyant.
            float localFoot=floatY-root.transform.position.y;
            float x=(.25f-(localFoot-.05f))/.125f;var from=Point(x,localFoot);
            for(int i=0;i<8;i++){
                Assert.That(Step(from,Vector3.zero,true,out var next,out var error),Is.True,error);
                Assert.That(next.Floating,Is.True,"A nearby bed is not the body's actual foot height");
                Assert.That(Vector3.Distance(next.Foot,from),Is.LessThan(.0001f));
                from=next.Foot;yield return null;
            }
        }
        [UnityTest]public IEnumerator ShoreMotorCannotUseTwoWetEndsToCrossAnActualRaisedBed(){
            for(int z=0;z<=16;z++)for(int x=0;x<=16;x++)field.heights[z*17+x]=x==8?.5f:0;
            view.Apply(new[]{field});SetLevel(.4f);ground.Capture(root.transform);Physics.SyncTransforms();
            var from=Point(-.8f,floatY-root.transform.position.y);var to=Point(.8f,floatY-root.transform.position.y);
            Assert.That(water.SurfaceCapsuleCavity(from,to,body,out var error),Is.True,error+" Cavity admission alone does not prove bed clearance.");
            Assert.That(motor.Travel(ground,from,to-from,1.6f,body,floatY,true,Mask,_=>true,_=>true,Allowed,out var result,out error),Is.False);
            Assert.That(result.Foot,Is.EqualTo(from),"A rejected multi-step proposal must not publish partial motion");
            yield return null;
        }
        [UnityTest]public IEnumerator ShoreMotorRejectsACapsuleEntirelyUnderTheBed(){
            var from=Point(-1.5f,.01f);
            Assert.That(Step(from,Vector3.right*.025f,true,out _,out var error),Is.False);
            Assert.That(error,Does.Contain("bed"));yield return null;
        }
        [UnityTest]public IEnumerator ShoreMotorStopsForAnAboveWaterObstacle(){
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform,false);
            blocker.layer=RoomPhysicsLayers.Environment;blocker.transform.localPosition=new(1.55f,.37f,0);blocker.transform.localScale=new(.03f,.1f,.5f);
            Physics.SyncTransforms();var current=Point(1.35f,floatY-root.transform.position.y);bool refused=false;
            for(int i=0;i<20;i++){
                if(!Step(current,Vector3.right*.025f,true,out var next,out _)){refused=true;break;}
                current=next.Foot;yield return null;
            }
            Assert.That(refused,Is.True,"The whole body above the water must also collide");Assert.That(current.x,Is.LessThan(root.transform.position.x+1.55f));
        }
        [UnityTest]public IEnumerator ShoreMotorAdjustsChangedWaterGraduallyAndDoesNotReplayHorizontalMotion(){
            var from=Point(1.6f,floatY-root.transform.position.y);SetLevel(.35f);
            Assert.That(Step(from,Vector3.left*.025f,true,out var next,out var error),Is.True,error);
            Assert.That(next.Foot.x,Is.EqualTo(from.x));Assert.That(next.Foot.y-from.y,Is.EqualTo(.025f).Within(.00001f));
            Assert.That(next.Foot.y,Is.LessThan(floatY));yield return null;
        }
        [UnityTest]public IEnumerator ShoreMotorCannotCrossAnUnsupportedDryGapOrIgnoreWorldAdmission(){
            view.Collision.enabled=false;Physics.SyncTransforms();
            var dry=Point(-1.5f,.4375f);Assert.That(Step(dry,Vector3.right*.025f,false,out _,out _),Is.False);
            view.Collision.enabled=true;Physics.SyncTransforms();
            var wet=Point(1.6f,floatY-root.transform.position.y);
            Assert.That(Step(wet,Vector3.left*.025f,true,out _,out _,permitted:p=>p.y>=0),Is.False,"An actor's real-room admission may exclude this below-floor location");
            Assert.That(Step(wet,Vector3.left*.025f,true,out _,out var error),Is.True,error);yield return null;
        }
        [UnityTest]public IEnumerator ShoreMotorUsesTheCallersSelectedCollisionMask(){
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform,false);
            blocker.layer=RoomPhysicsLayers.Scanned;blocker.transform.localPosition=new(1.58f,.25f,0);blocker.transform.localScale=new(.02f,.5f,.5f);Physics.SyncTransforms();
            var from=Point(1.5f,floatY-root.transform.position.y);
            Assert.That(Step(from,Vector3.right*.025f,true,out _,out var error),Is.True,error);
            Assert.That(Step(from,Vector3.right*.025f,true,out _,out _,mask:Mask|(1<<RoomPhysicsLayers.Scanned)),Is.False);yield return null;
        }
    }
}
