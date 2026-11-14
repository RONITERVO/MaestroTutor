// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class VisualLookupTests {
        static RoomSpatialExtent Extent(Bounds bounds)=>new(){Known=true,HasBounds=true,Bounds=bounds};
        [TestCase(false,0)] [TestCase(false,45)] [TestCase(false,150)]
        [TestCase(true,0)] [TestCase(true,45)] [TestCase(true,150)]
        public void VisualLookupMatchesExhaustiveViewsAcrossProjectionPredictionAndWorldTransforms(bool orthographic,float angle){
            var root=new GameObject("Visual lookup camera",typeof(Camera));
            try{
                var camera=root.GetComponent<Camera>();camera.stereoTargetEye=StereoTargetEyeMask.None;camera.nearClipPlane=.1f;camera.farClipPlane=80;
                camera.aspect=1.3f;camera.fieldOfView=65;camera.orthographic=orthographic;camera.orthographicSize=4;
                var random=new System.Random(891);float Next(float range)=>(float)((random.NextDouble()-.5)*range);
                var entries=Enumerable.Range(0,1024).Select(i=>new RoomSpatialBounds{Visual=i%43==0?default:i%47==0?RoomSpatialExtent.Empty:Extent(new Bounds(new(Next(150),Next(45),Next(150)),new(Math.Abs(Next(10)),Math.Abs(Next(10)),Math.Abs(Next(10))))),VisualLayers=i%7==0?1<<17:1}).ToArray();
                entries[1].Visual=Extent(new Bounds(Vector3.zero,Vector3.one*300)); // Enclosing geometry must survive every partition.
                var lookup=new RoomSpatialLookup(entries.Select(e=>e.Visual).ToArray());var indices=new List<int>();
                for(int probe=0;probe<60;probe++){
                    var mapping=Matrix4x4.TRS(new(Next(30),Next(20),Next(30)),Quaternion.Euler(Next(50),Next(360),Next(50)),Vector3.one*(.25f+(float)random.NextDouble()*3));
                    root.transform.SetPositionAndRotation(new(Next(35),Next(15),Next(35)),Quaternion.Euler(Next(30),Next(360),0));camera.cullingMask=probe%3==0?1<<17:-1;
                    var first=new RoomObservationView(camera,translation:.7f,angle:angle,uncertain:probe==0);
                    root.transform.rotation*=Quaternion.Euler(0,70,0);var second=new RoomObservationView(camera,translation:.2f,angle:angle);
                    bool Included(int i){var spatial=entries[i].Transform(mapping);return first.Includes(spatial)||second.Includes(spatial);}
                    var expected=Enumerable.Range(0,entries.Length).Where(Included).ToArray();
                    lookup.Query((min,max)=>{var bounds=RoomVisualEnvelope.Project(min,max,mapping);return first.MayInclude(bounds)||second.MayInclude(bounds);},indices);
                    CollectionAssert.AreEquivalent(expected,indices.Where(Included).ToArray(),"Probe "+probe);
                }
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void VisualEnvelopeContainsLeafProjectionThroughCancellationAndLargeCoordinates(){
            var boxes=new[]{new Bounds(new(3,2,-4),new(1,8,3)),new Bounds(new(1e20f,-1e20f,1e20f),Vector3.one*1e15f),new Bounds(Vector3.zero,Vector3.one*1e30f)};
            var cancellation=Matrix4x4.identity;cancellation.m00=1e10f;cancellation.m01=1e10f;cancellation.m03=-1e20f;
            foreach(var box in boxes)foreach(var mapping in new[]{Matrix4x4.identity,Matrix4x4.TRS(new(1e10f,-1e10f,3),Quaternion.Euler(73,21,13),Vector3.one*2),cancellation}){
                var exact=Extent(box).Transform(mapping);var outer=RoomVisualEnvelope.Project(box.min,box.max,mapping);
                if(!outer.Known)continue;
                Assert.That(exact.Known,Is.True);Assert.That(outer.Bounds.Contains(exact.Bounds.min)&&outer.Bounds.Contains(exact.Bounds.max),Is.True,$"Lost projected leaf {box}");
            }
            var invalid=Matrix4x4.identity;invalid.m11=float.NaN;
            Assert.That(RoomVisualEnvelope.Project(Vector3.zero,Vector3.one,invalid).Known,Is.False);
        }
        [Test] public void VisualEnvelopeKeepsFloatOverflowRiskUnknownForShadows(){
            var source=Extent(new Bounds(new(1e30f,-1e30f,0),Vector3.one*10));
            var mapping=Matrix4x4.identity;mapping.m00=mapping.m01=1e10f;mapping.m03=1e37f;
            // Mono can retain wider intermediates. Force a float32 storage
            // boundary to exercise the range that target backends must support.
            float Stored(float value)=>BitConverter.ToSingle(BitConverter.GetBytes(value),0);
            float positive=Stored(mapping.m00*source.Bounds.center.x),negative=Stored(mapping.m01*source.Bounds.center.y);
            Assert.That(float.IsNaN(positive+negative),Is.True,"Float32 products overflow before cancellation");
            var envelope=RoomVisualEnvelope.Project(source.Bounds.min,source.Bounds.max,mapping);
            Assert.That(envelope.Known,Is.False,"Finite double cancellation must not hide unknown float geometry");
            var influence=Extent(new Bounds(Vector3.zero,Vector3.one));var indices=new List<int>();
            new RoomSpatialLookup(new[]{source}).Query((min,max)=>RoomLightObservation.Intersects(influence,RoomVisualEnvelope.Project(min,max,mapping)),indices);
            CollectionAssert.AreEqual(new[]{0},indices,"An unknown caster remains a shadow dependency");
        }
        [Test] public void VisualEnvelopeRejectsOverflowingExtentSize(){
            var bounds=new Bounds{center=Vector3.zero,extents=Vector3.one*2e38f};var source=Extent(bounds);
            Assert.That(RoomRecipe.Finite(bounds.size),Is.False);
            Assert.That(source.Transform(Matrix4x4.identity).Known,Is.False);
            Assert.That(RoomVisualEnvelope.Project(bounds.min,bounds.max,Matrix4x4.identity).Known,Is.False);
        }
        [Test] public void VisualLookupPrunesDistantBranchesAndKeepsUnknownInputs(){
            var root=new GameObject("Visual locality",typeof(Camera));
            try{
                var camera=root.GetComponent<Camera>();camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=40;camera.aspect=1;camera.farClipPlane=30;
                var values=Enumerable.Range(0,1024).Select(i=>Extent(new Bounds(i==0?Vector3.forward*5:new Vector3(20+i,0,-10),Vector3.one))).ToList();values.Add(default);values.Add(RoomSpatialExtent.Empty);
                var lookup=new RoomSpatialLookup(values);var view=new RoomObservationView(camera);var indices=new List<int>();
                int visits=lookup.Query((min,max)=>view.MayInclude(RoomVisualEnvelope.Project(min,max,Matrix4x4.identity)),indices);
                CollectionAssert.AreEquivalent(new[]{0,1024},indices);Assert.That(visits,Is.LessThan(lookup.NodeCount/8));
                lookup.Query((_,__)=>true,indices,includeEmpty:true);Assert.That(indices.Count,Is.EqualTo(values.Count));
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
