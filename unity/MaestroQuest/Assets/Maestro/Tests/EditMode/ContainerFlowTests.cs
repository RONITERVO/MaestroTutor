// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class ContainerFlowTests {
        [Test] public void OpenCylinderRetainsUprightContentsAndSpillsAboveItsLowestLip(){
            var c=new RoomContainer{capacityMl=500,amountMl=500};Assert.That(ContainerFlowGeometry.Excess(c,Quaternion.identity,Vector3.up),Is.Zero.Within(1e-9));c.amountMl=250;
            Assert.That(ContainerFlowGeometry.Excess(c,Quaternion.Euler(0,0,10),Vector3.up),Is.Zero.Within(1e-9));
            double previous=0;foreach(int angle in new[]{20,40,60,80,90,120,180}){double spill=ContainerFlowGeometry.Excess(c,Quaternion.Euler(0,0,angle),Vector3.up);Assert.That(spill,Is.InRange(previous,250),angle.ToString());previous=spill;}Assert.That(previous,Is.EqualTo(250));
        }
        [Test] public void RenderedLevelAndPouringUseTheSameCylinderVolume(){
            foreach(float angle in new[]{0,18,45,80,90,135,180})foreach(float fraction in new[]{.01f,.25f,.5f,.9f,.999f}){
                var n=Quaternion.Euler(0,0,angle)*Vector3.up;float level=ContainerFillView.Level(n,.08f,.2f,fraction);Assert.That(ContainerFlowGeometry.FractionBelow(n,.08,.2,level),Is.EqualTo(fraction).Within(.0001));
            }
        }
        [Test] public void ReceiverRequiresDownwardEntryThroughTheScaledOpening(){
            var go=new GameObject("Opening test");try{go.transform.position=new Vector3(2,1,0);go.transform.localScale=Vector3.one*2;var c=new RoomContainer{radius=.1f,height=.2f};Assert.That(ContainerFlowGeometry.Enters(c,go.transform,new Vector3(2.15f,2,0),new Vector3(2.15f,1,0),Vector3.up,out var fraction),Is.True);Assert.That(fraction,Is.EqualTo(.6f).Within(.0001));
                Assert.That(ContainerFlowGeometry.Enters(c,go.transform,new Vector3(2.21f,2,0),new Vector3(2.21f,1,0),Vector3.up,out _),Is.False);Assert.That(ContainerFlowGeometry.Enters(c,go.transform,new Vector3(2,1,0),new Vector3(2,2,0),Vector3.up,out _),Is.False);go.transform.rotation=Quaternion.Euler(0,0,180);Assert.That(ContainerFlowGeometry.Enters(c,go.transform,new Vector3(2,2,0),new Vector3(2,0,0),Vector3.up,out _),Is.False);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
