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
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (string moving,string mount,RoomConnectionView view) SliderStage(SliderSettings settings=null,float distance=0,float scale=1,Quaternion? frame=null){
            var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="Body",size=Vector3.one*.05f}}};var q=frame??Quaternion.identity;
            Assert.That(editor.CreateRecipe("Slider mount",new Vector3(4,2,4),scale,recipe,null,new ObjectPhysicsSettings{mode="fixed",shape="box",mass=1},out var mount,out var error),Is.True,error);
            Assert.That(editor.CreateRecipe("Slider cap",new Vector3(4,2,4)+q*Vector3.right*(distance*scale),scale,recipe,null,new ObjectPhysicsSettings{mode="solid",shape="box",mass=.1f},out var moving,out error),Is.True,error);
            var h=new RoomConnection{kind="slider",connected=mount,slide=settings??new SliderSettings(),ownerFrame=new ConnectionFrame{rotation=q},connectedFrame=new ConnectionFrame{rotation=q}};
            Assert.That(editor.EditConnection(moving,editor.ObjectRevision(moving),"configure",mount,h,0,out error),Is.True,error);
            physics.SetSurfaces(true,"Test room aligned");physics.StartPhysics();var view=editor.Find(moving).GetComponent<RoomConnectionView>();view.Refresh();return(moving,mount,view);
        }
        [UnityTest] public IEnumerator SliderAsymmetricLimitsConstrainTranslationAndRotationUnderImpulse(){
            var (id,mount,view)=SliderStage(new SliderSettings{minimum=-.03f,maximum=.12f});yield return new WaitForFixedUpdate();Assert.That(view.Active,Is.True,view.Error);
            var body=editor.Find(id).GetComponent<Rigidbody>();body.AddForce(new Vector3(.03f,.03f,.03f),ForceMode.Impulse);body.AddTorque(Vector3.one*.01f,ForceMode.Impulse);
            for(int i=0;i<45;i++){body.AddForce(Vector3.right);yield return new WaitForFixedUpdate();Assert.That(view.Travel,Is.InRange(-.04f,.13f));}Assert.That(view.Travel,Is.EqualTo(.12f).Within(.008f));
            var delta=body.position-editor.Find(mount).transform.position;Assert.That(new Vector2(delta.y,delta.z).magnitude,Is.LessThan(.015f));Assert.That(Quaternion.Angle(body.rotation,Quaternion.identity),Is.LessThan(3));
            for(int i=0;i<45;i++){body.AddForce(Vector3.left);yield return new WaitForFixedUpdate();Assert.That(view.Travel,Is.InRange(-.04f,.13f));}Assert.That(view.Travel,Is.EqualTo(-.03f).Within(.008f));
        }
        [UnityTest] public IEnumerator SliderSpringUsesSavedReferenceAfterReloadAtNonzeroTravel(){
            var (id,mount,view)=SliderStage(new SliderSettings{minimum=-.05f,maximum=.15f,mode="spring",target=.03f,spring=40,damper=4,speed=0},.11f);
            for(int i=0;i<80;i++)yield return new WaitForFixedUpdate();Assert.That(view.Travel,Is.EqualTo(.03f).Within(.007f));
            physics.PausePhysics();view.Refresh();yield return null;Assert.That(editor.EditConnection(id,editor.ObjectRevision(id),"slide",mount,null,.09f,out var error),Is.True,error);var data=editor.Read(id);
            Object.Destroy(view);yield return null;view=editor.Find(id).gameObject.AddComponent<RoomConnectionView>();view.Apply(editor,data.connections);physics.StartPhysics();
            for(int i=0;i<70;i++)yield return new WaitForFixedUpdate();Assert.That(view.Travel,Is.EqualTo(.03f).Within(.007f));
        }
        [UnityTest] public IEnumerator SliderMotorUsesPositiveFrameDirectionAndScalesTravel(){
            var q=Quaternion.Euler(0,90,0);var (id,mount,view)=SliderStage(new SliderSettings{minimum=-.05f,maximum=.1f,mode="motor",speed=.2f,damper=15,force=20},0,1.6f,q);
            for(int i=0;i<60;i++)yield return new WaitForFixedUpdate();Assert.That(view.Travel,Is.EqualTo(.1f).Within(.008f));Assert.That(Vector3.Distance(editor.Find(id).transform.position,editor.Find(mount).transform.position),Is.EqualTo(.16f).Within(.012f));
            physics.PausePhysics();view.Refresh();yield return null;var d=editor.Read(id).connections.Single();d.slide.speed=-.2f;Assert.That(editor.EditConnection(id,editor.ObjectRevision(id),"configure",mount,d,0,out var error),Is.True,error);physics.StartPhysics();
            for(int i=0;i<80;i++)yield return new WaitForFixedUpdate();Assert.That(view.Travel,Is.EqualTo(-.05f).Within(.008f));
        }
        [UnityTest] public IEnumerator SliderGripPreservesJointButOwnershipAndMissingMembersSuspendIt(){
            var (id,mount,view)=SliderStage();yield return new WaitForFixedUpdate();var item=editor.Find(id);var hand=Hand(1,item.transform.position);
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);view.Refresh();Assert.That(view.Active,Is.True);manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);
            var other=editor.Find(mount).GetComponent<RigidRoomItem>();other.SetAnimationOwner(this,true);view.Refresh();Assert.That(view.Phase,Is.EqualTo("owned"));Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);other.SetAnimationOwner(this,false);yield return null;view.Refresh();Assert.That(view.Active,Is.True,view.Error);
            editor.Find(mount).gameObject.SetActive(false);view.Refresh();Assert.That(view.Phase,Is.EqualTo("missing"));Assert.That(view.HasTravel,Is.False);Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);
            Assert.That(BehaviourCatalog.TryRead("object.connection.travel",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SliderAlignmentIsAtomicRevisionBoundAndRejectsAngularAlignment(){
            var (id,mount,view)=SliderStage();physics.PausePhysics();view.Refresh();yield return null;int revision=editor.ObjectRevision(id);var before=editor.Find(id).transform.localPosition;
            Assert.That(editor.EditConnection(id,revision,"align",mount,null,0,out _),Is.False);Assert.That(editor.EditConnection(id,revision,"slide",mount,null,.2f,out _),Is.False);
            var pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);try{Assert.That(editor.EditConnection(id,revision,"slide",mount,null,.07f,out _),Is.False);Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(before));}finally{Directory.Delete(pending);}
            Assert.That(editor.EditConnection(id,revision,"slide",mount,null,.07f,out var error),Is.True,error);Assert.That(view.Travel,Is.EqualTo(.07f).Within(.001f));Assert.That(editor.EditConnection(id,revision,"slide",mount,null,0,out _),Is.False);
            Assert.That(BehaviourCatalog.TryRead("object.connection.travel",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That((double)fact.Value,Is.EqualTo(.07).Within(.001));
            editor.Undo();Assert.That(Vector3.Distance(editor.Find(id).transform.localPosition,before),Is.LessThan(.001f));yield return null;
        }
        [UnityTest] public IEnumerator SliderIncludedButtonCanBeCapturedWithFreshIdentitiesAndReturnsAfterPress(){
            var module=JObject.Parse(Resources.Load<TextAsset>("Programs/Modules/SpringButton").text);ProgramModuleLibrary.Validate(module);var args=(JObject)module["program"]["functions"][1]["body"][0]["arguments"].DeepClone();args["position"]=new JObject{["x"]=4,["y"]=2,["z"]=4};
            var batch=JsonUtility.FromJson<CreationBatch>(args.ToString());Assert.That(editor.CreateBatch(batch,out var ids,out var error),Is.True,error);var id=ids[1];var view=editor.Find(id).GetComponent<RoomConnectionView>();
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();for(int i=0;i<15;i++)yield return new WaitForFixedUpdate();Assert.That(view.Active,Is.True,view.Error);Assert.That(view.Travel,Is.LessThan(.005f));
            var sequence=workshop.Selected;
            var source=JObject.Parse(@"{'version':3,'dataVersion':1,'moduleVersion':1,'entry':'main','resources':[],'state':[{'name':'phase','initial':'waiting'}],'events':[],'functions':[{'name':'main','returns':'void','parameters':[],'locals':[{'name':'received','initial':false}],'body':[{'id':'pressed','op':'call','module':'button','function':'waitForTravel','args':[],'result':'received'},{'id':'showPress','op':'setState','variable':'phase','value':{'value':'pressed'}},{'id':'released','op':'call','module':'button','function':'waitForTravel','args':[],'result':'received'},{'id':'showRelease','op':'setState','variable':'phase','value':{'value':'released'}},{'id':'hold','op':'sleep','seconds':{'value':20}}]}]}");
            source["imports"]=new JArray(new JObject{["alias"]="button",["hash"]=ProgramModules.Hash(module),["module"]=module,["signals"]=new JObject()});
            source["functions"][0]["body"][0]["args"]=new JArray(new JObject{["value"]=id},new JObject{["value"]=.02},new JObject{["value"]=true},new JObject{["value"]=0});
            source["functions"][0]["body"][2]["args"]=new JArray(new JObject{["value"]=id},new JObject{["value"]=.005},new JObject{["value"]=false},new JObject{["value"]=0});sequence.program=source.ToString();sequence.repeat=false;
            Assert.That(new RoomAgentExecutor(editor).Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new Maestro.Quest.Rules.RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new Maestro.Quest.Rules.RuleEdit{kind="save",sequence=sequence}}}}}},out error,out _),Is.True,error);Assert.That(runtime.Trigger(sequence.id),Is.True);
            var body=editor.Find(id).GetComponent<Rigidbody>();for(int i=0;i<25;i++){body.AddForce(Vector3.down*9);yield return new WaitForFixedUpdate();}Assert.That(view.Travel,Is.GreaterThan(.025f).And.LessThan(.045f));Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(v=>v.name=="phase").value,Is.EqualTo("pressed"));
            for(int i=0;i<70;i++)yield return new WaitForFixedUpdate();Assert.That(view.Travel,Is.LessThan(.005f));Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(v=>v.name=="phase").value,Is.EqualTo("released"));runtime.StopAll();physics.PausePhysics();view.Refresh();yield return null;
            var members=ids.Select((target,index)=>new ConstructionMember{target=target,slot="piece_"+index,revision=editor.ObjectRevision(target)}).ToArray();Assert.That(editor.CaptureConstruction(members,out var captured,out error),Is.True,error);
            ProgramModuleLibrary.Validate(ConstructionModule.Definition(captured,"My button"));Assert.That(editor.CreateBatch(captured,out var fresh,out error),Is.True,error);Assert.That(editor.Read(fresh[1]).connections.Single().kind,Is.EqualTo("slider"));Assert.That(editor.Read(fresh[1]).connections.Single().connected,Is.EqualTo(fresh[0]));Assert.That(fresh,Does.Not.Contain(id));yield return null;
        }
    }
}
