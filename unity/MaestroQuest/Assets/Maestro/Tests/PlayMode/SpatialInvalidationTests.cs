// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest] public IEnumerator SpatialInvalidationWorldLightingPreservesDormantPartitions(){
            var ids=GroundLookupGroup();yield return VisualLookupPrepared();var camera=NativeObservationCamera(false);var envelope=new Bounds(GroundPoint,Vector3.one);
            editor.CaptureMovementCandidates(false,false);VisualLookupVisible(camera);editor.GroundComplete(envelope);
            var visual=editor.VisualQueryWork;var ground=editor.GroundQueryWork;var light=editor.Lighting;light.enabled=true;light.sunIntensity=.8f;
            Assert.That(editor.SetLighting(editor.LightingRevision,light,out var error),Is.True,error);yield return null;
            var movement=editor.CaptureMovementCandidates(false,false);VisualLookupMatches(ids,camera);Assert.That(editor.GroundComplete(envelope),Is.False);
            Assert.That(movement.DormantBoundsReads,Is.Zero);Assert.That(movement.IndexBuildWork,Is.Zero);
            Assert.That(editor.VisualQueryWork.reads,Is.EqualTo(visual.reads));Assert.That(editor.VisualQueryWork.build,Is.EqualTo(visual.build));
            Assert.That(editor.GroundQueryWork.reads,Is.EqualTo(ground.reads));Assert.That(editor.GroundQueryWork.build,Is.EqualTo(ground.build));
        }
        [UnityTest] public IEnumerator SpatialInvalidationLiveMetadataPublicationPreservesDormantPartitions(){
            var ids=GroundLookupGroup();yield return VisualLookupPrepared();var camera=NativeObservationCamera(false);var envelope=new Bounds(GroundPoint,Vector3.one);
            var data=editor.Read(editor.Identity(block));data.color=Color.blue;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That((bool)editor.ObserveAuthoredBounds(data.id)["prepared"],Is.False,"Exercise pending metadata, not an already prepared fixture");
            editor.CaptureMovementCandidates(false,false);VisualLookupVisible(camera);editor.GroundComplete(envelope);
            var visual=editor.VisualQueryWork;var ground=editor.GroundQueryWork;yield return VisualLookupPrepared();
            var movement=editor.CaptureMovementCandidates(false,false);VisualLookupMatches(ids,camera);Assert.That(editor.GroundComplete(envelope),Is.False);
            Assert.That(movement.DormantBoundsReads,Is.Zero);Assert.That(movement.IndexBuildWork,Is.Zero);
            Assert.That(editor.VisualQueryWork.reads,Is.EqualTo(visual.reads));Assert.That(editor.VisualQueryWork.build,Is.EqualTo(visual.build));
            Assert.That(editor.GroundQueryWork.reads,Is.EqualTo(ground.reads));Assert.That(editor.GroundQueryWork.build,Is.EqualTo(ground.build));
        }
        [UnityTest] public IEnumerator SpatialInvalidationRemembersLivePlacementsWithoutRebuildingDormantGeometry(){
            var ids=GroundLookupGroup();yield return VisualLookupPrepared();var camera=NativeObservationCamera(false);
            var envelope=new Bounds(GroundPoint,Vector3.one);
            editor.CaptureMovementCandidates(false,repeatedQueries:false);VisualLookupVisible(camera);Assert.That(editor.GroundComplete(envelope),Is.False);
            var visual=editor.VisualQueryWork;var ground=editor.GroundQueryWork;string live=editor.Identity(block);int revision=editor.ObjectRevision(live);
            long movementReads=0,movementBuild=0;
            for(int i=0;i<30;i++){
                block.transform.localPosition+=Vector3.right*.01f;editor.RememberPlacement(live);yield return null;
                var movement=editor.CaptureMovementCandidates(false,repeatedQueries:false);movementReads+=movement.DormantBoundsReads;movementBuild+=movement.IndexBuildWork;
                VisualLookupMatches(ids,camera);Assert.That(editor.GroundComplete(envelope),Is.False);
            }
            var evidence=new JObject{["frames"]=30,["dormantObjects"]=ids.Length,["movementReads"]=movementReads,["movementBuild"]=movementBuild,
                ["visualReads"]=editor.VisualQueryWork.reads-visual.reads,["visualBuild"]=editor.VisualQueryWork.build-visual.build,
                ["groundReads"]=editor.GroundQueryWork.reads-ground.reads,["groundBuild"]=editor.GroundQueryWork.build-ground.build};
            string folder=Environment.GetEnvironmentVariable("MAESTRO_SPATIAL_INVALIDATION_EVIDENCE");
            if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"live-placement.json"),evidence.ToString());}
            Assert.That(editor.ObjectRevision(live),Is.GreaterThan(revision));Assert.That(Vector3.Distance(editor.Read(live).position,block.transform.localPosition),Is.LessThan(.001));
            Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);
            Assert.That(new[]{movementReads,movementBuild,(long)evidence["visualReads"],(long)evidence["visualBuild"],(long)evidence["groundReads"],(long)evidence["groundBuild"]},Is.All.Zero,
                "Remembering a live actor's current placement must not rebuild unrelated dormant spatial partitions");
        }
    }
}
