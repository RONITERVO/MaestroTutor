// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest.Tests
{
    public sealed class AvatarSpatialTests
    {
        GameObject root, viewer;
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
            root = new GameObject("Spatial room test"); root.AddComponent<XRInteractionManager>();
            world = root.AddComponent<RoomPhysicsWorld>(); navigation = root.AddComponent<RoomNavigation>(); navigation.Initialize(world);
            RoomPhysicsLayers.Configure(); tracked = true; yield return null;
        }
        GameObject Surface(Vector3 position,Vector3 scale)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.transform.SetParent(root.transform,false);
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
            Assert.That(motion.Begin("blocked",AvatarSpatialMode.Look,out _),Is.False); authoring.Stop();
            Assert.That(motion.Begin("recovery",AvatarSpatialMode.Look,out _),Is.True); room.RestoreInFrontOfViewer(); Assert.That(motion.Active,Is.False);
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
            var sequence = new RuleSequence { id=Guid.NewGuid().ToString("N"),name="Look",steps=new[] { step } };
            scheduler.Configure(new RuleDocument { sequences=new[] { sequence } });
            Assert.That(scheduler.Trigger(sequence.id,0),Is.True,scheduler.LastError);
            Assert.That(motion.Active,Is.True); yield return new WaitForSeconds(.2f);
            scheduler.Tick(.6f); Assert.That(motion.Active,Is.False); Assert.That(scheduler.RunningCount,Is.Zero);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root); if (viewer) UnityEngine.Object.Destroy(viewer);
            Time.captureDeltaTime = captureDelta; yield return null; yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
