// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class InkAdmissionTests
    {
        [Test]public void InkAdmissionSurfaceStrokeAndVertexLimitsAreIndependentAcrossWorlds(){
            string before=InkResources.ObserveBudget().ToString();var held=new List<InkResources.Lease>();
            var first=new RoomResourceOwner(RoomWorldIdentity.Create(),"first","ink");var second=new RoomResourceOwner(RoomWorldIdentity.Create(),"second","preview");
            try{
                for(int i=0;i<InkResources.MaximumSurfaces;i++)held.Add(InkResources.Surface(i%2==0?first:second,"patch"));
                Assert.Throws<RoomResourceLimitException>(()=>InkResources.Surface(second,"overflow"));
                for(int i=0;i<InkResources.MaximumStrokes;i++)held.Add(InkResources.Stroke(2,first,"stroke"));
                Assert.Throws<RoomResourceLimitException>(()=>InkResources.Stroke(2,second,"overflow"));
                Assert.That((long)InkResources.ObserveBudget()["vertices"],Is.EqualTo(384*6));
            }finally{foreach(var lease in held)lease.Dispose();held.Clear();}
            try{
                int points=InkResources.MaximumVertices/3;
                while(points>0){int take=Math.Min(points,PencilStrokeMesh.MaximumPoints);held.Add(InkResources.Stroke(take,first,"vertices"));points-=take;}
                Assert.Throws<RoomResourceLimitException>(()=>InkResources.Stroke(1,second,"overflow"));
                using var surface=InkResources.Surface(second,"still allowed");
                Assert.That((long)InkResources.ObserveBudget()["vertices"],Is.EqualTo(InkResources.MaximumVertices));
                Assert.That((int)InkResources.ObserveBudget()["strokes"],Is.LessThan(InkResources.MaximumStrokes));
            }finally{foreach(var lease in held)lease.Dispose();}
            Assert.That(InkResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void InkAdmissionRejectsLargerActualCostAndReapsNeverActiveOwners(){
            var before=InkResources.ObserveBudget().ToString();var owner=new RoomResourceOwner(null,"draft","preview");
            using(var lease=InkResources.Stroke(1,owner,"mismatch")){
                var mesh=new Mesh();lease.Own(mesh);mesh.vertices=new Vector3[9];
                Assert.Throws<RoomResourceLimitException>(()=>lease.CheckMesh(mesh));Assert.That(lease.Vertices,Is.EqualTo(9));
            }
            var host=new GameObject("never activated");host.SetActive(false);
            var marks=host.AddComponent<PencilMarks>();
            using(var candidate=PencilGeometry.Prepare(new[]{new[]{Vector3.zero,Vector3.right}},.003f,owner,"draft"))marks.SetPrepared(candidate);
            var native=host.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(host);InkResources.Collect();Assert.That(!native,Is.True);
            Assert.That(InkResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void InkAdmissionReservesBeforePathExpansionAndValidatesProjectedCount(){
            string before=InkResources.ObserveBudget().ToString();var owner=new RoomResourceOwner(null,"draft","preview");var held=new List<InkResources.Lease>();bool expanded=false;
            try{
                for(int i=0;i<InkResources.MaximumStrokes;i++)held.Add(InkResources.Stroke(0,owner,"pending"));
                Assert.Throws<RoomResourceLimitException>(()=>new PencilGeometry(2,()=>{expanded=true;return new[]{new[]{Vector3.zero,Vector3.right}};},.003f,owner,"draft"));Assert.That(expanded,Is.False);
            }finally{foreach(var lease in held)lease.Dispose();}
            Assert.Throws<ArgumentException>(()=>new PencilGeometry(1,()=>new[]{new[]{Vector3.zero,Vector3.right}},.003f,owner,"draft"));
            Assert.That(InkResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
    }
}
