// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
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
    public sealed partial class RoomRulesTests
    {
        ControllerFrame bindingFrame;
        RoomExecutions controlActions;
        MovementControls BindingControls()
        {
            bindingFrame=new ControllerFrame {leftTracked=true,rightTracked=true};
            var controls=root.AddComponent<MovementControls>();controls.Initialize(root.GetComponent<RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,()=>bindingFrame,directory);
            controlActions=new RoomExecutions(editor);return controls;
        }
        JObject ControlFact()
        {
            Assert.That(BehaviourCatalog.TryRead("controller.settings",1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        JObject ControlArgs(string operation)=>new() {["operation"]=operation,["configurationId"]=ControlFact()["configurationId"].DeepClone()};
        JObject MovementArgs(){var args=ControlArgs("movement.left");args["userStick"]="right";args["deadZone"]=.3;args["userSpeed"]=.8;return args;}
        JObject ButtonArgs(string button="a"){var args=ControlArgs("button.program");args["button"]=button;args["programId"]=sequenceId;return args;}
        JObject ControlRequest(JObject args)=>new() {["operation"]="start",["runId"]=controlActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="controller.configure",["version"]=1,["arguments"]=args}};
        IEnumerator ConfigureControls(JObject args)
        {
            Assert.That(controlActions.Execute(ControlRequest(args),out var error),Is.True,error);yield return null;
            Assert.That((string)controlActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),controlActions.Observe().ToString());
        }
        [UnityTest] public IEnumerator SharedControllerConfigurationPersistsExactBindingsAndReplaysReceiptWithoutResaving()
        {
            var controls=BindingControls();var before=ControlFact();Assert.That(RoomControls.Capabilities(editor),Does.Contain("controllerConfiguration.v1"));
            yield return ConfigureControls(MovementArgs());var movement=controlActions.Observe().DeepClone();var afterMovement=ControlFact();
            Assert.That((string)afterMovement["avatarStick"],Is.EqualTo("left"));Assert.That((string)afterMovement["userStick"],Is.EqualTo("right"));Assert.That(JToken.DeepEquals(before["buttons"],afterMovement["buttons"]),Is.True);
            var request=ControlRequest(ButtonArgs());Assert.That(controlActions.Execute(request,out var error),Is.True,error);yield return null;var button=controlActions.Observe().DeepClone();var after=ControlFact();
            Assert.That((string)after["buttons"][1]["programId"],Is.EqualTo(sequenceId));Assert.That((bool)after["buttons"][1]["available"],Is.True);
            Assert.That((string)after["configurationId"],Is.EqualTo((string)button["selected"]["output"]["configurationId"]));Assert.That((string)after["configurationId"],Is.Not.EqualTo((string)afterMovement["configurationId"]));
            var persisted=new ControllerPreferenceStorage(directory).Load(out error);Assert.That(error,Is.Null);Assert.That(persisted.avatarStick,Is.EqualTo(MovementStick.Left));Assert.That(persisted.userSpeed,Is.EqualTo(.8f));Assert.That(persisted.buttons[1].sequenceId,Is.EqualTo(sequenceId));
            Assert.That(controlActions.Execute(request,out error),Is.True,error);Assert.That(JToken.DeepEquals(ControlFact(),after),Is.True,"Duplicate receipts never rotate the settings identity or resave");
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(controls.UserEnabled||controls.AvatarEnabled||controls.Virtual,Is.False);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_CONTROLLER_CONFIGURATION");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"configuration.json"),new JObject {["before"]=before,["movement"]=movement,["afterMovement"]=afterMovement,["button"]=button,["after"]=after,["programId"]=sequenceId}.ToString());}
        }
        [UnityTest] public IEnumerator RebindingAHeldButtonWaitsForReleaseThenUsesTheExistingProgramScheduler()
        {
            var controls=BindingControls();bindingFrame.a=true;yield return null;var position=block.transform.position;
            yield return ConfigureControls(ButtonArgs());yield return new WaitForSeconds(.12f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(block.transform.position,Is.EqualTo(position));
            bindingFrame.a=false;yield return null;bindingFrame.a=true;yield return new WaitForSeconds(.3f);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));Assert.That(block.transform.position.x,Is.GreaterThan(position.x+.02f));
            yield return new WaitForSeconds(.1f);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Holding the button does not repeatedly restart its program");runtime.StopAll();
            controls.BindButton(1,ControllerCommand.None);Assert.That((string)ControlFact()["buttons"][1]["command"],Is.EqualTo("none"));
        }
        [UnityTest] public IEnumerator SharedBindingsRejectStaleReservedConflictingAndMissingProgramChoices()
        {
            var controls=BindingControls();var stale=MovementArgs();controls.SwapSticks();var current=ControlFact();
            Assert.That(BehaviourCatalog.TryCall("controller.configure",1,stale,out var call,out var error),Is.True,error);Assert.That(call.Definition.Module.CanRun(new CapabilityContext(editor,animations),stale,out error),Is.False);StringAssert.Contains("changed",error);
            var conflict=MovementArgs();conflict["userStick"]="left";Assert.That(BehaviourCatalog.TryCall("controller.configure",1,conflict,out _,out _),Is.False);
            foreach(string reserved in new[]{"b","y","menu","system","trigger","grip"}){var args=ButtonArgs(reserved);Assert.That(BehaviourCatalog.TryCall("controller.configure",1,args,out _,out _),Is.False,reserved);}
            var absent=ButtonArgs();absent["programId"]=new string('f',32);Assert.That(BehaviourCatalog.TryCall("controller.configure",1,absent,out call,out error),Is.True,error);Assert.That(call.Definition.Module.CanRun(new CapabilityContext(editor,animations),absent,out error),Is.False);StringAssert.Contains("existing readable",error);
            controls.BindButton(1,ControllerCommand.Sequence,new string('f',32));Assert.That(JToken.DeepEquals(ControlFact(),current),Is.True,"Manual and agent entries share missing-program validation");yield return null;
        }
        [UnityTest] public IEnumerator FailedControlSaveKeepsAcceptedIdentityAndAllowsExplicitRetry()
        {
            var controls=BindingControls();Assert.That(controls.Apply(controls.Preferences),Is.True);var before=ControlFact();var args=MovementArgs();string path=Path.Combine(directory,"controls.v2.json"),pending=path+".pending",original=File.ReadAllText(path);Directory.CreateDirectory(pending);
            try{Assert.That(controlActions.Execute(ControlRequest(args),out _),Is.False);yield return null;Assert.That((string)controlActions.Observe()["selected"]["phase"],Is.EqualTo("failed"));Assert.That(JToken.DeepEquals(ControlFact(),before),Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(original));}
            finally{Directory.Delete(pending);}
            yield return ConfigureControls(args);Assert.That((string)ControlFact()["configurationId"],Is.Not.EqualTo((string)before["configurationId"]));
        }
        [UnityTest] public IEnumerator ControlFactsFitAllFourProgramBindingsAndRestartInvalidatesOldConfiguration()
        {
            var controls=BindingControls();for(int i=0;i<4;i++)controls.BindButton(i,ControllerCommand.Sequence,sequenceId);
            var before=ControlFact();Assert.That(((JArray)before["buttons"]).All(b=>(string)b["programId"]==sequenceId),Is.True);var stale=MovementArgs();
            UnityEngine.Object.Destroy(controls);yield return null;controls=BindingControls();var after=ControlFact();Assert.That(JToken.DeepEquals(before["buttons"],after["buttons"]),Is.True);Assert.That((string)before["configurationId"],Is.Not.EqualTo((string)after["configurationId"]));
            Assert.That(BehaviourCatalog.TryCall("controller.configure",1,stale,out var call,out _),Is.True);Assert.That(call.Definition.Module.CanRun(new CapabilityContext(editor,animations),stale,out _),Is.False);Assert.That(controls.UserEnabled||controls.AvatarEnabled,Is.False);
        }
        [UnityTest] public IEnumerator ControllerConfigurationRespectsPreservationAndKeepsItsSeparateSavedBoundary()
        {
            var controls=BindingControls();var before=ControlFact();var next=controls.Preferences;next.deadZone=.35f;
            using(editor.RuntimeGate.Hold("Review room first")){Assert.That(controls.Apply(next),Is.True,"Activity review still permits manual configuration");Assert.That(controls.AvatarEnabled||controls.UserEnabled,Is.False);}
            before=ControlFact();
            string error;using(editor.WriteGate.TryFreeze(out error)){Assert.That(error,Is.Null);Assert.That(controls.Apply(next),Is.False);Assert.That(JToken.DeepEquals(ControlFact(),before),Is.True);}
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            yield return ConfigureControls(MovementArgs());Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(controls.Preferences.userSpeed,Is.EqualTo(.8f));Assert.That(new ControllerPreferenceStorage(directory).Load(out error).userSpeed,Is.EqualTo(.8f));Assert.That(error,Is.Null);
        }
        [UnityTest] public IEnumerator UnavailablePreferenceStorageIsExplicitAndCannotBeOverwrittenBySharedOrManualEdits()
        {
            var controls=BindingControls();Assert.That(controls.Apply(controls.Preferences),Is.True);var before=ControlFact();string file=Path.Combine(directory,"controls.v2.json"),original=File.ReadAllText(file);
            File.WriteAllText(Path.Combine(directory,"controls.v99.json"),"future format");var args=MovementArgs();Assert.That(controlActions.Execute(ControlRequest(args),out _),Is.False);yield return null;
            var after=ControlFact();Assert.That((bool)after["storageReady"],Is.False);Assert.That((string)after["configurationId"],Is.EqualTo((string)before["configurationId"]));Assert.That(File.ReadAllText(file),Is.EqualTo(original));
            Assert.That(controls.Apply(controls.Preferences),Is.False);UnityEngine.Object.Destroy(controls);yield return null;controls=BindingControls();Assert.That((bool)ControlFact()["storageReady"],Is.False);Assert.That(File.ReadAllText(file),Is.EqualTo(original));
        }
    }
}
