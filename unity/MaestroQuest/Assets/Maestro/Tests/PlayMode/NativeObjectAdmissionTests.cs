// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    static class NativeObjectAdmissionFixture
    {
        internal static List<NativeObjectResources.Lease> Fill(RoomWorldIdentity world,int spare=0){
            var leases=new List<NativeObjectResources.Lease>();int count=NativeObjectResources.MaximumInstances-(int)NativeObjectResources.ObserveBudget()["instances"]-spare;
            try{for(int i=0;i<count;i++){var data=Data();leases.Add(NativeObjectResources.Begin(data,new RoomResourceOwner(world,data.id,"object")));}return leases;}
            catch{foreach(var lease in leases)lease.Dispose();throw;}
        }
        internal static RoomObjectData Data()=>new(){id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block,name="Reserved object",position=new Vector3(1,1,2)};
    }
    public sealed partial class RoomRulesTests
    {
        JObject[] ObjectReservations(string target){int count=(int)CollisionFact("runtime.objectBudget")["instances"];return Enumerable.Range(0,count).Select(i=>CollisionFact("runtime.objectReservation",new JObject{["index"]=i})).Where(x=>(string)x["target"]==target).ToArray();}
        [UnityTest] public IEnumerator NativeObjectAdmissionFullBudgetKeepsSaveHistoryAndAllowsExistingEditAndRetry()
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);var data=NativeObjectAdmissionFixture.Data();string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision,roots=root.GetComponentsInChildren<CreatedRoomObject>(true).Length;bool undo=editor.CanUndo,redo=editor.CanRedo;
            var pending=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
            try{
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.False);StringAssert.Contains("live object budget",error);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                Assert.That(root.GetComponentsInChildren<CreatedRoomObject>(true).Length,Is.EqualTo(roots));Assert.That(editor.Find(data.id),Is.Null);
                var current=editor.Read(editor.Identity(block));current.color=Color.blue;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{current},Array.Empty<string>(),out error),Is.True,error);Assert.That(editor.Find(current.id),Is.SameAs(block));
            }finally{foreach(var lease in pending)lease.Dispose();}
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);Assert.That(editor.Find(data.id),Is.Not.Null);
            editor.Undo();Assert.That(editor.Read(data.id),Is.Null);yield return null;editor.Redo();Assert.That(editor.Read(data.id),Is.Not.Null);Assert.That(editor.Find(data.id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator NativeObjectAdmissionLaterBatchRefusalReleasesEarlierReservationWithoutPartialEdit()
        {
            var first=NativeObjectAdmissionFixture.Data();first.id=new string('0',32);var second=NativeObjectAdmissionFixture.Data();second.id=new string('f',32);string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            var pending=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity,1);var before=NativeObjectResources.ObserveBudget().ToString();
            try{Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{first,second},Array.Empty<string>(),out var error),Is.False);StringAssert.Contains("live object budget",error);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.Find(first.id),Is.Null);Assert.That(editor.Find(second.id),Is.Null);Assert.That(NativeObjectResources.ObserveBudget().ToString(),Is.EqualTo(before));}
            finally{foreach(var lease in pending)lease.Dispose();}yield return null;
        }
        [UnityTest] public IEnumerator NativeObjectAdmissionFailedSaveReleasesUnboundCandidate()
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            var data=NativeObjectAdmissionFixture.Data();string before=NativeObjectResources.ObserveBudget().ToString(),saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;bool undo=editor.CanUndo,redo=editor.CanRedo;string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.That(editor.CreatePrimitive(data.kind,data.name,data.position,data.scale,data.color,out var id,out error),Is.False);Assert.That(id,Is.Null);Assert.That(error,Is.Not.Null.And.Not.Empty);}finally{Directory.Delete(pending);}
            Assert.That(NativeObjectResources.ObserveBudget().ToString(),Is.EqualTo(before));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));yield return null;
        }
        [UnityTest] public IEnumerator NativeObjectAdmissionUndoRefusalKeepsRestorableHistory()
        {
            var data=NativeObjectAdmissionFixture.Data();Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{data.id},out error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;bool undo=editor.CanUndo,redo=editor.CanRedo;var pending=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
            try{editor.Undo();Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That(editor.Find(data.id),Is.Null);}
            finally{foreach(var lease in pending)lease.Dispose();}
            editor.Undo();Assert.That(editor.Read(data.id),Is.Not.Null);Assert.That(editor.Find(data.id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator NativeObjectAdmissionTransfersExactCandidateAndRetainsDisabledAndRetiringRoot()
        {
            root.AddComponent<RuntimeDiagnostics>();var data=NativeObjectAdmissionFixture.Data();var journal=new RoomJournal(editor.Snapshot());Assert.That(journal.Prepare(new[]{data},Array.Empty<string>(),out var edit,out var error),Is.True,error);
            RoomItem item=null;List<NativeObjectResources.Lease> pending=null;
            using(edit)using(var prepared=RoomEditPreparation.TryCreate(editor,edit,out error)){
                Assert.That(prepared,Is.Not.Null,error);var preparing=ObjectReservations(data.id).Single();Assert.That((string)preparing["phase"],Is.EqualTo("preparing"));Assert.That((bool)preparing["active"],Is.False);
                var wrong=data.Copy();wrong.kind=RoomObjectKind.Ball;Assert.Throws<InvalidOperationException>(()=>prepared.TakeObject(wrong));
                try{
                    item=CreatedRoomObject.CreatePrepared(data,root.transform,editor.Models,editor.RuntimeGate,editor.WorldIdentity,prepared);var ready=ObjectReservations(data.id).Single();prepared.Dispose();Assert.That(ObjectReservations(data.id).Single().ToString(),Is.EqualTo(ready.ToString()));Assert.That(editor.Find(data.id),Is.Null,"A private built root has not been published to saved identity");
                    item.gameObject.SetActive(false);var disabled=ObjectReservations(data.id).Single();Assert.That((bool)disabled["active"],Is.False);Assert.That((string)disabled["phase"],Is.EqualTo("ready"));pending=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);var full=CollisionFact("runtime.objectBudget");
                    NativeResourceLifetime.Release(item.gameObject);UnityEngine.Object.Destroy(item.gameObject);var retiring=ObjectReservations(data.id).Single();Assert.That((string)retiring["phase"],Is.EqualTo("retiring"));
                    var another=NativeObjectAdmissionFixture.Data();Assert.Throws<RoomResourceLimitException>(()=>NativeObjectResources.Begin(another,new RoomResourceOwner(editor.WorldIdentity,another.id,"object")));
                    string evidence=Environment.GetEnvironmentVariable("MAESTRO_NATIVE_OBJECT_EVIDENCE");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,new JObject{["budget"]=full,["preparing"]=preparing,["ready"]=ready,["disabled"]=disabled,["retiring"]=retiring}.ToString());
                    yield return null;Assert.That(ObjectReservations(data.id),Is.Empty);using var retry=NativeObjectResources.Begin(another,new RoomResourceOwner(editor.WorldIdentity,another.id,"object"));
                }finally{if(item){NativeResourceLifetime.Release(item.gameObject);UnityEngine.Object.Destroy(item.gameObject);}if(pending!=null)foreach(var lease in pending)lease.Dispose();}
            }
        }
        [UnityTest] public IEnumerator NativeObjectAdmissionFailedUnpublishedBuildRetainsOnlyUntilDestroyed()
        {
            root.AddComponent<RuntimeDiagnostics>();var parent=new GameObject("Inactive candidate parent");parent.transform.SetParent(root.transform,false);parent.SetActive(false);var data=NativeObjectAdmissionFixture.Data();data.kind=RoomObjectKind.Drawing;data.points=null;
            Assert.Catch<Exception>(()=>CreatedRoomObject.CreatePrepared(data,parent.transform,editor.Models,editor.RuntimeGate,editor.WorldIdentity,null));
            Assert.That((string)ObjectReservations(data.id).Single()["phase"],Is.EqualTo("retiring"));yield return null;Assert.That(ObjectReservations(data.id),Is.Empty);Assert.That(parent.transform.childCount,Is.Zero);UnityEngine.Object.Destroy(parent);
        }
        [UnityTest] public IEnumerator NativeObjectAdmissionDestroyedNeverActiveRootReleasesBoundPreparation()
        {
            var data=NativeObjectAdmissionFixture.Data();using var lease=NativeObjectResources.Begin(data,new RoomResourceOwner(editor.WorldIdentity,data.id,"object"));var candidate=new GameObject("Never published");candidate.SetActive(false);lease.Bind(candidate);var before=(int)NativeObjectResources.ObserveBudget()["instances"];
            UnityEngine.Object.Destroy(candidate);Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(before));yield return null;Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(before-1));
        }
        [UnityTest] public IEnumerator NativeObjectAdmissionDormantFailureKeepsSavedIdentityAndCanRetry()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Read(id));int revision=editor.Revision;var pending=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
            try{var failed=editor.ActivateNativeArea(area);yield return NativeAreaCompletion(failed,false);StringAssert.Contains("live object budget",failed.Result);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));Assert.That(editor.Revision,Is.EqualTo(revision));}
            finally{foreach(var lease in pending)lease.Dispose();}
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.Identity(editor.Find(id)),Is.EqualTo(id));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));Assert.That(editor.Revision,Is.EqualTo(revision));
        }
    }
    public sealed partial class RoomEditorTests
    {
        [UnityTest] public IEnumerator NativeObjectAdmissionFreshWorkspacePreservesStarterWhenFullAndCanOpenLater()
        {
            var pending=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);GameObject fresh=null;
            try{
                fresh=new GameObject("Fresh capacity test");var interaction=fresh.AddComponent<RoomInteraction>();
                RoomItem Builtin(string name){var go=new GameObject(name);go.transform.SetParent(fresh.transform,false);var collider=go.AddComponent<BoxCollider>();var item=go.AddComponent<RoomItem>();item.Configure(new Collider[]{collider});return item;}
                var book=Builtin("book");var maestro=Builtin("maestro");var second=fresh.AddComponent<RoomEditor>();second.Initialize(interaction,book,maestro,Path.Combine(directory,"fresh"));
                var saved=second.Snapshot();Assert.That(saved.objects.Length,Is.EqualTo(5));Assert.That(fresh.GetComponentsInChildren<CreatedRoomObject>(true),Is.Empty);Assert.That(saved.objects.Where(x=>!x.IsBuiltIn).All(x=>second.NativeEntityDormant(x.id)),Is.True);
                Assert.That(second.Find("book"),Is.SameAs(book));Assert.That(second.Find("maestro"),Is.SameAs(maestro));int revision=second.Revision;string encoded=JsonUtility.ToJson(saved);
                foreach(var lease in pending)lease.Dispose();var loading=second.ActivateNativeArea("");float deadline=Time.realtimeSinceStartup+12;while(!loading.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(loading.IsCompleted,Is.True);Assert.That(loading.Exception,Is.Null);Assert.That(loading.Result,Is.Null);Assert.That(fresh.GetComponentsInChildren<CreatedRoomObject>(true).Length,Is.EqualTo(3));Assert.That(second.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(second.Snapshot()),Is.EqualTo(encoded));
            }finally{foreach(var lease in pending)lease.Dispose();if(fresh)UnityEngine.Object.Destroy(fresh);}yield return null;
        }
    }
}
