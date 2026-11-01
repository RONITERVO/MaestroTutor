// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class AquaticGeometryTests {
        [TestCase(0)][TestCase(45)][TestCase(90)][TestCase(179)]
        public void SweptEnvelopeContainsEveryCornerThroughoutRotationAndTranslation(float angle){
            var local=new Bounds(new(1,.3f,-.2f),new(2,.1f,.2f));var scale=new Vector3(.7f,1.5f,1.2f);
            var from=new Vector3(2,1,4);var to=new Vector3(3,-1,2);
            var a=Quaternion.Euler(15,23,6);var b=Quaternion.AngleAxis(angle,new Vector3(.2f,.8f,.6f).normalized)*a;
            var swept=new RoomSweptBody(local,scale,from,a,to,b);
            for(int step=0;step<=100;step++){
                float t=step/100f;var frame=Matrix4x4.TRS(Vector3.Lerp(from,to,t),Quaternion.Slerp(a,b,t),scale);
                var box=new Bounds(Vector3.Lerp(swept.From,swept.To,t),swept.Extents*2+Vector3.one*.0001f);
                for(int i=0;i<8;i++){
                    var corner=local.center+Vector3.Scale(local.extents,new((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Assert.That(box.Contains(frame.MultiplyPoint3x4(corner)),Is.True,$"step {step}, corner {i}, angle {angle}");
                }
            }
        }
        [Test]public void AquaticSearchFindsVerticalDetourAndEveryEdgeWasProved(){
            bool Clear(Pose a,Pose b,out string reason){
                reason=null;
                var obstacle=new Bounds(new(.7f,0,0),new(.2f,.6f,2));
                var delta=b.position-a.position;
                return !obstacle.Contains(a.position)&&!obstacle.Contains(b.position)&&
                    (delta.sqrMagnitude<1e-10f||!obstacle.IntersectRay(new Ray(a.position,delta.normalized),out var t)||t>delta.magnitude);
            }
            var planner=new RoomAquaticRoute(new(Vector3.zero,Quaternion.identity),Vector3.right*1.4f,false,Clear);
            for(int i=0;i<600&&!planner.Done;i++)planner.Tick();
            Assert.That(planner.Done,Is.True);Assert.That(planner.Path,Is.Not.Null,planner.Error);
            bool vertical=false;for(int i=1;i<planner.Path.Length;i++){Assert.That(Clear(planner.Path[i-1],planner.Path[i],out _),Is.True);vertical|=Mathf.Abs(planner.Path[i].position.y)>.3f;}
            Assert.That(vertical,Is.True);Assert.That(planner.Path[^1].position,Is.EqualTo(Vector3.right*1.4f));
        }
        [Test]public void AquaticSearchTerminatesWhenEveryEdgeIsBlocked(){
            int checks=0;bool Clear(Pose a,Pose b,out string reason){checks++;reason="blocked";return false;}
            var planner=new RoomAquaticRoute(new(Vector3.zero,Quaternion.identity),Vector3.right,true,Clear);
            for(int i=0;i<100&&!planner.Done;i++)planner.Tick();
            Assert.That(planner.Done,Is.True);Assert.That(planner.Path,Is.Null);Assert.That(checks,Is.LessThan(20));
        }
        [Test]public void AquaticBoxSweepStopsAtAThinWallAndReleasesItsBuffers(){
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try{
                go.layer=10;go.transform.position=new(1,10,0);go.transform.localScale=new(.001f,2,2);Physics.SyncTransforms();
                var motor=new RoomBoxMotor();var body=new RoomSweptBody(new Bounds(Vector3.zero,Vector3.one*.1f),Vector3.one,new(0,10,0),Quaternion.identity,new(2,10,0),Quaternion.identity);
                Assert.That(motor.Clear(body,1<<10,c=>true),Is.False);
                Assert.That(motor.Clear(body,1<<10,c=>false),Is.True);motor.Release();
                Assert.That(motor.Clear(body,1<<10,c=>true),Is.False);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
