// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    // Match the production input-before-motion order, including loading frames.
    [DefaultExecutionOrder(100)]
    public sealed class NavigationManualInputProbe:MonoBehaviour {
        public Action Sample;
        void Update()=>Sample?.Invoke();
    }
    public sealed partial class AvatarSpatialTests {
        (RoomItem actor,string ground,string far) ColdWalkingScene(bool maestro=false,bool aligned=true){
            Surface(new(0,-.1f,0),new(12,.2f,12));Tutor();
            editor.ConfigureObservationTracking(()=>tracked);
            var(ground,area)=NavigationGround(new(0,-2.2f,0));
            var(far,farArea)=NavigationGround(new(12,-2.2f,0));
            var actor=maestro?editor.Find("maestro"):WalkingBlock(new(0,-2,0));
            string id=editor.Identity(actor);
            if(maestro)Assert.That(editor.MoveObject(id,new(0,-2,0),out _),Is.True);
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Cold virtual ground",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(farArea,out error),Is.True,error);
            Physics.SyncTransforms();world.SetSurfaces(aligned,"Synthetic physical scan state");world.StartPhysics();
            Assert.That(world.Running,Is.True);Assert.That(world.EnvironmentReady(actor),Is.False);
            Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.Find(far),Is.Null);
            viewer.transform.position=new(2,1.6f,0);
            return(actor,ground,far);
        }
        CapabilityOperation EnableWaitingPhysics(){
            world.PausePhysics();var capability=new PhysicsSimulationCapability();
            var args=new JObject{["operation"]="start",["stateId"]=world.ObserveSimulation()["stateId"]};
            Assert.That((bool)world.ObserveSimulation()["canStart"],Is.True);
            Assert.That(capability.Start(new CapabilityContext(editor,authoring),"cold-start",args,out var operation,out var error),Is.True,error);
            Assert.That(operation.Complete(out error),Is.True,error);
            Assert.That(world.Running,Is.True);Assert.That(world.AnySimulationReady,Is.False);
            Assert.That((bool)operation.Result["running"],Is.True);Assert.That((bool)operation.Result["ready"],Is.False);
            Assert.That((string)operation.Result["status"],Does.Contain("waiting"));return operation;
        }
        [UnityTest]public IEnumerator NavigationStartupEnabledFixedActorWalksWithoutScanOrObservedGround(){
            var(actor,ground,far)=ColdWalkingScene(aligned:false);int revision=editor.Revision;
            var start=EnableWaitingPhysics();start.Stop(false);int intent=world.SimulationIntent;
            Assert.That(world.RealCollisions,Is.True);Assert.That(world.SurfacesReady,Is.False);
            Assert.That(editor.NativeSimulationTargets(),Is.Empty);Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision),"Enabling physics does not save the world");
            var travel=new GroundedTravel(editor,actor,new(.6f,-2,0),1.2f);
            try{
                Assert.That(travel.Begin(out var error),Is.True,error);yield return FinishWalking(travel);
                Assert.That(actor.transform.position.x,Is.EqualTo(.6f).Within(.015f));Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));
                Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
                Assert.That(world.Running,Is.True);Assert.That(world.SimulationIntent,Is.EqualTo(intent));
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator NavigationStartupEnabledWithoutTrackingDoesNotInventOrLoadGround(){
            var(actor,ground,far)=ColdWalkingScene(aligned:false);tracked=false;
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
            var before=actor.transform.position;var operation=EnableWaitingPhysics();operation.Stop(false);
            for(int i=0;i<20;i++)yield return null;
            Assert.That(world.Running,Is.True);Assert.That(world.CanSimulate(actor.transform.position,actor),Is.False);
            Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.Find(far),Is.Null);
        }
        [UnityTest]public IEnumerator NavigationStartupReadinessLoadingAndRetirementDoNotCreateSimulationIntent(){
            var(_,ground,far)=ColdWalkingScene(aligned:false);EnableWaitingPhysics().Stop(false);
            int intent=world.SimulationIntent,setup=0;string before=(string)world.ObserveSimulation()["stateId"];
            Action changed=()=>setup++;world.SetupChanged+=changed;
            try{
                var load=editor.AcquireNativeEntities(new[]{ground},()=>true,System.Threading.CancellationToken.None);
                yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);Assert.That(load.Result,Is.Null);yield return null;
                Assert.That(world.AnySimulationReady,Is.True);Assert.That(world.Running,Is.True);Assert.That(world.SimulationIntent,Is.EqualTo(intent));
                Assert.That((string)world.ObserveSimulation()["stateId"],Is.Not.EqualTo(before));
                Assert.That(editor.RetireNativeArea(editor.RegionFor(ground),out var error),Is.True,error);yield return null;
                Assert.That(world.AnySimulationReady,Is.False);Assert.That(world.Running,Is.True);Assert.That(world.SimulationIntent,Is.EqualTo(intent));
                Assert.That(setup,Is.Zero,"Native ground availability must not impersonate a scan or simulation command");Assert.That(editor.Find(far),Is.Null);
            }finally{world.SetupChanged-=changed;}
        }
        [UnityTest]public IEnumerator NavigationStartupWaitingFactsAndViewChangesDoNotLoadSavedAreas(){
            var(actor,ground,far)=ColdWalkingScene(aligned:false);EnableWaitingPhysics().Stop(false);var before=actor.transform.position;
            for(int i=0;i<20;i++){
                Assert.That((bool)world.ObserveSimulation()["ready"],Is.False);world.ObserveEnvironment();
                viewer.transform.position+=Vector3.right*.2f;yield return null;
            }
            Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.Find(far),Is.Null);
        }
        [UnityTest]public IEnumerator NavigationStartupWaitingMovementCannotOutlivePause()=>WaitingMovementCancelled(false);
        [UnityTest]public IEnumerator NavigationStartupWaitingMovementCannotOutliveWorldPlacement()=>WaitingMovementCancelled(true);
        IEnumerator WaitingMovementCancelled(bool place){
            var(actor,_,far)=ColdWalkingScene(aligned:false);EnableWaitingPhysics().Stop(false);
            var before=actor.transform.localPosition;var travel=new GroundedTravel(editor,actor,new(.6f,-2,0),1.2f);
            try{
                Assert.That(travel.Begin(out var error),Is.True,error);Assert.That(travel.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                if(place)root.transform.position+=Vector3.right;else world.PausePhysics();
                yield return null;Assert.That(travel.State(out _),Is.EqualTo(RuleActionState.Failed));travel.Stop(false);
                for(int i=0;i<25;i++){travel.Tick();yield return null;}
                Assert.That(actor.transform.localPosition,Is.EqualTo(before));Assert.That(editor.Find(far),Is.Null);
                if(!place)Assert.That(world.Running,Is.False);
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator NavigationStartupTravelPreparesColdGroundBelowTheScanWithoutLoadingDistantAreas(){
            var(actor,ground,far)=ColdWalkingScene();var before=actor.transform.position;
            var travel=new GroundedTravel(editor,actor,new(.6f,-2,0),1.2f);
            try{
                Assert.That(travel.Begin(out var error),Is.True,error);
                Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That(editor.Find(ground),Is.Null,"Admission must not load without the movement owner ticking");
                yield return FinishWalking(travel);
                Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
                Assert.That(actor.transform.position.x,Is.EqualTo(.6f).Within(.015f));Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator NavigationStartupSharedCapabilityPreparesBeforeArrivalAndReleasesItsChannel(){
            var(actor,ground,far)=ColdWalkingScene();string id=editor.Identity(actor);var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);
            var call=new JObject{["id"]="object.navigate",["version"]=1,["arguments"]=new JObject{["target"]=id,["mode"]="walk",["destination"]=new JObject{["x"]=.6,["y"]=-2,["z"]=0},["speed"]=1.2}};
            Assert.That(scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
            try{
                for(int i=0;i<260;i++){actions.Tick();scheduler.Tick(Time.unscaledTime);yield return null;}
                var result=scheduler.ObserveInvocations(run).ToString();Assert.That(result,Does.Contain("completed"),result);
                Assert.That(actor.transform.position.x,Is.EqualTo(.6f).Within(.015f));Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));
                Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
                Assert.That(editor.Ownership.Covers(run,new[]{new BehaviourCatalog.Claim(id,"locomotion")}),Is.False);
            }finally{scheduler.Suspend(true);actions.Stop(run,false);}
        }
        [UnityTest]public IEnumerator NavigationStartupFollowPreparesItsOwnGroundWithoutUsingThePhysicalFloor(){
            var(actor,ground,far)=ColdWalkingScene();var follow=new GroundedFollowing(editor,actor,null,1,1.2f,30);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowingFrames(follow,260);
                Assert.That(actor.transform.position.x,Is.GreaterThan(.4f));Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));
                Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator NavigationStartupLegacyMaestroFollowUsesTheSameOwnedPreparation(){
            var(actor,ground,far)=ColdWalkingScene(maestro:true);
            Assert.That(motion.Begin("cold-follow",AvatarSpatialMode.Follow,out var error),Is.True,error);
            Assert.That(editor.Find(ground),Is.Null);
            for(int i=0;i<240;i++)yield return null;
            Assert.That(motion.Active,Is.True,motion.Status);Assert.That(actor.transform.position.x,Is.GreaterThan(.2f));
            Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);motion.Stop();
        }
        IEnumerator IdleFollowReady(){
            for(int i=0;i<240&&motion.Active&&!motion.Status.Contains("chosen distance");i++)yield return null;
            Assert.That(motion.Active,Is.True,motion.Status);Assert.That(motion.Status,Does.Contain("chosen distance"));
        }
        [UnityTest]public IEnumerator NavigationStartupIdleFollowRetainsPreparedSupportThenWalksAndStops(){
            var(actor,ground,far)=ColdWalkingScene(maestro:true,aligned:false);viewer.transform.position=new(0,1.6f,.8f);
            Assert.That(motion.Begin("idle-follow",AvatarSpatialMode.Follow,out var error),Is.True,error);yield return IdleFollowReady();
            var before=actor.transform.position;
            for(int i=0;i<45;i++){
                yield return null;Assert.That(motion.Active,Is.True,motion.Status);
                Assert.That(NavigationField<bool>("startingPrepared"),Is.True,"Idle following retains accepted startup preparation instead of acquiring it every frame");
                Assert.That(NavigationField<RoomSpatialInputs>("startingInputs"),Is.Null,"The accepted map owns support after preparation");
                Assert.That(actor.transform.position,Is.EqualTo(before));
            }
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
            viewer.transform.position=new(2,1.6f,.8f);for(int i=0;i<180;i++)yield return null;
            Assert.That(motion.Active,Is.True,motion.Status);Assert.That(actor.transform.position.x,Is.GreaterThan(.2f));Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));
            world.PausePhysics();yield return null;Assert.That(motion.Active,Is.False);Assert.That(NavigationField<bool>("startingPrepared"),Is.False);
            var stopped=actor.transform.position;world.StartPhysics();for(int i=0;i<20;i++)yield return null;
            Assert.That(motion.Active,Is.False);Assert.That(actor.transform.position,Is.EqualTo(stopped));
        }
        [UnityTest]public IEnumerator NavigationStartupIdleFollowStopsOnGroundLossAndNeedsAFreshOwnerAfterRepair(){
            var(actor,ground,_)=ColdWalkingScene(maestro:true,aligned:false);viewer.transform.position=new(0,1.6f,.8f);
            Assert.That(motion.Begin("idle-ground",AvatarSpatialMode.Follow,out var error),Is.True,error);yield return IdleFollowReady();
            var before=actor.transform.position;var surface=editor.Find(ground).GetComponentInChildren<RoomWalkableSurface>().Collision;
            surface.enabled=false;Physics.SyncTransforms();for(int i=0;i<3;i++)yield return null;
            Assert.That(world.Running,Is.True);Assert.That(motion.Active,Is.False);Assert.That(NavigationField<bool>("startingPrepared"),Is.False);
            surface.enabled=true;Physics.SyncTransforms();for(int i=0;i<20;i++)yield return null;
            Assert.That(motion.Active,Is.False);Assert.That(actor.transform.position,Is.EqualTo(before));
            Assert.That(motion.Begin("fresh-idle",AvatarSpatialMode.Follow,out error),Is.True,error);yield return IdleFollowReady();
            Assert.That(NavigationField<bool>("startingPrepared"),Is.True);motion.Stop();Assert.That(NavigationField<bool>("startingPrepared"),Is.False);
        }
        [UnityTest]public IEnumerator NavigationStartupManualNeutralDoesNotLoadAndFreshInputCanPrepare(){
            var(actor,ground,far)=ColdWalkingScene(maestro:true);var before=actor.transform.position;
            Assert.That(motion.Begin("cold-stick",AvatarSpatialMode.Manual,out var error),Is.True,error);
            var input=physicalRoot.AddComponent<NavigationManualInputProbe>();var direction=Vector3.zero;
            input.Sample=()=>motion.ManualDirection("cold-stick",direction);yield return null;
            Assert.That(editor.Find(ground),Is.Null);Assert.That(actor.transform.position,Is.EqualTo(before));
            direction=Vector3.right;
            for(int i=0;i<110;i++)yield return null;
            Assert.That(motion.Active,Is.True,motion.Status);
            Assert.That(actor.transform.position.x,Is.GreaterThan(.2f),motion.Status);
            direction=Vector3.zero;var stopped=actor.transform.position;
            for(int i=0;i<5;i++)yield return null;
            Assert.That(actor.transform.position,Is.EqualTo(stopped));Assert.That(stopped.x,Is.GreaterThan(.2f));Assert.That(stopped.y,Is.LessThan(-1.8f));
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);motion.Stop();
        }
        [UnityTest]public IEnumerator NavigationStartupStopDuringPreparationCannotMoveLater(){
            var(actor,_,_)=ColdWalkingScene();var before=actor.transform.position;
            var travel=new GroundedTravel(editor,actor,new(.6f,-2,0),1.2f);
            Assert.That(travel.Begin(out var error),Is.True,error);
            Assert.That(travel.State(out error),Is.EqualTo(RuleActionState.Preparing),error);travel.Stop(false);
            for(int i=0;i<40;i++){travel.Tick();yield return null;}
            Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That((bool)GroundedMovement.Observe(editor,actor)["active"],Is.False);
        }
        [UnityTest]public IEnumerator NavigationStartupTrackingLossCancelsMaestroAndDoesNotResume(){
            var(actor,_,_)=ColdWalkingScene(maestro:true);var before=actor.transform.position;
            Assert.That(motion.Begin("cold-follow",AvatarSpatialMode.Follow,out var error),Is.True,error);
            yield return null;tracked=false;yield return null;
            Assert.That(motion.Active,Is.False);var stopped=actor.transform.position;tracked=true;
            for(int i=0;i<25;i++)yield return null;
            Assert.That(actor.transform.position,Is.EqualTo(stopped));Assert.That(stopped.y,Is.LessThan(-1.8f));
        }
        [UnityTest]public IEnumerator NavigationStartupMissingVirtualGroundDoesNotBorrowTheScan(){
            var(actor,ground,_)=ColdWalkingScene();
            // Retirement drains its native handles before saved geometry can be edited.
            for(int i=0;i<10;i++)yield return null;
            Assert.That(editor.DeleteObject(ground,out var error),Is.True,error);
            var before=actor.transform.position;var travel=new GroundedTravel(editor,actor,new(.6f,-2,0),1.2f);
            try{Assert.That(travel.Begin(out _),Is.False);yield return null;Assert.That(actor.transform.position,Is.EqualTo(before));}
            finally{travel.Stop(false);}
        }
    }
}
