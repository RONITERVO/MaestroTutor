// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject SurfaceCall(string op,JObject fields=null)
        {
            string id=editor.Identity(block);var args=new JObject {["operation"]=op,["target"]=id,["revision"]=editor.ObjectRevision(id),["surface"]="Front"};if(fields!=null)foreach(var p in fields.Properties())args[p.Name]=p.Value;
            return new JObject {["id"]="object.surface.edit",["version"]=1,["arguments"]=args};
        }
        JObject SurfaceConfigure()=>SurfaceCall("configure",new JObject {["definition"]=new DrawingSurfaceCapability().Example["definition"].DeepClone()});
        JObject SurfaceAdd()=>SurfaceCall("add",new JObject {["stroke"]="",["red"]=.8,["green"]=.2,["blue"]=.1,["radius"]=.003,["points"]=new JArray(new JObject {["x"]=-.04,["y"]=0,["z"]=0},new JObject {["x"]=0,["y"]=.03,["z"]=0},new JObject {["x"]=.04,["y"]=0,["z"]=0})});
        JObject SurfaceRun(RoomAgentExecutor ex,JObject call){Assert.That(ex.Execute(ObjectEditRequest(call),out var error,out _),Is.True,error);return ex.Executions.Observe();}
        [UnityTest] public IEnumerator SurfaceInkUsesSharedCommandsReadbackUndoAndObjectMotion()
        {
            string target=editor.Identity(block);var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());SurfaceRun(ex,SurfaceAdd());var data=editor.Read(target);var stroke=data.surfaces[0].strokes.Single();var view=block.GetComponent<DrawingSurfaceView>();Assert.That(view.StrokeCount,Is.EqualTo(1));var patch=view.Surface("Front");var mesh=patch.GetComponentInChildren<MeshFilter>();Assert.That(mesh,Is.Not.Null);Assert.That(patch.GetComponentsInChildren<Collider>(),Is.Empty);
            var sample=mesh.transform.TransformPoint(mesh.sharedMesh.vertices[0]);block.transform.position+=Vector3.right;block.transform.rotation=Quaternion.Euler(0,40,0);Assert.That(Vector3.Distance(sample,mesh.transform.TransformPoint(mesh.sharedMesh.vertices[0])),Is.GreaterThan(.5f));
            Assert.That(BehaviourCatalog.TryRead("object.surface.stroke",1,new JObject {["target"]=target,["surface"]="Front",["stroke"]=stroke.id,["revision"]=editor.ObjectRevision(target),["offset"]=0},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(((JArray)((JObject)fact.Value)["points"]).Count,Is.EqualTo(3));
            SurfaceRun(ex,SurfaceCall("removeStroke",new JObject {["stroke"]=stroke.id}));Assert.That(view.StrokeCount,Is.Zero);editor.Undo();Assert.That(view.StrokeCount,Is.EqualTo(1));Assert.That(editor.Read(target).surfaces[0].strokes[0].points,Is.EqualTo(stroke.points));editor.Redo();Assert.That(view.StrokeCount,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator PhysicalSurfacePencilRetainsFailedInkAndFollowsTheObjectOnRetry()
        {
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());var view=block.GetComponent<DrawingSurfaceView>();var patch=view.Surface("Front");var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleSurfaceDrawing();int count=editor.Snapshot().objects.Length;
            Ray At(float x,float y)=>new(patch.TransformPoint(new Vector3(x,y,-.12f)),patch.forward);
            pencil.Begin(0,At(-.03f,0));pencil.Move(0,At(0,.025f));pencil.Move(0,At(.03f,0));Assert.That(pencil.IsDrawing,Is.True);
            string obstacle=Path.Combine(directory,"room.v5.json.pending");Directory.CreateDirectory(obstacle);pencil.End(0);Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That(view.StrokeCount,Is.Zero);string session=pencil.SessionId;
            block.transform.position+=Vector3.up*.15f;Directory.Delete(obstacle);Assert.That(pencil.Resolve(session,false,out var result,out var error),Is.True,error);Assert.That((string)result["objectId"],Is.EqualTo(editor.Identity(block)));Assert.That(pencil.HasUnsavedStroke,Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(view.StrokeCount,Is.EqualTo(1));Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes[0].points.Length,Is.EqualTo(3));Assert.That(pencil.Resolve(session,false,out _,out _),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator PhysicalSurfaceEraserAndMissNeverDeleteTheObjectOrCreateAirInk()
        {
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());SurfaceRun(ex,SurfaceAdd());var view=block.GetComponent<DrawingSurfaceView>();var patch=view.Surface("Front");string target=editor.Identity(block);int count=editor.Snapshot().objects.Length;
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;Assert.That(editor.ConfigureDrawing("surfaceErase",Color.white,.003f,out var error),Is.True,error);
            pencil.Begin(0,new Ray(patch.TransformPoint(new Vector3(0,.03f,-.12f)),patch.forward));Assert.That(pencil.IsDrawing,Is.False);Assert.That(view.StrokeCount,Is.Zero);Assert.That(editor.Read(target),Is.Not.Null);editor.Undo();Assert.That(view.StrokeCount,Is.EqualTo(1));
            Assert.That(editor.ConfigureDrawing("surface",Color.white,.003f,out error),Is.True,error);pencil.Begin(0,new Ray(new Vector3(100,100,100),Vector3.forward));pencil.Move(0,new Ray(new Vector3(101,100,100),Vector3.forward));pencil.End(0);Assert.That(pencil.IsDrawing,Is.False);Assert.That(pencil.HasUnsavedStroke,Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            var disabled=SurfaceConfigure();disabled["arguments"]["definition"]["enabled"]=false;SurfaceRun(ex,disabled);patch=view.Surface("Front");pencil.Begin(0,new Ray(patch.TransformPoint(new Vector3(0,0,-.12f)),patch.forward));Assert.That(pencil.IsDrawing,Is.False);Assert.That(view.StrokeCount,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator RetainedSurfaceInkReclaimsProgramOwnershipBeforeRetry()
        {
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());string target=editor.Identity(block);var view=block.GetComponent<DrawingSurfaceView>();var patch=view.Surface("Front");var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleSurfaceDrawing();
            Ray At(float x)=>new(patch.TransformPoint(new Vector3(x,0,-.12f)),patch.forward);
            pencil.Begin(0,At(-.03f));pencil.Move(0,At(.03f));var claims=new[]{new BehaviourCatalog.Claim(target,"wholeTarget")};
            Assert.That(editor.Ownership.TryAcquire("test-grab","Test grab",Maestro.Quest.Interaction.RoomActorRole.Grab,claims,null,out var grab,out var error),Is.True,error);Assert.That(pencil.IsDrawing,Is.False);Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That(pencil.Resolve(pencil.SessionId,false,out _,out _),Is.False);Assert.That(view.StrokeCount,Is.Zero);grab.Dispose();
            bool interrupted=false;Assert.That(editor.Ownership.TryAcquire("test-program","Test program",Maestro.Quest.Interaction.RoomActorRole.Program,claims,_=>interrupted=true,out var program,out error),Is.True,error);
            Assert.That(pencil.Resolve(pencil.SessionId,false,out _,out error),Is.True,error);Assert.That(interrupted,Is.True);Assert.That(program.Held,Is.False);Assert.That(view.StrokeCount,Is.EqualTo(1));Assert.That(editor.Ownership.Observe().owners,Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator ChalkboardTemplateInkFollowsItsRecipePartAndSurvivesRebuild()
        {
            var ex=new RoomAgentExecutor(editor);var receipt=TemplateRun(ex,TemplateCall("chalkboard",new Vector3(2,1,0)));string target=(string)receipt["selected"]["output"]["objectId"];var item=editor.Find(target);var view=item.GetComponent<DrawingSurfaceView>();Assert.That(view.Surface("Front"),Is.Not.Null);Assert.That(editor.Read(target).surfaces[0].strokes,Is.Empty);
            var add=SurfaceAdd();add["arguments"]["target"]=target;add["arguments"]["revision"]=editor.ObjectRevision(target);var request=ObjectEditRequest(add);Assert.That(ex.Execute(request,out var error,out _),Is.True,error);
            var part=item.GetComponent<RecipeObject>().Part("Board");var patch=view.Surface("Front");Assert.That(patch.parent,Is.EqualTo(part));var before=patch.position;part.localPosition+=Vector3.up*.1f;Assert.That(Vector3.Distance(before,patch.position),Is.GreaterThan(.09f));
            Assert.That(editor.PaintObject(target,Color.blue,out error),Is.True,error);yield return null;Assert.That(view.Surface("Front").parent,Is.EqualTo(item.GetComponent<RecipeObject>().Part("Board")));Assert.That(view.StrokeCount,Is.EqualTo(1));Assert.That(new RoomStorage(directory).Load(out error).objects.Single(o=>o.id==target).surfaces[0].strokes.Length,Is.EqualTo(1),error);
        }
        [UnityTest] public IEnumerator FullInkPagesAndFourPatchOverviewStayWithinProgramValueBudgets()
        {
            var ex=new RoomAgentExecutor(editor);string target=editor.Identity(block);
            for(int i=0;i<4;i++){var cfg=SurfaceConfigure();cfg["arguments"]["surface"]="Patch"+new string('a',26)+i;SurfaceRun(ex,cfg);}
            string surface="Patch"+new string('a',26)+0;
            for(int i=0;i<32;i++){var call=SurfaceAdd();call["arguments"]["surface"]=surface;SurfaceRun(ex,call);}
            var context=new BehaviourCatalog.FactContext(editor:editor);Assert.That(BehaviourCatalog.TryRead("object.surfaces",1,new JObject {["target"]=target},context,out var overview),Is.True);Assert.That(overview.Characters,Is.LessThanOrEqualTo(1024));
            int revision=editor.ObjectRevision(target);Assert.That(BehaviourCatalog.TryRead("object.surface.strokes",1,new JObject {["target"]=target,["surface"]=surface,["revision"]=revision,["offset"]=0},context,out var listing),Is.True);Assert.That(listing.Characters,Is.LessThanOrEqualTo(1024));Assert.That(((JArray)((JObject)listing.Value)["strokes"]).Count,Is.EqualTo(6));
            string stroke=editor.Read(target).surfaces[0].strokes[0].id;var splice=SurfaceCall("splice",new JObject {["surface"]=surface,["stroke"]=stroke,["index"]=0,["deleteCount"]=3,["points"]=new JArray(Enumerable.Range(0,64).Select(i=>new JObject {["x"]=(i%2==0?-.04:.04),["y"]=i*.0001,["z"]=0}))});SurfaceRun(ex,splice);revision=editor.ObjectRevision(target);
            Assert.That(BehaviourCatalog.TryRead("object.surface.stroke",1,new JObject {["target"]=target,["surface"]=surface,["stroke"]=stroke,["revision"]=revision,["offset"]=0},context,out var page),Is.True);Assert.That(page.Characters,Is.LessThanOrEqualTo(1024));Assert.That(((JArray)((JObject)page.Value)["points"]).Count,Is.EqualTo(6));yield return null;
        }
        [UnityTest] public IEnumerator InvalidSurfaceEditsAndTemporaryPaintPreservePriorInk()
        {
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());var stale=SurfaceAdd();SurfaceRun(ex,SurfaceAdd());Assert.That(ex.Execute(ObjectEditRequest(stale),out _,out _),Is.False);
            var invalid=SurfaceConfigure();invalid["arguments"]["definition"]["width"]=.02;Assert.That(ex.Execute(ObjectEditRequest(invalid),out _,out _),Is.False);Assert.That(block.GetComponent<DrawingSurfaceView>().StrokeCount,Is.EqualTo(1));
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;SurfaceRun(ex,SurfaceCall("clear"));Assert.That(block.GetComponent<DrawingSurfaceView>().StrokeCount,Is.Zero);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Find(editor.Identity(block)).GetComponent<DrawingSurfaceView>().StrokeCount,Is.EqualTo(1));yield return null;
        }
    }
}
