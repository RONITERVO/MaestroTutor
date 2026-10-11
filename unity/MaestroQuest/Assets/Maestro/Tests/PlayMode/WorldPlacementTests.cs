// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
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
namespace Maestro.Quest.Tests
{
    public sealed class PhysicalPlacementTestBinding:MonoBehaviour,IPhysicalRoomBinding { public bool PhysicalFrame=>true; }
    public sealed partial class WorkspaceHostTests
    {
        JObject ViewpointFact(){
            Assert.That(BehaviourCatalog.TryRead("world.viewpoint",1,null,new BehaviourCatalog.FactContext(editor:host.Current.Editor),out var value),Is.True);
            return JObject.FromObject(value.Value);
        }
        RoomViewpoint TravelPoint()=>new(){active=true,position=new Vector3(3,0,4),yaw=70};
        JObject TravelRequest(RoomExecutions actions,RoomViewpoint target)=>new(){["operation"]="start",["runId"]=actions.Observe()["nextRunId"].DeepClone(),
            ["call"]=new JObject {["id"]="world.viewpoint.set",["version"]=1,["arguments"]=new JObject {
                ["stateId"]=ViewpointFact()["stateId"].DeepClone(),["position"]=WorkspaceViewpoint.Point(target.position),["yaw"]=target.yaw}}};
        void SetupTravel(){PhysicalFloor();room.Viewer.SetPositionAndRotation(new Vector3(-2,1.7f,1),Quaternion.Euler(0,35,0));recoveryHeadTracked=true;Open();}
        void TravelGround(){
            var floor=new GameObject("Accepted test ground");floor.layer=RoomPhysicsLayers.Environment;floor.transform.SetParent(room.transform,false);floor.transform.localPosition=new Vector3(0,-.05f,0);
            var collider=floor.AddComponent<BoxCollider>();collider.size=new Vector3(20,.1f,20);floor.AddComponent<RoomWalkableSurface>().Publish(collider);Physics.SyncTransforms();
        }
        [UnityTest]public IEnumerator SharedWorldPlacementKeepsActivityAndReplaysOnlyReceipt()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;var actions=new RoomExecutions(editor,host);var target=TravelPoint();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Moving prop",new Vector3(-3,1,4),1,Color.red,out var id,out var error),Is.True,error);
            var body=editor.Find(id).GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=false;body.linearVelocity=new Vector3(.3f,.2f,-.1f);
            var oldFrame=editor.Frame;var authoredVelocity=oldFrame.VectorToRoom(body.linearVelocity);var authoredPose=oldFrame.PointToRoom(body.position);
            var definitions=editor.Snapshot().objects.Select(JsonUtility.ToJson).ToArray();int revision=editor.Revision;var physical=room.Viewer.position;var heading=room.Viewer.rotation;
            var runtime=editor.GetComponent<Maestro.Quest.Rules.RoomRules>();Assert.That(runtime.Scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}},Time.unscaledTime,out var waiting,out error),Is.True,error);
            Assert.That(editor.Ownership.TryAcquire("placement-npc","NPC activity",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},null,out var actor,out error),Is.True,error);
            using(actor){
                var before=ViewpointFact();var request=TravelRequest(actions,target);Assert.That(actions.Execute(request,out error),Is.True,error);
                Assert.That((string)actions.Observe()["selected"]["phase"],Is.EqualTo("completed"));
                Assert.That(view.ReadViewpoint(out var actual),Is.True);Assert.That(Vector3.Distance(actual.position,target.position),Is.LessThan(.0001f));Assert.That(Mathf.Abs(Mathf.DeltaAngle(actual.yaw,target.yaw)),Is.LessThan(.001f));
                Assert.That(room.Viewer.position,Is.EqualTo(physical));Assert.That(room.Viewer.rotation,Is.EqualTo(heading));Assert.That(actor.Held,Is.True);
                Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));Assert.That(editor.Revision,Is.EqualTo(revision));
                CollectionAssert.AreEqual(definitions,editor.Snapshot().objects.Select(JsonUtility.ToJson).ToArray());
                Assert.That(Vector3.Distance(editor.Frame.PointToRoom(body.position),authoredPose),Is.LessThan(.0001f));Assert.That(Vector3.Distance(editor.Frame.VectorToRoom(body.linearVelocity),authoredVelocity),Is.LessThan(.0001f));
                var saved=new RoomStorage(editor.SaveDirectory).Load(out error);Assert.That(JsonUtility.ToJson(saved.viewpoint),Is.EqualTo(JsonUtility.ToJson(target)),error);
                var placed=room.transform.position;Assert.That(actions.Execute(request,out error),Is.True,error);Assert.That(room.transform.position,Is.EqualTo(placed));
                Assert.That(editor.SetWorldViewpoint((string)before["stateId"],new RoomViewpoint{active=true},false,out _,out error),Is.False);Assert.That(error,Does.Contain("changed"));
                string evidence=System.Environment.GetEnvironmentVariable("MAESTRO_WORLD_PLACEMENT");
                if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"placement.json"),new JObject {["before"]=before,["request"]=request,["receipt"]=actions.Observe(),["after"]=ViewpointFact()}.ToString());}
            }
        }
        [UnityTest]public IEnumerator WorldPlacementFailureLeavesFrameAndDurableBookmarkUnchanged()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;Assert.That(editor.MoveObject("book",new Vector3(0,1,2),out var error),Is.True,error);Assert.That(editor.TryFlush(out error),Is.True,error);
            var original=JsonUtility.ToJson(editor.Snapshot());var position=room.transform.position;var rotation=room.transform.rotation;var state=(string)ViewpointFact()["stateId"];
            using(var locked=new FileStream(Path.Combine(editor.SaveDirectory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.None)){
                Assert.That(editor.SetWorldViewpoint(state,TravelPoint(),false,out _,out error),Is.False);Assert.That(error,Is.Not.Null.And.Not.Empty);
                Assert.That(room.transform.position,Is.EqualTo(position));Assert.That(room.transform.rotation,Is.EqualTo(rotation));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(original));
                Assert.That((string)ViewpointFact()["stateId"],Is.EqualTo(state));
            }
        }
        [UnityTest]public IEnumerator WorldPlacementRefusesTrackingWorkspaceBoundsAndOccupiedDestination()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;var target=TravelPoint();var point=room.transform.position;
            recoveryHeadTracked=false;Assert.That((bool)ViewpointFact()["located"],Is.False);
            Assert.That(editor.SetWorldViewpoint(null,target,true,out _,out var error),Is.False);recoveryHeadTracked=true;
            using(editor.RuntimeGate.Hold("Review"))Assert.That(editor.SetWorldViewpoint(null,target,true,out _,out error),Is.False);
            using(editor.WriteGate.TryFreeze(out _))Assert.That(editor.SetWorldViewpoint(null,target,true,out _,out error),Is.False);
            var bad=target.Copy();bad.position=new Vector3(25,0,25);Assert.That(editor.SetWorldViewpoint(null,bad,true,out _,out error),Is.False);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Occupied destination",target.position+Vector3.up*.8f,1,Color.blue,out var id,out error),Is.True,error);
            Assert.That(editor.SetWorldViewpoint(null,target,true,out _,out error),Is.False);Assert.That(error,Does.Contain("occupied"));Assert.That(room.transform.position,Is.EqualTo(point));
            Assert.That(editor.DeleteObject(id,out error),Is.True,error);yield return null;
            Assert.That(editor.SetWorldViewpoint(null,target,true,out _,out error),Is.True,error);
        }
        [UnityTest]public IEnumerator WorldPlacementRequiresAcceptedVirtualGroundAndKeepsModes()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;var target=TravelPoint();
            Assert.That(view.Enter(),Is.True);Assert.That(editor.SetWorldViewpoint(null,target,true,out _,out var error),Is.False);Assert.That(error,Does.Contain("ground"));
            TravelGround();Assert.That(editor.SetWorldViewpoint(null,target,true,out _,out error),Is.True,error);Assert.That(view.Active,Is.True);
            var controls=host.Current.Controls;controls.ToggleUser();Assert.That(controls.UserEnabled,Is.True,controls.Status);
            controls.ReturnToWorldOrigin();Assert.That(view.ReadViewpoint(out var origin),Is.True);Assert.That(origin.position.magnitude,Is.LessThan(.0001f));Assert.That(Mathf.Abs(origin.yaw),Is.LessThan(.001f));
            Assert.That(controls.UserEnabled,Is.True);Assert.That(view.Active,Is.True);
            // Tray is owned by the workspace alongside the controls component.
            var actions=editor.GetComponentsInChildren<Maestro.Quest.Rules.RuleToolAction>(true);
            Assert.That(actions.Any(x=>x.AccessibleName=="World origin"),Is.True);
        }
        [UnityTest]public IEnumerator WorldPlacementChecksRealContainmentAndCrossFrameJoint()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;
            physics.SetSurfaces(true,"Accepted room");physics.StartPhysics();Assert.That(physics.Running,Is.True);
            physics.Contains=point=>false;
            Assert.That(editor.SetWorldViewpoint(null,TravelPoint(),true,out _,out var error),Is.False);Assert.That(error,Does.Contain("outside"));Assert.That(physics.Running,Is.True);
            physics.Contains=null;Assert.That(editor.SetWorldViewpoint(null,TravelPoint(),true,out _,out error),Is.True,error);
            physics.PausePhysics();
            var joint=book.gameObject.AddComponent<FixedJoint>();joint.connectedBody=null;
            Assert.That(editor.SetWorldViewpoint(null,TravelPoint(),true,out _,out error),Is.False);Assert.That(error,Does.Contain("connection"));
            UnityEngine.Object.Destroy(joint);yield return null;
            Assert.That(editor.SetWorldViewpoint(null,TravelPoint(),true,out _,out error),Is.True,error);
        }
        [UnityTest]public IEnumerator WorldPlacementExcludesPhysicalBindingsFromVirtualDestinationQuery()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;var target=TravelPoint();
            var attached=new GameObject("Physical binding");attached.layer=RoomPhysicsLayers.Item;attached.transform.SetParent(room.transform,false);
            attached.transform.position=target.position+Vector3.up;attached.AddComponent<BoxCollider>();
            var body=attached.AddComponent<Rigidbody>();body.isKinematic=true;attached.AddComponent<PhysicalPlacementTestBinding>();Physics.SyncTransforms();
            var fixedPosition=body.position;
            Assert.That(editor.SetWorldViewpoint((string)ViewpointFact()["stateId"],target,false,out _,out var error),Is.True,error);
            Assert.That(Vector3.Distance(body.position,fixedPosition),Is.LessThan(.0001f),"Controller/scan bindings stay physical");
            Assert.That(view.ReadViewpoint(out var actual),Is.True);Assert.That(Vector3.Distance(actual.position,target.position),Is.LessThan(.0001f));
        }
        [UnityTest]public IEnumerator TemporaryWorldPlacementKeepsNavigationOnDiscardWithoutSavingFork()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;
            while(editor.Find("maestro").GetComponent<Maestro.Quest.Avatar.MaestroAvatar>().ModelBusy)yield return null;
            Assert.That(editor.MoveObject("book",new Vector3(0,1,2),out var error),Is.True,error);Assert.That(editor.TryFlush(out error),Is.True,error);var before=JsonUtility.ToJson(new RoomStorage(editor.SaveDirectory).Load(out error).viewpoint);var old=ViewpointFact();
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);
            while(editor.TemporarySavePending){editor.PollTemporarySave();yield return null;}
            Assert.That(editor.SetWorldViewpoint((string)old["stateId"],TravelPoint(),false,out _,out error),Is.False);
            Assert.That(editor.SetWorldViewpoint((string)ViewpointFact()["stateId"],TravelPoint(),false,out var result,out error),Is.True,error);Assert.That((bool)result["temporary"],Is.True);
            Assert.That(JsonUtility.ToJson(new RoomStorage(editor.SaveDirectory).Load(out error).viewpoint),Is.EqualTo(before));
            var placed=room.transform.position;Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(room.transform.position,Is.EqualTo(placed));
            Assert.That(editor.TryFlush(out error),Is.True,error);Assert.That(JsonUtility.ToJson(new RoomStorage(editor.SaveDirectory).Load(out error).viewpoint),Is.EqualTo(JsonUtility.ToJson(TravelPoint())));
        }
    }
}
