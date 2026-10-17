// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
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
        string geometryTarget;
        IEnumerator GeometryModel(byte[] bytes)
        {
            var asset=ModelLibrary.Inspect("Original geometry.glb",bytes);var save=editor.Models.SaveAsync(asset);
            yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Assert.That(editor.CreateImportedModel(asset.Hash,out geometryTarget,out var error),Is.True,error);
            float deadline=Time.realtimeSinceStartup+8;
            var view=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>();
            while(!view.ModelGeometryReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(view.ModelGeometryReady,Is.True,view.ModelGeometryIssue);
            Assert.That(editor.SetItemPhysics(geometryTarget,new ObjectPhysicsSettings{mode="fixed",shape="automatic",mass=.5f}),Is.True,editor.Status);
        }
        static RoomModelGeometry BuildingSettings()=>new(){scaleMode="source",pivot="source",meshCollision=true,walkable=true};
        JObject GeometryCall(RoomModelGeometry settings)=>new(){["id"]="object.model.geometry.set",["version"]=1,["arguments"]=new JObject{
            ["target"]=geometryTarget,["revision"]=editor.ObjectRevision(geometryTarget),["settings"]=JObject.FromObject(settings)}};
        [UnityTest] public IEnumerator ModelGeometryScalePivotAndPlaybackUseActualImportedTransforms()
        {
            yield return GeometryModel(ModelFixture.Create(root=>root["nodes"][0]["translation"]=new JArray(2,0,0)));
            var view=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>();var model=view.Model;
            Assert.That(model.LocalBounds.size.y,Is.EqualTo(.35f).Within(.001));
            var settings=new RoomModelGeometry{scaleMode="source",metresPerUnit=2,pivot="base"};
            var ex=new RoomAgentExecutor(editor);ContainerRun(ex,GeometryCall(settings));
            Assert.That(model.LocalBounds.size.y,Is.EqualTo(2).Within(.001));Assert.That(model.LocalBounds.min.y,Is.EqualTo(0).Within(.001));
            var before=model.Instance.transform.localPosition;model.Play(0,true);yield return new WaitForSeconds(.05f);Assert.That(model.IsPlaying,Is.True);
            Assert.That(ex.Execute(ContainerRequest(GeometryCall(new RoomModelGeometry{pivot="base"})),out _,out _),Is.False);model.Stop();
            Assert.That(model.Instance.transform.localPosition,Is.EqualTo(before));
            settings.meshCollision=true;ContainerRun(ex,GeometryCall(settings));
            Assert.That(model.PlaybackIssue,Does.Contain("Disable"));Assert.That(model.SampleClip(0,.5f,false),Is.False);model.Play(0,true);Assert.That(model.IsPlaying,Is.False);
            Assert.That(animations.CanStartRecording(animations.RecordingSessionId,geometryTarget,editor.ObjectRevision(geometryTarget),out var error),Is.False);StringAssert.Contains("rigid mesh",error);
            Assert.That(editor.CanConfigurePhysics(geometryTarget,editor.ObjectRevision(geometryTarget),new ObjectPhysicsSettings{mode="solid",shape="automatic",mass=.5f},out _),Is.False);
            settings.meshCollision=false;settings.pivot="source";ContainerRun(ex,GeometryCall(settings));
            Assert.That(Vector3.Distance(model.LocalBounds.center,model.SourceBounds.center*2),Is.LessThan(.001f));
            model.Play(0,true);Assert.That(model.IsPlaying,Is.True);model.Stop();
            Assert.That(BehaviourCatalog.TryRead("object.model.geometry",1,new JObject{["target"]=geometryTarget},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);
            var observation=(JObject)fact.Value;Assert.That(observation["settings"],Is.Not.Null);Assert.That((bool)observation["ready"],Is.True);Assert.That(fact.Characters,Is.LessThanOrEqualTo(1024));
        }
        [UnityTest] public IEnumerator ModelGeometryUsesDoorwayAndInteriorFloorRatherThanBoundingBox()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var ex=new RoomAgentExecutor(editor);ContainerRun(ex,GeometryCall(BuildingSettings()));
            var item=editor.Find(geometryTarget);var view=item.GetComponent<CreatedRoomObject>();var colliders=item.Grab.colliders;
            Assert.That(colliders.Count,Is.EqualTo(1));Assert.That(colliders[0],Is.TypeOf<MeshCollider>());
            var collision=colliders[0];Physics.SyncTransforms();
            Ray RayAt(Vector3 p,Vector3 direction)=>new(item.transform.TransformPoint(p),item.transform.TransformDirection(direction));
            Assert.That(collision.Raycast(RayAt(new Vector3(0,1,-2),Vector3.forward),out _,4),Is.False,"Door opening must be empty");
            Assert.That(collision.Raycast(RayAt(new Vector3(2,1,-2),Vector3.forward),out _,4),Is.True,"Wall remains solid");
            Assert.That(collision.Raycast(RayAt(new Vector3(0,1,-1),Vector3.down),out var hit,2),Is.True);
            Assert.That(item.transform.InverseTransformPoint(hit.point).y,Is.EqualTo(0).Within(.002));
            Assert.That(collision.GetComponent<RoomWalkableSurface>().Available,Is.True);
            Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
            Assert.That(physics.SetRunning(true,out error),Is.True,error);
            var interior=item.transform.TransformPoint(new Vector3(0,1,-1));Assert.That(physics.CanSimulate(interior,item),Is.True);
            var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.transform.SetParent(root.transform,false);ball.transform.position=interior;ball.transform.localScale=Vector3.one*.15f;ball.layer=RoomPhysicsLayers.Item;
            var body=ball.AddComponent<Rigidbody>();body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            float deadline=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(item.transform.InverseTransformPoint(ball.transform.position).y,Is.InRange(.06f,.12f),"A real PhysX body rests inside the model below its roof");
            physics.PausePhysics();ContainerRun(ex,GeometryCall(new RoomModelGeometry()));
            Assert.That(collision.enabled,Is.False);Assert.That(item.Grab.colliders.Single(),Is.TypeOf<BoxCollider>());
            editor.Undo();Assert.That(view.ModelGeometryReady,Is.True,view.ModelGeometryIssue);Assert.That(item.Grab.colliders.Single(),Is.TypeOf<MeshCollider>());
            yield return null;Assert.That(item.GetComponentsInChildren<RoomWalkableSurface>().Count(s=>s.Available),Is.EqualTo(1),"Undo must not leave duplicate ground surfaces");
        }
        [UnityTest] public IEnumerator ModelGeometrySaveFailureAndStaleRevisionPreservePublishedGeometry()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var ex=new RoomAgentExecutor(editor);var stale=GeometryCall(BuildingSettings());
            ContainerRun(ex,GeometryCall(new RoomModelGeometry{pivot="base"}));var item=editor.Find(geometryTarget);var prior=item.Grab.colliders.Single();int revision=editor.ObjectRevision(geometryTarget);
            Assert.That(ex.Execute(ContainerRequest(stale),out _,out _),Is.False);Assert.That(editor.ObjectRevision(geometryTarget),Is.EqualTo(revision));
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try {Assert.That(ex.Execute(ContainerRequest(GeometryCall(BuildingSettings())),out _,out _),Is.False);Assert.That(editor.ObjectRevision(geometryTarget),Is.EqualTo(revision));Assert.That(item.Grab.colliders.Single(),Is.SameAs(prior));Assert.That(prior.enabled,Is.True);}
            finally {Directory.Delete(pending);}
            yield return null;Assert.That(item.GetComponentsInChildren<MeshCollider>().Count(c=>c.enabled),Is.Zero);
            ContainerRun(ex,GeometryCall(BuildingSettings()));Assert.That(item.Grab.colliders.Single(),Is.TypeOf<MeshCollider>());
            var saved=new RoomStorage(directory).Load(out var error);Assert.That(saved.objects.Single(o=>o.id==geometryTarget).modelGeometry.walkable,Is.True,error);
        }
        [UnityTest] public IEnumerator ModelGeometryCopiesReloadExactGeometryWithoutDuplicatingAssetBytes()
        {
            yield return GeometryModel(BuildingModelFixture.Create());var ex=new RoomAgentExecutor(editor);ContainerRun(ex,GeometryCall(BuildingSettings()));
            string original=geometryTarget;Assert.That(editor.CopyObject(original,editor.ObjectRevision(original),"Second building",new Vector3(8,0,0),out var copy,out var error),Is.True,error);
            var view=editor.Find(copy).GetComponent<CreatedRoomObject>();float deadline=Time.realtimeSinceStartup+8;
            while(!view.ModelGeometryReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(view.ModelGeometryReady,Is.True,view.ModelGeometryIssue);Assert.That(editor.Find(copy).Grab.colliders.Single(),Is.TypeOf<MeshCollider>());
            Assert.That(view.Model.LocalBounds.size.x,Is.EqualTo(6).Within(.001));Assert.That(editor.Read(copy).modelHash,Is.EqualTo(editor.Read(original).modelHash));
            geometryTarget=copy;ContainerRun(ex,GeometryCall(new RoomModelGeometry()));Assert.That(editor.Read(original).modelGeometry.meshCollision,Is.True);
            Assert.That(editor.Find(original).Grab.colliders.Single(),Is.TypeOf<MeshCollider>());yield return null;
        }
        [UnityTest] public IEnumerator ModelGeometryRefusesSkinOversizeAndCompetingHumanEdit()
        {
            yield return GeometryModel(ModelFixture.Create(avatar:true));var ex=new RoomAgentExecutor(editor);int revision=editor.ObjectRevision(geometryTarget);
            Assert.That(ex.Execute(ContainerRequest(GeometryCall(BuildingSettings())),out _,out _),Is.False);
            Assert.That(ex.Execute(ContainerRequest(GeometryCall(new RoomModelGeometry{scaleMode="source",metresPerUnit=100})),out _,out _),Is.False);
            var view=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>();
            Assert.That(editor.Ownership.TryAcquire("human","Your edit",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(geometryTarget,"wholeTarget")},null,out var lease,out var error),Is.True,error);
            try {Assert.That(ex.Execute(ContainerRequest(GeometryCall(new RoomModelGeometry{pivot="base"})),out _,out _),Is.False);}finally{lease.Dispose();}
            Assert.That(editor.ObjectRevision(geometryTarget),Is.EqualTo(revision));Assert.That(view.ModelGeometryReady,Is.True);
            ContainerRun(ex,GeometryCall(new RoomModelGeometry{scaleMode="source",metresPerUnit=.5f,pivot="base"}));
            Assert.That(editor.Read(geometryTarget).modelGeometry.metresPerUnit,Is.EqualTo(.5f));yield return null;
        }
    }
}
