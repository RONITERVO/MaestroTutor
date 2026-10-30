// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class WaterEgressGeometryTests {
        [TestCase(false,1f,0f)][TestCase(true,1f,0f)][TestCase(false,2f,37f)][TestCase(true,.1f,53f)]
        public void ShallowExitUsesTheSameScaledRotatedCavityAndRejectsDeeperOrFartherTravel(bool rectangular,float scale,float yaw){
            var go=new GameObject("Shallow egress");
            try{
                go.transform.position=new(3,2,-1);go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
                var item=go.AddComponent<RoomItem>();var c=new RoomContainer{radius=1,height=.4f,capacityMl=100,amountMl=50};
                if(rectangular){c.version=2;c.rectangle=new(){width=2,depth=2};}
                var water=new LiquidMediumGeometry("water",item,c,Vector3.up,null);float r=.1f*scale,h=1.7f*scale,max=.25f*scale;
                Vector3 P(float x,float y=0,float z=0)=>go.transform.TransformPoint(new Vector3(x,y,z));
                bool Exit(Vector3 from,Vector3 to){Assert.That(water.Sweep(from,to,r,h,out float d,out _),Is.True);return water.ShallowExit(from,to,r,h,max,d);}
                Assert.That(Exit(P(.6f),P(.7f)),Is.True,"Small steps toward the near edge must work while both endpoints are wet");
                Assert.That(Exit(P(.6f),P(1.3f)),Is.True);
                Assert.That(Exit(P(.6f),P(.5f)),Is.False,"Do not head farther into the pool");
                Assert.That(Exit(P(.6f),P(-1.3f)),Is.False,"A dry endpoint across the pool is not egress");
                Assert.That(Exit(P(.6f),P(.7f,-.02f)),Is.False,"A closer edge cannot excuse deeper water");
                Assert.That(Exit(P(.6f),P(.5f,.05f)),Is.True,"Uphill movement makes the whole wet segment shallower");
                Assert.That(Exit(P(.6f),P(.6f)),Is.False);
                Assert.That(Exit(P(1.3f),P(.7f)),Is.False,"A dry actor cannot borrow egress permission");
                Assert.That(water.Sweep(P(.6f),P(.7f),r,h,out float depth,out _),Is.True);
                Assert.That(water.ShallowExit(P(.6f),P(.7f),r,.2f*scale,.12f*scale,depth),Is.False,"No over-head wading");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
