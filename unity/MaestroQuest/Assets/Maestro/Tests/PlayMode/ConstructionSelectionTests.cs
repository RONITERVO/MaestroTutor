// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Book;
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
        [UnityTest] public IEnumerator ConstructionSelectionSharesOrderedIdentitiesWithoutSavingOrMovingTheRoom() {
            var ids=editor.Snapshot().objects.Where(x=>!x.IsBuiltIn).Take(2).Select(x=>x.id).ToArray();var before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;bool undo=editor.CanUndo;
            var initial=editor.ObserveConstructionSelection();Assert.That(editor.SetConstructionSelection(initial.stateId,ids,false,out var error),Is.True,error);
            var selected=editor.ObserveConstructionSelection();Assert.That(selected.members,Is.EqualTo(ids));Assert.That(selected.stateId,Is.Not.EqualTo(initial.stateId));
            foreach(var id in ids)Assert.That(editor.Find(id).GetComponent<CreatedRoomObject>().ConstructionMarked,Is.True);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            Assert.That(editor.SetConstructionSelection(initial.stateId,Array.Empty<string>(),false,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            selected.members[0]="bad";Assert.That(editor.ObserveConstructionSelection().members,Is.EqualTo(ids));
            var current=editor.ObserveConstructionSelection();Assert.That(editor.SetConstructionSelection(current.stateId,ids.Reverse().ToArray(),false,out error),Is.True,error);Assert.That(editor.ObserveConstructionSelection().members,Is.EqualTo(ids.Reverse()));
            var snapshot=editor.ObserveConstructionSelection();foreach(var invalid in new[]{new[]{ids[0],ids[0]},new[]{"book"},new[]{new string('f',32)},Enumerable.Repeat(ids[0],17).ToArray()}){Assert.That(editor.SetConstructionSelection(snapshot.stateId,invalid,false,out error),Is.False);Assert.That(editor.ObserveConstructionSelection().stateId,Is.EqualTo(snapshot.stateId));}
            yield return null;
        }
        [UnityTest] public IEnumerator ConstructionPickingUsesPhysicalTrayAndPointerWithoutStartingTapBehaviours() {
            var tray=new GameObject("Selection tray");tray.transform.SetParent(root.transform,false);tray.transform.position=new Vector3(5,5,5);tray.AddComponent<RoomToolTray>().Build(editor,root.GetComponent<RoomInteraction>());
            var tool=tray.GetComponentsInChildren<PhysicalRoomAction>().Single(t=>t.Tool==RoomTool.CollectPieces);int taps=0;editor.ItemTapped+=_=>taps++;
            tool.Activate();Assert.That(editor.ObserveConstructionSelection().collecting,Is.True);string id=editor.Identity(block);
            block.transform.position=new Vector3(0,0,1);var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;Physics.SyncTransforms();var ray=new Ray(Vector3.zero,Vector3.forward);
            Assert.That(router.Begin(0,ray),Is.True);router.End(0,ray);Assert.That(editor.ObserveConstructionSelection().members,Is.EqualTo(new[]{id}));Assert.That(taps,Is.Zero);Assert.That(tool.AccessibleName,Is.EqualTo("Finish (1)"));
            Assert.That(router.Begin(0,ray),Is.True);router.End(0,ray);Assert.That(editor.ObserveConstructionSelection().members,Is.Empty);Assert.That(taps,Is.Zero);
            editor.Tapped(block);yield return new WaitForSecondsRealtime(.31f);tool.Activate();Assert.That(editor.ObserveConstructionSelection().collecting,Is.False);Assert.That(editor.ObserveConstructionSelection().members,Is.EqualTo(new[]{id}));
            editor.Tapped(block);Assert.That(taps,Is.EqualTo(1));Assert.That(tool.AccessibleName,Is.EqualTo("Collect pieces"));yield return null;
        }
        [UnityTest] public IEnumerator DeletedSelectionMembersStayRemovedAfterUndoAndRoomBoundariesClearSelection() {
            string id=editor.Identity(block);Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{id},false,out var error),Is.True,error);var selected=editor.ObserveConstructionSelection();
            Assert.That(editor.DeleteObject(id,out error),Is.True,error);Assert.That(editor.ObserveConstructionSelection().members,Is.Empty);Assert.That(editor.ObserveConstructionSelection().stateId,Is.Not.EqualTo(selected.stateId));
            editor.Undo();Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.ObserveConstructionSelection().members,Is.Empty);
            Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{id},true,out error),Is.True,error);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);Assert.That(editor.ObserveConstructionSelection().members,Is.Empty);Assert.That(editor.ObserveConstructionSelection().collecting,Is.False);
            for(int i=0;i<180&&editor.TemporarySavePending;i++)yield return null;Assert.That(editor.TemporarySavePending,Is.False);
            Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{id},true,out error),Is.True,error);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.ObserveConstructionSelection().members,Is.Empty);Assert.That(editor.ObserveConstructionSelection().collecting,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SuspensionAndDrawingEndPickingWithoutLosingChosenMembers() {
            string id=editor.Identity(block);Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{id},true,out var error),Is.True,error);var before=editor.ObserveConstructionSelection();
            using(var lease=editor.RuntimeGate.Hold("test")){Assert.That(editor.ObserveConstructionSelection().collecting,Is.False);Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,Array.Empty<string>(),false,out error),Is.False);}
            Assert.That(editor.ObserveConstructionSelection().stateId,Is.Not.EqualTo(before.stateId));Assert.That(editor.ObserveConstructionSelection().members,Is.EqualTo(new[]{id}));
            Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{id},true,out error),Is.True,error);Assert.That(editor.ConfigureDrawing("space",Color.blue,.003f,out error),Is.True,error);Assert.That(editor.ObserveConstructionSelection().collecting,Is.False);
            Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{id},true,out error),Is.False);Assert.That(error,Does.Contain("pencil"));yield return null;
        }
        [UnityTest] public IEnumerator SharedSelectionCapabilityRequiresExactObjectConditionsAndReturnsNoCreatedAuthority() {
            var definition=ConstructionSelectionCapability.Fact();Assert.That(definition.TryRead(new BehaviourCatalog.FactContext(editor:editor),out var empty),Is.True);Assert.That(JObject.FromObject(empty.Value)["members"],Is.Empty);
            string id=editor.Identity(block);var before=editor.ObserveConstructionSelection();var call=new JObject {["id"]="room.selection.set",["version"]=1,["arguments"]=new JObject {["stateId"]=before.stateId,["members"]=new JArray(id),["collecting"]=false}};
            var duplicate=(JObject)call["arguments"].DeepClone();duplicate["members"]=new JArray(id,id);Assert.That(CapabilityArguments.Validate(duplicate,BehaviourCatalog.Action("room.selection.set").InputSchema,out var validation),Is.False);Assert.That(validation,Does.Contain("distinct"));
            var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(call);Assert.That(executor.Execute(request,out var error,out _),Is.False);Assert.That(editor.ObserveConstructionSelection().members,Is.Empty);
            request.conditions=new[]{new RoomObjectCondition {id=id,revision=editor.ObjectRevision(id)}};Assert.That(executor.Execute(request,out error,out var created),Is.True,error);Assert.That(created,Is.Empty);
            Assert.That(executor.Executions.Observe()["selected"]["phase"].Value<string>(),Is.EqualTo("completed"));Assert.That(editor.ObserveConstructionSelection().members,Is.EqualTo(new[]{id}));
            var output=(JObject)executor.Executions.Observe()["selected"]["output"];Assert.That(CapabilityArguments.Resources(output,BehaviourCatalog.Action("room.selection.set").OutputSchema),Is.Empty);
            Assert.That(ConstructionSelectionCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(JObject.FromObject(fact.Value)["members"][0].Value<string>(),Is.EqualTo(id));
            Assert.That(ConstructionSelectionCapability.RunManual(editor,Array.Empty<string>(),false,out error),Is.True,error);Assert.That(definition.TryRead(new BehaviourCatalog.FactContext(editor:editor),out var cleared),Is.True);Assert.That(JObject.FromObject(cleared.Value)["members"],Is.Empty);yield return null;
        }
    }
}
