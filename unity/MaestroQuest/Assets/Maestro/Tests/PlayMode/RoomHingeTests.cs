// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
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
        (string moving,string mount,RoomHingeView view) HingeStage(HingeDrive drive=null,HingeLimits limits=null,float angle=0)
        {
            var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="Body",size=new Vector3(.1f,.1f,.1f)}}};
            Assert.That(editor.CreateRecipe("Hinge mount",new Vector3(4,2,4),1,recipe,null,new ObjectPhysicsSettings{mode="fixed",shape="box",mass=1},out var mount,out var error),Is.True,error);
            Assert.That(editor.CreateRecipe("Hinge rotor",new Vector3(4,2,4),1,recipe,null,new ObjectPhysicsSettings{mode="solid",shape="box",mass=1},out var moving,out error),Is.True,error);
            var item=editor.Find(moving);item.transform.localRotation=Quaternion.AngleAxis(angle,Vector3.right);item.GetComponent<RigidRoomItem>().Teleported();editor.RememberPlacement(moving);
            var h=new RoomHinge{connected=mount,drive=drive??new HingeDrive(),limits=limits??new HingeLimits()};
            Assert.That(editor.EditHinge(moving,editor.ObjectRevision(moving),"configure",mount,h,0,out error),Is.True,error);
            physics.SetSurfaces(true,"Test room aligned");physics.StartPhysics();var view=item.GetComponent<RoomHingeView>();view.Refresh();return(moving,mount,view);
        }
        [UnityTest] public IEnumerator PhysicalHingeSupportsMotorDirectionAndKeepsItsAnchorUnderGravity()
        {
            var (id,mount,view)=HingeStage(new HingeDrive{mode="motor",speed=45,force=2});yield return new WaitForFixedUpdate();Assert.That(view.Active,Is.True,view.Error);
            for(int i=0;i<25;i++)yield return new WaitForFixedUpdate();Assert.That(view.Angle,Is.GreaterThan(5).And.LessThan(90));
            Assert.That(Vector3.Distance(editor.Find(id).transform.position,editor.Find(mount).transform.position),Is.LessThan(.015f));Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.False);
        }
        [UnityTest] public IEnumerator SavedFramesKeepSpringReferenceAfterReloadAtANonzeroAngle()
        {
            var (id,mount,view)=HingeStage(new HingeDrive{mode="spring",target=20,spring=5,damper=1},new HingeLimits{enabled=true,minimum=-40,maximum=60},45);
            for(int i=0;i<70;i++)yield return new WaitForFixedUpdate();Assert.That(view.Angle,Is.EqualTo(20).Within(4));
            physics.PausePhysics();view.Refresh();editor.RememberPlacement(id);var data=editor.Read(id);yield return null;
            Object.Destroy(view);yield return null;view=editor.Find(id).gameObject.AddComponent<RoomHingeView>();view.Apply(editor,data.hinges);physics.StartPhysics();view.Refresh();
            for(int i=0;i<50;i++)yield return new WaitForFixedUpdate();Assert.That(view.Angle,Is.EqualTo(20).Within(4));
        }
        [UnityTest] public IEnumerator MissingAndMisalignedHingesFreezeUntilExplicitRepairAndPauseDropsVelocity()
        {
            var (id,mount,view)=HingeStage(new HingeDrive{mode="motor",speed=180,force=2});yield return new WaitForFixedUpdate();
            physics.PausePhysics();view.Refresh();Assert.That(view.Phase,Is.EqualTo("paused"));Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);yield return null;
            editor.Find(mount).transform.position+=Vector3.right;physics.StartPhysics();view.Refresh();Assert.That(view.Phase,Is.EqualTo("misaligned"));Assert.That(view.Active,Is.False);
            Assert.That(editor.EditHinge(id,editor.ObjectRevision(id),"align",mount,null,0,out var error),Is.True,error);yield return null;view.Refresh();Assert.That(view.Active,Is.True,view.Error);
            editor.Find(mount).gameObject.SetActive(false);view.Refresh();Assert.That(view.Phase,Is.EqualTo("missing"));Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);yield return null;
        }
        [UnityTest] public IEnumerator HingeEditsAreRevisionBoundAndFailedAlignmentCannotMoveTheLiveObject()
        {
            var (id,mount,view)=HingeStage();physics.PausePhysics();view.Refresh();yield return null;
            int revision=editor.ObjectRevision(id);var before=editor.Find(id).transform.position;editor.Find(mount).transform.position+=Vector3.right;
            var pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            Assert.That(editor.EditHinge(id,revision,"align",mount,null,30,out _),Is.False);Assert.That(editor.Find(id).transform.position,Is.EqualTo(before));Directory.Delete(pending);
            Assert.That(editor.EditHinge(id,revision,"align",mount,null,30,out var error),Is.True,error);Assert.That(editor.EditHinge(id,revision,"remove",null,null,0,out _),Is.False);
            Assert.That(BehaviourCatalog.TryRead("object.hinge",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(fact.Characters,Is.LessThanOrEqualTo(1024));
            editor.Undo();Assert.That(Vector3.Distance(editor.Find(id).transform.position,before),Is.LessThan(.001f));yield return null;
        }
        [UnityTest] public IEnumerator ExplicitTeleportInvalidatesTheOldConstraintAndLimitsBoundAMotor()
        {
            var (id,mount,view)=HingeStage(new HingeDrive{mode="motor",speed=180,force=2},new HingeLimits{enabled=true,minimum=-20,maximum=35});
            for(int i=0;i<45;i++)yield return new WaitForFixedUpdate();Assert.That(view.Angle,Is.InRange(25f,38f));
            var item=editor.Find(id);item.transform.position+=Vector3.right;item.GetComponent<RigidRoomItem>().Teleported();view.Refresh();Assert.That(view.Active,Is.False);yield return null;view.Refresh();Assert.That(view.Phase,Is.EqualTo("misaligned"));Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);
        }
        [UnityTest] public IEnumerator ControllerGripKeepsTheConstraintAndAnimationOwnershipSuspendsIt()
        {
            var (id,mount,view)=HingeStage();yield return new WaitForFixedUpdate();var item=editor.Find(id);var hand=Hand(1,item.transform.position);
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);view.Refresh();Assert.That(view.Active,Is.True);Assert.That(item.Grab.movementType,Is.EqualTo(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking));
            manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);var other=editor.Find(mount).GetComponent<RigidRoomItem>();other.SetAnimationOwner(this,true);view.Refresh();Assert.That(view.Phase,Is.EqualTo("owned"));Assert.That(view.Active,Is.False);other.SetAnimationOwner(this,false);yield return null;view.Refresh();Assert.That(view.Active,Is.True,view.Error);
        }
        [UnityTest] public IEnumerator HingeTemporaryDiscardRestoresTheSavedConnectionWithoutStartingPhysics()
        {
            var (id,mount,view)=HingeStage();physics.PausePhysics();view.Refresh();yield return null;
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.EditHinge(id,editor.ObjectRevision(id),"remove",null,null,0,out error),Is.True,error);Assert.That(editor.Read(id).hinges,Is.Empty);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(id).hinges.Single().connected,Is.EqualTo(mount));Assert.That(physics.Running,Is.False);yield return null;
        }
    }
}
