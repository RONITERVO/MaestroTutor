// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class ContainerScoopingTests {
        GameObject donor,receiver;RoomContainer source,destination;
        [SetUp] public void SetUp(){donor=new GameObject("Donor");receiver=new GameObject("Receiver");source=new RoomContainer{radius=.4f,height=.5f,capacityMl=10000,amountMl=8000};destination=new RoomContainer{radius=.08f,height=.2f,capacityMl=500};receiver.transform.position=new Vector3(0,.1f,0);}
        [TearDown] public void TearDown(){Object.DestroyImmediate(donor);Object.DestroyImmediate(receiver);}
        bool Contact(out ContainerScoopingGeometry.Contact contact)=>ContainerScoopingGeometry.TryContact(source,donor.transform,destination,receiver.transform,Vector3.up,out contact);
        [Test] public void ScoopingRequiresAnImmersedSmallerOpeningAndDoesNotMutateContents(){
            Assert.That(Contact(out var contact),Is.True);Assert.That(contact.Path(0,out var from,out var to),Is.True);Assert.That(from.y,Is.EqualTo(.401f).Within(.00001f));Assert.That(to.y,Is.EqualTo(.299f).Within(.00001f));
            Assert.That(source.amountMl,Is.EqualTo(8000));Assert.That(destination.amountMl,Is.Zero);
            receiver.transform.position=Vector3.up*.25f;Assert.That(Contact(out _),Is.False,"Opening above liquid");
            receiver.transform.position=Vector3.up*.1f;destination.radius=.4f;Assert.That(Contact(out _),Is.False,"Equal or larger vessel cannot nest");
        }
        [Test] public void ScoopingRejectsFloorWallInversionAndIncompatibleContents(){
            receiver.transform.position=Vector3.up*-.01f;Assert.That(Contact(out _),Is.False,"Cavity through floor");
            receiver.transform.position=new Vector3(.35f,.1f,0);Assert.That(Contact(out _),Is.False,"Cavity through wall");
            receiver.transform.position=Vector3.up*.1f;receiver.transform.rotation=Quaternion.Euler(0,0,180);Assert.That(Contact(out _),Is.False);
            receiver.transform.rotation=Quaternion.identity;destination.amountMl=1;destination.liquid="Juice";Assert.That(Contact(out _),Is.False);
            destination.amountMl=0;Assert.That(Contact(out _),Is.True,"An empty vessel can adopt the donor liquid");
            source.amountMl=0;Assert.That(Contact(out _),Is.False);
        }
        [Test] public void ScoopingUsesTranslatedScaledAndTiltedCavityFrames(){
            donor.transform.SetPositionAndRotation(new Vector3(3,2,-1),Quaternion.Euler(0,35,8));donor.transform.localScale=Vector3.one*2;
            receiver.transform.SetPositionAndRotation(donor.transform.TransformPoint(new Vector3(0,.1f,0)),donor.transform.rotation);receiver.transform.localScale=Vector3.one*2;
            Assert.That(Contact(out var contact),Is.True);Assert.That(contact.Path(0,out var from,out var to),Is.True);Assert.That(from.y,Is.GreaterThan(to.y));Assert.That(from.x,Is.EqualTo(to.x).Within(.00001f));Assert.That(from.z,Is.EqualTo(to.z).Within(.00001f));
            source.amountMl=100;Assert.That(Contact(out _),Is.False,"Actual depleted surface replaces the nominal rim");
        }
    }
}
