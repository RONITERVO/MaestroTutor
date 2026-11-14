// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string StaticGroundBlock(Vector3 position,Vector3? offset=null,float scale=1){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Static ground fixture",position,scale,Color.white,out var id,out var error),Is.True,error);
            var value=editor.Read(id);value.physics=ItemPhysics.Fixed;value.staticSupport.enabled=true;
            if(offset.HasValue)value.collision=new(){shapes=new[]{new CollisionShape{id="platform",position=offset.Value,size=new(.6f,.12f,.4f),rotation=Quaternion.Euler(0,45,0)}}};
            GroundEdit(value);return id;
        }
        string StaticGroundRetire(params string[] ids){
            string area=NewRegion();GroundRegionPolicy(area,true);
            foreach(string id in ids)Assert.That(AssignRegion(id,area,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out var issue),Is.True,issue);return area;
        }
        void StaticGroundMissing(string id,Bounds envelope,bool expected=true){
            var dependencies=editor.GroundDependencies(envelope);
            Assert.That(dependencies.Any(x=>(string)x["target"]==id),Is.EqualTo(expected));
            if(expected){Assert.That(editor.GroundComplete(envelope),Is.False);Assert.That((bool)dependencies.First(x=>(string)x["target"]==id)["boundsKnown"],Is.True);}
        }
        [UnityTest]public IEnumerator StaticSupportColdOffsetReplacementUsesCollisionBoundsWithoutNativeActivation(){
            string id=StaticGroundBlock(new(4,0,4),Vector3.right*1.5f,2);StaticGroundRetire(id);yield return VisualLookupPrepared();
            var old=new Bounds(new(7,0,4),Vector3.one*.05f);StaticGroundMissing(id,old);
            var value=editor.Read(id);value.collision.shapes[0].position=Vector3.left*1.5f;GroundEdit(value);yield return VisualLookupPrepared();
            var current=new Bounds(new(1,0,4),Vector3.one*.05f);var spatial=editor.ReadSpatialBounds(id,out var source);
            Assert.That(source,Is.EqualTo("authored"));Assert.That(spatial.Visual.Bounds.Intersects(current),Is.False,"Offset support is outside all artwork");
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            StaticGroundMissing(id,current);StaticGroundMissing(id,old,false);
            for(int i=0;i<16;i++)Assert.That(editor.GroundComplete(current),Is.False);
            Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator StaticSupportMixedTerrainKeepsBothColdProvidersWhenPrimaryChanges(){
            string id=StaticGroundBlock(new(4,0,4),Vector3.right*1.5f);
            var value=editor.Read(id);value.heightFields=IncrementalGroundField();GroundEdit(value);StaticGroundRetire(id);yield return VisualLookupPrepared();
            value=editor.Read(id);value.collision.shapes[0].position=Vector3.left*1.5f;GroundEdit(value);yield return VisualLookupPrepared();
            var terrain=new Bounds(new(4,.1f,4),Vector3.one*.05f);var primary=new Bounds(new(2.5f,0,4),Vector3.one*.05f);
            StaticGroundMissing(id,terrain);StaticGroundMissing(id,primary);
            value=editor.Read(id);value.staticSupport.enabled=false;GroundEdit(value);yield return VisualLookupPrepared();
            StaticGroundMissing(id,terrain);StaticGroundMissing(id,primary,false);
            var journal=(RoomJournal)MovementPrivate(editor,"journal");Assert.That(journal.GroundCandidate(id,out var definition),Is.True);
            Assert.That(definition.terrain&&!definition.ordinary&&!definition.imported,Is.True);
        }
        [UnityTest]public IEnumerator StaticSupportLiveOwnershipAndRuntimeMismatchCannotBorrowLowerGround(){
            string id=StaticGroundBlock(new(4,.3f,4),Vector3.zero);var lower=IncrementalGroundSurface(new(4,-.2f,4));
            var envelope=new Bounds(new(4,.3f,4),new(.05f,1,.05f));yield return VisualLookupPrepared();
            Assert.That(editor.GroundComplete(envelope),Is.True);var item=editor.Find(id);var body=item.GetComponent<RigidRoomItem>();int revision=editor.Revision;
            var owner=new object();body.SetAnimationOwner(owner,true);
            StaticGroundMissing(id,envelope);Assert.That(editor.GroundDependencies(envelope).Any(x=>(string)x["target"]==lower),Is.False);
            body.SetAnimationOwner(owner,false);Assert.That(editor.GroundComplete(envelope),Is.True);
            body.Configure(physics,ItemPhysics.Solid,.5f);Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True,"Paused dynamic physics must not look fixed");StaticGroundMissing(id,envelope);
            body.Configure(physics,ItemPhysics.Fixed,.5f);Assert.That(editor.GroundComplete(envelope),Is.True);
            body.SetGeometryReady(false);StaticGroundMissing(id,envelope);body.SetGeometryReady(true);Assert.That(editor.GroundComplete(envelope),Is.True);
            item.GetComponent<RoomEnvironmentBinding>().BeginRestoration(true);StaticGroundMissing(id,envelope);
            Assert.That(editor.Revision,Is.EqualTo(revision),"Runtime readiness is fresh without a saved edit");
        }
        [UnityTest]public IEnumerator StaticSupportCapturedLowerGroundRefusesNewUpperSourceInSameFrame(){
            ProviderPlatform();var lower=block.SolidColliders.Single();var query=ProviderCapture(root.transform);var column=new RoomGroundColumn(lower);
            var point=new Vector3(4,.35f,4);Assert.That(query.Sample(point,.5f,.5f,out var original),Is.True);Assert.That(original.collider,Is.SameAs(lower));
            Assert.That(column.Contains(point),Is.True);int frame=Time.frameCount;
            string upper=StaticGroundBlock(new(4,.2f,4),Vector3.zero);Physics.SyncTransforms();
            Assert.That(Time.frameCount,Is.EqualTo(frame));Assert.That(editor.Find(upper).GetComponent<CreatedRoomObject>().StaticSupportReady,Is.True);
            Assert.That(query.Sample(point,.5f,.5f,out _),Is.False,"A retained lower-floor list cannot miss a newly published upper source");
            Assert.That(query.Contains(lower),Is.False);Assert.That(column.Contains(point),Is.False,"A standalone captured column also pins source membership");
            var fresh=ProviderCapture(root.transform);Assert.That(fresh.Sample(point,.5f,.5f,out var current),Is.True);
            Assert.That(current.collider.GetComponentInParent<RoomItem>(),Is.SameAs(editor.Find(upper)));yield return null;
        }
        [UnityTest]public IEnumerator StaticSupportUnavailableSiblingRefusesStandaloneColumnAndCapturedSet(){
            string id=ProviderPlatform();var value=editor.Read(id);
            value.collision.shapes=new[]{new CollisionShape{id="left",position=new(-.5f,-.1f,0),size=new(.8f,.2f,1)},new CollisionShape{id="right",position=new(.5f,-.1f,0),size=new(.8f,.2f,1)}};
            GroundEdit(value);Physics.SyncTransforms();var native=block.GetComponent<CreatedRoomObject>();
            var left=block.SolidColliders.Single(c=>RoomWalkableSurface.Find(c).SourceId=="left");
            var right=block.SolidColliders.Single(c=>RoomWalkableSurface.Find(c).SourceId=="right");
            var query=ProviderCapture(root.transform);var column=new RoomGroundColumn(right);var point=new Vector3(4.5f,.3f,4);
            Assert.That(query.Sample(point,.5f,.5f,out var hit),Is.True);Assert.That(hit.collider,Is.SameAs(right));Assert.That(column.Contains(point),Is.True);
            ulong revision=RoomWalkableSurface.ChangeRevision;
            try{
                left.enabled=false;
                Assert.That(RoomWalkableSurface.ChangeRevision,Is.EqualTo(revision),"Direct collider unavailability must be checked live, not inferred from a publication notification");
                Assert.That(right.enabled,Is.True);Assert.That(native.StaticSupportReady,Is.False);
                Assert.That(query.Sample(point,.5f,.5f,out _),Is.False,"The complete captured source cannot salvage only its still-enabled sibling");
                Assert.That(column.Contains(point),Is.False,"A single-column caller also requires the complete selected source");
            }finally{left.enabled=true;}
            Assert.That(native.StaticSupportReady,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator StaticSupportGroundCompactsTheThirtyThirdDistinctOverrideWithinAcceptedCaps(){
            var ids=IncrementalMovementGroup(40,out _,independent:true,fixedPhysics:true);yield return VisualLookupPrepared();
            var edits=ids.Select(editor.Read).ToArray();foreach(var value in edits)value.staticSupport.enabled=true;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,edits,Array.Empty<string>(),out var error),Is.True,error);yield return VisualLookupPrepared();
            Assert.That(editor.Snapshot().Validate(out error),Is.True,error);Assert.That(ids.All(editor.NativeEntityDormant),Is.True);
            editor.GroundComplete(IncrementalGroundAll);var original=MovementPrivate(editor,"groundDormant");
            var first=editor.Read(ids[0]);var oldProbe=new Bounds(first.position,Vector3.one*.01f);
            CollectionAssert.Contains(GroundOverlayTargets(original,oldProbe),ids[0]);
            for(int i=0;i<33;i++){
                var value=editor.Read(ids[i]);value.position+=Vector3.up*2;var before=new GroundCosts(editor);GroundEdit(value);
                editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,1,1,i==32?1:0,i==32);
                Assert.That(editor.GroundQueryWork.overrides,Is.EqualTo(i==32?0:i+1));
            }
            CollectionAssert.Contains(GroundOverlayTargets(original,oldProbe),ids[0]);
            CollectionAssert.DoesNotContain(GroundOverlayTargets(MovementPrivate(editor,"groundDormant"),oldProbe),ids[0]);
            foreach(string id in ids)StaticGroundMissing(id,new Bounds(editor.Read(id).position,Vector3.one*.01f));
            Assert.That(editor.Snapshot().Validate(out error),Is.True,error);Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(editor.Snapshot().objects.Count(value=>value.kind==RoomObjectKind.ImportedModel),Is.Zero);
            Assert.That(editor.Snapshot().objects.Sum(value=>value.heightFields.Length),Is.Zero);
        }
    }
}
