// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject AppearanceArgs(string id="") {
            var args=new AppearanceSaveCapability().Example;args["id"]=id;args["revision"]=editor.AppearanceRevision(id);
            if(id!=""){args["name"]=editor.ReadAppearance(id).name;args["style"]=JObject.Parse(JsonUtility.ToJson(editor.ReadAppearance(id).style));args["members"]=new JArray(editor.AppearanceMembers(id));}
            return args;
        }
        JObject AppearanceAssignment(string id,string operation="assign",string tint="")=>new() {
            ["operation"]=operation,["target"]=editor.Identity(block),["revision"]=editor.ObjectRevision(editor.Identity(block)),["appearanceRevision"]=editor.AppearanceRevision(id),
            ["binding"]=JObject.Parse(JsonUtility.ToJson(new AppearanceBinding{appearanceId=id,tint=tint}))};
        [UnityTest] public IEnumerator PendingAppearanceQueueCapturesStyleAndReturnsImmutableSourceReceipt()
        {
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation first=null,second=null;
            try {
                yield return Prepared(blocker);var context=new CapabilityContext(editor,animations);var args=AppearanceArgs();var module=new AppearanceSaveCapability();
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(context,"first",args,out first,out error),error);
                args["style"]["opacity"]=.8;args["name"]="Another appearance";
                Assert.IsTrue(module.Start(context,"second",args,out second,out error),error);Assert.IsEmpty(first.Result);Assert.IsEmpty(editor.Appearances());
                blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Preparing,second.State(out error),error);Assert.IsEmpty(editor.Appearances());
                yield return PlacementOperationDone(first);yield return PlacementOperationDone(second);
                string id=(string)first.Result["id"],other=(string)second.Result["id"];Assert.AreNotEqual(id,other);
                Assert.AreEqual(.4f,editor.ReadAppearance(id).style.opacity);Assert.AreEqual(.8f,editor.ReadAppearance(other).style.opacity);
                int revision=(int)first.Result["revision"];first.Result["revision"]=-1;Assert.AreEqual(revision,(int)first.Result["revision"]);
                Assert.AreEqual(2,new RoomStorage(directory).Load(out error).appearances.Length,error);editor.Undo();Assert.IsNull(editor.ReadAppearance(other));
            }finally{first?.Stop(false);second?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAppearanceSharedEditRejectsChangedWaitingMembershipAndKeepsAllMembers()
        {
            string id=NewAppearance(),target=editor.Identity(block);Assert.IsTrue(AssignAppearance(target,id,out var error),error);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);var args=AppearanceArgs(id);args["style"]["opacity"]=.8;
                Assert.IsTrue(new AppearanceSaveCapability().Start(new CapabilityContext(editor,animations),"shared",args,out operation,out error),error);
                Assert.IsTrue(editor.CopyObject(target,editor.ObjectRevision(target),"Another shared object",Vector3.one,out var other,out error),error);
                blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("every",error);
                Assert.IsEmpty(operation.Result);Assert.AreEqual(.4f,AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"));Assert.AreEqual(id,editor.Read(other).appearanceBindings.Single().appearanceId);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAppearanceCopyIsOneCapturedAtomicEditAndUndoRemovesBothChanges()
        {
            string id=NewAppearance(),target=editor.Identity(block);Assert.IsTrue(AssignAppearance(target,id,out var error),error);
            var args=AppearanceAssignment(id,"copy","#0000FF");Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(new AppearanceBindCapability().Start(new CapabilityContext(editor,animations),"copy",args,out operation,out error),error);
                args["binding"]["tint"]="#FF0000";Assert.IsEmpty(operation.Result);Assert.AreEqual(1,editor.Appearances().Length);
                blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(operation);
                string copy=(string)operation.Result["appearanceId"];Assert.AreNotEqual(id,copy);Assert.AreEqual(copy,editor.Read(target).appearanceBindings.Single().appearanceId);
                Assert.AreEqual("#0000FF",editor.ReadAppearance(copy).style.tint);Assert.AreEqual("",editor.Read(target).appearanceBindings.Single().tint);
                Assert.AreEqual(.4f,AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"));Assert.AreEqual(Color.blue,AppearanceBlockMaterial().color);
                Assert.AreEqual(editor.ObjectRevision(target),(int)operation.Result["revision"]);Assert.AreEqual(editor.AppearanceRevision(copy),(int)operation.Result["appearanceRevision"]);
                var disk=new RoomStorage(directory).Load(out error);Assert.AreEqual(copy,disk.objects.Single(x=>x.id==target).appearanceBindings.Single().appearanceId,error);Assert.IsTrue(disk.appearances.Any(x=>x.id==copy));
                editor.Undo();Assert.IsNull(editor.ReadAppearance(copy));Assert.AreEqual(id,editor.Read(target).appearanceBindings.Single().appearanceId);
                editor.Redo();Assert.AreEqual(copy,editor.Read(target).appearanceBindings.Single().appearanceId);Assert.AreEqual(2,editor.Appearances().Length);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAppearanceCancelledCopyAndLostNativeSurfacePublishNeitherResourceNorBinding()
        {
            string id=NewAppearance(),target=editor.Identity(block);
            foreach(bool loseSurface in new[]{false,true}) {
                string created=Guid.NewGuid().ToString("N");var binding=new AppearanceBinding{appearanceId=id};
                Assert.IsTrue(editor.BeginAppearanceBinding(target,editor.ObjectRevision(target),binding,editor.AppearanceRevision(id),false,true,out var pending,out var error,copyId:created),error);
                var renderer=block.GetComponentsInChildren<Renderer>().First(x=>x.sharedMaterials.Any(m=>m&&m.shader.name=="Maestro/Watercolor"));var materials=renderer.sharedMaterials;
                try {using(pending) {
                    yield return Prepared(pending);Assert.IsNull(editor.ReadAppearance(created));Assert.IsEmpty(editor.Read(target).appearanceBindings);
                    if(loseSurface){renderer.sharedMaterials=Array.Empty<Material>();Assert.IsFalse(pending.Poll(out _,out error));StringAssert.Contains("surface",error);}
                    else pending.Dispose();
                    yield return Drained(pending);Assert.IsNull(editor.ReadAppearance(created));Assert.IsEmpty(editor.Read(target).appearanceBindings);
                    var disk=new RoomStorage(directory).Load(out error);Assert.IsFalse(disk.appearances.Any(x=>x.id==created),error);Assert.IsEmpty(disk.objects.Single(x=>x.id==target).appearanceBindings);
                }}finally{renderer.sharedMaterials=materials;}
            }
        }
        [UnityTest] public IEnumerator PendingAppearanceCopyRevalidatesCapacityAndExactSourceWhileWaiting()
        {
            string id=NewAppearance();var context=new CapabilityContext(editor,animations);
            foreach(bool capacity in new[]{false,true}) {
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
                try {
                    yield return Prepared(blocker);Assert.IsTrue(new AppearanceBindCapability().Start(context,"copy",AppearanceAssignment(id,"copy"),out operation,out error),error);
                    if(capacity)for(int i=editor.Appearances().Length;i<RoomAppearance.MaximumDefinitions;i++)NewAppearance();
                    else {var changed=editor.ReadAppearance(id);changed.style.opacity=.6f;Assert.IsTrue(editor.EditAppearance(changed,id,editor.AppearanceRevision(id),Array.Empty<string>(),out error),error);}
                    blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));
                    StringAssert.Contains(capacity?"maximum":"exact",error);Assert.IsEmpty(editor.Read(editor.Identity(block)).appearanceBindings);Assert.IsEmpty(operation.Result);
                    Assert.AreEqual(capacity?64:1,editor.Appearances().Length);
                }finally{operation?.Stop(false);blocker.Dispose();}
            }
        }
        [UnityTest] public IEnumerator PendingAppearanceReceiptsKeepAcceptedSourceAndTargetRevisionsAfterHumanEdits()
        {
            string id=NewAppearance(),target=editor.Identity(block);var context=new CapabilityContext(editor,animations);
            foreach(string kind in new[]{"save","assign","copy"}) {
                CapabilityOperation operation;string error;
                if(kind=="save"){var args=AppearanceArgs(id);args["style"]["opacity"]=.7;Assert.IsTrue(new AppearanceSaveCapability().Start(context,"save",args,out operation,out error),error);}
                else Assert.IsTrue(new AppearanceBindCapability().Start(context,kind,AppearanceAssignment(id,kind),out operation,out error),error);
                try {
                    float end=Time.realtimeSinceStartup+20;while(operation.State(out error)==RuleActionState.Preparing){Assert.Less(Time.realtimeSinceStartup,end,error);yield return null;}
                    Assert.AreEqual(RuleActionState.Ready,operation.State(out error),error);
                    string acceptedId=kind=="copy"?editor.Read(target).appearanceBindings.Single().appearanceId:id;
                    int sourceRevision=editor.AppearanceRevision(acceptedId),targetRevision=editor.ObjectRevision(target);
                    var later=editor.ReadAppearance(acceptedId);later.name="Later human edit "+kind;
                    Assert.IsTrue(editor.EditAppearance(later,acceptedId,sourceRevision,editor.AppearanceMembers(acceptedId),out error),error);
                    Assert.IsTrue(operation.Complete(out error),error);
                    Assert.AreEqual(sourceRevision,(int)operation.Result[kind=="save"?"revision":"appearanceRevision"]);
                    if(kind!="save")Assert.AreEqual(targetRevision,(int)operation.Result["revision"]);
                    Assert.AreNotEqual(sourceRevision,editor.AppearanceRevision(acceptedId));
                }finally{operation.Stop(false);}
            }
        }
        [UnityTest] public IEnumerator PendingAppearanceSharedSourcePreservesLivePlacementClockAndRenderUntilAccepted()
        {
            string id=NewAppearance(),target=editor.Identity(block);Assert.IsTrue(AssignAppearance(target,id,out var error),error);
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new(){running=true},out error),error);
            var changed=editor.ReadAppearance(id);changed.style.opacity=.8f;
            Assert.IsTrue(editor.BeginAppearance(changed,id,editor.AppearanceRevision(id),new[]{target},out var pending,out error),error);
            changed.style.opacity=.1f;
            using(pending) {
                yield return Prepared(pending);Assert.AreEqual(.4f,AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"));
                var moved=block.transform.position+Vector3.right*.2f;block.transform.position=moved;editor.RememberPlacement(target);editor.TickWorldTime(.2);double now=editor.WorldSecond;
                yield return Completed(pending);Assert.AreEqual(.8f,AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"));Assert.AreEqual(moved,block.transform.position);Assert.GreaterOrEqual(editor.WorldSecond,now);
                Assert.IsTrue(editor.HasUnsavedChanges);editor.Undo();Assert.AreEqual(.4f,AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"));Assert.AreEqual(moved,block.transform.position);
            }
        }
        [UnityTest] public IEnumerator PendingAppearanceAcceptedCopyStopDrainsAtomicSaveWithoutSuccessfulReceipt()
        {
            string id=NewAppearance(),target=editor.Identity(block),copy=Guid.NewGuid().ToString("N");int revision=editor.ObjectRevision(target),sourceRevision=editor.AppearanceRevision(id);
            var binding=new AppearanceBinding{appearanceId=id};using var pause=new PublicationPause();RoomEditor.PendingPersistedEdit pending=null;
            Assert.IsTrue(PersistedEditOperation.Start(editor,target,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool ok=editor.BeginAppearanceBinding(target,revision,binding,sourceRevision,false,true,out edit,out issue,pause.Check,copy);pending=edit;return ok;},
                out var operation,out var error,validate:(out string issue)=>editor.CanBindAppearance(target,revision,binding,sourceRevision,false,true,out issue,copy)),error);
            try {
                yield return Prepared(pending);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(pending.Accepted);
                operation.Stop(false);Assert.IsEmpty(operation.Result);StringAssert.Contains("Saving is still pending",operation.InterruptionStatus);Assert.IsFalse(operation.Complete(out _));
            }finally{pause.Release();operation.Stop(false);}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            var disk=new RoomStorage(directory).Load(out error);Assert.IsTrue(disk.appearances.Any(x=>x.id==copy),error);Assert.AreEqual(copy,disk.objects.Single(x=>x.id==target).appearanceBindings.Single().appearanceId);
            editor.Undo();Assert.IsNull(editor.ReadAppearance(copy));Assert.IsEmpty(editor.Read(target).appearanceBindings);
        }
        [UnityTest] public IEnumerator PendingAppearanceNoopAndRemovalHaveExactReceiptsAndTemporaryCopyKeepsOrDiscardsAtomically()
        {
            string id=NewAppearance();int revision=editor.AppearanceRevision(id);var context=new CapabilityContext(editor,animations);
            Assert.IsTrue(new AppearanceSaveCapability().Start(context,"same",AppearanceArgs(id),out var operation,out var error),error);yield return PlacementOperationDone(operation);
            Assert.AreEqual(revision,(int)operation.Result["revision"]);editor.Undo();Assert.IsNull(editor.ReadAppearance(id));editor.Redo();
            Assert.IsTrue(new AppearanceRemoveCapability().Start(context,"remove",new JObject{["id"]=id,["revision"]=editor.AppearanceRevision(id)},out operation,out error),error);
            yield return PlacementOperationDone(operation);Assert.AreEqual(0,(int)operation.Result["revision"]);editor.Undo();Assert.IsNotNull(editor.ReadAppearance(id));
            foreach(bool keep in new[]{false,true}) {
                Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);while(editor.TemporarySavePending)yield return null;
                Assert.IsTrue(new AppearanceBindCapability().Start(context,"copy",AppearanceAssignment(id,"copy"),out operation,out error),error);yield return PlacementOperationDone(operation);
                string copy=(string)operation.Result["appearanceId"];Assert.IsTrue((bool)operation.Result["temporary"]);Assert.IsFalse(new RoomStorage(directory).Load(out error).appearances.Any(x=>x.id==copy),error);
                if(keep)Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);else Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
                while(editor.TemporarySavePending)yield return null;
                var disk=new RoomStorage(directory).Load(out error);Assert.AreEqual(keep,disk.appearances.Any(x=>x.id==copy),error);Assert.AreEqual(keep,disk.objects.Single(x=>x.id==editor.Identity(block)).appearanceBindings.Any(x=>x.appearanceId==copy));
            }
        }
    }
}
