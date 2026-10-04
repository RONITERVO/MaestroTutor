// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
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
    public sealed partial class AvatarSpatialTests
    {
        static ScannedSurface InkWall(int id=1)=>new(){Id=id.ToString("x32"),Label="WALL_FACE",Position=new Vector3(0,1.3f,0),Rotation=Quaternion.identity,Plane=new Rect(-1,-1,2,2),Boundary=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}};
        JObject LayerArgs(string operation="create",string target=null)=>operation=="rebind"?new JObject{["operation"]=operation,["stateId"]=ScanStatus()["stateId"].DeepClone(),["target"]=target,["revision"]=editor.ObjectRevision(target),["anchorId"]=InkWall().Id,["x"]=0,["y"]=0,["angle"]=0}:new JObject{["operation"]=operation,["stateId"]=ScanStatus()["stateId"].DeepClone(),["name"]="Wall ink",["width"]=.6,["height"]=.4,["angle"]=0};
        JObject CreateLayerArgs(){var a=LayerArgs();a["anchorId"]=InkWall().Id;a["x"]=.1;a["y"]=0;return a;}
        string MakeLayer(out JObject receipt)
        {
            var request=SettingsRequest("drawing.layer.edit",CreateLayerArgs());Assert.That(modeActions.Execute(request,out var error),Is.True,error);receipt=(JObject)modeActions.Observe().DeepClone();string id=(string)receipt["selected"]["output"]["objectId"];
            Assert.That(id,Is.Not.Null,receipt.ToString());int count=editor.Snapshot().objects.Length;Assert.That(modeActions.Execute(request,out error),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count),"Receipt replay cannot create a second layer");return id;
        }
        JObject InkFact(string id){Assert.That(ScanFact("object.scanDrawing",new JObject{["target"]=id},out var fact),Is.True);return (JObject)fact;}
        void InkAdd(string id)
        {
            var args=new JObject{["operation"]="add",["target"]=id,["revision"]=editor.ObjectRevision(id),["surface"]="Canvas",["stroke"]="",["red"]=.2,["green"]=.4,["blue"]=.9,["radius"]=.003,["points"]=new JArray(new JObject{["x"]=-.03,["y"]=0,["z"]=0},new JObject{["x"]=.03,["y"]=0,["z"]=0})};
            Assert.That(modeActions.Execute(SettingsRequest("object.surface.edit",args),out var error),Is.True,error);
        }
        [UnityTest] public IEnumerator ScannedInkUsesSharedSurfaceEditsUndoStorageAndExactReceipts()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);string id=MakeLayer(out var receipt);var item=editor.Find(id);var view=item.GetComponent<ScannedDrawingView>();var status=InkFact(id);
            Assert.That(view.Visible,Is.True);Assert.That(item.PoseLocked,Is.True);Assert.That(item.transform.position,Is.EqualTo(new Vector3(.1f,1.3f,ScanDrawingAnchor.Offset)));Assert.That(Vector3.Dot(-item.transform.forward,Vector3.forward),Is.GreaterThan(.999f));Assert.That(item.GetComponentsInChildren<Collider>().All(c=>c.isTrigger),Is.True);Assert.That(item.GetComponent<MeshRenderer>(),Is.Null,"No background panel");
            InkAdd(id);Assert.That(item.GetComponent<DrawingSurfaceView>().StrokeCount,Is.EqualTo(1));Assert.That(ScanFact("object.drawing",new JObject{["target"]=id},out _),Is.False);editor.Undo();Assert.That(item.GetComponent<DrawingSurfaceView>().StrokeCount,Is.Zero);editor.Redo();Assert.That(item.GetComponent<DrawingSurfaceView>().StrokeCount,Is.EqualTo(1));
            var saved=new RoomStorage(directory).Load(out var error);Assert.That(saved,Is.Not.Null,error);Assert.That(saved.objects.Single(o=>o.id==id).scanAnchors[0].roomId,Is.EqualTo(source.RoomId));Assert.That(editor.MoveObject(id,Vector3.one,out _),Is.False);Assert.That(editor.CanEditObject(id,true,out _),Is.False);Assert.That(authoring.CanStartRecording(authoring.RecordingSessionId,id,editor.ObjectRevision(id),out _),Is.False);var before=item.transform.position;item.RestoreHome();Assert.That(item.transform.position,Is.EqualTo(before));
            var legacy=new RoomAgentExecutor(editor);string unchanged=JsonUtility.ToJson(editor.Read(id));
            foreach(string action in new[]{"move","paint","resize"}){
                var request=new RoomAgentRequest{version=2,sceneRevision=editor.Revision,conditions=new[]{new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}},commands=new[]{new RoomAgentCommand{action=action,target=id,position=Vector3.zero,scale=1,color=Color.red}}};
                Assert.That(legacy.Execute(request,out error,out _),Is.False,action);Assert.That(error,Does.Contain("rebind"));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(unchanged));
            }
            string output=Environment.GetEnvironmentVariable("MAESTRO_SCAN_DRAWING");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"scan-drawing.json"),new JObject{["receipt"]=receipt,["layer"]=status,["boundary"]="Synthetic SDK source; native persistence, actions and rendering components. No headset or provider."}.ToString());}
            yield return null;
        }
        [UnityTest] public IEnumerator MissingAndWrongRoomAnchorsHideInkUntilExactRecoveryOrExplicitRebind()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);string id=MakeLayer(out _);InkAdd(id);var item=editor.Find(id);var view=item.GetComponent<ScannedDrawingView>();string originalRoom=source.RoomId;var original=editor.Read(id).Copy();
            source.Entries=Array.Empty<ScannedSurface>();yield return null;Assert.That((bool)InkFact(id)["visible"],Is.False);Assert.That(item.gameObject.activeSelf,Is.False);Assert.That(editor.Read(id).surfaces[0].strokes.Length,Is.EqualTo(1));
            source.Entries=new[]{InkWall(2)};yield return null;Assert.That((bool)InkFact(id)["visible"],Is.False,"Same label does not replace the anchor");source.Entries=new[]{InkWall()};source.RoomId=Guid.NewGuid().ToString("N");yield return null;Assert.That((bool)InkFact(id)["visible"],Is.False);
            var rebind=LayerArgs("rebind",id);rebind["x"]=-.2;Assert.That(modeActions.Execute(SettingsRequest("drawing.layer.edit",rebind),out var error),Is.True,error);Assert.That((bool)InkFact(id)["visible"],Is.True);Assert.That(editor.Read(id).surfaces[0].strokes.Length,Is.EqualTo(1));editor.Undo();Assert.That((bool)InkFact(id)["visible"],Is.False);Assert.That(editor.Read(id).scanAnchors[0].roomId,Is.EqualTo(originalRoom));source.RoomId=originalRoom;yield return null;yield return null;Assert.That(view.Visible,Is.True,"Editor must recover an inactive layer without another edit");
            source.Entries[0].Plane=new Rect(-.05f,-.05f,.1f,.1f);yield return null;Assert.That((bool)InkFact(id)["visible"],Is.False);Assert.That(editor.Read(id).surfaces[0].strokes.Length,Is.EqualTo(1));Assert.That(editor.DeleteObject(id,out error),Is.True,error);Assert.That(editor.Read(id),Is.Null);editor.Undo();Assert.That(editor.Read(id).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator AnchorLossRetainsPhysicalInkAndRecoveryRequiresMatchingBinding()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);string id=MakeLayer(out _);var item=editor.Find(id);var patch=item.GetComponent<DrawingSurfaceView>().Surface("Canvas");var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;Assert.That(editor.ConfigureDrawing("surface",Color.white,.003f,out var error),Is.True,error);
            Ray At(float x)=>new(patch.TransformPoint(new Vector3(x,0,-.12f)),patch.forward);pencil.Begin(0,At(-.03f));pencil.Move(0,At(.03f));Assert.That(pencil.IsDrawing,Is.True);
            source.Available=false;yield return null;InkFact(id);Assert.That(pencil.IsDrawing,Is.False);Assert.That(pencil.HasUnsavedStroke,Is.True);string session=pencil.SessionId;Assert.That(pencil.Resolve(session,false,out _,out _),Is.False);Assert.That(item.GetComponent<DrawingSurfaceView>().StrokeCount,Is.Zero);
            source.Available=true;yield return null;InkFact(id);Assert.That(pencil.Resolve(session,false,out _,out error),Is.True,error);Assert.That(item.GetComponent<DrawingSurfaceView>().StrokeCount,Is.EqualTo(1));
            pencil.Begin(0,At(-.03f));pencil.Move(0,At(.03f));source.Entries[0].Position+=Vector3.up*.1f;yield return null;InkFact(id);Assert.That(pencil.HasUnsavedStroke,Is.True,"Geometry change also interrupts without auto-saving");Assert.That(pencil.Resolve(pencil.SessionId,true,out _,out error),Is.True,error);yield return null;
        }
        [UnityTest] public IEnumerator FailedLayerSaveDoesNotPublishAndFailedInkRetainsItsDraft()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);int count=editor.Snapshot().objects.Length;
            Assert.That(modeActions.Execute(SettingsRequest("drawing.layer.edit",CreateLayerArgs()),out _),Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Directory.Delete(obstacle);
            string id=MakeLayer(out _);var patch=editor.Find(id).GetComponent<DrawingSurfaceView>().Surface("Canvas");var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;Assert.That(editor.ConfigureDrawing("surface",Color.white,.003f,out var error),Is.True,error);
            Ray At(float x)=>new(patch.TransformPoint(new Vector3(x,0,-.12f)),patch.forward);pencil.Begin(0,At(-.03f));pencil.Move(0,At(.03f));Directory.CreateDirectory(obstacle);pencil.End(0);
            Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That(editor.Read(id).surfaces[0].strokes,Is.Empty);Directory.Delete(obstacle);Assert.That(pencil.Resolve(pencil.SessionId,false,out _,out error),Is.True,error);
            Assert.That(editor.Read(id).surfaces[0].strokes.Length,Is.EqualTo(1));Assert.That(editor.ConfigureDrawing("surfaceErase",Color.white,.003f,out error),Is.True,error);pencil.Begin(0,At(0));Assert.That(editor.Read(id).surfaces[0].strokes,Is.Empty);editor.Undo();Assert.That(editor.Read(id).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator GazePlacementRejectsStaleAndTooSmallFirstHitsAndTemporaryLayerDiscards()
        {
            var scan=SharedEnvironment(out var platform);var near=InkWall();near.Plane=new Rect(-.1f,-.1f,.2f,.2f);near.Position+=Vector3.forward;var far=InkWall(2);var source=new LayoutSource{Entries=new[]{near,far}};yield return LoadedLayout(scan,platform,source);viewer.transform.SetPositionAndRotation(new Vector3(0,1.3f,3),Quaternion.LookRotation(Vector3.back));
            var args=LayerArgs("atGaze");Assert.That(modeActions.Execute(SettingsRequest("drawing.layer.edit",args),out _),Is.False,"A too-small first surface cannot silently choose the farther wall");source.Entries=new[]{InkWall()};yield return null;Assert.That(modeActions.Execute(SettingsRequest("drawing.layer.edit",args),out _),Is.False,"Stale scan state must be reread");
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;args=LayerArgs("atGaze");Assert.That(modeActions.Execute(SettingsRequest("drawing.layer.edit",args),out error),Is.True,error);var output=modeActions.Observe()["selected"]["output"];Assert.That((bool)output["temporary"],Is.True);string id=(string)output["objectId"];Assert.That(editor.Read(id),Is.Not.Null);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(id),Is.Null);yield return null;
        }
    }
}
