// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Book;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest.Tests
{
    // Coroutines resume before LateUpdate. Read the pose after avatar layers,
    // gaze and retargeting, at the same stage consumed by rendering.
    [DefaultExecutionOrder(500)]
    public sealed class AvatarChannelProbe:MonoBehaviour
    {
        public Action Sample;
        void LateUpdate()=>Sample?.Invoke();
    }
    public sealed partial class AvatarSpatialTests
    {
        GameObject root, physicalRoot, viewer;
        RoomPhysicsWorld world;
        RoomNavigation navigation;
        RoomInteraction room;
        RoomEditor editor;
        AnimationWorkshop authoring;
        MaestroAvatar avatar;
        AvatarSpatialMotion motion;
        string directory;
        bool tracked = true;
        float captureDelta;
        [UnitySetUp] public IEnumerator Setup()
        {
            captureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f/72;
            directory = Path.Combine(Path.GetTempPath(),"MaestroSpatialTests-"+Guid.NewGuid().ToString("N"));
            physicalRoot=new GameObject("Physical spatial test");physicalRoot.AddComponent<XRInteractionManager>();
            root=new GameObject("Spatial room test");root.transform.SetParent(physicalRoot.transform,false);
            world=physicalRoot.AddComponent<RoomPhysicsWorld>();navigation=physicalRoot.AddComponent<RoomNavigation>();navigation.Initialize(world);
            RoomPhysicsLayers.Configure(); tracked = true; yield return null;
        }
        GameObject Surface(Vector3 position,Vector3 scale)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.transform.SetParent(physicalRoot.transform,false);
            value.transform.position = position; value.transform.localScale = scale; value.layer = RoomPhysicsLayers.Scanned; return value;
        }
        void Ready()
        {
            Physics.SyncTransforms(); world.SetSurfaces(true,"Synthetic aligned scan"); world.StartPhysics();
            Assert.That(navigation.Prepare(.25f,1.7f,out var error),Is.True,error);
        }
        void Tutor()
        {
            room = root.AddComponent<RoomInteraction>(); viewer = new GameObject("Tracked test viewer"); viewer.transform.position = new Vector3(0,1.6f,3); room.Viewer = viewer.transform;
            RoomItem Included(string name,Vector3 position)
            {
                var value = new GameObject(name); value.transform.SetParent(root.transform,false); value.transform.position = position;
                var collider = value.AddComponent<BoxCollider>(); collider.size = Vector3.one*.1f;
                var item = value.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item;
            }
            var book = Included("book",new Vector3(2,1,2)); var tutor = Included("maestro",Vector3.zero); avatar = tutor.gameObject.AddComponent<MaestroAvatar>();
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory,world);
            authoring = root.AddComponent<AnimationWorkshop>(); authoring.Initialize(editor);
            motion = tutor.gameObject.AddComponent<AvatarSpatialMotion>(); motion.Initialize(editor,authoring,room,navigation,() => tracked);
        }
        [UnityTest] public IEnumerator UpperBodyGestureCooperatesWithWalkingAndGazeAndStopsIndependently()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(12,.2f,12));Tutor();Ready();
            viewer.transform.position=new Vector3(1.5f,1.6f,5);
            var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);
            Newtonsoft.Json.Linq.JObject Call(string id)=>new() {["id"]=id,["version"]=1,
                ["arguments"]=new Newtonsoft.Json.Linq.JObject {["target"]="maestro",["seconds"]=10}};
            var follow=Call("avatar.follow.user");
            var wave=Call("animation.play");wave["arguments"]["source"]=Newtonsoft.Json.Linq.JObject.Parse("{\"kind\":\"gesture\",\"gesture\":\"greeting\"}");wave["arguments"]["channel"]="upperBody";
            Assert.That(scheduler.Invoke(follow,0,out var walking,out var error),Is.True,error);
            var hand=avatar.PoseRig.CanonicalBone(PoseJoint.RightUpperArm);
            var foot=avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperLeg);
            var head=avatar.PoseRig.CanonicalBone(PoseJoint.Head);
            Quaternion shownArm=Quaternion.identity,shownLeg=Quaternion.identity,shownHead=Quaternion.identity;
            var probe=avatar.gameObject.AddComponent<AvatarChannelProbe>();Action capture=null;
            probe.Sample=()=>{shownArm=hand.localRotation;shownLeg=foot.localRotation;shownHead=head.rotation;var pending=capture;capture=null;pending?.Invoke();};
            yield return new WaitForSeconds(.25f);
            var handBefore=shownArm;var footBefore=shownLeg;
            var start=avatar.transform.position;
            Assert.That(scheduler.Invoke(wave,0,out var waving,out error),Is.True,error);
            yield return new WaitForSeconds(.4f);
            Assert.That(motion.OwnedBy(walking),Is.True);Assert.That(avatar.UpperBodyOwnedBy(waving),Is.True);
            Assert.That(Vector3.Distance(start,avatar.transform.position),Is.GreaterThan(.1f),motion.Status);
            Assert.That(Quaternion.Angle(handBefore,shownArm),Is.GreaterThan(70),"A greeting must raise the arm, not merely restore the bind pose");
            var reference=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Avatars/DefaultMaestro"),root.transform);
            if(!reference.TryGetComponent<Animator>(out var referenceAnimator))referenceAnimator=reference.AddComponent<Animator>();referenceAnimator.enabled=false;
            var clip=System.Linq.Enumerable.First(Resources.Load<RuntimeAnimatorController>("Avatars/MaestroAnimations").animationClips,x=>x.name.Split('|')[^1]=="Greeting");
            clip.SampleAnimation(reference,.4f);
            var referenceArm=System.Linq.Enumerable.Single(reference.GetComponentsInChildren<Transform>(),x=>x.name==PoseJoint.RightUpperArm.ToString());
            Assert.That(Quaternion.Angle(referenceArm.localRotation,shownArm),Is.LessThan(5),"The composed arm must match the actual authored clip");
            reference.SetActive(false);UnityEngine.Object.Destroy(reference);
            Assert.That(Quaternion.Angle(footBefore,shownLeg),Is.GreaterThan(2),"The base gait keeps animating");
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_CHANNEL_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)) {
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence,"walking-and-wave.json"),new Newtonsoft.Json.Linq.JObject {
                    ["execution"]=scheduler.ObserveInvocations(waving),["distanceMoved"]=Vector3.Distance(start,avatar.transform.position),
                    ["armDegrees"]=Quaternion.Angle(handBefore,shownArm),["legDegrees"]=Quaternion.Angle(footBefore,shownLeg)
                }.ToString());
                bool captured=false;capture=()=>{CaptureLayer(Path.Combine(evidence,"walking-and-wave.png"));captured=true;};
                yield return new WaitUntil(()=>captured);
            }
            var atStop=avatar.transform.position;
            Assert.That(scheduler.CancelInvocation(waving,out _),Is.True);
            Assert.That(avatar.transform.position,Is.EqualTo(atStop),"Stopping arms cannot restore an old room placement");
            Assert.That(motion.OwnedBy(walking),Is.True);
            yield return new WaitForSeconds(.2f);Assert.That(Vector3.Distance(atStop,avatar.transform.position),Is.GreaterThan(.05f));
            Assert.That(scheduler.Invoke(wave,1,out waving,out error),Is.True,error);
            Assert.That(scheduler.CancelInvocation(walking,out _),Is.True);Assert.That(avatar.UpperBodyOwnedBy(waving),Is.True);
            var stopped=avatar.transform.position;yield return new WaitForSeconds(.2f);Assert.That(avatar.transform.position,Is.EqualTo(stopped));
            var headBefore=shownHead;
            var look=Call("avatar.look.user");viewer.transform.position=avatar.transform.position+new Vector3(2,1.6f,2);
            Assert.That(scheduler.Invoke(look,2,out var looking,out error),Is.True,error);
            yield return new WaitForSeconds(.5f);
            Assert.That(Quaternion.Angle(headBefore,shownHead),Is.GreaterThan(10),"Gaze still turns the displayed head during a gesture");
            scheduler.StopTarget("maestro",true);
            Assert.That(avatar.UpperBodyActive,Is.False);Assert.That(motion.Active,Is.False);
            Assert.That(scheduler.RunningCount,Is.Zero);
            Assert.That(motion.Begin("direct",AvatarSpatialMode.Follow,out error),Is.True,error);
            Assert.That(scheduler.Invoke(wave,3,out waving,out error),Is.True,error);
            var fullBody=Call("animation.play");fullBody["arguments"]["source"]=Newtonsoft.Json.Linq.JObject.Parse("{\"kind\":\"gesture\",\"gesture\":\"greeting\"}");fullBody["arguments"]["channel"]="wholeTarget";
            Assert.That(scheduler.Invoke(fullBody,3,out _,out error),Is.False,"Programs cannot steal direct controller/tool movement");
            Assert.That(motion.OwnedBy("direct"),Is.True);
            Assert.That(scheduler.Invoke(look,3,out _,out error),Is.False);Assert.That(motion.OwnedBy("direct"),Is.True);
            Assert.That(RoomControls.AvatarMotion(editor,"stop",out _),Is.True);Assert.That(avatar.UpperBodyOwnedBy(waving),Is.True);
            scheduler.StopAll();
        }
        [UnityTest] public IEnumerator DirectTakeoverPreservesArmsAndInvalidOrRepeatedCommandsDoNotLeakOwnership()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(12,.2f,12));Tutor();Ready();yield return null;
            Assert.That(System.Linq.Enumerable.Any(editor.Ownership.Observe().owners,x=>x.role=="ambient"),Is.True);
            var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);
            Newtonsoft.Json.Linq.JObject Call(string id)=>new(){["id"]=id,["version"]=1,["arguments"]=new Newtonsoft.Json.Linq.JObject{["target"]="maestro",["seconds"]=10}};
            var wave=Call("animation.play");wave["arguments"]["source"]=Newtonsoft.Json.Linq.JObject.Parse("{\"kind\":\"gesture\",\"gesture\":\"greeting\"}");wave["arguments"]["channel"]="upperBody";
            Assert.That(scheduler.Invoke(Call("avatar.follow.user"),0,out var walking,out var error),Is.True,error);
            Assert.That(scheduler.Invoke(wave,0,out var waving,out error),Is.True,error);
            tracked=false;Assert.That(motion.Begin("direct",AvatarSpatialMode.Look,out _),Is.False);Assert.That(motion.OwnedBy(walking),Is.True);
            tracked=true;Assert.That(motion.Begin("direct",AvatarSpatialMode.Look,out error),Is.True,error);
            Assert.That(avatar.UpperBodyOwnedBy(waving),Is.True);Assert.That(scheduler.RunningCount,Is.EqualTo(1));
            Assert.That(motion.Begin("direct",AvatarSpatialMode.Follow,out error),Is.True,error);
            Assert.That(editor.Ownership.Covers("direct",new[]{new Maestro.Quest.Programs.BehaviourCatalog.Claim("maestro","locomotion")}),Is.True);
            Assert.That(scheduler.Invoke(Call("avatar.look.user"),0,out _,out _),Is.False);
            editor.SendMessage("OnApplicationFocus",false);Assert.That(motion.Active,Is.False);Assert.That(scheduler.RunningCount,Is.Zero);
            Assert.That(editor.Ownership.Observe().owners,Is.Empty);
            editor.SendMessage("OnApplicationFocus",true);yield return null;Assert.That(motion.Active,Is.False);Assert.That(avatar.UpperBodyActive,Is.False);
            authoring.TogglePose();Assert.That(authoring.IsPosing,Is.True);tracked=false;
            Assert.That(RoomControls.AvatarMotion(editor,"look",out _),Is.False);Assert.That(authoring.IsPosing,Is.True,"An unavailable control must not cancel authoring");
            tracked=true;Assert.That(RoomControls.AvatarMotion(editor,"look",out error),Is.True,error);Assert.That(authoring.IsPosing,Is.False);motion.Stop();
            var evidence=Environment.GetEnvironmentVariable("MAESTRO_OWNERSHIP_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"native-ownership.json"),JsonUtility.ToJson(editor.Ownership.Observe()));}
        }
        [UnityTest] public IEnumerator SuccessfulDirectTakeoverRetiresQueuedMovementButRefusedInputLeavesItIntact()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(12,.2f,12));Tutor();Ready();
            var workshop=root.AddComponent<RuleWorkshop>();workshop.Initialize(editor,directory);
            var runtime=root.AddComponent<RoomRules>();runtime.Initialize(workshop,editor,authoring,null,room,null);
            var program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(new RuleStep{action=RuleActionKind.FollowUser,targetId="maestro",seconds=10});
            var first=new RuleSequence{id=Guid.NewGuid().ToString("N"),name="Following",program=program};
            var queued=new RuleSequence{id=Guid.NewGuid().ToString("N"),name="Queued movement",program=program,interruption=RuleInterruption.QueueLatest};
            runtime.Scheduler.Configure(new RuleDocument{sequences=new[]{first,queued}});
            Assert.That(runtime.Scheduler.Trigger(first.id,Time.unscaledTime),Is.True,runtime.Scheduler.LastError);
            Assert.That(runtime.Scheduler.Trigger(queued.id,Time.unscaledTime),Is.True);Assert.That(runtime.Scheduler.QueuedCount,Is.EqualTo(1));
            tracked=false;Assert.That(RoomControls.AvatarMotion(editor,"look",out _),Is.False);Assert.That(runtime.Scheduler.QueuedCount,Is.EqualTo(1));
            tracked=true;Assert.That(RoomControls.AvatarMotion(editor,"look",out var error),Is.True,error);Assert.That(runtime.Scheduler.QueuedCount,Is.Zero);
            motion.Stop();yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(motion.Active,Is.False,"Old queued movement cannot resume after direct controls end");
        }
        void CaptureLayer(string path)
        {
            var cameraRoot=new GameObject("Layer verification camera");cameraRoot.transform.SetParent(root.transform,false);
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.89f,.91f);
            cameraRoot.transform.position=avatar.transform.position+new Vector3(1.7f,1.4f,2.7f);
            cameraRoot.transform.LookAt(avatar.transform.position+Vector3.up*.9f);camera.fieldOfView=42;
            var render=new RenderTexture(900,1000,24);var pixels=new Texture2D(900,1000,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try {camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,900,1000),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally {RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraRoot);}
        }
        [UnityTest] public IEnumerator UpperBodyLayerDoesNotAccumulateIntoASavedPoseAndManualAuthoringTakesOver()
        {
            Tutor();avatar.SetEditing(true);avatar.Gesture("Idle");yield return null;
            var pose=avatar.PoseRig.Capture();avatar.SetEditing(false);avatar.SetSavedPose(pose);
            var rootPose=avatar.transform.position;var leg=avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperLeg);
            var legRotation=leg.localRotation;var spine=avatar.PoseRig.CanonicalBone(PoseJoint.Spine);var spineRotation=spine.localRotation;
            Assert.That(avatar.BeginUpperBody("saved-pose","Greeting"),Is.True);
            yield return new WaitForSeconds(.5f);avatar.EndUpperBody("wrong-owner");Assert.That(avatar.UpperBodyActive,Is.True);
            avatar.EndUpperBody("saved-pose");Assert.That(avatar.UpperBodyActive,Is.False);
            Assert.That(Quaternion.Angle(spineRotation,spine.localRotation),Is.LessThan(.01f),"Restore the input pose, not the previous layered frame");
            Assert.That(Quaternion.Angle(legRotation,leg.localRotation),Is.LessThan(.01f));Assert.That(avatar.transform.position,Is.EqualTo(rootPose));
            Assert.That(avatar.BeginUpperBody("manual-takeover","Pointing"),Is.True);
            yield return null;avatar.SetEditing(true);Assert.That(avatar.UpperBodyActive,Is.False);
            avatar.SetEditing(false);yield return null;Assert.That(avatar.UpperBodyActive,Is.False);
        }
        [UnityTest] public IEnumerator AgentAndPhysicalAvatarControlsShareMovementPreferencesStopAndLiveStatus()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(8,.2f,8));Tutor();
            var agent=new RoomAgentExecutor(editor);
            bool Run(RoomAgentCommand command) => agent.Execute(new RoomAgentRequest {version=2,sceneRevision=editor.Revision,
                conditions=new[] {new RoomObjectCondition {id="maestro",revision=editor.ObjectRevision("maestro")}},commands=new[] {command}},out _,out _);
            Assert.That(Run(new RoomAgentCommand {action="avatarMotion",target="maestro",operation="follow"}),Is.False);
            Assert.That(motion.Active,Is.False);Assert.That(RoomControls.ObserveAvatar(editor).canFollow,Is.False);Ready();
            Assert.That(Run(new RoomAgentCommand {action="avatarSettings",target="maestro",movement=new AvatarMovementSettings {distance=1.8f,speed=1}}),Is.True);
            Assert.That(motion.Distance,Is.EqualTo(1.8f));editor.Undo();Assert.That(motion.Distance,Is.EqualTo(1.3f));
            var stale=editor.ObjectRevision("maestro");editor.SetAvatarMovement(1.8f,1);
            Assert.That(agent.Execute(new RoomAgentRequest {version=2,conditions=new[] {new RoomObjectCondition {id="maestro",revision=stale}},commands=new[] {
                new RoomAgentCommand {action="avatarSettings",target="maestro",movement=new AvatarMovementSettings {distance=1,speed=.5f}}}},out _,out _),Is.False);
            Assert.That(motion.Distance,Is.EqualTo(1.8f));
            var board=new GameObject("Shared movement tray");board.transform.SetParent(root.transform,false);board.transform.position=new Vector3(20,0,0);
            board.AddComponent<AvatarSpatialTools>().Build(motion,editor,authoring,null,room);
            var stop=Array.Find(board.GetComponentsInChildren<RuleToolAction>(),tool=>tool.AccessibleName=="Stop");
            Assert.That(Run(new RoomAgentCommand {action="avatarMotion",target="maestro",operation="follow"}),Is.True);
            yield return new WaitForSeconds(.4f);Assert.That(avatar.transform.position.z,Is.GreaterThan(.1f));
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var state=observer.Observe();
            Assert.That(state.avatar.active,Is.True);Assert.That(state.avatar.mode,Is.EqualTo("follow"));
            Assert.That(state.objects[Array.FindIndex(state.objects,x=>x.id=="maestro")].position,Is.EqualTo(avatar.transform.localPosition));
            var evidence=Environment.GetEnvironmentVariable("MAESTRO_CONTROL_EVIDENCE");if(!string.IsNullOrEmpty(evidence)) {Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"native-avatar-state.json"),RoomAgentWire.Serialize(state));}
            // Exercise the same ray/click/release path used for a physical tool.
            var router=root.AddComponent<BookPointerRouter>();Physics.SyncTransforms();var ray=new Ray(stop.transform.position-stop.transform.forward*.3f,stop.transform.forward);
            Assert.That(router.Begin(901,ray),Is.True);router.End(901,ray);Assert.That(motion.Active,Is.False);
            Array.Find(board.GetComponentsInChildren<RuleToolAction>(),tool=>tool.AccessibleName=="Look at me").Command();Assert.That(motion.Active,Is.True);
            Assert.That(Run(new RoomAgentCommand {action="avatarMotion",target="maestro",operation="stop"}),Is.True);Assert.That(motion.Active,Is.False);
            Assert.That(Run(new RoomAgentCommand {action="avatarMotion",target="maestro",operation="follow"}),Is.True);
            tracked=false;yield return null;Assert.That(RoomControls.ObserveAvatar(editor).active,Is.False);Assert.That(RoomControls.ObserveAvatar(editor).canLook,Is.False);
            editor.Create(RoomObjectKind.Ball);authoring.ToggleRecord();Assert.That(authoring.IsRecording,Is.True);
            Assert.That(Run(new RoomAgentCommand {action="avatarMotion",target="maestro",operation="stop"}),Is.True);
            Assert.That(authoring.IsRecording,Is.True,"Stopping Maestro must preserve recording of another object");authoring.Stop();
        }
        [UnityTest] public IEnumerator NavigationRoutesAroundWallsAndRejectsDisconnectedFloorAndLostScan()
        {
            var floor = Surface(new Vector3(0,-.1f,0),new Vector3(8,.2f,8));
            var wall = Surface(new Vector3(0,1.5f,0),new Vector3(.15f,3,2)); Ready();
            var path = new NavMeshPath(); Assert.That(navigation.Path(new Vector3(-2,0,0),new Vector3(2,0,0),path),Is.True);
            Assert.That(path.corners.Length,Is.GreaterThan(2));
            bool detoured = false; foreach (var corner in path.corners) detoured |= Mathf.Abs(corner.z)>1.1f;
            Assert.That(detoured,Is.True,"The route must clear the scanned wall with body radius");
            world.SetSurfaces(false,"Room scan replaced"); Assert.That(navigation.Ready,Is.False);
            UnityEngine.Object.Destroy(floor); UnityEngine.Object.Destroy(wall); yield return null;
            Surface(new Vector3(-3,-.1f,0),new Vector3(2,.2f,2)); Surface(new Vector3(3,-.1f,0),new Vector3(2,.2f,2)); Ready();
            Assert.That(navigation.Path(new Vector3(-3,0,0),new Vector3(3,0,0),path),Is.False);
            world.SetSurfaces(false,"Tracking alignment unavailable");
            Assert.That(navigation.Prepare(.25f,1.7f,out _),Is.False);
        }
        [UnityTest] public IEnumerator SmallAvatarCanClearTheBookAndMovementToolsControlPreviewAndStop()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(8,.2f,8)); Tutor(); Ready();
            var book = editor.Find("book"); book.gameObject.layer = RoomPhysicsLayers.Environment;
            book.transform.position = new Vector3(0,1,.6f); book.GetComponent<BoxCollider>().size = new Vector3(.8f,.4f,.15f);
            Physics.SyncTransforms();
            Assert.That(motion.Begin("large",AvatarSpatialMode.Follow,out var error),Is.True,error);
            yield return new WaitForSeconds(1);
            Assert.That(motion.Status,Does.Contain("blocked by book")); var stoppedAt = avatar.transform.position.z;
            Assert.That(editor.SetAvatarSize(.35f),Is.True); Assert.That(motion.Active,Is.False);
            Assert.That(motion.Begin("small",AvatarSpatialMode.Follow,out error),Is.True,error);
            yield return new WaitForSeconds(1.5f);
            Assert.That(avatar.transform.position.z,Is.GreaterThan(stoppedAt+.4f),"A smaller avatar must use its own body clearance"); motion.Stop();
            var board = new GameObject("Movement tray"); board.transform.SetParent(root.transform,false);
            board.AddComponent<AvatarSpatialTools>().Build(motion,editor,authoring,null,room);
            var tools = board.GetComponentsInChildren<RuleToolAction>();
            RuleToolAction Find(string label) => Array.Find(tools,tool => tool.AccessibleName == label);
            Find("Preview walk").Command(); Assert.That(authoring.ControlsTarget("maestro"),Is.True); Assert.That(motion.Active,Is.False);
            Find("Stop").Command(); Assert.That(authoring.ControlsTarget("maestro"),Is.False);
            Find("Size").Command(); Assert.That(editor.Read("maestro").scale,Is.EqualTo(.5f));
        }
        [UnityTest] public IEnumerator GazeTurnsHeadAndTrackingAuthoringAndRecoveryInterruptOwnership()
        {
            Tutor(); viewer.transform.position = new Vector3(2,1.6f,3);
            avatar.SetEditing(true); avatar.Gesture("Idle"); yield return null;
            var head = avatar.PoseRig.CanonicalBone(PoseJoint.Head); var start = head.rotation;
            Assert.That(motion.Begin("first",AvatarSpatialMode.Look,out var error),Is.True,error);
            yield return new WaitForSeconds(.65f);
            Assert.That(Quaternion.Angle(start,head.rotation),Is.InRange(10,80));
            Assert.That(motion.Begin("second",AvatarSpatialMode.Look,out _),Is.True); motion.End("first"); Assert.That(motion.Active,Is.True);
            tracked = false; yield return null; Assert.That(motion.Active,Is.False); Assert.That(motion.Begin("lost",AvatarSpatialMode.Look,out _),Is.False);
            tracked = true; Assert.That(motion.Begin("recording",AvatarSpatialMode.Look,out _),Is.True);
            editor.Select(editor.Find("maestro")); authoring.ToggleRecord(); Assert.That(motion.Active,Is.False);
            Assert.That(motion.Begin("blocked",AvatarSpatialMode.Look,out _,RoomActorRole.Program),Is.False,"Program movement cannot override recording"); Assert.That(authoring.IsRecording,Is.True); authoring.Stop();
            Assert.That(motion.Begin("recovery",AvatarSpatialMode.Look,out _),Is.True); room.RestoreInFrontOfViewer(); Assert.That(motion.Active,Is.True,"Tool recall must leave Maestro active");
            Assert.That(motion.Begin("pause",AvatarSpatialMode.Look,out _),Is.True); motion.SendMessage("OnApplicationPause",true); Assert.That(motion.Active,Is.False);
            motion.SendMessage("OnApplicationPause",false); yield return null; Assert.That(motion.Active,Is.False,"Resume never starts movement automatically");
        }
        [UnityTest] public IEnumerator FollowingWalksOnTheFloorKeepsDistanceAndSavesPlacementAndPreferences()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(8,.2f,8)); Tutor();
            Assert.That(motion.Begin("no scan",AvatarSpatialMode.Follow,out _),Is.False); Ready();
            editor.SetAvatarMovement(1.3f,.65f);
            Assert.That(motion.Begin("following",AvatarSpatialMode.Follow,out var error),Is.True,error);
            var leg = avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperLeg); var start = leg.localRotation;
            yield return new WaitForSeconds(.35f); Assert.That(Quaternion.Angle(start,leg.localRotation),Is.GreaterThan(2),"Walking must animate the legs");
            yield return new WaitForSeconds(3);
            Assert.That(avatar.transform.position.z,Is.InRange(1.45f,1.8f));
            Assert.That(avatar.transform.position.y,Is.InRange(-.02f,.12f));
            world.PausePhysics(); yield return null; Assert.That(motion.Active,Is.False);
            var stopped = avatar.transform.localPosition; yield return new WaitForSeconds(.1f); Assert.That(avatar.transform.localPosition,Is.EqualTo(stopped));
            Assert.That(Vector3.Distance(editor.Read("maestro").position,stopped),Is.LessThan(.001f));
            editor.SetAvatarMovement(1.8f,1); editor.Undo(); Assert.That(motion.Distance,Is.EqualTo(1.3f)); Assert.That(motion.Speed,Is.EqualTo(.65f));
            editor.SaveNow(); yield return new WaitForSeconds(.3f);
            var document = new RoomStorage(directory).Load(out error); Assert.That(document,Is.Not.Null,error);
            var saved = Array.Find(document.objects,item => item.id=="maestro"); Assert.That(saved.walkSpeed,Is.EqualTo(.65f)); Assert.That(saved.followDistance,Is.EqualTo(1.3f));
        }
        [UnityTest] public IEnumerator VisualRuleOwnsSpatialActionUntilItsDurationEnds()
        {
            Tutor(); var actions = new RoomRuleActions(editor,authoring);
            var step = new RuleStep { action=RuleActionKind.LookAtUser,targetId="maestro",seconds=.5f };
            var scheduler = new RuleScheduler(actions);
            var sequence = new RuleSequence { id=Guid.NewGuid().ToString("N"),name="Look",program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(step) };
            scheduler.Configure(new RuleDocument { sequences=new[] { sequence } });
            Assert.That(scheduler.Trigger(sequence.id,0),Is.True,scheduler.LastError);
            Assert.That(motion.Active,Is.True); yield return new WaitForSeconds(.2f);
            scheduler.Tick(.6f); Assert.That(motion.Active,Is.False); Assert.That(scheduler.RunningCount,Is.Zero);
        }
        [UnityTest] public IEnumerator FollowingAnimatesVisibleFeetAcrossCyclesAfterASavedPose()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(12,.2f,12)); Tutor(); Ready();
            viewer.transform.position = new Vector3(0,1.6f,5);
            avatar.SetSavedPose(avatar.PoseRig.Capture());
            Assert.That(motion.Begin("foot motion",AvatarSpatialMode.Follow,out var error),Is.True,error);
            yield return new WaitForSeconds(.3f);
            var baked = new Mesh();
            try
            {
                var feet = new[] { avatar.PoseRig.CanonicalBone(PoseJoint.LeftFoot),avatar.PoseRig.CanonicalBone(PoseJoint.RightFoot) };
                var skins = new SkinnedMeshRenderer[2]; var indices = new int[2];
                var distances = new[] { float.PositiveInfinity,float.PositiveInfinity };
                foreach (var skin in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(baked,true); var vertices = baked.vertices;
                    for (int vertex=0;vertex<vertices.Length;vertex++)
                    {
                        var point = skin.transform.TransformPoint(vertices[vertex]);
                        for (int foot=0;foot<2;foot++)
                        {
                            float distance = Vector3.SqrMagnitude(point-feet[foot].position);
                            if (distance >= distances[foot]) continue;
                            distances[foot] = distance; skins[foot] = skin; indices[foot] = vertex;
                        }
                    }
                }
                Assert.That(distances[0],Is.LessThan(.04f)); Assert.That(distances[1],Is.LessThan(.04f));
                for (int cycle=0;cycle<2;cycle++)
                {
                    var bounds = new Bounds[2];
                    for (int sample=0;sample<12;sample++)
                    {
                        yield return new WaitForSeconds(.08f);
                        for (int foot=0;foot<2;foot++)
                        {
                            skins[foot].BakeMesh(baked,true);
                            // Exclude movement of the whole avatar: verify its visible skin deforms.
                            var point = avatar.transform.InverseTransformPoint(skins[foot].transform.TransformPoint(baked.vertices[indices[foot]]));
                            if (sample==0) bounds[foot] = new Bounds(point,Vector3.zero); else bounds[foot].Encapsulate(point);
                        }
                    }
                    Assert.That(motion.Active,Is.True);
                    for (int foot=0;foot<2;foot++) Assert.That(bounds[foot].size.magnitude,Is.GreaterThan(.06f),"Each visible foot must keep stepping, including after the room autosaves");
                }
                motion.Stop();
            }
            finally { UnityEngine.Object.Destroy(baked); }
        }
        GameObject TravelGround()
        {
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Authored travel ground";
            ground.transform.SetParent(root.transform,false);ground.transform.localPosition=Vector3.down*.1f;
            ground.transform.localScale=new Vector3(20,.2f,20);ground.layer=RoomPhysicsLayers.Environment;
            ground.AddComponent<RoomWalkableSurface>().Publish(ground.GetComponent<Collider>());Physics.SyncTransforms();return ground;
        }
        ControllerFrame frame;
        MovementControls Controls(out VirtualRoomView view,out Transform userOrigin,RoomRules rules=null,RuleWorkshop workshop=null,BookControllerInput controller=null)
        {
            var origin=new GameObject("Simulated user origin"); origin.transform.SetParent(physicalRoot.transform,false); userOrigin=origin.transform;
            viewer.transform.SetParent(userOrigin,true); var camera=viewer.AddComponent<Camera>(); camera.backgroundColor=Color.clear;
            view=physicalRoot.AddComponent<VirtualRoomView>(); view.Initialize(root.transform,userOrigin,camera,null,world);
            frame=new ControllerFrame { leftTracked=true,rightTracked=true };
            var controls=root.AddComponent<MovementControls>(); controls.Initialize(room,editor,authoring,motion,rules,workshop,controller,view,() => tracked,controller ? null : () => frame,directory);
            return controls;
        }
        [UnityTest] public IEnumerator IndependentSticksRequireOptInAndAnimateOnlyTheirTarget()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(10,.2f,10)); Tutor(); Ready();
            var controls=Controls(out var view,out var origin); var initial=viewer.transform.position;
            frame.rightStick=Vector2.up; frame.leftStick=Vector2.right; yield return new WaitForSeconds(.12f);
            Assert.That(avatar.transform.position.z,Is.EqualTo(0)); Assert.That(viewer.transform.position,Is.EqualTo(initial));
            controls.ToggleAvatar(); yield return new WaitForSeconds(.12f); Assert.That(motion.Active,Is.False,"A held stick cannot start movement on enable");
            frame.rightStick=Vector2.zero; yield return null; frame.rightStick=Vector2.up;
            var foot=avatar.PoseRig.CanonicalBone(PoseJoint.LeftFoot); var footBefore=foot.localRotation;
            yield return new WaitForSeconds(.35f); Assert.That(avatar.transform.position.z,Is.GreaterThan(.1f));
            Assert.That(Quaternion.Angle(footBefore,foot.localRotation),Is.GreaterThan(1)); Assert.That(viewer.transform.position,Is.EqualTo(initial));
            controls.ToggleUser(); Assert.That(controls.UserEnabled,Is.True,"MR enables virtual-world movement without moving passthrough tracking");
            TravelGround(); world.PausePhysics(); controls.ToggleView(); Assert.That(view.Active,Is.True); Assert.That(viewer.GetComponent<Camera>().backgroundColor.a,Is.EqualTo(1));
            Assert.That(controls.UserEnabled,Is.True); frame.leftStick=Vector2.zero; frame.rightStick=Vector2.zero; yield return null;
            frame.leftStick=Vector2.right; yield return new WaitForSeconds(.3f);
            Assert.That(root.transform.position.x,Is.LessThan(-.1f)); Assert.That(viewer.transform.position,Is.EqualTo(initial)); Assert.That(origin.localPosition,Is.EqualTo(Vector3.zero)); Assert.That(motion.Active,Is.False);
            controls.SwapSticks(); Assert.That(controls.Preferences.userStick,Is.EqualTo(MovementStick.Right));
            var saved=new ControllerPreferenceStorage(directory).Load(out _); Assert.That(saved.avatarStick,Is.EqualTo(MovementStick.Left));
            var at=avatar.transform.position; yield return new WaitForSeconds(.1f); Assert.That(avatar.transform.position,Is.EqualTo(at),"Rebinding requires neutral");
            controls.Recover(); Assert.That(view.Active,Is.False); Assert.That(origin.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(viewer.GetComponent<Camera>().backgroundColor.a,Is.Zero); Assert.That(controls.AvatarEnabled || controls.UserEnabled,Is.False);
        }
        [UnityTest] public IEnumerator DirectAvatarMovementCannotCrossWallsAndTrackingLossNeedsReenable()
        {
            Surface(new Vector3(0,-.1f,0),new Vector3(8,.2f,8)); Surface(new Vector3(0,1,.9f),new Vector3(2,2,.12f)); Tutor(); Ready();
            var controls=Controls(out _,out _); controls.ToggleAvatar(); yield return null; frame.rightStick=Vector2.up;
            yield return new WaitForSeconds(1.5f); Assert.That(avatar.transform.position.z,Is.InRange(.2f,.62f));
            var stopped=avatar.transform.position; yield return new WaitForSeconds(.2f); Assert.That(Vector3.Distance(stopped,avatar.transform.position),Is.LessThan(.025f));
            frame.rightStick=Vector2.down; yield return new WaitForSeconds(.3f); Assert.That(avatar.transform.position.z,Is.LessThan(stopped.z-.1f),"User can reverse away from a blocked step");
            tracked=false; yield return null; Assert.That(motion.Active,Is.False); Assert.That(controls.AvatarEnabled,Is.False);
            tracked=true; yield return null; Assert.That(motion.Active,Is.False); controls.ToggleAvatar(); yield return null; Assert.That(motion.Active,Is.False);
            frame.rightStick=Vector2.zero; yield return null; frame.rightStick=Vector2.up; yield return null; Assert.That(motion.Active,Is.True);
        }
        [UnityTest] public IEnumerator VirtualContentCollisionsSnapTurnAndPauseKeepPhysicalOriginFixed()
        {
            Tutor(); TravelGround(); var controls=Controls(out var view,out var origin);
            var wall=Surface(new Vector3(.65f,1,3),new Vector3(.12f,2,2));wall.transform.SetParent(root.transform,true);wall.layer=RoomPhysicsLayers.Environment; Physics.SyncTransforms();
            controls.ToggleView(); controls.ToggleUser(); yield return null; frame.leftStick=Vector2.right;
            yield return new WaitForSeconds(1.2f); Assert.That(root.transform.position.x,Is.InRange(-.41f,-.25f));Assert.That(origin.position,Is.EqualTo(Vector3.zero));
            frame.leftStick=Vector2.zero; frame.a=true; yield return null; var turned=root.transform.rotation;
            Assert.That(Quaternion.Angle(Quaternion.identity,turned),Is.EqualTo(30).Within(.1f));
            yield return null; Assert.That(Quaternion.Angle(root.transform.rotation,turned),Is.LessThan(.01f),"Holding a button must not keep turning");
            var retained=root.transform.position;controls.SendMessage("OnApplicationPause",true); Assert.That(view.Active,Is.False); Assert.That(origin.localPosition,Is.EqualTo(Vector3.zero)); Assert.That(origin.localRotation,Is.EqualTo(Quaternion.identity));Assert.That(root.transform.position,Is.EqualTo(retained));Assert.That(root.transform.rotation,Is.EqualTo(turned));
            controls.SendMessage("OnApplicationPause",false); yield return null; Assert.That(controls.UserEnabled,Is.False);
        }
        [UnityTest] public IEnumerator ControllerButtonsUseSavedActionsAndSolidToolsExposeBindings()
        {
            Tutor(); var workshop=root.AddComponent<RuleWorkshop>(); workshop.Initialize(editor,directory); workshop.NewSequence();
            var runtime=root.AddComponent<RoomRules>(); runtime.Initialize(workshop,editor,authoring,null,room,null);
            var controls=Controls(out _,out _,runtime,workshop); controls.BindSelected(2);
            var board=new GameObject("Controller tools"); board.transform.SetParent(root.transform,false); board.AddComponent<MovementTools>().Build(controls,room);
            var tools=board.GetComponentsInChildren<RuleToolAction>(); Assert.That(tools.Length,Is.EqualTo(15));
            RuleToolAction Tool(string label) => Array.Find(tools,x => x.AccessibleName == label);
            Assert.That(Tool("Backdrop"),Is.Not.Null);Assert.That(Tool("Real occlusion"),Is.Not.Null);
            Assert.That(Tool("World origin"),Is.Not.Null);Tool("World origin").Command();
            Assert.That(controls.Status,Does.Contain("unavailable"),"A bare fixture without workspace placement must refuse the shortcut");
            Tool("Swap sticks").Command(); Tool("Swap sticks").Command();
            Assert.That(Array.Exists(board.GetComponentsInChildren<TextMesh>(),x => x.text.Contains("Maestro Right / off")),Is.True,"Repeated settings edits must refresh the markings even when the status message is unchanged");
            Tool("Select button").Command(); Tool("Select button").Command();
            yield return null; frame.leftClick=true; yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.StopAll(); yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Held button cannot replay an action");
            frame.leftClick=false; yield return null; frame.leftClick=true; yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            frame.busy=true; yield return null; runtime.StopAll(); frame.busy=false; yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Release buttons after manipulation");
            workshop.DeleteSequence(); Assert.That(Array.Exists(board.GetComponentsInChildren<TextMesh>(),x => x.text.Contains("Missing action")),Is.True); frame.leftClick=false; yield return null; frame.leftClick=true; yield return null;
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero); Assert.That(controls.Status,Does.Contain("unavailable"));
            Assert.That(new ControllerPreferenceStorage(directory).Load(out _).buttons[2].command,Is.EqualTo(ControllerCommand.Sequence));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE");
            if (!string.IsNullOrEmpty(evidence))
            {
                Directory.CreateDirectory(evidence); var camera=viewer.GetComponent<Camera>(); camera.transform.SetParent(root.transform,false);
                board.transform.position=new Vector3(20,0,0); camera.transform.position=new Vector3(20,0,-1); camera.transform.rotation=Quaternion.identity; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.93f,.91f,.87f,1); camera.orthographic=true; camera.orthographicSize=.44f; camera.nearClipPlane=.01f;
                yield return null; // Let TextMesh refresh its dynamic font geometry before the capture.
                var target=new RenderTexture(1600,1350,24); camera.targetTexture=target; var pixels=new Texture2D(1600,1350,TextureFormat.RGB24,false); var previous=RenderTexture.active;
                try { camera.Render(); RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1600,1350),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(evidence,"movement-controls-unity.png"),pixels.EncodeToPNG()); }
                finally { RenderTexture.active=previous; camera.targetTexture=null; UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels); }
            }
        }
        [UnityTest] public IEnumerator MovementReadsRealOpenXRTouchStickAndClickBindings()
        {
            Tutor();
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>("MaestroTestTouch");
            var device=(OculusTouchControllerProfile.OculusTouchController)InputSystem.AddDevice("MaestroTestTouch");
            BookControllerInput input=null;
            try
            {
                InputSystem.SetDeviceUsage(device,UnityEngine.InputSystem.CommonUsages.LeftHand);
                var router=root.AddComponent<BookPointerRouter>();
                input=root.AddComponent<BookControllerInput>(); input.Router=router; input.Room=room; input.TrackingSpace=root.transform;
                var controls=Controls(out var presentation,out var userOrigin,controller:input); input.TrackingSpace=userOrigin;
                var token=new GameObject("Virtual view click test"); token.transform.SetParent(root.transform,false); token.transform.position=new Vector3(20,0,0);
                token.AddComponent<BoxCollider>().size=Vector3.one*.2f; token.AddComponent<RuleToolAction>().Command=controls.ToggleView;
                Physics.SyncTransforms();
                void Send(bool available,bool pressed=false)
                {
                    using (StateEvent.From(device,out var state))
                    {
                        device.isTracked.WriteValueIntoEvent(available ? 1f : 0f,state);
                        device.GetChildControl<QuaternionControl>("pointerRotation").WriteValueIntoEvent(Quaternion.identity,state);
                        device.GetChildControl<Vector3Control>("pointerPosition").WriteValueIntoEvent(new Vector3(20,0,-1),state);
                        device.triggerPressed.WriteValueIntoEvent(pressed ? 1f : 0f,state);
                        device.thumbstick.WriteValueIntoEvent(new Vector2(.4f,.7f),state);
                        device.thumbstickClicked.WriteValueIntoEvent(1f,state); device.primaryButton.WriteValueIntoEvent(1f,state);
                        InputSystem.QueueEvent(state);
                    }
                    InputSystem.Update();
                }
                Send(true); yield return null;
                var sample=input.ReadMovement(); Assert.That(sample.leftTracked,Is.True); Assert.That(sample.rightTracked,Is.False);
                Assert.That(sample.leftStick.x,Is.GreaterThan(.2f)); Assert.That(sample.leftStick.y,Is.GreaterThan(.4f));
                Assert.That(sample.leftClick,Is.True,"OpenXR exposes stick click by usage, not a primary2DAxisClick name alias"); Assert.That(sample.x,Is.True); Assert.That(sample.a,Is.False);
                Send(true,true); yield return null; Assert.That(input.ReadMovement().busy,Is.True);
                Send(true,false); yield return null; Assert.That(presentation.Active,Is.True,"Releasing a real tool click must be allowed to change view despite the previous PageHeld state");
                Send(false); yield return null;
                Assert.That(input.ReadMovement().leftTracked,Is.False);
            }
            finally { if (input) { input.enabled=false; UnityEngine.Object.Destroy(input); } InputSystem.RemoveDevice(device); InputSystem.RemoveLayout("MaestroTestTouch"); }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(physicalRoot); if (viewer) UnityEngine.Object.Destroy(viewer);
            Time.captureDeltaTime = captureDelta; yield return null; yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
