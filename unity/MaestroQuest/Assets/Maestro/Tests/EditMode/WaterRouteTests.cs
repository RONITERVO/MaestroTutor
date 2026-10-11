// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class WaterRouteGeometryTests {
        [TestCase(false,0)][TestCase(false,30)][TestCase(true,0)][TestCase(true,30)]
        public void ExpandedLiveFootprintProvidesDryEdges(bool rectangle,float tilt){
            var root=new GameObject("Route volume");
            try{
                root.transform.position=new Vector3(3,0,2);root.transform.rotation=Quaternion.Euler(tilt,35,0);root.transform.localScale=Vector3.one*1.4f;
                var item=root.AddComponent<RoomItem>();var c=new RoomContainer{height=.4f,radius=.5f,capacityMl=300,amountMl=200};
                if(rectangle){c.version=2;c.rectangle=new(){width=1,depth=.7f};}
                var volume=new LiquidMediumGeometry("pool",item,c,Vector3.up,null);var points=new List<Vector3>();LiquidRouteFootprint.Build(in volume,.31f,1.7f,0,points);
                Assert.That(points.Count,Is.GreaterThanOrEqualTo(4));
                for(int i=0;i<points.Count;i++)Assert.That(volume.Sweep(points[i],points[(i+1)%points.Count],.25f,1.7f,out _,out _),Is.False,"Expanded footprint edge must be dry");
                c.amountMl=0;volume=new("pool",item,c,Vector3.up,null);LiquidRouteFootprint.Build(in volume,.31f,1.7f,0,points);Assert.That(points,Is.Empty);
            }finally{Object.DestroyImmediate(root);}
        }
        [Test]public void SlopingCrossingReportsTheActualWetFootHeightForRouteCandidates(){
            var root=new GameObject("Sloping water path");
            try{
                var item=root.AddComponent<RoomItem>();var c=new RoomContainer{version=2,rectangle=new(){width=1,depth=1},height=.4f,capacityMl=300,amountMl=150};
                var volume=new LiquidMediumGeometry("pool",item,c,Vector3.up,null);
                var from=new Vector3(-4,3,0);var to=Vector3.zero;
                Assert.That(volume.Sweep(from,to,.25f,1.7f,out var depth,out _,out var foot),Is.True);
                Assert.That(foot.y,Is.EqualTo(0).Within(.001));Assert.That(depth,Is.EqualTo(.2f).Within(.001));
                var points=new List<Vector3>();LiquidRouteFootprint.Build(in volume,.31f,1.7f,foot.y,points);Assert.That(points,Is.Not.Empty);
                LiquidRouteFootprint.Build(in volume,.31f,1.7f,(from.y+to.y)*.5f,points);Assert.That(points,Is.Empty,"A segment midpoint can be above the liquid even when the path enters it");
            }finally{Object.DestroyImmediate(root);}
        }
        [Test]public void CooperativeSearchBudgetsNativeWorkAndRevalidatesItsResult(){
            int queried=0,sampled=0,validated=0;
            bool Edge(Vector3 a,Vector3 b,out Vector3[] path,out string liquid,out Vector3 foot){queried++;path=new[]{a,b};liquid=null;foot=a;return true;}
            void Footprint(string id,Vector3 at,List<Vector3> points){points.Add(new Vector3(0,0,1));points.Add(new Vector3(2,0,1));}
            bool Sample(Vector3 p,out Vector3 floor){sampled++;floor=p;return true;}
            bool Segment(Vector3 a,Vector3 b){validated++;return false;}
            var search=new RoomRouteSearch(Vector3.zero,Vector3.right*2,"pool",Vector3.zero,Edge,Footprint,Sample,Segment);
            for(int i=0;i<1000&&search.Pending;i++){int before=queried+sampled+validated;search.Tick();Assert.That(queried+sampled+validated-before,Is.LessThanOrEqualTo(RoomRouteSearch.QueriesPerTick));}
            Assert.That(search.Pending,Is.False);Assert.That(search.Result,Is.Null);StringAssert.Contains("changed",search.Failure);Assert.That(validated,Is.GreaterThan(0));
        }
    }
}
