// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
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
        string NewRegion() {
            var cap=new RegionSaveCapability();Assert.That(cap.Start(new CapabilityContext(editor,animations),"region",cap.Example,out var op,out var error),Is.True,error);return (string)op.Result["id"];
        }
        JObject RegionCall(string target,string id)=>new(){["id"]="object.region.assign",["version"]=1,["arguments"]=new JObject{["target"]=target,["revision"]=editor.ObjectRevision(target),["regionId"]=id,["regionRevision"]=editor.RegionRevision(id)}};
        bool AssignRegion(string target,string id,out string error)=>editor.BindRegion(target,editor.ObjectRevision(target),id,editor.RegionRevision(id),out error);
        [UnityTest] public IEnumerator RegionMembershipPreservesMovingNativeObjectsAndSharedReceiptReplaysOnce() {
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(RegionCapability.Feature));string id=NewRegion(),target=editor.Identity(block);var item=editor.Find(target);int instance=item.GetInstanceID();var position=item.transform.position;
            var body=item.GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=false;body.linearVelocity=new Vector3(1,2,3);body.angularVelocity=Vector3.one;
            var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(RegionCall(target,id));request.conditions=new[]{new RoomObjectCondition{id=target,revision=editor.ObjectRevision(target)}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);int revision=editor.RegionRevision(id);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.RegionRevision(id),Is.EqualTo(revision));
            Assert.That(editor.Find(target).GetInstanceID(),Is.EqualTo(instance));Assert.That(item.transform.position,Is.EqualTo(position));Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity,Is.EqualTo(new Vector3(1,2,3)));Assert.That(body.angularVelocity,Is.EqualTo(Vector3.one));body.isKinematic=true;
            var context=new BehaviourCatalog.FactContext(editor:editor);
            Assert.That(RegionFacts.Region().TryRead(context,1,new JObject{["id"]=id},out _),Is.True);
            Assert.That(RegionFacts.Regions().TryRead(context,1,new JObject{["offset"]=0},out _),Is.True);
            Assert.That(RegionFacts.Members().TryRead(context,1,new JObject{["id"]=id,["offset"]=0},out _),Is.True);
            Assert.That(RegionFacts.Binding().TryRead(context,1,new JObject{["target"]=target},out _),Is.True);
            Assert.That(new RoomStorage(directory).Load(out error).regions.Single(x=>x.id==id).members,Is.EqualTo(new[]{target}),error);
            editor.Undo();Assert.That(editor.RegionFor(target),Is.Empty);editor.Redo();Assert.That(editor.RegionFor(target),Is.EqualTo(id));yield return null;
        }
        [UnityTest] public IEnumerator RegionTransferRefusesStaleAndFailedSavesWithoutPartialMembership() {
            string a=NewRegion(),b=NewRegion(),target=editor.Identity(block);Assert.That(AssignRegion(target,a,out var error),Is.True,error);
            int oldObject=editor.ObjectRevision(target),oldRegion=editor.RegionRevision(b);string before=JsonUtility.ToJson(editor.Snapshot());
            Assert.That(editor.BindRegion(target,oldObject-1,b,oldRegion,out _),Is.False);Assert.That(editor.BindRegion(target,oldObject,b,oldRegion-1,out _),Is.False);
            Assert.That(editor.EditRegion(null,a,editor.RegionRevision(a),new[]{target},out _),Is.False);Assert.That(editor.BindRegion("maestro",editor.ObjectRevision("maestro"),b,oldRegion,out _),Is.False);
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(AssignRegion(target,b,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.ObjectRevision(target),Is.EqualTo(oldObject));Assert.That(editor.RegionRevision(b),Is.EqualTo(oldRegion));}finally{Directory.Delete(obstacle);}
            Assert.That(AssignRegion(target,b,out error),Is.True,error);Assert.That(editor.ReadRegion(a).members,Is.Empty);Assert.That(editor.ReadRegion(b).members,Is.EqualTo(new[]{target}));
            int noOp=editor.ObjectRevision(target);Assert.That(AssignRegion(target,b,out error),Is.True,error);Assert.That(editor.ObjectRevision(target),Is.EqualTo(noOp));editor.Undo();Assert.That(editor.RegionFor(target),Is.EqualTo(a));yield return null;
        }
        [UnityTest] public IEnumerator RegionMembershipDeleteUndoAndCopyKeepOwnershipExplicit() {
            string id=NewRegion(),target=editor.Identity(block);Assert.That(AssignRegion(target,id,out var error),Is.True,error);
            Assert.That(editor.CopyObject(target,editor.ObjectRevision(target),"Home copy",Vector3.one,out var copy,out error),Is.True,error);Assert.That(editor.RegionFor(copy),Is.Empty);Assert.That(editor.RegionFor(target),Is.EqualTo(id));
            Assert.That(editor.DeleteObject(target,out error),Is.True,error);Assert.That(editor.ReadRegion(id).members,Is.Empty);editor.Undo();Assert.That(editor.Find(target),Is.Not.Null);Assert.That(editor.RegionFor(target),Is.EqualTo(id));editor.Redo();Assert.That(editor.ReadRegion(id).members,Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator RegionTemporaryDiscardAndKeepPersistOneMembershipHistory() {
            yield return WaitForModuleLibrary();string id=NewRegion(),target=editor.Identity(block);Assert.That(AssignRegion(target,id,out var error),Is.True,error);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;string other=NewRegion();Assert.That(AssignRegion(target,other,out error),Is.True,error);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.RegionFor(target),Is.EqualTo(id));Assert.That(editor.ReadRegion(other),Is.Null);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;other=NewRegion();Assert.That(AssignRegion(target,other,out error),Is.True,error);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.regions.Single(x=>x.id==other).members,Is.EqualTo(new[]{target}),error);Assert.That(saved.regions.Single(x=>x.id==id).members,Is.Empty);
        }
    }
}
