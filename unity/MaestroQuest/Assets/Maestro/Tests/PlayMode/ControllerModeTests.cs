// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        RoomExecutions modeActions;
        RoomRules modeRules;
        MovementControls SharedModes(out VirtualRoomView view,out Transform origin)
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(10,.2f,10));Tutor();Ready();
            var workshop=root.AddComponent<RuleWorkshop>();workshop.Initialize(editor,directory);Assert.That(workshop.Memory.Initialization.Wait(TimeSpan.FromSeconds(5)),Is.True,"Memory fixture did not load");Assert.That(workshop.Memory.Error,Is.Null);
            modeRules=root.AddComponent<RoomRules>();modeRules.Initialize(workshop,editor,authoring,null,room,null);
            var controls=Controls(out view,out origin,modeRules,workshop);modeActions=new RoomExecutions(editor);return controls;
        }
        JObject ModeFact()
        {
            Assert.That(BehaviourCatalog.TryRead("controller.mode",1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        JObject ModeRequest(string operation)=>new() {["operation"]="start",["runId"]=modeActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="controller.mode.set",["version"]=1,["arguments"]=new JObject {["operation"]=operation,["stateId"]=ModeFact()["stateId"].DeepClone()}}};
        IEnumerator ChangeMode(string operation)
        {
            Assert.That(modeActions.Execute(ModeRequest(operation),out var error),Is.True,error);yield return null;
            Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),modeActions.Observe().ToString());
        }
        [UnityTest] public IEnumerator SharedModesMoveIndependentTargetsOnlyAfterNeutralWithoutMovingTracking()
        {
            var controls=SharedModes(out var view,out var origin);var before=ModeFact();var home=origin.localPosition;var rotation=origin.localRotation;
            frame.rightStick=Vector2.up;frame.leftStick=Vector2.right;
            yield return ChangeMode("maestro.enable");var enable=modeActions.Observe().DeepClone();yield return new WaitForSeconds(.15f);
            Assert.That(motion.Active,Is.False);Assert.That(avatar.transform.position.z,Is.Zero);
            frame.rightStick=Vector2.zero;yield return null;frame.rightStick=Vector2.up;yield return new WaitForSeconds(.3f);
            Assert.That(avatar.transform.position.z,Is.GreaterThan(.1f));Assert.That(origin.localPosition,Is.EqualTo(home));
            world.PausePhysics();yield return ChangeMode("view.virtual");var virtualView=modeActions.Observe().DeepClone();Assert.That(view.Active,Is.True);Assert.That(controls.UserEnabled,Is.False);
            yield return ChangeMode("user.enable");var user=modeActions.Observe().DeepClone();yield return new WaitForSeconds(.1f);Assert.That(origin.localPosition,Is.EqualTo(home));
            frame.leftStick=frame.rightStick=Vector2.zero;yield return null;frame.leftStick=Vector2.right;yield return new WaitForSeconds(.3f);
            Assert.That(root.transform.position.x,Is.LessThan(-.05f));Assert.That(origin.localPosition,Is.EqualTo(home));var walked=viewer.transform.position;frame.leftStick=Vector2.zero;frame.a=true;yield return null;
            Assert.That(Quaternion.Angle(root.transform.rotation,rotation),Is.EqualTo(30).Within(.1f));Assert.That(Vector3.Distance(viewer.transform.position,walked),Is.LessThan(.001f),"Snap turn pivots around the viewer");
            yield return ChangeMode("view.mixedReality");var mixed=modeActions.Observe().DeepClone();var after=ModeFact();
            Assert.That(view.Active||controls.UserEnabled||controls.AvatarEnabled||motion.Active||world.Running,Is.False);
            Assert.That(origin.localPosition,Is.EqualTo(home));Assert.That(Quaternion.Angle(origin.localRotation,rotation),Is.LessThan(.001f));Assert.That(viewer.GetComponent<Camera>().backgroundColor.a,Is.Zero);
            Assert.That(modeRules.Scheduler.RunningCount,Is.Zero,"View transitions must complete, not cancel their own invocation");
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_CONTROLLER_MODES");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"modes.json"),new JObject {["before"]=before,["enable"]=enable,["virtualView"]=virtualView,["user"]=user,["mixed"]=mixed,["after"]=after}.ToString());}
        }
        [UnityTest] public IEnumerator ModeIdentityRejectsManualReversalsRebindingRecoveryAndDuplicateReenable()
        {
            var controls=SharedModes(out _,out _);var stale=ModeRequest("view.virtual");controls.Recover();Assert.That(modeActions.Execute(stale,out _),Is.False,"Recall while already off still invalidates an older enable request");
            stale=ModeRequest("view.virtual");controls.ToggleAvatar();controls.ToggleAvatar();
            Assert.That(modeActions.Execute(stale,out var error),Is.False);StringAssert.Contains("changed",error);
            stale=ModeRequest("view.virtual");controls.SwapSticks();Assert.That(modeActions.Execute(stale,out error),Is.False);
            var request=ModeRequest("view.virtual");Assert.That(modeActions.Execute(request,out error),Is.True,error);yield return null;
            string notified=null;Action observed=()=>notified=(string)ModeFact()["stateId"];controls.Changed+=observed;controls.Recover();controls.Changed-=observed;var recovered=ModeFact();Assert.That(notified,Is.EqualTo((string)recovered["stateId"]),"Change listeners must observe the final recovery identity");Assert.That(modeActions.Execute(request,out error),Is.True,error);Assert.That(JToken.DeepEquals(recovered,ModeFact()),Is.True);Assert.That(controls.Virtual,Is.False,"A replayed completion cannot enter Virtual again after Recall");
            stale=ModeRequest("view.virtual");controls.SendMessage("OnApplicationFocus",false);controls.SendMessage("OnApplicationFocus",true);Assert.That(modeActions.Execute(stale,out _),Is.False);
            stale=ModeRequest("view.virtual");tracked=false;yield return null;tracked=true;Assert.That(modeActions.Execute(stale,out _),Is.False);
        }
        [UnityTest] public IEnumerator SharedModeRefusesOtherActorsAndBusyInputWithoutStoppingThem()
        {
            var controls=SharedModes(out var view,out _);
            Assert.That(motion.Begin("existing follow",Avatar.AvatarSpatialMode.Follow,out var error),Is.True,error);
            Assert.That(modeActions.Execute(ModeRequest("view.virtual"),out error),Is.False);StringAssert.Contains("Stop the current",error);Assert.That(motion.OwnedBy("existing follow"),Is.True);Assert.That(view.Active,Is.False);motion.Stop();
            var follow=new JObject {["operation"]="start",["call"]=new JObject {["id"]="avatar.follow.user",["version"]=1,["arguments"]=new JObject {["target"]="maestro",["seconds"]=10}}};
            Assert.That(modeActions.Execute(follow,out error),Is.True,error);yield return null;string followId=(string)modeActions.Observe()["selected"]["id"];
            Assert.That(modeActions.Execute(ModeRequest("view.virtual"),out _),Is.False);Assert.That(modeRules.Scheduler.RunningCount,Is.EqualTo(1));
            // Safe disable can run without requiring an unrelated action to finish.
            yield return ChangeMode("user.disable");Assert.That(modeRules.Scheduler.RunningCount,Is.EqualTo(1));modeRules.StopAll();
            view.enabled=false;Assert.That(modeActions.Execute(ModeRequest("view.virtual"),out _),Is.False);Assert.That(controls.Virtual,Is.False);view.enabled=true;
            frame.busy=true;Assert.That(modeActions.Execute(ModeRequest("view.virtual"),out error),Is.False);StringAssert.Contains("Release",error);frame.busy=false;
            using(editor.RuntimeGate.Hold("Review alignment")){Assert.That(modeActions.Execute(ModeRequest("view.virtual"),out _),Is.False);Assert.That(controls.Virtual,Is.False);}
        }
        [UnityTest] public IEnumerator SharedModesRequireExplicitVirtualViewAndBoundStickAndDoNotResumeAfterPause()
        {
            var controls=SharedModes(out var view,out var origin);
            Assert.That(modeActions.Execute(ModeRequest("user.enable"),out var error),Is.False);StringAssert.Contains("Virtual view",error);
            var prefs=controls.Preferences;prefs.avatarStick=MovementStick.None;Assert.That(controls.Apply(prefs),Is.True);
            Assert.That(modeActions.Execute(ModeRequest("maestro.enable"),out error),Is.False);StringAssert.Contains("binding",error);
            world.PausePhysics();yield return ChangeMode("view.virtual");yield return ChangeMode("user.enable");frame.leftStick=Vector2.right;yield return new WaitForSeconds(.2f);Assert.That(root.transform.position.x,Is.LessThan(0));Assert.That(origin.position,Is.EqualTo(Vector3.zero));
            controls.SendMessage("OnApplicationPause",true);Assert.That(view.Active||controls.UserEnabled,Is.False);Assert.That(origin.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(modeActions.Execute(ModeRequest("view.virtual"),out _),Is.False);controls.SendMessage("OnApplicationPause",false);yield return null;Assert.That(controls.UserEnabled||controls.Virtual,Is.False);
        }
        [UnityTest] public IEnumerator SameModeIsInertAndHeldTurnNeedsReleaseAfterActivation()
        {
            SharedModes(out _,out var origin);frame.a=true;world.PausePhysics();yield return ChangeMode("view.virtual");yield return ChangeMode("user.enable");yield return null;
            Assert.That(Quaternion.Angle(origin.rotation,Quaternion.identity),Is.LessThan(.001f));var before=ModeFact();
            yield return ChangeMode("user.enable");Assert.That(JToken.DeepEquals(before,ModeFact()),Is.True,"An already satisfied request does not reset gates or identities");
            frame.a=false;yield return null;frame.a=true;yield return null;Assert.That(Quaternion.Angle(root.transform.rotation,Quaternion.identity),Is.EqualTo(30).Within(.1f));
        }
    }
}
