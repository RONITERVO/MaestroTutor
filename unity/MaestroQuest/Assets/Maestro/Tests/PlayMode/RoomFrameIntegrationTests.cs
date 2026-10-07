// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        Transform FrameGroup(string id)
        {
            root.transform.SetPositionAndRotation(new Vector3(7,0,-4),Quaternion.Euler(0,63,0));
            var group=new GameObject("Nested world region").transform;group.SetParent(root.transform,false);
            group.SetLocalPositionAndRotation(new Vector3(-3,.2f,2),Quaternion.Euler(0,-40,0));group.localScale=Vector3.one*1.5f;
            editor.Find(id).transform.SetParent(group,true);return group;
        }
        static void FrameNear(Vector3 actual,Vector3 expected)=>Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.0001f));
        [UnityTest] public IEnumerator RoomFrameSharedLayoutObservationUndoAndDiskAgreeForNestedObjects()
        {
            string id=LayoutObject(new Vector3(.4f,1.2f,.7f));var group=FrameGroup(id);var item=editor.Find(id);
            var before=new Vector3(.7f,1.8f,.2f);item.transform.position=editor.transform.TransformPoint(before);
            Assert.That(BehaviourCatalog.TryRead("object.placement",1,new JObject {["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);
            var observed=JsonUtility.FromJson<ObjectPlacement>(((JObject)fact.Value).ToString());FrameNear(observed.position,before);
            FrameNear(editor.ObserveObjects().Single(x=>x.id==id).position,before);
            var layout=Layout(id);layout.placements[0].scale=.8f;layout.placements[0].rotation=Quaternion.Euler(0,20,0);
            var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(LayoutRequest(layout),out var error,out _),Is.True,error);
            FrameNear(item.transform.position,editor.transform.TransformPoint(layout.placements[0].position));Assert.That(item.transform.parent,Is.SameAs(group));
            var saved=new RoomStorage(directory).Load(out error);Assert.That(error,Is.Null);FrameNear(saved.objects.Single(x=>x.id==id).position,layout.placements[0].position);
            editor.Undo();FrameNear(item.transform.position,editor.transform.TransformPoint(before));
            editor.Redo();FrameNear(item.transform.position,editor.transform.TransformPoint(layout.placements[0].position));
            yield return null;
        }
        [UnityTest] public IEnumerator RoomFramePhysicalPlacementAndCopiesUseAuthoredAxesAfterRootMotion()
        {
            string id=LayoutObject(new Vector3(.4f,1.2f,.7f));FrameGroup(id);var item=editor.Find(id);var desired=new Vector3(-.7f,1.5f,.8f);
            editor.Select(item);Assert.That(editor.PlaceSelected(editor.transform.TransformPoint(desired)),Is.True);FrameNear(editor.Read(id).position,desired);
            var later=new Vector3(.1f,1.4f,1.2f);item.transform.position=editor.transform.TransformPoint(later);editor.RememberPlacement(id);FrameNear(editor.Read(id).position,later);
            var snapshot=JsonUtility.ToJson(editor.Snapshot());root.transform.position+=new Vector3(2,0,1);root.transform.rotation=Quaternion.Euler(0,100,0);
            FrameNear(editor.ObserveObjects().Single(x=>x.id==id).position,later);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(snapshot),"Moving the presentation frame cannot rewrite saved world coordinates");
            Assert.That(editor.CopyObject(id,editor.ObjectRevision(id),"Frame copy",later+Vector3.right*.3f,out var copy,out var error),Is.True,error);
            FrameNear(editor.Find(copy).transform.position,editor.transform.TransformPoint(later+Vector3.right*.3f));yield return null;
        }
        [UnityTest] public IEnumerator RoomFrameFailedSaveAndUnrepresentableParentPreserveAcceptedLayout()
        {
            string id=LayoutObject(new Vector3(.4f,1.2f,.7f));var group=FrameGroup(id);var item=editor.Find(id);var before=item.transform.localToWorldMatrix;string saved=JsonUtility.ToJson(editor.Snapshot());
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try { Assert.That(editor.ApplyLayout(Layout(id),out _),Is.False);Assert.That(item.transform.localToWorldMatrix,Is.EqualTo(before));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved)); }
            finally { Directory.Delete(pending); }
            group.localScale=new Vector3(2,1,1);before=item.transform.localToWorldMatrix;
            Assert.That(editor.MoveObject(id,Vector3.zero,out var error),Is.False);StringAssert.Contains("frame",error);
            Assert.That(BehaviourCatalog.TryRead("object.placement",1,new JObject {["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            Assert.That(item.transform.localToWorldMatrix,Is.EqualTo(before));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            // Restore the supported hierarchy before lifecycle persistence runs.
            group.localScale=Vector3.one*1.5f;yield return null;
        }
        [UnityTest] public IEnumerator RoomFrameRecordedKeysAndNativeThrowUseTheSameAuthoredPath()
        {
            string id=LayoutObject(new Vector3(.4f,1.2f,.7f));FrameGroup(id);var item=editor.Find(id);
            Assert.That(editor.SetItemPhysics(id,new ObjectPhysicsSettings {mode="solid",shape="box",mass=1}),Is.True);
            editor.Select(item);var start=new Vector3(.5f,1.4f,.3f);var end=start+Vector3.right;
            item.transform.position=editor.transform.TransformPoint(start);animations.AddFrame();
            item.transform.position=editor.transform.TransformPoint(end);animations.AddFrame();
            var motion=editor.Read(id).motion;Assert.That(motion.frames.Length,Is.EqualTo(2));FrameNear(motion.frames[0].position,start);FrameNear(motion.frames[1].position,end);
            animations.StepFrame(-1);FrameNear(item.transform.position,editor.transform.TransformPoint(start));animations.Stop();
            physics.SetSurfaces(true,"Synthetic aligned room");physics.StartPhysics();
            var actions=new Maestro.Quest.Rules.RoomRuleActions(editor,animations);
            var step=new Maestro.Quest.Rules.RuleStep {action=Maestro.Quest.Rules.RuleActionKind.ThrowRecording,targetId=id};
            Assert.That(actions.Start("frame throw",step,out _,out var error),Is.True,error);
            FrameNear(item.transform.position,editor.transform.TransformPoint(start));
            Assert.That(actions.Complete("frame throw",out error),Is.True,error);
            FrameNear(item.transform.position,editor.transform.TransformPoint(end));
            FrameNear(item.GetComponent<Rigidbody>().linearVelocity,editor.Frame.VectorToWorld(Vector3.right));
            Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.False);physics.PausePhysics();yield return null;
        }
    }
}
