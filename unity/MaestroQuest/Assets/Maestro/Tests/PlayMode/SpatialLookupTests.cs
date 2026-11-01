// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator SpatialLookupFreshCaptureUsesMovedAndRetiredRuntimePositionsThenAcceptedEdits()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);var original=editor.ReadCollisionBounds(id).Bounds;
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.True);var first=editor.CaptureMovementCandidates(false);
            block.transform.localPosition+=Vector3.right*5;var moved=editor.ReadCollisionBounds(id).Bounds;var second=editor.CaptureMovementCandidates(false);
            Assert.That(first.Targets(new Bounds(original.center,Vector3.one*.01f),null),Does.Contain(id));
            Assert.That(second.Targets(new Bounds(original.center,Vector3.one*.01f),null),Does.Not.Contain(id));
            Assert.That(second.Targets(new Bounds(moved.center,Vector3.one*.01f),null),Does.Contain(id));
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.CaptureMovementCandidates(false).Targets(new Bounds(moved.center,Vector3.one*.01f),null),Does.Contain(id));
            var changed=editor.Read(id);changed.position+=Vector3.left*5;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            var last=editor.CaptureMovementCandidates(false);var accepted=editor.ReadCollisionBounds(id).Bounds;
            Assert.That(last.Targets(new Bounds(moved.center,Vector3.one*.01f),null),Does.Not.Contain(id));
            Assert.That(last.Targets(new Bounds(accepted.center,Vector3.one*.01f),null),Does.Contain(id));
            CollectionAssert.AreEqual(last.Targets(new Bounds(accepted.center,Vector3.one*.01f),null),editor.SpatialMovementTargets(new Bounds(accepted.center,Vector3.one*.01f),null));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            editor.Undo();Assert.That(editor.CaptureMovementCandidates(false).Targets(new Bounds(original.center,Vector3.one*.01f),null),Does.Contain(id));
        }
        [UnityTest] public IEnumerator SpatialLookupFlowPrunesDistantNativeObstaclesWhileBothRealStreamsTransfer()
        {
            var(a,b,c,d)=IndependentFlowCups();
            for(int i=0;i<24;i++){
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Distant query obstacle",new(10+i*.2f,1,10),.1f,Color.white,out var id,out var error),Is.True,error);
                Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            }
            physics.StartPhysics();editor.Liquids.Tick(.05f);var work=editor.Liquids.LastFlowDiscovery;CaptureFlowDiscovery("spatial-tree-locality");
            Assert.That(work.Captures,Is.EqualTo(1));Assert.That(work.Probes,Is.GreaterThan(1));
            Assert.That(work.CollisionReads,Is.LessThanOrEqualTo(editor.SpatialObjectCount));
            Assert.That(work.CollisionIndexBuild,Is.GreaterThan(0).And.LessThanOrEqualTo(RoomSpatialLookup.MaximumBuildWork(editor.SpatialObjectCount)));
            Assert.That(work.CollisionCandidates,Is.LessThan(editor.SpatialObjectCount*work.Probes/2),"Distant native objects must be rejected by the spatial tree before narrow candidate inspection");
            Assert.That(work.CollisionNodes,Is.GreaterThan(0).And.LessThanOrEqualTo(2*editor.SpatialObjectCount*work.Probes));
            Assert.That(FlowAmount(b),Is.GreaterThan(0));Assert.That(FlowAmount(d),Is.GreaterThan(0));
            Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(a,"spilledMl"),Is.EqualTo(400).Within(1e-8));
            Assert.That(FlowAmount(c)+FlowAmount(d)+FlowAmount(c,"spilledMl"),Is.EqualTo(400).Within(1e-8));
            physics.PausePhysics();yield return null;
        }
    }
}
