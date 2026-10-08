// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class WorldPathTests
    {
        [TestCase(-179f)][TestCase(-30f)][TestCase(0f)][TestCase(.001f)][TestCase(30f)][TestCase(179f)]
        public void SweptEnvelopeContainsEveryCornerThroughoutYawAndTranslation(float yaw) {
            var origin=new Vector3(3,1,-2);var facing=Quaternion.Euler(0,22,0);var destination=new Vector3(-4,1.3f,5);var rotation=Quaternion.Euler(0,22+yaw,0);
            var path=new RoomWorldPath(origin,facing,destination,rotation);Assert.That(path.Valid,Is.True);
            var bounds=new Bounds(new Vector3(5,2,3),new Vector3(.2f,.8f,1.2f));
            var expected=destination+rotation*Quaternion.Inverse(facing)*(bounds.center-origin);
            Assert.That(Vector3.Distance(path.Point(bounds.center,1),expected),Is.LessThan(.01f));
            for(int step=0;step<path.Steps;step++) {
                path.Envelope(bounds,step,out var from,out var to,out var extents);
                for(int sample=0;sample<=20;sample++)for(int corner=0;corner<8;corner++) {
                    float local=sample/20f,time=(step+local)/path.Steps;
                    var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    var offset=path.Point(point,time)-Vector3.Lerp(from,to,local);
                    Assert.That(Mathf.Abs(offset.x),Is.LessThanOrEqualTo(extents.x+.002f));Assert.That(Mathf.Abs(offset.y),Is.LessThanOrEqualTo(extents.y+.0001f));Assert.That(Mathf.Abs(offset.z),Is.LessThanOrEqualTo(extents.z+.002f));
                }
            }
        }
        [Test]public void SnapTurnPathRetainsItsPhysicalPivotAndHeight() {
            var pivot=new Vector3(2,1.6f,-3);var origin=new Vector3(4,.2f,1);var rotation=Quaternion.Euler(0,30,0);
            var path=new RoomWorldPath(origin,Quaternion.identity,pivot+rotation*(origin-pivot),rotation);
            for(int i=0;i<=100;i++)Assert.That(Vector3.Distance(path.Point(pivot,i/100f),pivot),Is.LessThan(.0001f));
        }
        [Test]public void UprightPathsAcceptEveryHeadingWithoutQuaternionAcosRoundingFailures() {
            for(float from=-180;from<=180;from+=.75f)foreach(float change in new[]{.001f,30f,-30f}) {
                var path=new RoomWorldPath(Vector3.zero,Quaternion.Euler(0,from,0),Vector3.right*.02f,Quaternion.Euler(0,from+change,0));
                Assert.That(path.Valid,Is.True,$"Heading {from}, change {change}");
            }
        }
    }
}
