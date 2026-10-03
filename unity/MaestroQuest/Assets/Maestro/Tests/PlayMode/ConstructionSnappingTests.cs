// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        void SnapMode(string mode){var settings=editor.ObserveConstructionSnapping();settings.mode=mode;Assert.That(new RoomExecutions(editor).Execute(new JObject{["operation"]="start",["call"]=new JObject{["id"]="room.selection.snapSettings",["version"]=1,["arguments"]=JObject.Parse(JsonUtility.ToJson(settings))}},out var error),Is.True,error);}
        ConstructionManipulator SnapMover(string mode,out string a,out string b,out string target){
            a=LayoutObject(new Vector3(2,1,0));b=LayoutObject(new Vector3(2.3f,1,0));target=LayoutObject(new Vector3(0,1,0));SnapPoint(b,"Bottom",-.05f);SnapPoint(target,"Top",.05f);SnapMode(mode);return Mover(a,b);
        }
        void AimSnap(ConstructionManipulator tool,string source,string target,float scale=1){
            var sourceIndex=Array.IndexOf(editor.ObserveConstructionSelection().members,source);var a=editor.Read(editor.ObserveConstructionSelection().members[0]);var b=editor.Read(source);var dest=editor.Find(target).transform;
            var position=dest.localPosition+Vector3.up*(.05f+.05f*scale)-(b.position-a.position)*scale+Vector3.right*.01f;
            Assert.That(sourceIndex,Is.GreaterThanOrEqualTo(0));tool.Handle.transform.localRotation=Quaternion.identity;tool.Handle.transform.localScale=Vector3.one*scale;tool.Handle.transform.localPosition=position+Vector3.up*.24f*scale;Assert.That(tool.Preview(out var error),Is.True,error);
        }
        void ReleaseSnap(XRRayInteractor hand,ConstructionManipulator tool){hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)hand,tool.Handle.Grab);}
        [UnityTest] public IEnumerator GripSnapUsesAnySelectedPointAndSharedScaledPlacementWithOneUndo(){
            var tool=SnapMover("place",out var a,out var b,out var target);var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);AimSnap(tool,b,target,1.5f);
            CaptureConstructionHandle(tool,"snap-preview.png");Assert.That(tool.SnapPreview.members[0].target,Is.EqualTo(b));Assert.That(editor.Read(b).position.x,Is.EqualTo(2.3f));Assert.That(editor.Find(b).transform.localPosition,Is.EqualTo(new Vector3(0,1.125f,0)));
            Assert.That(BehaviourCatalog.TryRead("room.selection.snapPreview",new BehaviourCatalog.FactContext(editor:editor),out var preview),Is.True);Assert.That(preview.Display,Does.Contain(target));
            var receipt=runtime.Scheduler.Receipts.NextId;ReleaseSnap(hand,tool);Assert.That(tool.Error,Is.Empty);var outcome=runtime.Scheduler.Invocation(receipt);Assert.That((string)outcome["call"]["id"],Is.EqualTo("object.layout.snap"));Assert.That((float)outcome["call"]["arguments"]["scale"],Is.EqualTo(1.5f));Assert.That((string)outcome["phase"],Is.EqualTo("completed"),outcome.ToString());
            Assert.That(editor.Read(a).position.x,Is.EqualTo(-.45f).Within(.0001));Assert.That(editor.Read(a).scale,Is.EqualTo(1.5f));Assert.That(editor.Read(b).connections,Is.Empty);editor.Undo();Assert.That(editor.Read(a).position.x,Is.EqualTo(2));Assert.That(editor.Read(b).position.x,Is.EqualTo(2.3f));Assert.That(editor.Read(a).scale,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator GripJoinCreatesRealConstraintAndFailureRestoresEveryMember(){
            var tool=SnapMover("join",out var a,out var b,out var target);Assert.That(editor.ConfigurePhysics(b,editor.ObjectRevision(b),new ObjectPhysicsSettings{mode="solid",shape="box",mass=1},out var error),Is.True,error);
            Assert.That(ConstructionManipulationCapability.RunManual(editor,true,out error),Is.True,error);var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);AimSnap(tool,b,target);Assert.That(tool.SnapPreview,Is.Not.Null);
            ReleaseSnap(hand,tool);Assert.That(tool.Error,Is.Empty);Assert.That(editor.Read(b).connections.Single().connected,Is.EqualTo(target));physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();Assert.That(editor.Find(b).GetComponent<FixedJoint>(),Is.Not.Null);physics.PausePhysics();editor.Undo();
            Assert.That(ConstructionManipulationCapability.RunManual(editor,true,out error),Is.True,error);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);AimSnap(tool,b,target);var pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{ReleaseSnap(hand,tool);Assert.That(tool.Error,Does.Contain("not saved"));Assert.That(editor.Read(b).connections,Is.Empty);Assert.That(editor.Find(b).transform.localPosition.x,Is.EqualTo(2.3f));Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(2));}finally{Directory.Delete(pending);}yield return null;
        }
        [UnityTest] public IEnumerator HeldDestinationAtReleaseCancelsSnapInsteadOfSilentlyPlacingLoose(){
            var tool=SnapMover("place",out var a,out var b,out var target);var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);AimSnap(tool,b,target);
            var other=Hand(2,editor.Find(target).transform.position);manager.SelectEnter((IXRSelectInteractor)other,editor.Find(target).Grab);var next=runtime.Scheduler.Receipts.NextId;ReleaseSnap(hand,tool);
            Assert.That(tool.Error,Does.Contain("target changed"));Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(2));Assert.That(editor.Find(b).transform.localPosition.x,Is.EqualTo(2.3f));Assert.That(runtime.Scheduler.Receipts.NextId,Is.EqualTo(next));Assert.That(editor.Find(target).Grab.isSelected,Is.True);manager.SelectExit((IXRSelectInteractor)other,editor.Find(target).Grab);yield return null;
        }
        [UnityTest] public IEnumerator SnapPreviewPullAwayOffAndLifecycleCancellationNeverCreateHiddenJoins(){
            var tool=SnapMover("place",out var a,out var b,out var target);var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);AimSnap(tool,b,target);Assert.That(tool.SnapPreview,Is.Not.Null);
            MoveHandle(tool,Vector3.right*.5f);Assert.That(tool.SnapPreview,Is.Null);Assert.That(editor.Find(b).transform.localPosition.x,Is.EqualTo(.51f).Within(.0001));AimSnap(tool,b,target);var saved=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            using(editor.RuntimeGate.Hold("Cancel snap")){Assert.That(tool.Holding,Is.False);Assert.That(tool.SnapPreview,Is.Null);Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(2));Assert.That(File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(saved));}
            SnapMode("off");Assert.That(ConstructionManipulationCapability.RunManual(editor,true,out var error),Is.True,error);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);AimSnap(tool,b,target);Assert.That(tool.SnapPreview,Is.Null);ReleaseSnap(hand,tool);Assert.That(editor.Read(b).connections,Is.Empty);Assert.That(editor.Read(b).position.x,Is.EqualTo(.01f).Within(.0001));yield return null;
        }
        [UnityTest] public IEnumerator BundledBricksSnapWithoutCollisionOverlapAndChangedDestinationCancelsRelease(){
            var brick=CreationTemplates.All.Single(t=>t.Id=="brick");
            string Make(Vector3 position){Assert.That(editor.CreateRecipe(brick.Name,position,1,brick.Recipe,brick.Collision,brick.Physics,out var id,out var error,snapPoints:brick.SnapPoints),Is.True,error);return id;}
            var moving=Make(new Vector3(.3f,1,0));var target=Make(new Vector3(0,1,0));SnapMode("place");Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{moving},false,out var error),Is.True,error);Assert.That(ConstructionManipulationCapability.RunManual(editor,true,out error),Is.True,error);
            var tool=editor.GetComponent<ConstructionManipulator>();var hand=Hand(1,tool.Handle.transform.position);
            void Near(){tool.Handle.transform.localScale=Vector3.one*1.5f;tool.Handle.transform.localPosition=new Vector3(.005f,1+.042f+.03f*1.5f+.24f*1.5f,0);Assert.That(tool.Preview(out var issue),Is.True,issue);}
            manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);Near();Assert.That(tool.SnapPreview.point,Is.EqualTo("Bottom"));Assert.That(tool.SnapPreview.destination.point,Is.EqualTo("Top"));
            var shape=brick.Collision.shapes.Single();float bottom=editor.Find(moving).transform.localPosition.y+(shape.position.y-shape.size.y*.5f)*1.5f;float top=editor.Find(target).transform.localPosition.y+shape.position.y+shape.size.y*.5f;Assert.That(bottom,Is.EqualTo(top).Within(.00001f));CaptureConstructionHandle(tool,"bundled-bricks-snap.png");
            ReleaseSnap(hand,tool);Assert.That(tool.Error,Is.Empty);Assert.That(editor.Read(moving).scale,Is.EqualTo(1.5f));editor.Undo();Assert.That(editor.Read(moving).scale,Is.EqualTo(1));
            Assert.That(ConstructionManipulationCapability.RunManual(editor,true,out error),Is.True,error);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);Near();SnapPoint(target,"Top",.04f);ReleaseSnap(hand,tool);Assert.That(tool.Error,Does.Contain("target changed"));Assert.That(editor.Find(moving).transform.localPosition,Is.EqualTo(new Vector3(.3f,1,0)));Assert.That(editor.Read(target).snapPoints.Single(p=>p.id=="Top").frame.position.y,Is.EqualTo(.04f));yield return null;
        }
        [UnityTest] public IEnumerator EquidistantPointChoiceIsStableAndRespectsOtherManualOwners(){
            var tool=SnapMover("place",out _,out var b,out var target);var other=LayoutObject(editor.Find(target).transform.localPosition);SnapPoint(other,"Top",.05f);var ordered=new[]{target,other}.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);AimSnap(tool,b,target);Assert.That(tool.SnapPreview.destination.target,Is.EqualTo(ordered[0]));
            Assert.That(editor.Ownership.TryAcquire("another-control","Other manual edit",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(ordered[0],"wholeTarget")},_=>Assert.Fail("Preview must not steal ownership"),out var lease,out var error),Is.True,error);
            Assert.That(tool.Preview(out error),Is.True,error);Assert.That(tool.SnapPreview.destination.target,Is.EqualTo(ordered[1]));Assert.That(lease.Held,Is.True);lease.Dispose();tool.Close();yield return null;
        }
        [UnityTest] public IEnumerator SnappingSettingsAreGuardedTransientAndRefuseChangesDuringGrip(){
            var tool=SnapMover("place",out _,out var b,out var target);var settings=editor.ObserveConstructionSnapping();var saved=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));settings.mode="join";
            Assert.That(editor.ConfigureConstructionSnapping(settings,out var error),Is.True,error);Assert.That(editor.ConfigureConstructionSnapping(settings,out _),Is.False);Assert.That(File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(saved));
            var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);settings=editor.ObserveConstructionSnapping();settings.mode="off";Assert.That(editor.ConfigureConstructionSnapping(settings,out error),Is.False);Assert.That(error,Does.Contain("Release"));tool.Close();yield return null;
        }
    }
}
