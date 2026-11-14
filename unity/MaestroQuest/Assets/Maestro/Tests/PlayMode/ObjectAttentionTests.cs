// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject CharacterAttention(string target,string part="Head",float seconds=5)=>new(){
            ["id"]="object.look",["version"]=1,["arguments"]=new JObject{["target"]=target,
                ["joint"]=part==null?new JObject{["kind"]="head"}:new JObject{["kind"]="part",["part"]=part},
                ["focus"]=new JObject{["kind"]="object",["objectId"]="book"},["seconds"]=seconds}};
        JObject CharacterAttentionFact(string target){
            Assert.That(BehaviourCatalog.TryRead("object.attention",1,new JObject{["target"]=target},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            return JObject.FromObject(value.Value);
        }
        [UnityTest]public IEnumerator ObjectAttentionRobotHeadMovesWhileArmsAnimateAndStopRestoresSource(){
            string id=RecipeTarget();var item=editor.Find(id);var recipe=item.GetComponent<RecipeObject>();var head=recipe.Part("Head");
            var before=head.localRotation;var rootPose=item.transform.localToWorldMatrix;var source=JsonUtility.ToJson(editor.Read(id));int revision=editor.ObjectRevision(id);
            editor.Find("book").transform.position=head.position+new Vector3(3,0,3);Physics.SyncTransforms();
            Quaternion rendered=before;bool capture=false;var probe=item.gameObject.AddComponent<AvatarChannelProbe>();probe.Sample=()=>{
                rendered=head.localRotation;if(capture){CaptureAttention(item,"robot-attention");capture=false;}
            };
            CaptureAttention(item,"robot-authored");
            Assert.That(runtime.Scheduler.Invoke(PartCall(id,"RightUpperArm",5),Time.unscaledTime,out var arms,out var error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(CharacterAttention(id),Time.unscaledTime,out var run,out error),Is.True,error);
            yield return new WaitForSeconds(.6f);
            Assert.That(Quaternion.Angle(before,rendered),Is.GreaterThan(25));Assert.That((float)CharacterAttentionFact(id)["yaw"],Is.GreaterThan(30));Assert.That((bool)CharacterAttentionFact(id)["applied"],Is.True);
            capture=true;yield return new WaitUntil(()=>!capture);
            Assert.That((string)runtime.Scheduler.Invocation(arms)["phase"],Is.EqualTo("running"));Assert.That(item.transform.localToWorldMatrix,Is.EqualTo(rootPose));
            Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(source));Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));
            var currentArms=recipe.Part("RightUpperArm").localRotation;
            Assert.That(runtime.Scheduler.CancelInvocation(run,out error),Is.True,error);yield return new WaitForSeconds(1.6f);
            Assert.That(Quaternion.Angle(before,head.localRotation),Is.LessThan(.01));Assert.That((bool)CharacterAttentionFact(id)["active"],Is.False);
            Assert.That(Quaternion.Angle(currentArms,recipe.Part("RightUpperArm").localRotation),Is.GreaterThan(1));
            runtime.Scheduler.CancelInvocation(arms,out _);
        }
        void CaptureAttention(RoomItem item,string name){
            string path=Environment.GetEnvironmentVariable("MAESTRO_OBJECT_ATTENTION_EVIDENCE");if(string.IsNullOrEmpty(path))return;
            Directory.CreateDirectory(path);
            var cameraObject=new GameObject("Attention evidence camera");cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.89f,.91f);
            var bounds=item.GetComponent<RecipeObject>().LocalBounds;var centre=item.transform.TransformPoint(bounds.center);
            float radius=bounds.extents.magnitude*item.transform.lossyScale.y;
            camera.orthographic=true;camera.orthographicSize=radius*1.12f;
            cameraObject.transform.position=centre+new Vector3(.12f,.1f,2.5f)*radius;cameraObject.transform.LookAt(centre);
            var render=new RenderTexture(900,800,24);var pixels=new Texture2D(900,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,900,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(path,name+".png"),pixels.EncodeToPNG());File.WriteAllText(Path.Combine(path,name+".json"),CharacterAttentionFact(editor.Identity(item)).ToString());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraObject);}
        }
        [UnityTest]public IEnumerator ObjectAttentionTracksHeldFocusButActorGripEndsWithoutResume(){
            string id=RecipeTarget();var item=editor.Find(id);var head=item.GetComponent<RecipeObject>().Part("Head");var original=head.localRotation;
            Assert.That(runtime.Scheduler.Invoke(CharacterAttention(id),Time.unscaledTime,out var run,out var error),Is.True,error);
            runtime.Scheduler.GrabTarget("book");Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("running"));
            editor.Find("book").transform.position=head.position+new Vector3(-3,0,3);Physics.SyncTransforms();yield return new WaitForSeconds(.6f);
            Assert.That((float)CharacterAttentionFact(id)["yaw"],Is.LessThan(-30));
            runtime.Scheduler.GrabTarget(id);yield return null;
            Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));Assert.That((bool)CharacterAttentionFact(id)["active"],Is.False);
            yield return new WaitForSeconds(.15f);Assert.That(Quaternion.Angle(head.localRotation,original),Is.LessThan(.01));
        }
        [UnityTest]public IEnumerator ObjectAttentionFailsOnReplacedJointAndRefusesPartAutoplay(){
            string id=RecipeTarget();var recipe=editor.Find(id).GetComponent<RecipeObject>();
            Assert.That(runtime.Scheduler.Invoke(CharacterAttention(id),Time.unscaledTime,out var run,out var error),Is.True,error);
            yield return null;var next=editor.Read(id).recipe.Copy();next.parts.Single(p=>p.id=="Head").size.x+=.01f;
            Assert.That(recipe.Apply(next),Is.True);yield return null;yield return null;
            Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));
            Assert.That(runtime.Scheduler.Invoke(CharacterAttention(id,"Missing"),Time.unscaledTime,out _,out _),Is.False);
            string animated=RecipeTarget(true);
            Assert.That(runtime.Scheduler.Invoke(CharacterAttention(animated,"RightUpperArm"),Time.unscaledTime,out _,out _),Is.False);
            Assert.That(editor.Find(animated).GetComponent<RecipeObject>().IsPlaying,Is.True,"A refused request does not stop autoplay");
        }
        [UnityTest]public IEnumerator ObjectAttentionSharesNativeFeatureAndNeverChangesSavedDataDuringReadiness(){
            string id=RecipeTarget();var before=JsonUtility.ToJson(editor.Snapshot());var module=new ObjectAttentionCapability();var args=(JObject)CharacterAttention(id)["arguments"];
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(ObjectAttentionCapability.Feature));
            for(int i=0;i<3;i++){Assert.That(module.CanRun(new CapabilityContext(editor,animations),args,out var error),Is.True,error);Assert.That((bool)CharacterAttentionFact(id)["active"],Is.False);}
            Assert.That(editor.Find(id).GetComponent<ObjectJointAttention>(),Is.Null);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));yield return null;
        }
        [UnityTest]public IEnumerator ObjectAttentionCompletionRetainsFinalSampleAndSourceWhenTimeScaleIsZero(){
            string id=RecipeTarget();var head=editor.Find(id).GetComponent<RecipeObject>().Part("Head");var original=head.localRotation;
            editor.Find("book").transform.position=head.position+new Vector3(2,0,2);Physics.SyncTransforms();float scale=Time.timeScale;
            try{
                Time.timeScale=0;Assert.That(runtime.Scheduler.Invoke(CharacterAttention(id,seconds:.35f),Time.unscaledTime,out var run,out var error),Is.True,error);
                yield return new WaitForSecondsRealtime(.5f);var receipt=runtime.Scheduler.Invocation(run);
                Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((bool)receipt["output"]["applied"],Is.True);Assert.That((float)receipt["output"]["yaw"],Is.GreaterThan(20));
                Assert.That((bool)CharacterAttentionFact(id)["active"],Is.False);Assert.That(Quaternion.Angle(head.localRotation,original),Is.LessThan(.01));
            }finally{Time.timeScale=scale;}
        }
        [UnityTest]public IEnumerator ObjectAttentionFocusLossAndNewerExplicitPoseCannotRestoreStaleRotation(){
            string id=RecipeTarget();var item=editor.Find(id);var head=item.GetComponent<RecipeObject>().Part("Head");
            var module=new ObjectAttentionCapability();Assert.That(module.Start(new CapabilityContext(editor,animations),"direct",(JObject)CharacterAttention(id)["arguments"],out var op,out var error),Is.True,error);
            var view=item.GetComponent<ObjectJointAttention>();view.Present(.05f);
            var explicitPose=Quaternion.Euler(4,5,6);head.localRotation=explicitPose;op.Stop(false);
            Assert.That(head.localRotation,Is.EqualTo(explicitPose));
            Assert.That(module.Start(new CapabilityContext(editor,animations),"again",(JObject)CharacterAttention(id)["arguments"],out op,out error),Is.True,error);
            view.Present(.05f);view.SendMessage("OnApplicationFocus",false);
            Assert.That(module.CanRun(new CapabilityContext(editor,animations),(JObject)CharacterAttention(id)["arguments"],out _),Is.False);
            view.SendMessage("OnApplicationFocus",true);
            yield return null;Assert.That(op.State(out _),Is.EqualTo(RuleActionState.Failed));Assert.That(view.Active,Is.False);Assert.That(Quaternion.Angle(head.localRotation,explicitPose),Is.LessThan(.01));
        }
    }
    public sealed partial class AvatarSpatialTests
    {
        [UnityTest]public IEnumerator ObjectAttentionImportedGaitPreservesOtherBonesAndCaptureRestoresHead(){
            yield return WalkingCharacter();Assert.That(SelectGait("embedded",null,out var error),Is.True,error);
            var item=gaitItem;var model=gaitModel;var head=model.Humanoid.GetBoneTransform(HumanBodyBones.Head);
            var module=new ObjectAttentionCapability();var args=(JObject)module.Example.DeepClone();args["target"]=editor.Identity(item);args["focus"]=PointFocus(new Vector3(3,1.4f,-3));
            Assert.That(module.Start(new CapabilityContext(editor,authoring),"npc",args,out var op,out error),Is.True,error);
            var nodes=model.Instance.Nodes.ToArray();var rotations=new Quaternion[nodes.Length];var positions=new Vector3[nodes.Length];var scales=new Vector3[nodes.Length];int frames=0,moved=0;
            var source=item.gameObject.AddComponent<FootSourceProbe>();var output=item.gameObject.AddComponent<AvatarChannelProbe>();
            source.Sample=()=>{for(int i=0;i<nodes.Length;i++){rotations[i]=nodes[i].localRotation;positions[i]=nodes[i].localPosition;scales[i]=nodes[i].localScale;}};
            output.Sample=()=>{
                if(!ImportedObjectWalk.Owns(item))return;
                for(int i=0;i<nodes.Length;i++){if(nodes[i]!=head)Assert.That(nodes[i].localRotation,Is.EqualTo(rotations[i]));Assert.That(nodes[i].localPosition,Is.EqualTo(positions[i]));Assert.That(nodes[i].localScale,Is.EqualTo(scales[i]));}
                if(Quaternion.Angle(head.localRotation,rotations[System.Array.IndexOf(nodes,head)])>5)moved++;frames++;
            };
            var travel=new Maestro.Quest.Interaction.GroundedTravel(editor,item,new Vector3(0,0,-2),.5f);
            try{
                Assert.That(travel.Begin(out error),Is.True,error);
                for(int i=0;i<60;i++){Assert.That(travel.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);Assert.That(op.State(out error),Is.EqualTo(RuleActionState.Ready),error);yield return null;}
                Assert.That(frames,Is.GreaterThan(40));Assert.That(moved,Is.GreaterThan(20));
                source.Sample=output.Sample=null;travel.Stop(false);
                var view=item.GetComponent<ObjectJointAttention>();view.Restore();var authored=head.localRotation;view.Present(.05f);
                var capture=new ImportedMotionPose(model,model.transform.localPosition);Assert.That(Quaternion.Angle(head.localRotation,authored),Is.LessThan(.01));view.Present(.05f);capture.Restore();Assert.That(Quaternion.Angle(head.localRotation,authored),Is.LessThan(.01));
            }finally{source.Sample=output.Sample=null;op.Stop(false);travel.Stop(false);}
        }
        [UnityTest]public IEnumerator ObjectAttentionMaestroAliasUsesExistingGazeAndReducedMotion(){
            Tutor();yield return null;var module=new ObjectAttentionCapability();var args=(JObject)module.Example.DeepClone();
            avatar.ReducedMotion=true;Assert.That(module.Start(new CapabilityContext(editor,authoring),"shared",args,out var op,out var error),Is.True,error);
            try{yield return new WaitForSeconds(.2f);Assert.That((bool)op.Result["active"],Is.True);Assert.That((bool)op.Result["applied"],Is.False);Assert.That(motion.OwnedBy("shared"),Is.True);}
            finally{op.Stop(false);}
        }
    }
}
