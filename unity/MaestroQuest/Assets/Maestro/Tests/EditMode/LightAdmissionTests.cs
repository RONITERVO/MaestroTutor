// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class LightAdmissionTests
    {
        [Test]public void LightAdmissionSharedSlotsAndShadowFacesHaveIndependentLimits(){
            var before=LightResources.ObserveBudget().ToString();var held=new List<LightResources.Lease>();
            var owner=new RoomResourceOwner(RoomWorldIdentity.Create(),"first","lighting");
            try{
                held.Add(LightResources.Begin(new RoomLight{shadows="hard"},owner));
                held.Add(LightResources.Begin(new RoomLight{shadows="hard"},new RoomResourceOwner(RoomWorldIdentity.Create(),"second","lighting")));
                Assert.Throws<RoomResourceLimitException>(()=>LightResources.Begin(new RoomLight{kind="spot",shadows="hard"},owner));
                Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(2));
                for(int i=2;i<8;i++)held.Add(LightResources.Begin(new RoomLight(),owner));
                Assert.Throws<RoomResourceLimitException>(()=>LightResources.Begin(new RoomLight(),owner));
                held[0].Dispose();held[0].Dispose();using var retry=LightResources.Begin(new RoomLight{shadows="hard"},owner);
                Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(8));Assert.That((int)LightResources.ObserveBudget()["shadowFaces"],Is.EqualTo(12));
            }finally{foreach(var lease in held)lease.Dispose();}
            Assert.That(LightResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void LightAdmissionRejectsActualShadowCostMismatchAndReleasesNeverActiveRoot(){
            var before=LightResources.ObserveBudget().ToString();using var lease=LightResources.Begin(new RoomLight{kind="spot",shadows="hard"},new RoomResourceOwner(null,"lamp","lighting"));
            var go=new GameObject("Unpublished lamp");go.SetActive(false);lease.Bind(go);
            try{var light=go.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.Hard;Assert.Throws<InvalidOperationException>(()=>lease.Complete(light));Assert.Throws<InvalidOperationException>(()=>lease.Ready());Assert.That((int)LightResources.ObserveBudget()["lights"],Is.EqualTo(1));}
            finally{UnityEngine.Object.DestroyImmediate(go);}
            Assert.That(LightResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [TestCase("directional","none")][TestCase("point","soft")]
        public void LightAdmissionRejectsUnsupportedCostInputs(string kind,string shadows){
            var before=LightResources.ObserveBudget().ToString();Assert.Throws<ArgumentException>(()=>LightResources.Begin(new RoomLight{kind=kind,shadows=shadows},new RoomResourceOwner(null,"lamp","lighting")));Assert.That(LightResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
    }
}
