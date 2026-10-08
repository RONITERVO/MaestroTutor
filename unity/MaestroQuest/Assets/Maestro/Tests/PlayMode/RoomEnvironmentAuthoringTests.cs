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
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject SaveEnvironmentArgs()=>new(){["id"]="",["revision"]=0,["name"]="Virtual actors",["realCollisions"]=false,["members"]=new JArray()};
        string NewEnvironment() {
            var capability=new EnvironmentProfileSaveCapability();Assert.That(capability.Start(new CapabilityContext(editor,animations),"profile",SaveEnvironmentArgs(),out var operation,out var error),Is.True,error);
            return (string)operation.Result["id"];
        }
        bool AssignEnvironment(string target,string id,out string error)=>new EnvironmentAssignCapability().Start(new CapabilityContext(editor,animations),"binding",new JObject{["target"]=target,["revision"]=editor.ObjectRevision(target),["profileId"]=id,["profileRevision"]=editor.EnvironmentRevision(id)},out _,out error);
        [UnityTest]public IEnumerator EnvironmentActionsShareSavedFactsAndUndoAndRejectStaleAssignments() {
            string id=NewEnvironment();int before=editor.ObjectRevision("maestro");Assert.That(AssignEnvironment("maestro",id,out var error),Is.True,error);
            var fact=editor.ObserveEnvironmentBinding("maestro");Assert.That((string)fact["profileId"],Is.EqualTo(id));Assert.That((bool)fact["state"]["effectiveRealCollisions"],Is.False);
            var args=new JObject{["target"]="maestro",["revision"]=before,["profileId"]="",["profileRevision"]=0};
            Assert.That(new EnvironmentAssignCapability().CanRun(new CapabilityContext(editor,animations),args,out _),Is.False);
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").environmentProfile,Is.EqualTo(id),error);
            editor.Undo();Assert.That(editor.Read("maestro").environmentProfile,Is.Empty);Assert.That(physics.IncludesRealRoom(editor.Find("maestro")),Is.True);
            editor.Redo();Assert.That(editor.Read("maestro").environmentProfile,Is.EqualTo(id));Assert.That(physics.IncludesRealRoom(editor.Find("maestro")),Is.False);
            // Facts must be valid even when a new profile has no members or the list is empty.
            var context=new BehaviourCatalog.FactContext(editor:editor);Assert.That(EnvironmentProfileFacts.Binding().TryRead(context,1,new JObject{["target"]="maestro"},out _),Is.True);
            string unused=NewEnvironment();Assert.That(EnvironmentProfileFacts.Profile().TryRead(context,1,new JObject{["id"]=unused},out _),Is.True);
            Assert.That(EnvironmentProfileFacts.Profiles().TryRead(context,1,new JObject{["offset"]=16},out _),Is.True);
            yield return null;
        }
        [UnityTest]public IEnumerator SharedProfileMutationRequiresExactMembersAndChecksAllBeforeChangingAny() {
            string id=NewEnvironment(),blockId=editor.Snapshot().objects.Single(x=>x.kind==RoomObjectKind.Block).id;
            Assert.That(AssignEnvironment("maestro",id,out var error),Is.True,error);Assert.That(AssignEnvironment(blockId,id,out error),Is.True,error);
            var args=SaveEnvironmentArgs();args["id"]=id;args["revision"]=editor.EnvironmentRevision(id);args["realCollisions"]=true;args["members"]=new JArray("maestro");
            var capability=new EnvironmentProfileSaveCapability();var context=new CapabilityContext(editor,animations);
            Assert.That(capability.CanRun(context,args,out error),Is.False);Assert.That(error,Does.Contain("every"));
            args["members"]=new JArray("maestro",blockId);
            Assert.That(capability.Claims(args).Select(c=>c.Target),Is.EquivalentTo(new[]{"maestro",blockId}));
            block.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
            Assert.That(capability.Start(context,"blocked",args,out _,out error),Is.False);Assert.That(editor.ReadEnvironment(id).realCollisions,Is.False);
            block.GetComponent<RigidRoomItem>().SetAnimationOwner(this,false);
            Assert.That(capability.Start(context,"edit",args,out _,out error),Is.True,error);
            Assert.That(physics.IncludesRealRoom(block)&&physics.IncludesRealRoom(editor.Find("maestro")),Is.True);
            Assert.That(editor.EditEnvironment(null,id,editor.EnvironmentRevision(id),Array.Empty<string>(),out _),Is.False);
            editor.Undo();Assert.That(physics.IncludesRealRoom(block)||physics.IncludesRealRoom(editor.Find("maestro")),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator EnvironmentTemporaryDiscardRestoresProfilesAndBindingsWithoutStaleRevisions() {
            string id=NewEnvironment();Assert.That(AssignEnvironment("maestro",id,out var error),Is.True,error);int before=editor.EnvironmentRevision(id);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var definition=editor.ReadEnvironment(id);definition.realCollisions=true;
            Assert.That(editor.EditEnvironment(definition,id,editor.EnvironmentRevision(id),new[]{"maestro"},out error),Is.True,error);
            Assert.That(physics.IncludesRealRoom(editor.Find("maestro")),Is.True);string added=NewEnvironment();
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.ReadEnvironment(added),Is.Null);Assert.That(physics.IncludesRealRoom(editor.Find("maestro")),Is.False);
            Assert.That(editor.Read("maestro").environmentProfile,Is.EqualTo(id));Assert.That(editor.EnvironmentRevision(id),Is.GreaterThan(before));
        }
        [UnityTest]public IEnumerator EnvironmentCapabilitiesHaveReplayableReceiptsAndNativeFacts() {
            var executor=new RoomAgentExecutor(editor);string run=runtime.Scheduler.Receipts.NextId;var args=SaveEnvironmentArgs();
            var call=new JObject{["id"]="environment.profile.save",["version"]=1,["arguments"]=args};
            var request=new RoomAgentRequest{version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand{action="execution",execution=new JObject{["operation"]="start",["runId"]=run,["call"]=call}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);yield return null;
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
            string id=(string)receipt["output"]["id"];int revision=editor.EnvironmentRevision(id);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.EnvironmentProfiles().Length,Is.EqualTo(1));Assert.That(editor.EnvironmentRevision(id),Is.EqualTo(revision));
            var before=editor.ObserveEnvironmentBinding("maestro");
            var assign=new JObject{["id"]="object.environment.assign",["version"]=1,["arguments"]=new JObject{["target"]="maestro",["revision"]=editor.ObjectRevision("maestro"),["profileId"]=id,["profileRevision"]=revision}};
            string assignRun=runtime.Scheduler.Receipts.NextId;
            request.commands[0].execution=new JObject{["operation"]="start",["runId"]=assignRun,["call"]=assign};
            request.conditions=new[]{new RoomObjectCondition{id="maestro",revision=editor.ObjectRevision("maestro")}};
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return null;
            var assigned=runtime.Scheduler.Invocation(assignRun);Assert.That((string)assigned["phase"],Is.EqualTo("completed"),assigned.ToString());
            var evidence=new JObject{["capabilities"]=new JArray(RoomControls.Capabilities(editor)),["saveCall"]=call,["saveReceipt"]=receipt,["assignCall"]=assign,["assignReceipt"]=assigned,["before"]=before,["after"]=editor.ObserveEnvironmentBinding("maestro"),["profile"]=editor.ObserveEnvironmentProfile(id),["profiles"]=editor.ObserveEnvironmentProfiles(0),["emptyPage"]=editor.ObserveEnvironmentProfiles(16)};
            string output=Environment.GetEnvironmentVariable("MAESTRO_ENTITY_ENVIRONMENT");
            if(!string.IsNullOrEmpty(output)){System.IO.Directory.CreateDirectory(output);System.IO.File.WriteAllText(System.IO.Path.Combine(output,"profiles.json"),evidence.ToString());}
        }
        [UnityTest]public IEnumerator EnablingPhysicalProfileRefusesAnIntersectingWallWithoutSaving() {
            RoomPhysicsLayers.Configure();string id=NewEnvironment();Assert.That(AssignEnvironment("maestro",id,out var error),Is.True,error);
            var avatar=editor.Find("maestro");var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.transform.position=avatar.transform.position;wall.layer=RoomPhysicsLayers.Scanned;
            physics.SetSurfaces(true,"Aligned");Physics.SyncTransforms();int revision=editor.ObjectRevision("maestro");
            Assert.That(AssignEnvironment("maestro","",out error),Is.False);Assert.That(error,Does.Contain("clear"));Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(editor.Read("maestro").environmentProfile,Is.EqualTo(id));
            yield return null;
        }
    }
}
