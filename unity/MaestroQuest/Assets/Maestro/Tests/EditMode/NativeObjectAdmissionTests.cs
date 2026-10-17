// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class NativeObjectAdmissionTests
    {
        [Test] public void NativeObjectAdmissionReservesAcrossWorkspacesAndReleasesUnboundCancellation()
        {
            var before=NativeObjectResources.ObserveBudget();var held=new List<NativeObjectResources.Lease>();
            try{
                for(int i=(int)before["instances"];i<NativeObjectResources.MaximumInstances;i++){
                    var data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block};held.Add(NativeObjectResources.Begin(data,new RoomResourceOwner(RoomWorldIdentity.Create(),data.id,"object")));
                }
                var last=new RoomObjectData{id="pending",kind=RoomObjectKind.Ball};var owner=new RoomResourceOwner(null,last.id,"object");
                Assert.Throws<RoomResourceLimitException>(()=>NativeObjectResources.Begin(last,owner));Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(128));
                held[0].Dispose();held[0].Dispose();using var retry=NativeObjectResources.Begin(last,owner);Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(128));
            }finally{foreach(var lease in held)lease.Dispose();}
            Assert.That(NativeObjectResources.ObserveBudget().ToString(),Is.EqualTo(before.ToString()));
        }
        [TestCase(RoomObjectKind.Book)][TestCase(RoomObjectKind.Maestro)][TestCase((RoomObjectKind)999)]
        public void NativeObjectAdmissionRejectsHostBuiltinsAndUnknownKinds(RoomObjectKind kind)
        {
            var before=NativeObjectResources.ObserveBudget().ToString();var data=new RoomObjectData{id="invalid",kind=kind};
            Assert.Throws<ArgumentException>(()=>NativeObjectResources.Begin(data,new RoomResourceOwner(null,data.id,"object")));Assert.That(NativeObjectResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
    }
}
