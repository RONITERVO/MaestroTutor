// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public class RoomRecipeTests
    {
        [Test] public void CancelledClientCannotReplayOrReuseAnOldSequenceReceipt()
        {
            var inbox=new RoomAgentInbox();var snapshot=new RoomAgentSnapshot {clientId=new string('a',32)};
            Assert.That(inbox.TryAccept(snapshot,out _),Is.False);string first=inbox.Session;
            snapshot.session=first;snapshot.request=new RoomAgentRequest {session=first,sequence=1};
            Assert.That(inbox.TryAccept(snapshot,out _),Is.True);Assert.That(inbox.TryAccept(snapshot,out _),Is.False);
            snapshot.clientId=new string('b',32);Assert.That(inbox.TryAccept(snapshot,out _),Is.False);Assert.That(inbox.Session,Is.Not.EqualTo(first));
            Assert.That(inbox.TryAccept(snapshot,out _),Is.False);Assert.That(inbox.Ack,Is.Zero);
            snapshot.session=inbox.Session;snapshot.request.session=inbox.Session;
            Assert.That(inbox.TryAccept(snapshot,out _),Is.True);Assert.That(inbox.Ack,Is.EqualTo(1));
        }
        [Test] public void BoxRobotHasSeventeenSemanticJointsAndEditableWave()
        {
            var recipe=RecipeTemplates.BoxRobot(true);
            Assert.That(recipe.Validate(out var error),Is.True,error);
            foreach(PoseJoint joint in System.Enum.GetValues(typeof(PoseJoint))) Assert.That(recipe.parts.Any(x=>x.id==joint.ToString()),Is.True,joint.ToString());
            Assert.That(Quaternion.Angle(recipe.Sample(recipe.tracks[0],0),recipe.Sample(recipe.tracks[0],.5f)),Is.GreaterThan(100));
            var copy=recipe.Copy();copy.parts[0].size=Vector3.one;
            Assert.That(copy.parts[0].size,Is.Not.EqualTo(recipe.parts[0].size));
            Assert.That(copy.Validate(out error),Is.True,error);
        }
        [Test] public void RecipesRejectCyclesUnsupportedShapesBadQuaternionAndUnboundedHierarchy()
        {
            var recipe=RecipeTemplates.BoxRobot(true);recipe.parts[0].parent="Head";Assert.That(recipe.Validate(out _),Is.False);
            recipe=RecipeTemplates.BoxRobot(true);recipe.parts[0].shape="script";Assert.That(recipe.Validate(out _),Is.False);
            recipe=RecipeTemplates.BoxRobot(true);recipe.tracks[0].keys[1].rotation=new Quaternion(0,0,0,0);Assert.That(recipe.Validate(out _),Is.False);
            recipe=RecipeTemplates.BoxRobot(true);recipe.parts[1].position=new Vector3(0,2,0);recipe.parts[2].position=new Vector3(0,2,0);Assert.That(recipe.Validate(out _),Is.False);
        }
        [Test] public void TracksRejectDuplicateTimesNonFiniteValuesAndDuplicateTargets()
        {
            var recipe=RecipeTemplates.BoxRobot(true);recipe.tracks[0].keys[1].time=0;Assert.That(recipe.Validate(out _),Is.False);
            recipe=RecipeTemplates.BoxRobot(true);recipe.parts[0].color=new Color(float.NaN,0,0,1);Assert.That(recipe.Validate(out _),Is.False);
            recipe=RecipeTemplates.BoxRobot(true);recipe.tracks[1].part=recipe.tracks[0].part;Assert.That(recipe.Validate(out _),Is.False);
        }
    }
}
