// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class RoomFrameTests
    {
        GameObject root;
        [SetUp] public void SetUp() => root = new GameObject("Authored world frame");
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        static void Near(Vector3 actual, Vector3 expected) => Assert.That(Vector3.Distance(actual,expected), Is.LessThan(.0001f));
        Transform Child(Transform parent)
        { var item=new GameObject("Frame member");item.transform.SetParent(parent,false);return item.transform; }

        [Test] public void SnapshotSeparatesPointsVectorsDirectionsAndScaleAndSurvivesRootMotion()
        {
            root.transform.SetPositionAndRotation(new Vector3(8,2,-5),Quaternion.Euler(0,60,0));root.transform.localScale=Vector3.one*.25f;
            var frame=new RoomFrame(root.transform);Assert.That(frame.Valid,Is.True);Assert.That(frame.MetresPerUnit,Is.EqualTo(.25f).Within(.00001f));
            var point=new Vector3(2,3,-4);var rendered=frame.PointToWorld(point);var vector=frame.VectorToWorld(Vector3.right);
            Near(frame.PointToRoom(rendered),point);Assert.That(vector.magnitude,Is.EqualTo(.25f).Within(.00001f));
            Near(frame.VectorToRoom(vector),Vector3.right);Assert.That(frame.DirectionToWorld(Vector3.right).magnitude,Is.EqualTo(1).Within(.00001f));
            Near(frame.DirectionToRoom(frame.DirectionToWorld(Vector3.forward)),Vector3.forward);
            root.transform.SetPositionAndRotation(Vector3.one*20,Quaternion.Euler(0,-30,0));
            Near(frame.PointToWorld(point),rendered);Near(frame.PointToRoom(rendered),point);
            Assert.That(Vector3.Distance(new RoomFrame(root.transform).PointToWorld(point),rendered),Is.GreaterThan(1));
        }
        [Test] public void MotionHistoryResamplesTranslationAndYawWithoutChangingItsUnits()
        {
            root.transform.localScale=Vector3.one*.25f;var space=new RoomMotionFrame(root.transform);
            Assert.That(space.TryRead(out var before),Is.True);var point=new Vector3(2,3,-4);
            root.transform.SetPositionAndRotation(new Vector3(8,2,-5),Quaternion.Euler(0,60,0));
            Assert.That(space.TryRead(out var after),Is.True);Near(after.PointToRoom(after.PointToWorld(point)),point);
            Assert.That(Vector3.Distance(before.PointToWorld(point),after.PointToWorld(point)),Is.GreaterThan(1));
            Assert.That(after.VectorToWorld(Vector3.right).magnitude,Is.EqualTo(.25f).Within(.00001f));
        }
        [Test] public void MotionHistoryRejectsScaleTiltDistortionAndOwnerLoss()
        {
            var space=new RoomMotionFrame(root.transform);root.transform.localScale=Vector3.one*2;Assert.That(space.TryRead(out _),Is.False);
            root.transform.localScale=Vector3.one;root.transform.rotation=Quaternion.Euler(10,0,0);Assert.That(space.TryRead(out _),Is.False);
            root.transform.rotation=Quaternion.identity;root.transform.localScale=new Vector3(1,2,1);Assert.That(space.TryRead(out _),Is.False);
            root.transform.localScale=Vector3.one;root.SetActive(false);Assert.That(space.TryRead(out _),Is.False);
            Object.DestroyImmediate(root);Assert.That(space.TryRead(out _),Is.False);Assert.That(default(RoomMotionFrame).TryRead(out _),Is.False);
        }
        [Test] public void AnInvalidInitialMotionFrameCannotBecomeAnAdmittedHistoryLater()
        {
            root.transform.localScale=new Vector3(1,2,1);var space=new RoomMotionFrame(root.transform);
            root.transform.localScale=Vector3.one;Assert.That(space.TryRead(out _),Is.False);
            root.transform.rotation=Quaternion.Euler(10,0,0);space=new RoomMotionFrame(root.transform);
            root.transform.rotation=Quaternion.identity;Assert.That(space.TryRead(out _),Is.False);
        }
        [Test] public void NestedParentPoseRoundTripsThroughOneExplicitRoomFrame()
        {
            root.transform.SetPositionAndRotation(new Vector3(-4,1,7),Quaternion.Euler(0,37,0));
            var group=Child(root.transform);group.SetLocalPositionAndRotation(new Vector3(2,1,-3),Quaternion.Euler(10,70,0));group.localScale=Vector3.one*1.5f;
            var item=Child(group);var frame=new RoomFrame(root.transform);var point=new Vector3(.7f,1.3f,-.4f);var orientation=Quaternion.Euler(0,-23,0);
            Assert.That(frame.Apply(item,point,orientation,.8f),Is.True);Assert.That(item.parent,Is.SameAs(group));
            Near(item.position,root.transform.TransformPoint(point));Assert.That(frame.Read(item,out var position,out var rotation,out var scale),Is.True);
            Near(position,point);Assert.That(Quaternion.Angle(rotation,orientation),Is.LessThan(.01f));Assert.That(scale,Is.EqualTo(.8f).Within(.00001f));
        }
        [Test] public void ABookOutsideTheContentParentStillUsesTheContentFrame()
        {
            var content=Child(root.transform);content.SetLocalPositionAndRotation(new Vector3(3,0,-2),Quaternion.Euler(0,90,0));var book=Child(root.transform);
            var frame=new RoomFrame(content);Assert.That(frame.Apply(book,new Vector3(0,1,1),Quaternion.identity,1),Is.True);
            Near(book.position,new Vector3(4,1,-2));Assert.That(frame.Read(book,out var point,out var rotation,out _),Is.True);
            Near(point,new Vector3(0,1,1));Assert.That(Quaternion.Angle(rotation,Quaternion.identity),Is.LessThan(.01f));
        }
        [Test] public void DirectChildCaptureAndApplyPreserveExactNoOpValues()
        {
            root.transform.SetPositionAndRotation(new Vector3(8,2,-5),Quaternion.Euler(0,60,0));
            var item=Child(root.transform);item.SetLocalPositionAndRotation(new Vector3(.1234567f,1.987654f,-.004321f),Quaternion.Euler(12,34,56));item.localScale=Vector3.one*.7f;
            var frame=new RoomFrame(root.transform);var before=item.localPosition;var rotation=item.localRotation.normalized;
            Assert.That(frame.Read(item,out var point,out var readRotation,out var scale),Is.True);Assert.That(point,Is.EqualTo(before));Assert.That(readRotation,Is.EqualTo(rotation));
            Assert.That(frame.Apply(item,point,readRotation,scale),Is.True);Assert.That(item.localPosition,Is.EqualTo(before));
        }
        [Test] public void DistortedParentsRefusePlacementWithoutMutatingTheTransform()
        {
            var group=Child(root.transform);group.localScale=new Vector3(2,1,1);var item=Child(group);item.localRotation=Quaternion.Euler(0,30,0);
            var frame=new RoomFrame(root.transform);var before=item.localToWorldMatrix;
            Assert.That(frame.Read(item,out _,out _,out _),Is.False);Assert.That(frame.Apply(item,Vector3.one,Quaternion.identity,1),Is.False);Assert.That(item.localToWorldMatrix,Is.EqualTo(before));
            item.localRotation=Quaternion.identity;item.localScale=new Vector3(.5f,1,1);
            Assert.That(frame.Read(item,out _,out _,out _),Is.False,"A compensating child scale does not make its parent safe for later rotations");
            root.transform.localScale=new Vector3(-1,1,1);Assert.That(new RoomFrame(root.transform).Valid,Is.False);
            root.transform.localScale=Vector3.zero;Assert.That(new RoomFrame(root.transform).Valid,Is.False);
            Assert.That(new RoomFrame(null).Valid,Is.False);
        }
    }
}
