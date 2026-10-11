// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
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
    public sealed partial class AvatarSpatialTests
    {
        string PresentationLayer(){var c=new VisibilityLayerSaveCapability();var args=c.Example;args["realDepth"]=true;Assert.That(c.Start(new CapabilityContext(editor,authoring),"layer",args,out var operation,out var error),Is.True,error);return (string)operation.Result["id"];}
        JObject LayerView(string id){Assert.That(BehaviourCatalog.TryRead("visibility.presentation",1,new JObject{["id"]=id},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);}
        JObject LayerViewRequest(string id,float opacity,float seconds=0,bool depth=true){var state=LayerView(id);return new JObject{["operation"]="start",["runId"]=modeActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject{["id"]="visibility.layer.present",["version"]=1,["arguments"]=new JObject{["id"]=id,["stateId"]=state["stateId"].DeepClone(),["viewStateId"]=state["viewStateId"].DeepClone(),["opacity"]=opacity,["realDepth"]=depth,["seconds"]=seconds}}};}
        IEnumerator BlendLayer(string id,float opacity,float seconds=0,bool depth=true){Assert.That(modeActions.Execute(LayerViewRequest(id,opacity,seconds,depth),out var error),Is.True,error);yield return null;Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),modeActions.Observe().ToString());}
        [UnityTest]public IEnumerator LayerViewStartsAndFinishesWithoutSavingStoppingActorsOrChangingPhysics(){
            SharedModes(out _,out _);string id=PresentationLayer();Assert.That(editor.BindVisibility("maestro",editor.ObjectRevision("maestro"),id,editor.VisibilityRevision(id),out var error),Is.True,error);
            Assert.That(motion.Begin("existing follow",Avatar.AvatarSpatialMode.Follow,out error),Is.True,error);int revision=editor.VisibilityRevision(id);var environment=world.ObserveEnvironment().ToString();
            Assert.That(modeActions.Execute(LayerViewRequest(id,0,.1f),out error),Is.True,error);
            // Observe the start before yielding: a slow Editor frame may correctly
            // finish this short fade before the coroutine resumes.
            var started=LayerView(id);Assert.That((bool)started["progress"]["blending"],Is.True);Assert.That((float)started["progress"]["currentOpacity"],Is.EqualTo(1));string state=(string)started["stateId"];
            yield return new WaitForSecondsRealtime(.15f);Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),modeActions.Observe().ToString());Assert.That((bool)LayerView(id)["progress"]["blending"],Is.False);Assert.That((float)LayerView(id)["progress"]["effectiveOpacity"],Is.Zero);
            Assert.That(editor.Find("maestro").GetComponent<RoomAppearanceView>().PointerVisible,Is.False);Assert.That(motion.OwnedBy("existing follow"),Is.True);Assert.That(world.ObserveEnvironment().ToString(),Is.EqualTo(environment));Assert.That(editor.VisibilityRevision(id),Is.EqualTo(revision));Assert.That(editor.ReadVisibility(id).opacity,Is.EqualTo(.5f));Assert.That((string)LayerView(id)["stateId"],Is.EqualTo(state));
            yield return BlendLayer(id,1);Assert.That((float)LayerView(id)["progress"]["effectiveOpacity"],Is.EqualTo(.5f));Assert.That(editor.Find("maestro").GetComponent<RoomAppearanceView>().PointerVisible,Is.True);
        }
        [UnityTest]public IEnumerator LayerViewRecallReplayAndStaleViewGuardsNeverRestoreHiddenLayers(){
            var controls=SharedModes(out _,out _);string id=PresentationLayer();var stale=LayerViewRequest(id,0);controls.Recover();Assert.That(modeActions.Execute(stale,out _),Is.False);
            var request=LayerViewRequest(id,0);Assert.That(modeActions.Execute(request,out var error),Is.True,error);yield return null;controls.Recover();var recovered=LayerView(id);
            Assert.That(modeActions.Execute(request,out error),Is.True,error);Assert.That(JToken.DeepEquals(recovered,LayerView(id)),Is.True);
            stale=LayerViewRequest(id,0);controls.CycleBackdrop();controls.CycleBackdrop();Assert.That(modeActions.Execute(stale,out _),Is.False);
            stale=LayerViewRequest(id,0);controls.SendMessage("OnApplicationFocus",false);controls.SendMessage("OnApplicationFocus",true);Assert.That(modeActions.Execute(stale,out _),Is.False);
        }
        [UnityTest]public IEnumerator LayerViewRecoversOnTrackingRuntimeTemporaryAndDisabledViewBoundaries(){
            var controls=SharedModes(out var view,out _);string id=PresentationLayer();yield return BlendLayer(id,0,10,false);
            tracked=false;yield return null;Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(1));Assert.That((bool)LayerView(id)["progress"]["blending"],Is.False);tracked=true;
            yield return BlendLayer(id,.2f);using(editor.RuntimeGate.Hold("align room")){Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(1));}
            yield return BlendLayer(id,.2f);view.enabled=false;yield return null;Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(1));view.enabled=true;
            world.PausePhysics();yield return BlendLayer(id,.2f);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(1));
            while(editor.TemporarySavePending)yield return null;yield return BlendLayer(id,.3f);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(1));
            yield return BlendLayer(id,.2f);editor.enabled=false;Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(1));editor.enabled=true;controls.Recover();
        }
        [UnityTest]public IEnumerator LayerViewToolRecallPreservesViewAndControlsUntilExplicitStopMR(){
            var controls=SharedModes(out var view,out _);string id=PresentationLayer();TravelGround();world.PausePhysics();yield return ChangeMode("user.enable");yield return BlendLayer(id,.2f);yield return Present(.5f,false);
            using(editor.WriteGate.TryFreeze(out var error)){
                Assert.That(editor.RecallTools(null,0,true,out _,out _),Is.False);
                Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(.2f));Assert.That(view.BackdropOpacity,Is.EqualTo(.5f));
            }
            Assert.That(editor.RecallTools(null,0,true,out _,out var issue),Is.True,issue);
            Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(.2f));Assert.That(view.BackdropOpacity,Is.EqualTo(.5f));Assert.That(controls.UserEnabled,Is.True);
            controls.Recover();Assert.That((float)LayerView(id)["opacity"],Is.EqualTo(1));Assert.That(view.BackdropOpacity,Is.Zero);Assert.That(view.RealDepth,Is.True);Assert.That(controls.UserEnabled,Is.False);
        }
        [UnityTest]public IEnumerator LayerViewAdmissionRejectsUntrackedBusyDeletedAndInvalidPreferences(){
            SharedModes(out _,out _);string id=PresentationLayer();frame.busy=true;Assert.That(modeActions.Execute(LayerViewRequest(id,0),out _),Is.False);frame.busy=false;
            tracked=false;Assert.That(modeActions.Execute(LayerViewRequest(id,0),out _),Is.False);tracked=true;
            foreach(float value in new[]{-1f,1.01f})Assert.That(modeActions.Execute(LayerViewRequest(id,value),out _),Is.False);
            Assert.That(modeActions.Execute(LayerViewRequest(id,.5f,31),out _),Is.False);
            var request=LayerViewRequest(id,.5f);Assert.That(editor.EditVisibility(null,id,editor.VisibilityRevision(id),new string[0],out var error),Is.True,error);Assert.That(modeActions.Execute(request,out _),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator LayerViewGroupMembershipStaysLiveAndAuthoredEditsResetOnlyChangedLayer(){
            SharedModes(out _,out _);string a=PresentationLayer(),b=PresentationLayer();world.PausePhysics();yield return BlendLayer(a,0);yield return BlendLayer(b,.25f);
            Assert.That(editor.BindVisibility("maestro",editor.ObjectRevision("maestro"),a,editor.VisibilityRevision(a),out var error),Is.True,error);Assert.That(editor.Find("maestro").GetComponent<RoomAppearanceView>().PointerVisible,Is.False);Assert.That((float)LayerView(a)["opacity"],Is.Zero);
            var stale=LayerViewRequest(a,0);var definition=editor.ReadVisibility(a);definition.opacity=.7f;Assert.That(editor.EditVisibility(definition,a,editor.VisibilityRevision(a),new[]{"maestro"},out error),Is.True,error);
            Assert.That(modeActions.Execute(stale,out _),Is.False);Assert.That((float)LayerView(a)["opacity"],Is.EqualTo(1));Assert.That((float)LayerView(a)["progress"]["effectiveOpacity"],Is.EqualTo(.7f));Assert.That((float)LayerView(b)["opacity"],Is.EqualTo(.25f));
            Assert.That(new RoomStorage(directory).Load(out error).visibilityLayers.Single(l=>l.id==b).opacity,Is.EqualTo(.5f));
        }
    }
}
