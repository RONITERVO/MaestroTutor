// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        JObject ToolFact(){Assert.That(BehaviourCatalog.TryRead("room.tools",1,null,new BehaviourCatalog.FactContext(editor:host.Current.Editor),out var value),Is.True);return JObject.FromObject(value.Value);}
        JObject ToolRequest(RoomExecutions actions,string tray,bool visible)=>new(){["operation"]="start",["runId"]=actions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="room.tools.set",["version"]=1,["arguments"]=new JObject {["stateId"]=ToolFact()["stateId"].DeepClone(),["tray"]=tray,["visible"]=visible}}};
        [UnityTest] public IEnumerator OptionalToolsStartHiddenAndSharedReceiptsNeverReplayOrStopRoomWork()
        {
            Open();yield return ReadyHost();var content=host.Current;var editor=content.Editor;var actions=new RoomExecutions(editor,host);var service=content.GetComponent<RoomToolVisibility>();
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(RoomToolsCapability.Feature));Assert.That(((JObject)ToolFact()["visible"]).Properties().All(p=>!(bool)p.Value),Is.True);
            var items=content.GetComponentsInChildren<RoomItem>(true).Where(x=>x.GetComponent<RoomToolTray>()||x.GetComponent<AnimationTools>()||x.GetComponent<RuleTools>()||x.GetComponent<Maestro.Quest.Imports.ImportTools>()||x.GetComponent<PhysicsTools>()||x.GetComponent<Maestro.Quest.Avatar.AvatarSpatialTools>()||x.GetComponent<MovementTools>()).ToArray();Assert.That(items.Length,Is.EqualTo(7));Assert.That(items.All(x=>!x.gameObject.activeInHierarchy),Is.True);
            Assert.That(editor.isActiveAndEnabled&&content.Rules.isActiveAndEnabled&&content.Controls.isActiveAndEnabled&&content.GetComponent<AnimationWorkshop>().isActiveAndEnabled&&content.GetComponent<SpatialDrawing>().isActiveAndEnabled,Is.True);
            var runtime=content.GetComponent<RoomRules>();Assert.That(runtime.Scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}},Time.unscaledTime,out var waiting,out var error),Is.True,error);
            int revision=editor.Revision;var original=JsonUtility.ToJson(editor.Snapshot());var open=ToolRequest(actions,"creation",true);Assert.That(actions.Execute(open,out error),Is.True,error);yield return null;
            Assert.That((string)actions.Observe()["selected"]["phase"],Is.EqualTo("completed"));var creation=content.GetComponentInChildren<RoomToolTray>();Assert.That(creation,Is.Not.Null);Assert.That(creation.GetComponent<RoomItem>().Grab.colliders.Count,Is.GreaterThan(1),"Opening must register the physical buttons for grip and ray selection");
            var close=ToolRequest(actions,"creation",false);Assert.That(actions.Execute(close,out error),Is.True,error);yield return null;Assert.That((bool)ToolFact()["visible"]["creation"],Is.False);
            Assert.That(actions.Execute(open,out error),Is.True,error);Assert.That((bool)ToolFact()["visible"]["creation"],Is.False,"Replaying a completed receipt must not reopen a hidden tray");
            Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(original));
            Assert.That(service.Set((string)ToolFact()["stateId"],"all",true,out _,out error),Is.True,error);yield return null;Assert.That(items.All(x=>x.gameObject.activeInHierarchy),Is.True);Assert.That(book.gameObject.activeInHierarchy,Is.True);
        }
        [UnityTest] public IEnumerator ToolVisibilityRejectsStaleAndHeldTraysAtomicallyAndKeepsRecallAvailable()
        {
            Open();yield return ReadyHost();var service=host.Current.GetComponent<RoomToolVisibility>();var actions=new RoomExecutions(host.Current.Editor,host);
            var stale=ToolRequest(actions,"animation",true);Assert.That(service.Set((string)ToolFact()["stateId"],"all",true,out _,out var error),Is.True,error);yield return null;
            Assert.That(actions.Execute(stale,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            var item=host.Current.GetComponentInChildren<RoomToolTray>().GetComponent<RoomItem>();var manager=root.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            var handRoot=new GameObject("Tool grip");handRoot.SetActive(false);handRoot.transform.SetParent(root.transform,false);handRoot.transform.position=item.transform.position-Vector3.forward*.25f;
            var hand=handRoot.AddComponent<XRRayInteractor>();hand.enableUIInteraction=false;hand.interactionManager=manager;hand.keepSelectedTargetValid=true;hand.manipulateAttachTransform=false;hand.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;
            hand.selectInput=new XRInputButtonReader {inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};handRoot.SetActive(true);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);yield return null;
            Assert.That(item.Grab.isSelected,Is.True);Assert.That((bool)ToolFact()["held"]["creation"],Is.True);var before=ToolFact();Assert.That(service.Set((string)before["stateId"],"all",false,out _,out error),Is.False);Assert.That(error,Does.Contain("Release"));Assert.That(JToken.DeepEquals(before,ToolFact()),Is.True);
            manager.SelectExit((IXRSelectInteractor)hand,item.Grab);handRoot.SetActive(false);Assert.That(service.Set((string)ToolFact()["stateId"],"all",false,out _,out error),Is.True,error);var hidden=ToolFact();
            room.RestoreInFrontOfViewer();yield return null;Assert.That(JToken.DeepEquals(hidden,ToolFact()),Is.True,"Recall restores positions without reopening hidden trays");Assert.That(book.gameObject.activeInHierarchy,Is.True);Assert.That(host.Current.Editor.Find("maestro").gameObject.activeInHierarchy,Is.True);
        }
        [UnityTest] public IEnumerator ToolVisibilityRespectsWorkspaceHoldsAndHiddenToolsRefreshWhenOpened()
        {
            Open();yield return ReadyHost();var editor=host.Current.Editor;var service=editor.GetComponent<RoomToolVisibility>();var before=ToolFact();
            using(editor.WriteGate.TryFreeze(out var error)){Assert.That(service.Set((string)before["stateId"],"creation",true,out _,out _),Is.False);Assert.That(JToken.DeepEquals(before,ToolFact()),Is.True);}
            using(editor.RuntimeGate.Hold("Review workspace")){Assert.That(service.Set((string)before["stateId"],"creation",true,out _,out _),Is.False);}
            editor.ToggleDrawing();Assert.That(editor.DrawingMode,Is.True);Assert.That(service.Set((string)ToolFact()["stateId"],"creation",true,out _,out var reason),Is.True,reason);yield return null;
            var tray=editor.GetComponentInChildren<RoomToolTray>();var pencil=tray.GetComponentsInChildren<PhysicalRoomAction>().Single(x=>x.Tool==RoomTool.Pencil);Assert.That(pencil.AccessibleName,Is.EqualTo("Draw"));
            var shown=ToolFact();Assert.That(service.Set((string)shown["stateId"],"creation",true,out var same,out reason),Is.True,reason);Assert.That(JToken.DeepEquals(shown,same),Is.True,"Satisfied requests do not churn visibility identity");
            Assert.That(service.Set((string)same["stateId"],"creation",false,out _,out reason),Is.True,reason);Assert.That(editor.DrawingMode,Is.True,"Hiding the tray does not stop an active drawing mode");
        }
    }
}
