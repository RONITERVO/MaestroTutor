// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string NativeAuthoringStart(RoomAgentExecutor executor,JObject call,out RoomAgentRequest request){
            request=ContainerRequest(call);Assert.That(executor.Execute(request,out var error,out _),Is.True,error);return (string)request.commands[0].execution["runId"];
        }
        [UnityTest] public IEnumerator NativeAuthoringCopyLoadsItsSourceAndReceiptReplayCreatesOnlyOneCopy(){
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);int count=editor.Snapshot().objects.Length;
            var call=new CopyObjectCapability().Example;call["kind"]="copy";call["target"]=id;call["revision"]=editor.ObjectRevision(id);call["name"]="Loaded copy";
            var executor=new RoomAgentExecutor(editor);string run=NativeAuthoringStart(executor,new JObject{["id"]="object.create",["version"]=1,["arguments"]=call},out var request);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));yield return NativeActionDone(run);
            string copy=(string)runtime.Scheduler.Invocation(run)["output"]["objectId"];Assert.That(editor.Read(copy).name,Is.EqualTo("Loaded copy"));Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));editor.Undo();Assert.That(editor.Read(copy),Is.Null);Assert.That(editor.Read(id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator NativeAuthoringTransferLoadsBothAreasAndKeepsAtomicUndo(){
            var(executor,from,to)=Containers();string first=NativeAreaFor(from),second=NativeAreaFor(to);
            Assert.That(editor.RetireNativeArea(first,out var error),Is.True,error);Assert.That(editor.RetireNativeArea(second,out error),Is.True,error);
            string run=NativeAuthoringStart(executor,TransferCall(from,to,100),out _);Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));yield return NativeActionDone(run);
            Assert.That(editor.Find(from),Is.Not.Null);Assert.That(editor.Find(to),Is.Not.Null);Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(100));Assert.That(editor.Read(to).containers.Single().amountMl,Is.EqualTo(100));
            editor.Undo();Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));Assert.That(editor.Read(to).containers.Single().amountMl,Is.Zero);
        }
        [UnityTest] public IEnumerator NativeAuthoringCaptureAndResetUseTheRestoredNativeArrangement(){
            string id=editor.Identity(block),area=NativeAreaFor(id);var placement=editor.Read(id).position+Vector3.up*.2f;block.transform.localPosition=placement;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);var executor=new RoomAgentExecutor(editor);
            string run=NativeAuthoringStart(executor,StructureSaveCall(id),out _);yield return NativeActionDone(run);string structure=(string)runtime.Scheduler.Invocation(run)["output"]["structureId"];
            Assert.That(editor.ReadStructure(structure).slots[0].placement.position,Is.EqualTo(placement));Assert.That(editor.MoveObject(id,placement+Vector3.right,out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);run=NativeAuthoringStart(executor,StructureResetCall(structure,editor.StructureRevision(structure),id),out _);yield return NativeActionDone(run);
            Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(placement));
        }
        [UnityTest] public IEnumerator NativeAuthoringSelectionLoadsItsMembersWithoutCreatingSavedEdits(){
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);int revision=editor.Revision;
            var call=new JObject{["id"]="room.selection.set",["version"]=1,["arguments"]=new JObject{["stateId"]=editor.ObserveConstructionSelection().stateId,["members"]=new JArray(id),["collecting"]=false}};
            string run=NativeAuthoringStart(new RoomAgentExecutor(editor),call,out _);yield return NativeActionDone(run);Assert.That(editor.ObserveConstructionSelection().members,Is.EqualTo(new[]{id}));Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest] public IEnumerator NativeAuthoringInactiveComponentCanBeEditedWithoutWeakeningLiveMotionChecks(){
            string id=editor.Identity(block);block.gameObject.SetActive(false);
            Assert.That(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out _,out _),Is.False);
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(block.gameObject.activeSelf,Is.False);
            var executor=new RoomAgentExecutor(editor);string run=NativeAuthoringStart(executor,ContainerCall(id,new RoomContainer{amountMl=75}),out _);yield return NativeActionDone(run);
            Assert.That(editor.Read(id).containers.Single().amountMl,Is.EqualTo(75));
        }
        [UnityTest] public IEnumerator NativeAuthoringForgettingSavedStructureDoesNotLoadItsDormantMembers(){
            string id=editor.Identity(block);var executor=new RoomAgentExecutor(editor);string structure=SaveGroup(executor,id),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var call=new JObject{["id"]="structure.forget",["version"]=1,["arguments"]=new JObject{["id"]=structure,["revision"]=editor.StructureRevision(structure)}};
            string run=NativeAuthoringStart(executor,call,out _);yield return NativeActionDone(run);Assert.That(editor.ReadStructure(structure),Is.Null);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);
        }
    }
    public sealed partial class AvatarSpatialTests {
        ScannedRoom NativePlacementRoom(out SurfaceSource source,out string target){
            var scan=PlacementRoom(out source,out target);var capability=new RegionSaveCapability();Assert.That(capability.Start(new CapabilityContext(editor,authoring),"area",capability.Example,out var operation,out var error),Is.True,error);string area=(string)operation.Result["id"];
            Assert.That(editor.BindRegion(target,editor.ObjectRevision(target),area,editor.RegionRevision(area),out error),Is.True,error);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);return scan;
        }
        [UnityTest] public IEnumerator NativeAuthoringSurfacePlacementLoadsItsTargetBeforeQueryingItsBounds(){
            var scan=NativePlacementRoom(out var source,out var target);string setup=scan.SetupIdentity;var physics=world.ObserveSimulation()["stateId"].DeepClone();
            Assert.That(modeActions.Execute(PlacementRequest(target),out var error),Is.True,error);Assert.That(source.Rays,Is.Zero);yield return PlacementFinished();
            Assert.That(source.Rays,Is.EqualTo(1));Assert.That(editor.Find(target),Is.Not.Null);Assert.That(editor.Read(target).position.y,Is.LessThan(1.2f));
            Assert.That(scan.SetupIdentity,Is.EqualTo(setup),"Restoring authored geometry cannot invalidate its own physical-surface request");Assert.That(JToken.DeepEquals(physics,world.ObserveSimulation()["stateId"]),Is.False,"Physics admission still changes when authored geometry becomes available");
        }
        [UnityTest] public IEnumerator NativeAuthoringSurfacePlacementStillRejectsRoomChangesWhileItsTargetLoads(){
            var scan=NativePlacementRoom(out var source,out var target);var before=editor.Read(target).position;string setup=scan.SetupIdentity;
            Assert.That(modeActions.Execute(PlacementRequest(target),out var error),Is.True,error);world.SetSurfaces(false,"Room geometry changed during loading");Assert.That(scan.SetupIdentity,Is.Not.EqualTo(setup));yield return PlacementFinished("failed");
            Assert.That(source.Rays,Is.Zero);Assert.That(editor.Read(target).position,Is.EqualTo(before));
        }
    }
}
