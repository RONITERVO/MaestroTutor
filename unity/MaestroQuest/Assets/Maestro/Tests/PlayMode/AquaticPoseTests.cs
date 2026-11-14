// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        [UnityTest]public IEnumerator AquaticPoseSamplesEveryImportedNodeAndPinsTheChosenAnchorOnAllAxes(){
            yield return WalkingCharacter();
            var nodes=gaitModel.Instance.Nodes;var hip=gaitModel.Humanoid.GetBoneTransform(HumanBodyBones.Hips);
            int anchor=Enumerable.Range(0,nodes.Count).Single(i=>nodes[i]==hip);
            var selection=new JObject{["source"]="embedded",["modelHash"]=gaitModel.AssetHash,["anchorNode"]=anchor,["rate"]=1,["clip"]=0};
            Assert.That(ImportedLocomotionPose.Valid(editor,gaitItem,selection,out var error),Is.True,error);
            var initial=new ImportedMotionPose(gaitModel,gaitModel.transform.localPosition);
            var origin=gaitItem.transform.InverseTransformPoint(hip.position);var restRoot=gaitModel.transform.localPosition;
            Assert.That(gaitModel.SampleClip(0,.5f,true),Is.True);
            var rotations=nodes.Select(n=>n.localRotation).ToArray();var positions=nodes.Select(n=>n.localPosition).ToArray();
            initial.Restore();
            using(var pose=new ImportedLocomotionPose(editor,gaitItem,selection)){
                for(int i=0;i<10;i++)Assert.That(pose.Apply(.05f,out error),Is.True,error);
                for(int i=0;i<nodes.Count;i++){
                    Assert.That(Quaternion.Angle(nodes[i].localRotation,rotations[i]),Is.LessThan(.01f),nodes[i].name);
                    Assert.That(Vector3.Distance(nodes[i].localPosition,positions[i]),Is.LessThan(.0001f),nodes[i].name);
                }
                Assert.That(Vector3.Distance(gaitItem.transform.InverseTransformPoint(hip.position),origin),Is.LessThan(.0001f));
            }
            Assert.That(gaitModel.transform.localPosition,Is.EqualTo(restRoot));
            var fact=AnimationNodeFacts.Fact();
            Assert.That(fact.TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]=editor.Identity(gaitItem),["offset"]=0},out var value),Is.True);
            Assert.That(value,Is.Not.Null);
        }
        [UnityTest]public IEnumerator AquaticPoseDoesNotRequireHumanoidAndRejectsAnotherModelHash(){
            yield return WalkingCharacter(ModelFixture.Create());
            Assert.That(gaitModel.IsHumanoid,Is.False);
            var choice=new JObject{["source"]="embedded",["modelHash"]=gaitModel.AssetHash,["anchorNode"]=0,["rate"]=1,["clip"]=0};
            Assert.That(ImportedLocomotionPose.Valid(editor,gaitItem,choice,out var error),Is.True,error);
            using(var pose=new ImportedLocomotionPose(editor,gaitItem,choice))Assert.That(pose.Apply(.05f,out error),Is.True,error);
            choice["modelHash"]=new string('0',64);
            Assert.That(ImportedLocomotionPose.Valid(editor,gaitItem,choice,out _),Is.False);
        }
    }
}
