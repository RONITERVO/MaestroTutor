// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
    [DefaultExecutionOrder(160)]
    public sealed class FootSourceProbe:MonoBehaviour
    {
        public Action Sample;
        void LateUpdate()=>Sample?.Invoke();
    }
    public sealed partial class AvatarSpatialTests
    {
        static void AssertFeetPose(JointPose[] expected,JointPose[] actual)
        {
            Assert.That(actual.Length,Is.EqualTo(expected.Length));
            for(int i=0;i<actual.Length;i++){
                Assert.That(actual[i].joint,Is.EqualTo(expected[i].joint));
                // Capture normalizes rotations; repeated float normalization can
                // move the last bit. This does not permit a visible pose change.
                var a=actual[i].rotation;var b=expected[i].rotation;
                Assert.That(new Vector4(a.x-b.x,a.y-b.y,a.z-b.z,a.w-b.w).magnitude,Is.LessThan(.000001f));
            }
        }
        IEnumerator FeetSave(string target,string hash="",float trim=0)
        {
            var workshop=root.GetComponent<RuleWorkshop>()??root.AddComponent<RuleWorkshop>();workshop.Initialize(editor,directory);
            while(!workshop.Memory.Ready)yield return null;
            modeRules=root.GetComponent<RoomRules>()??root.AddComponent<RoomRules>();modeRules.Initialize(workshop,editor,authoring,null,room,null);
            var args=new JObject{["target"]=target,["revision"]=editor.ObjectRevision(target),["mode"]="grounded",["modelHash"]=hash,["leftClearance"]=trim,["rightClearance"]=trim};
            var executions=new RoomExecutions(editor);
            Assert.That(executions.Execute(new JObject{["operation"]="start",["call"]=new JObject{["id"]="object.feet.configure",["version"]=1,["arguments"]=args}},out var error),Is.True,error);
            Assert.That((string)executions.Observe()["selected"]["phase"],Is.EqualTo("preparing"));
            yield return SpatialSettingsCompletion.Wait(executions);
            Assert.That((int)executions.Observe()["selected"]["output"]["revision"],Is.EqualTo(editor.ObjectRevision(target)));
        }
        [UnityTest]public IEnumerator FeetSharedSaveUndoAndPassiveFactPreserveSourcePoseAndJournal()
        {
            WalkingScene();yield return FeetSave("maestro","",.02f);
            var item=editor.Find("maestro");var rig=avatar.PoseRig.FootRig;Assert.That(rig,Is.Not.Null);
            var probe=avatar.gameObject.AddComponent<AvatarChannelProbe>();float left=-10,right=-10;string status=null;
            probe.Sample=()=>{left=rig.Left.Contact.y;right=rig.Right.Contact.y;status=item.GetComponent<HumanoidFootPlacement>().Status;};
            string before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.ObjectRevision("maestro");
            for(int i=0;i<30;i++)yield return null;
            Assert.That(left,Is.EqualTo(.02f).Within(.002f),status);Assert.That(right,Is.EqualTo(.02f).Within(.002f),status);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            var saved=new RoomStorage(directory).Load(out var error);Assert.That(error,Is.Null);Assert.That(saved.objects.Single(x=>x.id=="maestro").footPlacement.enabled,Is.True);
            Assert.That(BehaviourCatalog.TryRead("object.feet.settings",1,new JObject{["target"]="maestro"},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);
            Assert.That((bool)JObject.FromObject(fact.Value)["available"],Is.True);
            probe.Sample=null;editor.Undo();Assert.That(editor.Read("maestro").footPlacement.Default,Is.True);editor.Redo();Assert.That(editor.Read("maestro").footPlacement.leftClearance,Is.EqualTo(.02f));
        }
        [UnityTest]public IEnumerator FeetCaptureAndAuthoringRestoreTheExactUncorrectedPose()
        {
            WalkingScene();avatar.SetEditing(true);avatar.SetEditing(false);
            var rig=avatar.PoseRig.FootRig;var rest=avatar.PoseRig.Capture();
            yield return FeetSave("maestro","",.04f);
            var source=avatar.PoseRig.Capture(); // Capture must remove the previous late correction synchronously.
            var view=avatar.GetComponent<HumanoidFootPlacement>();
            avatar.PoseRig.SetManual(true);
            for(int i=0;i<20;i++){view.Restore();avatar.PoseRig.Apply(source);view.Present(1f/72);}
            Assert.That(rig.Left.Contact.y,Is.EqualTo(.04f).Within(.002));
            AssertFeetPose(source,avatar.PoseRig.Capture());
            authoring.TogglePose();view.Present(1f/72);Assert.That(view.Status,Is.EqualTo("authored pose"));
            authoring.Stop();yield return null;
        }
        [UnityTest]public IEnumerator FeetDynamicProfileHeldKinematicPreservesItsAuthoredPose()
        {
            yield return WalkingCharacter();yield return FeetSave(editor.Identity(gaitItem),gaitModel.AssetHash,.04f);
            var rigid=gaitItem.GetComponent<RigidRoomItem>();var body=gaitItem.GetComponent<Rigidbody>();
            rigid.Configure(world,ItemPhysics.Solid,1);rigid.SetGeometryReady(false);
            Assert.That(rigid.Dynamic,Is.True);Assert.That(body.isKinematic,Is.True);
            var view=gaitItem.GetComponent<HumanoidFootPlacement>();view.Restore();
            var rig=gaitModel.FootRig;var foot=rig.Left.Foot.localRotation;var hips=rig.Hips.localPosition;
            view.Present(1f/72);
            Assert.That(view.Status,Is.EqualTo("authored pose"));
            Assert.That(rig.Left.Foot.localRotation,Is.EqualTo(foot));Assert.That(rig.Hips.localPosition,Is.EqualTo(hips));
        }
        [UnityTest]public IEnumerator FeetObserveAReplacementWorkshopBeforeItCapturesAnAuthoredFrame()
        {
            WalkingScene();yield return FeetSave("maestro","",.04f);
            UnityEngine.Object.DestroyImmediate(authoring);
            authoring=root.AddComponent<AnimationWorkshop>();authoring.Initialize(editor);
            var view=avatar.GetComponent<HumanoidFootPlacement>();var rig=avatar.PoseRig.FootRig;
            // The replacement appears after this component's configuration.
            yield return null;avatar.PoseRig.SetManual(true);view.Restore();
            float original=rig.Left.Contact.y;
            for(int i=0;i<20;i++)view.Present(1f/72);
            Assert.That(rig.Left.Contact.y,Is.EqualTo(.04f).Within(.002));
            float atStarting=float.NaN;authoring.Starting+=_=>atStarting=rig.Left.Contact.y;
            editor.Select(editor.Find("maestro"));authoring.AddFrame();
            Assert.That(atStarting,Is.EqualTo(original).Within(.000001f),"Presentation must be restored before authoring listeners and capture");
            authoring.Stop();yield return null;
        }
        [UnityTest]public IEnumerator FeetExactCalibrationAndColdFactsOnlyLoadThroughExplicitActions()
        {
            yield return WalkingCharacter();string id=editor.Identity(gaitItem);
            Assert.That(gaitModel.FootRig,Is.Not.Null,"Walking fixture has measured rest feet");
            yield return FeetSave(id,gaitModel.AssetHash);
            var wrong=editor.Read(id).footPlacement.Copy();wrong.modelHash=new string('0',64);
            Assert.That(editor.CanConfigureFeet(id,editor.ObjectRevision(id),wrong,out var error),Is.False);Assert.That(error,Does.Contain("exact model"));
            world.PausePhysics();var retire=editor.RetireNativeArea("");
            for(int i=0;i<600&&!retire.IsCompleted;i++)yield return null;
            Assert.That(retire.IsCompleted,Is.True);Assert.That(retire.Result,Is.Null);yield return null;
            var budget=ImportedModel.LiveBudget;int revision=editor.ObjectRevision(id);
            for(int i=0;i<3;i++){var fact=editor.ObserveFeet(id);Assert.That((bool)fact["available"],Is.False);Assert.That((string)fact["mode"],Is.EqualTo("grounded"));yield return null;}
            Assert.That(editor.Find(id),Is.Null);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));
            // A real action, unlike passive inspection, acquires retired geometry
            // before checking rig readiness and releases its native lease afterward.
            var executions=new RoomExecutions(editor);
            var args=new JObject{["target"]=id,["revision"]=revision,["mode"]="grounded",["modelHash"]=editor.Read(id).modelHash,["leftClearance"]=.025f,["rightClearance"]=.015f};
            Assert.That(executions.Execute(new JObject{["operation"]="start",["call"]=new JObject{["id"]="object.feet.configure",["version"]=1,["arguments"]=args}},out error),Is.True,error);
            yield return SpatialSettingsCompletion.Wait(executions);
            Assert.That(editor.Read(id).footPlacement.leftClearance,Is.EqualTo(.025f));
            Assert.That(editor.Read(id).footPlacement.rightClearance,Is.EqualTo(.015f));
        }
        [UnityTest]public IEnumerator FeetUseVirtualGroundBelowTheRoomFloorAndPreserveThatPolicyWhenWorldMoves()
        {
            WalkingScene();Terrain(new(0,-.1f,0),0,out _);
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Virtual actor",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.BindEnvironment("maestro",editor.ObjectRevision("maestro"),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            Assert.That(editor.MoveObject("maestro",new(0,-.06f,0),out error),Is.True,error);
            yield return FeetSave("maestro");
            var rig=avatar.PoseRig.FootRig;float y=100;string status=null;
            var probe=avatar.gameObject.AddComponent<AvatarChannelProbe>();probe.Sample=()=>{y=root.transform.InverseTransformPoint(rig.Left.Contact).y;status=avatar.GetComponent<HumanoidFootPlacement>().Status;};
            for(int i=0;i<25;i++)yield return null;
            Assert.That(y,Is.EqualTo(-.1f).Within(.003),status);
            root.transform.SetPositionAndRotation(new(2,0,1),Quaternion.Euler(0,40,0));
            for(int i=0;i<20;i++)yield return null;
            Assert.That(y,Is.EqualTo(-.1f).Within(.003),status);
        }
        [UnityTest]public IEnumerator FeetImportedGaitPreservesEveryNonLegNode()
        {
            yield return WalkingCharacter();Assert.That(SelectGait("embedded",null,out var error),Is.True,error);
            yield return FeetSave(editor.Identity(gaitItem),gaitModel.AssetHash);
            var rig=gaitModel.FootRig;var excluded=new[]{rig.Left.Upper,rig.Left.Lower,rig.Left.Foot,rig.Right.Upper,rig.Right.Lower,rig.Right.Foot};
            var nodes=gaitModel.Instance.Nodes.ToArray();var rotations=new Quaternion[nodes.Length];var positions=new Vector3[nodes.Length];var scales=new Vector3[nodes.Length];
            var skins=gaitModel.Instance.SkinnedMeshRenderers.ToArray();var weights=skins.Select(x=>new float[x.sharedMesh.blendShapeCount]).ToArray();
            var before=gaitItem.gameObject.AddComponent<FootSourceProbe>();
            var after=gaitItem.gameObject.AddComponent<AvatarChannelProbe>();int supported=0,checkedFrames=0;
            before.Sample=()=>{for(int i=0;i<nodes.Length;i++){rotations[i]=nodes[i].localRotation;positions[i]=nodes[i].localPosition;scales[i]=nodes[i].localScale;}for(int j=0;j<skins.Length;j++)for(int k=0;k<weights[j].Length;k++)weights[j][k]=skins[j].GetBlendShapeWeight(k);};
            after.Sample=()=>{
                if(!ImportedObjectWalk.Owns(gaitItem))return;
                for(int i=0;i<nodes.Length;i++){
                    if(!excluded.Contains(nodes[i]))Assert.That(nodes[i].localRotation,Is.EqualTo(rotations[i]));
                    if(nodes[i]!=rig.Hips)Assert.That(nodes[i].localPosition,Is.EqualTo(positions[i]));
                    Assert.That(nodes[i].localScale,Is.EqualTo(scales[i]));
                }
                for(int j=0;j<skins.Length;j++)for(int k=0;k<weights[j].Length;k++)Assert.That(skins[j].GetBlendShapeWeight(k),Is.EqualTo(weights[j][k]));
                if(gaitItem.GetComponent<HumanoidFootPlacement>().Status.Contains("supported"))supported++;
                checkedFrames++;
            };
            var travel=new GroundedTravel(editor,gaitItem,new(0,0,-2),.5f);var start=gaitItem.transform.position;
            try{
                Assert.That(travel.Begin(out error),Is.True,error);
                for(int i=0;i<90;i++){Assert.That(travel.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);yield return null;}
                Assert.That(checkedFrames,Is.GreaterThan(60));Assert.That(supported,Is.GreaterThan(20));
                Assert.That(Vector3.Distance(start,gaitItem.transform.position),Is.GreaterThan(.2f));
            }finally{before.Sample=null;after.Sample=null;travel.Stop(false);}
        }
        [UnityTest]public IEnumerator FeetNeverModifyAnExplicitImportedClipOrItsOtherSourceChannels()
        {
            WalkingScene();yield return LoadTravelAvatar(ModelFixture.WalkingCharacter());avatar.SetEditing(false);
            yield return FeetSave("maestro",avatar.ModelHash,.03f);
            var view=avatar.GetComponent<HumanoidFootPlacement>();var model=avatar.CustomModel;
            Assert.That(avatar.PlayImportedClip(0,true),Is.True);Assert.That(avatar.SampleImportedAt(.4f),Is.True);
            var rotations=model.Instance.Nodes.Select(n=>n.localRotation).ToArray();var positions=model.Instance.Nodes.Select(n=>n.localPosition).ToArray();
            view.Present(1f/72);Assert.That(view.Status,Is.EqualTo("authored pose"));
            Assert.That(model.Instance.Nodes.Select(n=>n.localRotation),Is.EqualTo(rotations));
            Assert.That(model.Instance.Nodes.Select(n=>n.localPosition),Is.EqualTo(positions));
            avatar.StopImportedClip();yield return null;
        }
        [UnityTest]public IEnumerator FeetIncludedMeshySolesFitASlopeAndProduceDesktopEvidence()
        {
            var floor=Surface(new(0,-.1f,0),new(12,.2f,12));floor.transform.rotation=Quaternion.Euler(0,0,10);
            Tutor();Ready();foreach(var prop in root.GetComponentsInChildren<CreatedRoomObject>())prop.transform.position=new(8,2,8);
            yield return LoadTravelAvatar(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"MaestroContent","IncludedMaestro.glb")));
            avatar.SetEditing(false);Assert.That(avatar.CustomModel.FootRig,Is.Not.Null);
            yield return FeetSave("maestro",avatar.ModelHash);
            var rig=avatar.CustomModel.FootRig;float error=100;string status=null;bool capture=false;
            var probe=avatar.gameObject.AddComponent<AvatarChannelProbe>();probe.Sample=()=>{
                status=avatar.GetComponent<HumanoidFootPlacement>().Status;
                var q=new RoomGroundQuery();q.Add(floor.GetComponent<Collider>());
                if(q.Sample(rig.Left.Contact,.3f,.3f,out var l)&&q.Sample(rig.Right.Contact,.3f,.3f,out var r))
                    error=Mathf.Max(Vector3.Distance(rig.Left.Contact,l.point),Vector3.Distance(rig.Right.Contact,r.point));
                if(capture){string path=Environment.GetEnvironmentVariable("MAESTRO_FOOT_EVIDENCE");if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(path);CaptureLayer(Path.Combine(path,"included-maestro-slope.png"));CaptureFootLevel(Path.Combine(path,"included-maestro-feet.png"));File.WriteAllText(Path.Combine(path,"included-maestro-slope.json"),new JObject{["maxSoleErrorMetres"]=error,["status"]=status,["modelHash"]=avatar.ModelHash}.ToString());}capture=false;}
            };
            for(int i=0;i<40;i++)yield return null;
            capture=true;yield return new WaitUntil(()=>!capture);
            Assert.That(error,Is.LessThan(.006f),status);
        }
        void CaptureFootLevel(string path)
        {
            var cameraRoot=new GameObject("Foot verification camera");cameraRoot.transform.SetParent(root.transform,false);
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.89f,.91f);
            cameraRoot.transform.position=avatar.transform.position+new Vector3(.8f,.27f,1.2f);cameraRoot.transform.LookAt(avatar.transform.position+Vector3.up*.18f);camera.fieldOfView=30;
            var render=new RenderTexture(1000,700,24);var pixels=new Texture2D(1000,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1000,700),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraRoot);}
        }
        [UnityTest]public IEnumerator FeetUnavailableGroundAndRuntimePauseNeverLeaveAStalePose()
        {
            WalkingScene();yield return FeetSave("maestro","",.04f);
            var rig=avatar.PoseRig.FootRig;var view=avatar.GetComponent<HumanoidFootPlacement>();
            view.Restore();avatar.PoseRig.SetManual(true);var source=avatar.PoseRig.Capture();
            for(int i=0;i<15;i++){view.Restore();avatar.PoseRig.Apply(source);view.Present(1f/72);}
            Assert.That(rig.Left.Contact.y,Is.EqualTo(.04).Within(.002));
            world.PausePhysics();view.Present(1f/72);Assert.That(view.Status,Is.EqualTo("accepted ground unavailable"));
            AssertFeetPose(source,avatar.PoseRig.Capture());
            yield return null;
        }
    }
    public sealed partial class RoomRulesTests
    {
        [UnityTest]public IEnumerator FeetAdvertisesItsSharedFeatureToConnectedClients()
        {
            Assert.That(editor.GetComponent<RuleWorkshop>(),Is.Not.Null);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(ObjectFeetCapability.Feature));
            yield return null;
        }
        [UnityTest]public IEnumerator FeetGroundCandidateCacheDoesNotRetainADeletedSurface()
        {
            string id=AvailabilityTerrain(),area=NativeAreaFor(id);
            var envelope=new Bounds(GroundPoint,new Vector3(.1f,.5f,.1f));
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            Assert.That(editor.GroundComplete(envelope),Is.False);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);
            Assert.That(editor.GroundComplete(envelope),Is.True,"Removing a ground definition must invalidate passive candidate caches even when no object is added");
        }
        [UnityTest]public IEnumerator FeetGroundCompletenessDoesNotBorrowLowerResidentFloor()
        {
            string id=AvailabilityTerrain(),area=NativeAreaFor(id);
            Assert.That(editor.GroundComplete(new Bounds(GroundPoint,new Vector3(.1f,.5f,.1f))),Is.True);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var before=JsonUtility.ToJson(editor.Snapshot());
            Assert.That(editor.GroundComplete(new Bounds(GroundPoint,new Vector3(.1f,.5f,.1f))),Is.False);
            Assert.That(editor.GroundComplete(new Bounds(Vector3.zero,Vector3.one)),Is.True);
            Assert.That(editor.Find(id),Is.Null);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
        }
    }
}
