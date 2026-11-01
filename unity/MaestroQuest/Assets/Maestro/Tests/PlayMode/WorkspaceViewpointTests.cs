// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        void PhysicalFloor(){view.Initialize(room.transform,root.transform,room.Viewer.GetComponent<Camera>(),scan,physics);}
        [UnityTest]public IEnumerator SavedViewpointWaitsForTrackingAndOnlyMovesAuthoredContent()
        {
            PhysicalFloor();var location=new RoomViewpoint{active=true,position=new Vector3(-3,2,4),yaw=35};
            var document=new RoomDocument{version=RoomDocument.CurrentVersion,viewpoint=location,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(0,1,1)},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
            Assert.That(new RoomStorage(Path.Combine(directory,"room")).Save(document,out var error),Is.True,error);
            room.Viewer.SetPositionAndRotation(new Vector3(8,1.7f,-6),Quaternion.Euler(-12,123,0));var head=room.Viewer.position;var rotation=room.Viewer.rotation;Open();
            var editor=host.Current.Editor;var placement=editor.GetComponent<WorkspaceViewpoint>();Assert.That(placement.Pending,Is.True);Assert.That(editor.RuntimeGate.Held,Is.True);Assert.That(room.transform.position,Is.EqualTo(Vector3.zero));
            yield return null;Assert.That(placement.Pending,Is.True);recoveryHeadTracked=true;placement.Tick();
            Assert.That(placement.Pending,Is.False);Assert.That(editor.RuntimeGate.Held,Is.False);Assert.That(room.Viewer.position,Is.EqualTo(head));Assert.That(room.Viewer.rotation,Is.EqualTo(rotation));
            Assert.That(Vector3.Distance(editor.Frame.PointToWorld(location.position),new Vector3(head.x,0,head.z)),Is.LessThan(.0001f));
            Assert.That(view.ReadViewpoint(out var restored),Is.True);Assert.That(Vector3.Distance(restored.position,location.position),Is.LessThan(.0001f));Assert.That(Mathf.Abs(Mathf.DeltaAngle(restored.yaw,location.yaw)),Is.LessThan(.001f));
            Assert.That(physics.Running,Is.False);Assert.That(host.Current.Controls.UserEnabled,Is.False);Assert.That(view.Active,Is.False);Assert.That(editor.Read("book").position,Is.EqualTo(new Vector3(0,1,1)));
        }
        [UnityTest]public IEnumerator NavigationBookmarkSurvivesRestartWithFreshPhysicalTracking()
        {
            PhysicalFloor();recoveryHeadTracked=true;room.Viewer.position=new Vector3(0,1.7f,0);Open();yield return ReadyHost();
            var editor=host.Current.Editor;Assert.That(editor.Viewpoint.active,Is.False);int revision=editor.Revision;
            Assert.That(view.Enter(),Is.True);Assert.That(view.Turn(30),Is.True);var placement=editor.GetComponent<WorkspaceViewpoint>();Assert.That(editor.Viewpoint.active,Is.True);
            room.transform.position+=new Vector3(-3,-1,2);placement.Capture();var expected=editor.Viewpoint;Assert.That(editor.Revision,Is.EqualTo(revision));
            Assert.That(editor.TryFlush(out var error),Is.True,error);Assert.That(new RoomStorage(editor.SaveDirectory).Load(out error).viewpoint.position,Is.EqualTo(expected.position));
            string saved=directory;var closing=host;UnityEngine.Object.Destroy(root);yield return null;while(!closing.Retirement.IsCompleted)yield return null;
            BuildShell(saved);PhysicalFloor();room.Viewer.SetPositionAndRotation(new Vector3(-5,1.6f,7),Quaternion.Euler(0,-72,0));recoveryHeadTracked=true;Open();yield return ReadyHost();
            Assert.That(view.ReadViewpoint(out var actual),Is.True);Assert.That(Vector3.Distance(actual.position,expected.position),Is.LessThan(.0001f));Assert.That(Mathf.Abs(Mathf.DeltaAngle(actual.yaw,expected.yaw)),Is.LessThan(.001f));Assert.That(view.Active,Is.False);
        }
        [UnityTest]public IEnumerator TrackingLossAndWorkspaceFreezeKeepLastAcceptedBookmark()
        {
            PhysicalFloor();Open();recoveryHeadTracked=true;Assert.That(view.Enter(),Is.True);Assert.That(view.Turn(30),Is.True);
            var editor=host.Current.Editor;var placement=editor.GetComponent<WorkspaceViewpoint>();string accepted=JsonUtility.ToJson(editor.Viewpoint);
            recoveryHeadTracked=false;room.Viewer.position+=Vector3.right*7;placement.Capture();Assert.That(JsonUtility.ToJson(editor.Viewpoint),Is.EqualTo(accepted));
            recoveryHeadTracked=true;using(editor.WriteGate.TryFreeze(out _)){placement.Capture();Assert.That(JsonUtility.ToJson(editor.Viewpoint),Is.EqualTo(accepted));}
            using(editor.RuntimeGate.Hold("Workspace review")){placement.Capture();Assert.That(JsonUtility.ToJson(editor.Viewpoint),Is.EqualTo(accepted));}
            placement.Capture();Assert.That(JsonUtility.ToJson(editor.Viewpoint),Is.Not.EqualTo(accepted));yield return null;
        }
        [UnityTest]public IEnumerator TemporaryDiscardKeepsCurrentViewpointAndGeometryUndoDoesNotMoveViewer()
        {
            PhysicalFloor();Open();yield return ReadyHost();recoveryHeadTracked=true;var editor=host.Current.Editor;
            while(editor.Find("maestro").GetComponent<Maestro.Quest.Avatar.MaestroAvatar>().ModelBusy)yield return null;
            Assert.That(view.Enter(),Is.True);Assert.That(view.Turn(30),Is.True);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            while(editor.TemporarySavePending){editor.PollTemporarySave();yield return null;}
            Assert.That(editor.TemporarySaveError,Is.Null);
            Assert.That(view.Turn(30),Is.True);editor.GetComponent<WorkspaceViewpoint>().Capture();var location=editor.Viewpoint;
            var worldPosition=room.transform.position;var worldRotation=room.transform.rotation;var original=editor.Read("book").position;
            Assert.That(editor.MoveObject("book",original+Vector3.right,out error),Is.True,error);editor.Undo();
            Assert.That(room.transform.position,Is.EqualTo(worldPosition));Assert.That(room.transform.rotation,Is.EqualTo(worldRotation));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(JsonUtility.ToJson(editor.Viewpoint),Is.EqualTo(JsonUtility.ToJson(location)));Assert.That(editor.Read("book").position,Is.EqualTo(original));
            Assert.That(room.transform.rotation,Is.EqualTo(worldRotation));Assert.That(editor.TryFlush(out error),Is.True,error);
            Assert.That(JsonUtility.ToJson(new RoomStorage(editor.SaveDirectory).Load(out error).viewpoint),Is.EqualTo(JsonUtility.ToJson(location)));
        }
        [UnityTest]public IEnumerator WorkspacePreservationCapturesLatestTrackedViewBeforeFreeze()
        {
            PhysicalFloor();Open();yield return ReadyHost();recoveryHeadTracked=true;var current=host.Current;current.Rules.Modules.Flush();
            while(current.Editor.Find("maestro").GetComponent<Maestro.Quest.Avatar.MaestroAvatar>().ModelBusy)yield return null;
            Assert.That(view.Enter(),Is.True);Assert.That(view.Turn(30),Is.True);var before=current.Editor.Viewpoint;
            room.Viewer.position+=Vector3.right*3;Assert.That(view.ReadViewpoint(out var latest),Is.True);
            Assert.That(WorkspaceEditHold.TryAcquire(current.Editor,current.Rules,current.Controls,out var held,out var error),Is.True,error);
            using(held){Assert.That(current.Editor.Viewpoint.position,Is.Not.EqualTo(before.position));Assert.That(Vector3.Distance(current.Editor.Viewpoint.position,latest.position),Is.LessThan(.0001f));
                room.Viewer.position+=Vector3.right;current.Editor.GetComponent<WorkspaceViewpoint>().Capture();Assert.That(Vector3.Distance(current.Editor.Viewpoint.position,latest.position),Is.LessThan(.0001f));}
        }
        [UnityTest]public IEnumerator UnsupportedViewpointRefusesMovementWithoutChangingWorldOrSave()
        {
            PhysicalFloor();Open();recoveryHeadTracked=true;Assert.That(view.Enter(),Is.True);Assert.That(view.Turn(30),Is.True);
            var editor=host.Current.Editor;string saved=JsonUtility.ToJson(editor.Viewpoint);var position=room.transform.position;var rotation=room.transform.rotation;
            room.Viewer.position=new Vector3(26,1.7f,0);
            Assert.That(view.Move(Vector3.right*.05f),Is.False);Assert.That(view.MovementError,Does.Contain("coordinates"));Assert.That(view.Turn(30),Is.False);
            editor.GetComponent<WorkspaceViewpoint>().Capture();Assert.That(room.transform.position,Is.EqualTo(position));Assert.That(room.transform.rotation,Is.EqualTo(rotation));Assert.That(JsonUtility.ToJson(editor.Viewpoint),Is.EqualTo(saved));yield return null;
        }
        [UnityTest]public IEnumerator WorkspaceReplacementDoesNotInheritPreviousWorldOffset()
        {
            PhysicalFloor();Open();yield return ReadyHost();var previous=host.Current;previous.Rules.Modules.Flush();
            room.transform.SetPositionAndRotation(new Vector3(5,0,-8),Quaternion.Euler(0,80,0));
            Assert.That(WorkspaceEditHold.TryAcquire(previous.Editor,previous.Rules,previous.Controls,out var held,out var error),Is.True,error);
            try{
                var incoming=Prepare(Archive("Fresh world"));var retained=Prepare(Archive("Retained world"));var selected=store.Activate(incoming.Id,incoming.Receipt.ManifestHash,host.Selection.Revision,retained.Id,retained.Receipt.ManifestHash);
                Assert.That(host.ReplaceCommitted(selected,held,out error),Is.True,error);yield return ReadyHost();
                Assert.That(room.transform.position,Is.EqualTo(Vector3.zero));Assert.That(Quaternion.Angle(room.transform.rotation,Quaternion.identity),Is.LessThan(.001f));Assert.That(host.Current.Editor.Viewpoint.active,Is.False);Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True,"Review must still be required");
            }finally{held.Dispose();}
        }
    }
}
