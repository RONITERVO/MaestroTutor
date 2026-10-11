// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        string NewAppearance() {
            var capability=new AppearanceSaveCapability();Assert.That(capability.Start(new CapabilityContext(editor,animations),"appearance",capability.Example,out var operation,out var error),Is.True,error);
            return (string)operation.Result["id"];
        }
        bool AssignAppearance(string target,string id,out string error,string operation="assign",string tint="")=>new AppearanceBindCapability().Start(new CapabilityContext(editor,animations),"binding",
            new JObject{["operation"]=operation,["target"]=target,["revision"]=editor.ObjectRevision(target),["appearanceRevision"]=editor.AppearanceRevision(id),
                ["binding"]=JObject.Parse(JsonUtility.ToJson(new AppearanceBinding{appearanceId=id,tint=tint}))},out _,out error);
        string AppearanceBlock()=>editor.Snapshot().objects.Single(x=>x.kind==RoomObjectKind.Block).id;
        Material AppearanceBlockMaterial()=>block.GetComponentsInChildren<Renderer>().First(r=>!r.GetComponent<PencilMarks>()).sharedMaterial;
        [UnityTest]public IEnumerator AppearanceAuthoringSavesRendersAndRestoresWithUndo() {
            string id=NewAppearance(),target=AppearanceBlock();Assert.That(AssignAppearance(target,id,out var error),Is.True,error);
            Assert.That(editor.Read(target).color,Is.EqualTo(Color.white));Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(.4f));
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).appearanceBindings[0].appearanceId,Is.EqualTo(id),error);
            var context=new BehaviourCatalog.FactContext(editor:editor);
            Assert.That(AppearanceFacts.Definition().TryRead(context,2,new JObject{["id"]=id},out _),Is.True);
            Assert.That(AppearanceFacts.Definitions().TryRead(context,1,new JObject{["offset"]=0},out _),Is.True);
            Assert.That(AppearanceFacts.Members().TryRead(context,1,new JObject{["id"]=id,["offset"]=0},out _),Is.True);
            Assert.That(AppearanceFacts.Bindings().TryRead(context,1,new JObject{["target"]=target,["offset"]=0},out _),Is.True);
            Assert.That(AppearanceFacts.Targets().TryRead(context,1,new JObject{["target"]=target,["offset"]=0},out _),Is.True);
            // Generated fact types are bounded independently of general JSON arguments.
            var definition=editor.ReadAppearance(id);definition.name=new string('W',80);definition.style.patternMode="replace";definition.style.pattern.kind="stripes";definition.style.pattern.columns=32;definition.style.pattern.rows=32;
            definition.style.tiling=new Vector2(64,64);definition.style.offset=new Vector2(-64,-64);definition.style.grain=1;definition.style.shading=.3f;
            Assert.That(editor.EditAppearance(definition,id,editor.AppearanceRevision(id),new[]{target},out error),Is.True,error);
            Assert.That(AppearanceFacts.Definition().TryRead(context,2,new JObject{["id"]=id},out _),Is.True);
            editor.Undo();
            editor.Undo();Assert.That(editor.Read(target).appearanceBindings,Is.Empty);Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(1));
            editor.Redo();Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(.4f));yield return null;
        }
        [UnityTest]public IEnumerator AppearancePaintIsLocalAndIndependentCopyRetainsSurfaceSettings() {
            string id=NewAppearance(),target=AppearanceBlock();Assert.That(AssignAppearance(target,id,out var error),Is.True,error);
            Assert.That(editor.CopyObject(target,editor.ObjectRevision(target),"Another brick",Vector3.one,out string other,out error),Is.True,error);
            var before=editor.ReadAppearance(id);int revision=editor.AppearanceRevision(id);
            Assert.That(editor.PaintObject(target,Color.blue,out error),Is.True,error);
            Assert.That(editor.AppearanceRevision(id),Is.EqualTo(revision));Assert.That(editor.Read(other).appearanceBindings[0].tint,Is.Empty);
            Assert.That(editor.ObserveObjects().Single(x=>x.id==target).color,Is.EqualTo(Color.blue));
            Assert.That(AppearanceBlockMaterial().color,Is.EqualTo(Color.blue));Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(.4f));
            Assert.That(AssignAppearance(target,id,out error,"copy","#0000FF"),Is.True,error);string copy=editor.Read(target).appearanceBindings[0].appearanceId;
            Assert.That(copy,Is.Not.EqualTo(id));Assert.That(editor.ReadAppearance(copy).style.tint,Is.EqualTo("#0000FF"));Assert.That(editor.ReadAppearance(copy).style.opacity,Is.EqualTo(before.style.opacity));
            Assert.That(editor.Read(target).appearanceBindings[0].tint,Is.Empty);
            Assert.That(editor.CaptureConstruction(new[]{new ConstructionMember{target=target,revision=editor.ObjectRevision(target),slot="brick"}},out var captured,out error),Is.True,error);
            Assert.That(captured.blueprint.resources.appearances.Single().style.tint,Is.EqualTo("#0000FF"));
            Assert.That(captured.blueprint.resources.appearances.Single().style.opacity,Is.EqualTo(before.style.opacity));editor.Undo();
            Assert.That(editor.ReadAppearance(copy),Is.Null);Assert.That(editor.Read(target).appearanceBindings[0].tint,Is.EqualTo("#0000FF"));
            yield return null;
        }
        [UnityTest]public IEnumerator AppearanceSharedChangeRefusesPartialMembershipAndOwnedTargets() {
            string id=NewAppearance(),target=AppearanceBlock();Assert.That(AssignAppearance(target,id,out var error),Is.True,error);
            Assert.That(editor.CopyObject(target,editor.ObjectRevision(target),"Second",Vector3.one,out string other,out error),Is.True,error);
            var changed=editor.ReadAppearance(id);changed.style.opacity=.7f;int revision=editor.AppearanceRevision(id);
            Assert.That(editor.EditAppearance(changed,id,revision,new[]{target},out error),Is.False);Assert.That(error,Does.Contain("every"));
            block.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
            Assert.That(editor.EditAppearance(changed,id,revision,new[]{target,other},out _),Is.False);Assert.That(editor.AppearanceRevision(id),Is.EqualTo(revision));
            block.GetComponent<RigidRoomItem>().SetAnimationOwner(this,false);
            Assert.That(editor.EditAppearance(changed,id,revision,new[]{target,other},out error),Is.True,error);Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(.7f));
            Assert.That(editor.EditAppearance(changed,id,revision,new[]{target,other},out _),Is.False);
            Assert.That(editor.EditAppearance(null,id,editor.AppearanceRevision(id),Array.Empty<string>(),out _),Is.False);
            editor.Undo();Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(.4f));yield return null;
        }
        [UnityTest]public IEnumerator AppearanceTemporaryDiscardAndUnbindingPreservePaintAuthority() {
            string id=NewAppearance(),target=AppearanceBlock();Assert.That(AssignAppearance(target,id,out var error),Is.True,error);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.PaintObject(target,Color.green,out error),Is.True,error);Assert.That(AssignAppearance(target,id,out error,"remove"),Is.True,error);
            Assert.That(editor.Read(target).appearanceBindings,Is.Empty);Assert.That(editor.Read(target).color,Is.EqualTo(Color.green));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(target).appearanceBindings[0].tint,Is.Empty);
            Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(.4f));yield return null;
        }
        [UnityTest]public IEnumerator AppearanceLegacyPaintAndRecipePaletteUseTheSameBindingColour() {
            string id=NewAppearance(),target=AppearanceBlock();Assert.That(AssignAppearance(target,id,out var error),Is.True,error);
            var executor=new RoomAgentExecutor(editor);var paint=new RoomAgentRequest{version=2,conditions=new[]{new RoomObjectCondition{id=target,revision=editor.ObjectRevision(target)}},commands=new[]{new RoomAgentCommand{action="paint",target=target,color=Color.green}}};
            Assert.That(executor.Execute(paint,out error,out _),Is.True,error);Assert.That(editor.Read(target).appearanceBindings[0].tint,Is.EqualTo("#00FF00"));Assert.That(AppearanceBlockMaterial().color,Is.EqualTo(Color.green));
            var recipe=RecipeTemplates.BoxRobot(false);Assert.That(editor.CreateRecipe("Robot",Vector3.one,1,recipe,out string robot,out error),Is.True,error);
            Assert.That(editor.PaintObject(robot,Color.green,out error),Is.True,error);
            string part=recipe.parts[0].id;var binding=new AppearanceBinding{appearanceId=id,kind="part",partId=part,tint="#0000FF"};
            Assert.That(editor.BindAppearance(robot,editor.ObjectRevision(robot),binding,editor.AppearanceRevision(id),false,false,out error),Is.True,error);
            var geometry=editor.Find(robot).GetComponent<RecipeObject>();Material Pigment()=>geometry.Part(part).GetComponentsInChildren<Renderer>().First(x=>geometry.AppearancePart(x)==part).sharedMaterial;
            Assert.That(Pigment().color,Is.EqualTo(Color.blue),"Part binding ignores the root paint");
            var edit=editor.RecipeForEditing(editor.Read(robot));Assert.That(edit.parts[0].color,Is.EqualTo(Color.blue));edit.parts[0].color=Color.red;
            var patch=JObject.Parse(JsonUtility.ToJson(edit));patch.Remove("version");patch.Remove("playing");patch["removeParts"]=new JArray();patch["removeTracks"]=new JArray();
            Assert.That(editor.EditRecipe(robot,editor.ObjectRevision(robot),patch,out error),Is.True,error);
            Assert.That(editor.Read(robot).recipe.parts[0].color,Is.EqualTo(Color.white));Assert.That(editor.Read(robot).appearanceBindings[0].tint,Is.EqualTo("#FF0000"));
            Assert.That(Pigment().color,Is.EqualTo(Color.red));editor.Undo();Assert.That(Pigment().color,Is.EqualTo(Color.blue));
            yield return null;
        }
        [UnityTest]public IEnumerator AppearanceSharedEditClaimsAndChecksMoreThanSixteenObjects() {
            string id=NewAppearance(),target=AppearanceBlock();Assert.That(AssignAppearance(target,id,out var error),Is.True,error);
            for(int i=0;i<18;i++)Assert.That(editor.CopyObject(target,editor.ObjectRevision(target),"Shared brick "+i,new Vector3(i*.1f,1,1),out _,out error),Is.True,error);
            string[] members=editor.AppearanceMembers(id);Assert.That(members.Length,Is.EqualTo(19));
            var context=new BehaviourCatalog.FactContext(editor:editor);
            Assert.That(AppearanceFacts.Members().TryRead(context,1,new JObject{["id"]=id,["offset"]=0},out var first),Is.True);
            Assert.That(AppearanceFacts.Members().TryRead(context,1,new JObject{["id"]=id,["offset"]=16},out var second),Is.True);
            Assert.That(((JArray)((JObject)first.Value)["members"]).Count,Is.EqualTo(16));Assert.That(((JArray)((JObject)second.Value)["members"]).Count,Is.EqualTo(3));
            var cap=new AppearanceSaveCapability();var args=cap.Example;args["id"]=id;args["revision"]=editor.AppearanceRevision(id);args["style"]["opacity"]=.6;args["members"]=new JArray(members);
            var executor=new RoomAgentExecutor(editor);string run=runtime.Scheduler.Receipts.NextId;var call=new JObject{["id"]=cap.Id,["version"]=1,["arguments"]=args};
            var request=new RoomAgentRequest{version=2,conditions=members.Select(x=>new RoomObjectCondition{id=x,revision=editor.ObjectRevision(x)}).ToArray(),commands=new[]{new RoomAgentCommand{action="execution",execution=new JObject{["operation"]="start",["runId"]=run,["call"]=call}}}};
            request.conditions[0].revision--;Assert.That(executor.Execute(request,out _,out _),Is.False);
            request.conditions[0].revision++;Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return null;
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
            Assert.That(((JArray)receipt["resources"]).Count,Is.EqualTo(19));Assert.That(editor.ReadAppearance(id).style.opacity,Is.EqualTo(.6f));
        }
        [UnityTest]public IEnumerator AppearanceUnavailableAddressDoesNotSaveAndReceiptsReplayOnce() {
            string target=AppearanceBlock(),id=NewAppearance();int before=editor.ObjectRevision(target);
            var missing=new AppearanceBinding{appearanceId=id,kind="material",modelHash=new string('f',64),materialIndex=1};
            Assert.That(editor.BindAppearance(target,before,missing,editor.AppearanceRevision(id),false,false,out _),Is.False);
            Assert.That(editor.ObjectRevision(target),Is.EqualTo(before));
            var cap=new AppearanceBindCapability();var args=cap.Example;args["target"]=target;args["revision"]=before;args["appearanceRevision"]=editor.AppearanceRevision(id);args["binding"]["appearanceId"]=id;
            var executor=new RoomAgentExecutor(editor);string run=runtime.Scheduler.Receipts.NextId;
            var call=new JObject{["id"]=cap.Id,["version"]=1,["arguments"]=args};
            var request=new RoomAgentRequest{version=2,conditions=new[]{new RoomObjectCondition{id=target,revision=before}},commands=new[]{new RoomAgentCommand{action="execution",execution=new JObject{["operation"]="start",["runId"]=run,["call"]=call}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);yield return null;
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());int after=editor.ObjectRevision(target);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.ObjectRevision(target),Is.EqualTo(after));
            string output=Environment.GetEnvironmentVariable("MAESTRO_APPEARANCE_EVIDENCE");
            if(!string.IsNullOrEmpty(output)) {
                System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllText(System.IO.Path.Combine(output,"appearance.json"),new JObject{["call"]=call,["receipt"]=receipt,["definition"]=editor.ObserveAppearance(id),["bindings"]=editor.ObserveObjectAppearances(target,0),["targets"]=editor.ObserveAppearanceTargets(target,0),["members"]=editor.ObserveAppearanceMembers(id,0)}.ToString());
            }
        }
    }
}
