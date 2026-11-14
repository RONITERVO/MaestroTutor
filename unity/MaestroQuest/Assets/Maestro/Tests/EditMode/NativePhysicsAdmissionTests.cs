// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class NativePhysicsAdmissionTests
    {
        static RoomObjectData Data()=>new(){id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block};
        [Test]public void NativePhysicsAdmissionIndependentLimitsCoverMultipleWorlds(){
            string before=NativePhysicsResources.ObserveBudget().ToString();var held=new List<NativePhysicsResources.Lease>();var a=RoomWorldIdentity.Create();var b=RoomWorldIdentity.Create();
            try{
                for(int i=0;i<NativePhysicsResources.MaximumBodies;i++){var data=Data();held.Add(NativePhysicsResources.Body(data,new RoomResourceOwner(i%2==0?a:b,data.id,"physics")));}
                var extra=Data();Assert.Throws<RoomResourceLimitException>(()=>NativePhysicsResources.Body(extra,new RoomResourceOwner(a,extra.id,"physics")));Assert.That((int)NativePhysicsResources.ObserveBudget()["joints"],Is.Zero);
                for(int i=0;i<NativePhysicsResources.MaximumJoints;i++)held.Add(NativePhysicsResources.Connection(new RoomResourceOwner(i%2==0?a:b,"connection","physics")));
                Assert.Throws<RoomResourceLimitException>(()=>NativePhysicsResources.Connection(new RoomResourceOwner(a,"extra","physics")));held[0].Dispose();held[0].Dispose();using var retry=NativePhysicsResources.Body(extra,new RoomResourceOwner(a,extra.id,"physics"));Assert.That((int)NativePhysicsResources.ObserveBudget()["bodies"],Is.EqualTo(128));Assert.That((int)NativePhysicsResources.ObserveBudget()["joints"],Is.EqualTo(32));
            }finally{foreach(var lease in held)lease.Dispose();}
            Assert.That(NativePhysicsResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void NativePhysicsAdmissionBodyRequiresExactSourceAndCompleteOwnedMaterial(){
            var data=Data();using var lease=NativePhysicsResources.Body(data,new RoomResourceOwner(null,data.id,"physics"));var other=data.Copy();other.kind=RoomObjectKind.Ball;Assert.Throws<InvalidOperationException>(()=>lease.ValidateBody(other));other=data.Copy();other.id=Guid.NewGuid().ToString("N");Assert.Throws<InvalidOperationException>(()=>lease.ValidateBody(other));Assert.Throws<InvalidOperationException>(()=>lease.ReadyBody());
            var host=new GameObject("Body reservation");try{var body=host.AddComponent<Rigidbody>();lease.BindBody(body);Assert.Throws<InvalidOperationException>(()=>lease.BindBody(body));Assert.Throws<InvalidOperationException>(()=>lease.ReadyBody());lease.OwnMaterial(new PhysicsMaterial("Owned test material"));lease.ReadyBody();Assert.That(lease.Phase,Is.EqualTo("ready"));}finally{UnityEngine.Object.DestroyImmediate(host);NativePhysicsResources.Collect();}
        }
        [Test]public void NativePhysicsAdmissionNeverActiveOwnersReapTheirBodiesMaterialsAndJoints(){
            string before=NativePhysicsResources.ObserveBudget().ToString();var data=Data();var host=new GameObject("Never active physics");host.SetActive(false);var body=host.AddComponent<Rigidbody>();var material=new PhysicsMaterial("Never active material");
            using var bodyLease=NativePhysicsResources.Body(data,new RoomResourceOwner(null,data.id,"physics"));bodyLease.BindBody(body);bodyLease.OwnMaterial(material);bodyLease.ReadyBody();
            var view=host.AddComponent<RoomConnectionView>();using var jointLease=NativePhysicsResources.Connection(new RoomResourceOwner(null,data.id,"physics"));jointLease.BindJointHost(view);var joint=host.AddComponent<FixedJoint>();jointLease.TrackJoint(joint);
            UnityEngine.Object.DestroyImmediate(host);NativePhysicsResources.Collect();Assert.That(!body&&!material&&!joint,Is.True);Assert.That(NativePhysicsResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void NativePhysicsAdmissionPartialNeverActiveBuildCannotOrphanAnUnboundBodySlot(){
            string before=NativePhysicsResources.ObserveBudget().ToString();var data=Data();data.kind=RoomObjectKind.Drawing;data.points=null;var host=new GameObject("Failed private body");host.SetActive(false);
            Assert.Catch<Exception>(()=>host.AddComponent<CreatedRoomObject>().Build(data));UnityEngine.Object.DestroyImmediate(host);NativePhysicsResources.Collect();NativeObjectResources.Collect();Assert.That(NativePhysicsResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void NativePhysicsAdmissionConnectionRequiresExactSourceAndOwner(){
            string before=NativePhysicsResources.ObserveBudget().ToString();var host=new GameObject("Connection source");var other=new GameObject("Other owner");
            try{var data=Data();data.connections=new[]{new RoomConnection{connected=Guid.NewGuid().ToString("N")}};var view=host.AddComponent<RoomConnectionView>();var otherView=other.AddComponent<RoomConnectionView>();using var candidate=new PreparedRoomConnection(null,data,view);var changed=data.Copy();changed.connections[0].drive.speed=20;Assert.Throws<InvalidOperationException>(()=>candidate.Validate(changed));changed=data.Copy();changed.id=Guid.NewGuid().ToString("N");Assert.Throws<InvalidOperationException>(()=>candidate.Validate(changed));Assert.Throws<InvalidOperationException>(()=>candidate.Take(otherView,data));}finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(other);}
            Assert.That(NativePhysicsResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
    }
}
