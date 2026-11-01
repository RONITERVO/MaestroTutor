// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed partial class FootPlacementMathTests
    {
        GameObject root;
        Transform hips,leftUpper,leftLower,leftFoot,rightUpper,rightLower,rightFoot;
        Transform Node(string name,Transform parent,Vector3 position)
        {
            var value=new GameObject(name).transform;value.SetParent(parent,false);value.localPosition=position;return value;
        }
        [SetUp]public void SetUp()
        {
            root=new GameObject("Grounded character");
            hips=Node("hips",root.transform,Vector3.up);
            leftUpper=Node("left upper",hips,new(-.1f,0,0));
            leftLower=Node("left lower",leftUpper,new(0,-.45f,.05f));
            leftFoot=Node("left foot",leftLower,new(0,-.45f,-.05f));
            rightUpper=Node("right upper",hips,new(.1f,0,0));
            rightLower=Node("right lower",rightUpper,new(0,-.45f,.05f));
            rightFoot=Node("right foot",rightLower,new(0,-.45f,-.05f));
        }
        [TearDown]public void TearDown(){Object.DestroyImmediate(root);}
        HumanoidFootRig Measure(float plane=0)=>HumanoidFootRig.Measure(root.transform,hips,leftUpper,leftLower,leftFoot,rightUpper,rightLower,rightFoot,plane);
        [TestCase(-.08f,.15f,.08f)][TestCase(-.2f,.22f,.15f)][TestCase(.05f,.35f,-.1f)]
        public void ReachUsesRotationsAndPreservesBothLegLengths(float x,float y,float z)
        {
            float thigh=Vector3.Distance(leftUpper.position,leftLower.position),shin=Vector3.Distance(leftLower.position,leftFoot.position);
            var upperPosition=leftUpper.localPosition;var lowerPosition=leftLower.localPosition;var footPosition=leftFoot.localPosition;
            var other=rightFoot.position;var target=new Vector3(x,y,z);var rotation=Quaternion.Euler(12,21,4);
            Assert.That(TwoBoneFootSolver.Solve(leftUpper,leftLower,leftFoot,target,rotation,Vector3.forward,out var error),Is.True,error);
            Assert.That(Vector3.Distance(leftFoot.position,target),Is.LessThan(.0005f));
            Assert.That(Quaternion.Angle(leftFoot.rotation,rotation),Is.LessThan(.01f));
            Assert.That(Vector3.Distance(leftUpper.position,leftLower.position),Is.EqualTo(thigh).Within(.00001f));
            Assert.That(Vector3.Distance(leftLower.position,leftFoot.position),Is.EqualTo(shin).Within(.00001f));
            Assert.That(leftUpper.localPosition,Is.EqualTo(upperPosition));Assert.That(leftLower.localPosition,Is.EqualTo(lowerPosition));Assert.That(leftFoot.localPosition,Is.EqualTo(footPosition));
            Assert.That(rightFoot.position,Is.EqualTo(other));
        }
        [Test]public void UnreachableTargetsNeverStretchOrPartiallyRotateTheLeg()
        {
            var rotations=new[]{leftUpper.localRotation,leftLower.localRotation,leftFoot.localRotation};
            Assert.That(TwoBoneFootSolver.Solve(leftUpper,leftLower,leftFoot,new(0,-2,0),Quaternion.Euler(30,0,0),Vector3.forward,out _),Is.False);
            Assert.That(new[]{leftUpper.localRotation,leftLower.localRotation,leftFoot.localRotation},Is.EqualTo(rotations));
        }
        [Test]public void StraightLegUsesExplicitKneeHintWithoutAFlip()
        {
            leftLower.localPosition=new(0,-.45f,0);leftFoot.localPosition=new(0,-.45f,0);
            Assert.That(TwoBoneFootSolver.Solve(leftUpper,leftLower,leftFoot,new(-.1f,.3f,0),Quaternion.identity,Vector3.forward,out var error),Is.True,error);
            Assert.That(leftLower.position.z,Is.GreaterThan(.1f));
            Assert.That(leftFoot.position.y,Is.EqualTo(.3f).Within(.0005f));
        }
        [Test]public void NonuniformScalingAndNonfiniteTargetsAreRefusedWithoutMutation()
        {
            var rotation=leftUpper.localRotation;root.transform.localScale=new(1,1.5f,1);
            Assert.That(TwoBoneFootSolver.Solve(leftUpper,leftLower,leftFoot,new(-.1f,.2f,0),Quaternion.identity,Vector3.forward,out _),Is.False);
            root.transform.localScale=Vector3.one;
            Assert.That(TwoBoneFootSolver.Solve(leftUpper,leftLower,leftFoot,new(float.NaN,0,0),Quaternion.identity,Vector3.forward,out _),Is.False);
            Assert.That(leftUpper.localRotation,Is.EqualTo(rotation));
        }
        [Test]public void RestContactsRemainOnTheMeasuredPlaneAfterTranslationYawAndScale()
        {
            var rig=Measure();Assert.That(rig,Is.Not.Null);
            root.transform.SetPositionAndRotation(new(3,2,-1),Quaternion.Euler(0,71,0));root.transform.localScale=Vector3.one*1.4f;
            Assert.That(root.transform.InverseTransformPoint(rig.Left.Contact).y,Is.EqualTo(0).Within(.00001f));
            Assert.That(root.transform.InverseTransformPoint(rig.Right.Contact).y,Is.EqualTo(0).Within(.00001f));
            Assert.That(rig.Left.Foot.TransformDirection(rig.Left.Up).y,Is.EqualTo(1).Within(.00001f));
        }
        [Test]public void UnrelatedGeometryBelowTheCharacterIsNotAssumedToBeAShoe()
        {
            Assert.That(Measure(-2),Is.Null);
        }
        [Test]public void PresentationRestorationReturnsTheExactAuthoredLegsAndHips()
        {
            var rig=Measure();var pose=new HumanoidFootRig.Pose(rig);
            var sourceHips=hips.localPosition;var sourceUpper=leftUpper.localRotation;
            var sourceLower=leftLower.localRotation;var sourceFoot=leftFoot.localRotation;
            for(int i=0;i<100;i++)
            {
                pose.Capture();hips.localPosition+=Vector3.down*.05f;
                Assert.That(TwoBoneFootSolver.Solve(leftUpper,leftLower,leftFoot,new(-.1f,.2f,.1f),Quaternion.Euler(10,0,0),Vector3.forward,out var error),Is.True,error);
                pose.Restore();
                Assert.That(hips.localPosition,Is.EqualTo(sourceHips));Assert.That(leftUpper.localRotation,Is.EqualTo(sourceUpper));
                Assert.That(leftLower.localRotation,Is.EqualTo(sourceLower));Assert.That(leftFoot.localRotation,Is.EqualTo(sourceFoot));
            }
            Assert.That(pose.Captured,Is.False);
        }
        [Test]public void SavedPreferencesAreFiniteExactModelBindingsAndDetachedCopies()
        {
            var value=new RoomFootPlacement{enabled=true,modelHash=new string('a',64),leftClearance=.01f,rightClearance=-.02f};
            Assert.That(value.Valid,Is.True);var copy=value.Copy();copy.leftClearance=.08f;
            Assert.That(value.leftClearance,Is.EqualTo(.01f));copy.modelHash=new string('A',64);Assert.That(copy.Valid,Is.False);
            copy=value.Copy();copy.rightClearance=float.NaN;Assert.That(copy.Valid,Is.False);
            copy=value.Copy();copy.enabled=false;Assert.That(copy.Valid,Is.False);
            Assert.That(new RoomFootPlacement().Default,Is.True);
        }
        [Test]public void FootPlacementMustBeOneSharedCompletionCapabilityAndPassiveFact()
        {
            var action=BehaviourCatalog.Actions.SingleOrDefault(x=>x.Id=="object.feet.configure");
            Assert.That(action,Is.Not.Null,"Human book controls and delegated agents need the same saved setting action");
            Assert.That(action.Duration,Is.EqualTo("completion"));
            Assert.That(BehaviourCatalog.Fact("object.feet.settings"),Is.Not.Null);
        }
    }
}
