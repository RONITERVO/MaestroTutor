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
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string ProviderPlatform(bool enabled=true,bool terrain=false){
            string id=editor.Identity(block);var data=editor.Read(id);
            data.position=new(4,0,4);data.rotation=Quaternion.identity;data.scale=1;data.motion=null;data.physics=ItemPhysics.Fixed;
            data.collisionShape=ItemCollider.Automatic;data.staticSupport=new(){enabled=enabled};
            data.collision=new(){shapes=new[]{new CollisionShape{id="tread",position=new(0,-.1f,0),size=new(2,.2f,2)}}};
            if(terrain)data.heightFields=new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.2f,25).ToArray()}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Physics.SyncTransforms();return id;
        }
        static RoomGroundQuery ProviderCapture(Transform root){var query=new RoomGroundQuery();query.Capture(root);return query;}
        static bool ProviderSample(RoomGroundQuery query)=>query.Sample(new(4,.05f,4),.5f,.5f,out _);
        [UnityTest] public IEnumerator StaticSupportUsesExactPrimaryCollisionAndStableAuthoredShapeIdentity(){
            string id=ProviderPlatform();var native=block.GetComponent<CreatedRoomObject>();
            Assert.That(native.StaticSupportReady,Is.True,native.StaticSupportIssue);
            var markers=block.GetComponentsInChildren<RoomWalkableSurface>();Assert.That(markers.Length,Is.EqualTo(1));
            var surface=markers[0];Assert.That(surface.Ordinary,Is.True);Assert.That(surface.SourceId,Is.EqualTo("tread"));Assert.That(surface.Piece,Is.Zero);
            Assert.That(surface.Collision,Is.SameAs(block.SolidColliders.Single()));Assert.That(native.SupportTarget,Is.EqualTo(id));
            Assert.That(ProviderSample(ProviderCapture(root.transform)),Is.True);
            var geometry=new RoomNavigationGeometry();Assert.That(geometry.Capture(root.transform,false),Is.True);
            Assert.That(geometry.BuildSources.Single(s=>s.component==surface.Collision).component,Is.SameAs(surface.Collision));
            Assert.That(new RoomGroundColumn(surface.Collision).Contains(new(4,.3f,4)),Is.True);
            Assert.That(editor.Read(id).staticSupport.enabled,Is.True);yield return null;
        }
        [UnityTest] public IEnumerator StaticSupportOwnershipRoundTripNeverRevivesCapturedFloor(){
            ProviderPlatform();var native=block.GetComponent<CreatedRoomObject>();var rigid=block.GetComponent<RigidRoomItem>();
            var query=ProviderCapture(root.transform);var collider=block.SolidColliders.Single();var column=new RoomGroundColumn(collider);
            Assert.That(ProviderSample(query),Is.True);var owner=new object();rigid.SetAnimationOwner(owner,true);
            Assert.That(native.StaticSupportReady,Is.False);Assert.That(ProviderSample(query),Is.False);Assert.That(column.Contains(new(4,.3f,4)),Is.False);
            rigid.SetAnimationOwner(owner,false);Assert.That(native.StaticSupportReady,Is.True,native.StaticSupportIssue);
            Assert.That(ProviderSample(query),Is.False,"An old handle must not revive within the same frame");
            Assert.That(ProviderSample(ProviderCapture(root.transform)),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator StaticSupportAvailabilityChangesInvalidateCapturesWithoutChangingSavedIntent(){
            string id=ProviderPlatform();var rigid=block.GetComponent<RigidRoomItem>();var native=block.GetComponent<CreatedRoomObject>();
            var original=JsonUtility.ToJson(editor.Read(id));var query=ProviderCapture(root.transform);
            rigid.SetGeometryReady(false);Assert.That(native.StaticSupportReady,Is.False);Assert.That(ProviderSample(query),Is.False);
            rigid.SetGeometryReady(true);Assert.That(native.StaticSupportReady,Is.True);Assert.That(ProviderSample(query),Is.False);
            query=ProviderCapture(root.transform);var owner=new object();rigid.SetConstraintBlocked(owner,true);
            Assert.That(native.StaticSupportReady,Is.False);rigid.SetConstraintBlocked(owner,false);Assert.That(ProviderSample(query),Is.False);
            query=ProviderCapture(root.transform);rigid.Configure(physics,ItemPhysics.Solid,1);Assert.That(native.StaticSupportReady,Is.False,"Kinematic paused Solid is not Fixed support");
            rigid.Configure(physics,ItemPhysics.Fixed,.5f);Assert.That(native.StaticSupportReady,Is.True,native.StaticSupportIssue);Assert.That(ProviderSample(query),Is.False);
            Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(original));yield return null;
        }
        [UnityTest] public IEnumerator StaticSupportReplacementCannotSubstituteANewProviderOnTheSameCollider(){
            var data=editor.Read(editor.Identity(block));data.motion=null;data.physics=ItemPhysics.Fixed;data.staticSupport=new(){enabled=true};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);Physics.SyncTransforms();
            var native=block.GetComponent<CreatedRoomObject>();var collider=block.SolidColliders.Single();var prior=RoomWalkableSurface.Find(collider);
            var query=ProviderCapture(root.transform);Assert.That(query.Contains(collider),Is.True);
            native.SetCollisionShape(ItemCollider.Automatic,true);
            Assert.That(block.SolidColliders.Single(),Is.SameAs(collider));var current=RoomWalkableSurface.Find(collider);
            Assert.That(current,Is.Not.SameAs(prior));Assert.That(prior.Available,Is.False);Assert.That(query.Contains(collider),Is.False);
            Assert.That(ProviderCapture(root.transform).Contains(collider),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator StaticSupportDisableRetiresOnlyPrimaryMarkersAndPreservesTerrain(){
            string id=ProviderPlatform(terrain:true);var terrain=block.GetComponent<HeightFieldView>().Collision;
            Assert.That(block.GetComponentsInChildren<RoomWalkableSurface>().Count(s=>s.Available),Is.EqualTo(2));
            var data=editor.Read(id);data.staticSupport.enabled=false;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(RoomWalkableSurface.Find(terrain),Is.Not.Null);Assert.That(RoomWalkableSurface.Find(terrain).Ordinary,Is.False);
            Assert.That(block.GetComponentsInChildren<RoomWalkableSurface>().Count(s=>s.Available),Is.EqualTo(1));
            Assert.That(block.GetComponent<CreatedRoomObject>().StaticSupportReady,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator StaticSupportSharedSaveWaitsForPersistenceAndUndoRestoresIntent(){
            string id=ProviderPlatform(false);int revision=editor.ObjectRevision(id);var settings=new RoomStaticSupport{enabled=true};
            Assert.That(editor.BeginConfigureStaticSupport(id,revision,settings,out var pending,out var error),Is.True,error);
            using(pending){
                settings.enabled=false;yield return Prepared(pending);Assert.That(editor.Read(id).staticSupport.enabled,Is.False);
                yield return Completed(pending);Assert.That(editor.Read(id).staticSupport.enabled,Is.True);
                Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).staticSupport.enabled,Is.True,error);
                Assert.That(editor.CanConfigureStaticSupport(id,revision,new(){enabled=false},out _),Is.False);
                Assert.That(BehaviourCatalog.TryRead("object.support",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var observed),Is.True);
                Assert.That((bool)JObject.FromObject(observed.Value)["ready"],Is.True);
                editor.Undo();Assert.That(editor.Read(id).staticSupport.enabled,Is.False);editor.Redo();Assert.That(editor.Read(id).staticSupport.enabled,Is.True);
            }
        }
        [UnityTest] public IEnumerator StaticSupportRejectsIncompatibleEditsWithoutChangingAcceptedWorld(){
            string id=ProviderPlatform();string before=JsonUtility.ToJson(editor.Snapshot());
            foreach(var mode in new[]{0,1,2}){
                var data=editor.Read(id);
                if(mode==0)data.physics=ItemPhysics.Solid;
                else if(mode==1)data.collision.shapes=new[]{new CollisionShape{id="ball",shape="sphere"}};
                else data.motion=new RoomMotion{frames=new[]{new MotionFrame{position=data.position}}};
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out _),Is.False);
                Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            }
            var combined=editor.Read(id);combined.staticSupport.enabled=false;combined.physics=ItemPhysics.Solid;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{combined},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(block.GetComponent<CreatedRoomObject>().StaticSupportReady,Is.False);yield return null;
        }
    }
}
