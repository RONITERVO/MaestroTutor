// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [Test]public void RestorationQueryBatchObservesBothParticipantHoldsAndExplicitRecoveryImmediately(){
            var(pool,prop)=MediumFixture(.05f);var vessel=editor.Find(pool);var actor=editor.Find(prop);
            var point=new Vector3(4,1.4f,0);var queries=new RoomEnvironmentQueries();
            using var batch=queries.Begin(physics);
            Assert.That(batch.CanSimulate(point,actor,vessel),Is.True);
            foreach(var item in new[]{actor,vessel}){
                var binding=item.GetComponent<RoomEnvironmentBinding>();binding.BeginRestoration(true);
                Assert.That(physics.CanSimulate(point,item),Is.False);
                Assert.That(batch.CanSimulate(point,actor,vessel),Is.False,"A captured ground list must not bypass either participant's live restoration hold");
                Assert.That(batch.CanOccupy(point,actor,vessel),Is.False);
                binding.FinishRestoration();
                Assert.That(binding.RestorationHeld,Is.True,"Available ground does not authorize recovery");
                Assert.That(batch.CanSimulate(point,actor,vessel),Is.False);
                binding.RetryRestoration();Assert.That(binding.RestorationHeld,Is.False,binding.RestorationReason);
                Assert.That(batch.CanSimulate(point,actor,vessel),Is.True,"An explicit successful recovery is visible to the same synchronous query batch");
            }
            physics.PausePhysics();Assert.That(batch.CanSimulate(point,actor,vessel),Is.False);
            Assert.That(batch.CanOccupy(point,actor,vessel),Is.True,"Paused inspection still uses accepted geometry");
        }
        [Test]public void RestorationQueryHeldWaterDoesNotReportActiveOrApplyBuoyancyBeforeExplicitRecovery(){
            var(pool,prop)=MediumFixture(.05f);var point=new Vector3(4,1.4f,0);
            var binding=editor.Find(pool).GetComponent<RoomEnvironmentBinding>();
            Assert.That((bool)editor.Liquids.ObserveMedium(point,prop)["active"],Is.True);
            binding.BeginRestoration(true);binding.FinishRestoration();
            Assert.That(binding.RestorationHeld,Is.True);
            var medium=editor.Liquids.ObserveMedium(point,prop);
            Assert.That((bool)medium["found"],Is.True,"Retain water identity while recovery is pending");
            Assert.That((bool)medium["active"],Is.False);
            editor.Liquids.TickMedium(.02f);
            Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False,"A visible but held vessel cannot supply water forces");
            binding.RetryRestoration();Assert.That(binding.RestorationHeld,Is.False,binding.RestorationReason);
            Assert.That((bool)editor.Liquids.ObserveMedium(point,prop)["active"],Is.True);
            editor.Liquids.TickMedium(.02f);
            Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.True);
            physics.PausePhysics();Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
        }
        [Test]public void RestorationQueryPausedWadingRequiresBothParticipantsRecovered(){
            var(pool,_)=MediumFixture(.05f);physics.PausePhysics();
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="wade",maxDepthMetres=.25f},out var error),Is.True,error);
            var actor=editor.Find("maestro");var from=new Vector3(3,1.4f,0);var to=new Vector3(5,1.4f,0);
            Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out var hit),Is.True,hit.Reason);
            foreach(var item in new[]{actor,editor.Find(pool)}){
                var binding=item.GetComponent<RoomEnvironmentBinding>();binding.BeginRestoration(true);binding.FinishRestoration();
                Assert.That(binding.RestorationHeld,Is.True);
                Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.False);
                Assert.That(hit.Reason,Does.Contain("environment"));
                binding.RetryRestoration();Assert.That(binding.RestorationHeld,Is.False,binding.RestorationReason);
                Assert.That(editor.Liquids.CheckTraversal(actor,from,to,.1f,1.7f,out hit),Is.True,hit.Reason);
                Assert.That(physics.Running,Is.False,"A geometry query or recovery does not start physics");
            }
        }
    }
}
