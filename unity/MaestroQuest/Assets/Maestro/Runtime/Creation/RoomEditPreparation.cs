// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Admission owns prepared resources until the exact journal candidate has
    // been persisted/accepted and reconciliation takes them. Failed preparation
    // or storage releases all candidates and preserves the active scene.
    // Compound and terrain candidates do not need a published entity component.
    // Transfer them by stable target identity; cancellation destroys every untaken
    // candidate. Native activation and edits share the same admission ownership.
    internal sealed class RoomEditPreparation:IDisposable
    {
        readonly List<IDisposable> resources=new();
        readonly Dictionary<string,NativeObjectResources.Lease> objects=new();
        readonly Dictionary<string,CollisionGeometry> collisions=new();
        readonly Dictionary<string,HeightFieldGeometry> fields=new();
        readonly Dictionary<string,RecipeVisual> recipes=new();
        readonly Dictionary<string,PreparedImportedModel> models=new();
        readonly Dictionary<string,PreparedRoomLights> lights=new();
        readonly Dictionary<string,(Vector3[] Points,float Radius,PencilGeometry Geometry)> drawings=new();
        readonly Dictionary<string,PreparedDrawingSurfaces> surfaces=new();
        internal void EnlistModel(PreparedImportedModel model,RoomJournal.PreparedEdit edit)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            RoomObjectData data=null;foreach(var item in edit.Snapshot().objects)if(item.id==model.Target){data=item;break;}
            EnlistModel(model,data);
        }
        internal void EnlistModel(PreparedImportedModel model,RoomObjectData data)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            model.Validate(data);models.Add(model.Target,model);resources.Add(model);
        }
        internal PreparedImportedModel TakeModel(RoomObjectData data)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            if(!models.TryGetValue(data.id,out var model))return null;model.Validate(data);
            models.Remove(data.id);resources.Remove(model);return model;
        }
        internal NativeObjectResources.Lease TakeObject(RoomObjectData data)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            if(!objects.TryGetValue(data.id,out var lease))return null;lease.Validate(data);
            objects.Remove(data.id);resources.Remove(lease);return lease;
        }
        internal static RoomEditPreparation TryCreateInitial(RoomEditor editor,RoomDocument document,out string error)
        {
            var prepared=new RoomEditPreparation();error=null;
            try{foreach(var data in document.objects)if(!data.IsBuiltIn&&!editor.NativeEntityDormant(data.id)&&!editor.Find(data.id))foreach(var step in prepared.PrepareGeometry(editor,data,null)){}return prepared;}
            catch(Exception failure){prepared.Dispose();error=failure is ModelImportException or RoomResourceLimitException?failure.Message:"The initial room geometry could not be prepared; saved objects remain available";return null;}
        }
        // Discard changes journals and resets dormant membership. Prepare the
        // saved destination, including missing roots, before ending temporary memory.
        internal static RoomEditPreparation TryCreateRestoration(RoomEditor editor,RoomDocument document,out string error)
        {
            var prepared=new RoomEditPreparation();error=null;
            try{foreach(var data in document.objects)foreach(var step in prepared.PrepareGeometry(editor,data,editor.Find(data.id)?editor.Read(data.id):null)){}return prepared;}
            catch(Exception failure){prepared.Dispose();error=failure is ModelImportException or RoomResourceLimitException?failure.Message:"The saved room could not be prepared; temporary changes remain available";return null;}
        }
        internal PreparedRoomLights TakeLights(RoomObjectData data)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            if(!lights.TryGetValue(data.id,out var candidate))return null;candidate.Validate(data.id,data.lights);
            lights.Remove(data.id);resources.Remove(candidate);return candidate;
        }
        internal PencilGeometry TakeDrawing(RoomObjectData data){
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");if(!drawings.TryGetValue(data.id,out var value))return null;
            if(data.kind!=RoomObjectKind.Drawing||value.Radius!=data.radius||!value.Points.SequenceEqual(data.points)||!value.Geometry.Ready)throw new InvalidOperationException("Drawing source changed after preparation");
            drawings.Remove(data.id);resources.Remove(value.Geometry);return value.Geometry;
        }
        internal PreparedDrawingSurfaces TakeSurfaces(RoomObjectData data){
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");if(!surfaces.TryGetValue(data.id,out var value))return null;value.Validate(data.id,data.surfaces);
            surfaces.Remove(data.id);resources.Remove(value);return value;
        }
        bool ended;
        internal CollisionGeometry TakeCollision(string target,CollisionRecipe source)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            if(!collisions.TryGetValue(target,out var candidate))return null;
            if(candidate.Encoded!=JsonUtility.ToJson(source))throw new InvalidOperationException("Collision source changed after preparation");
            collisions.Remove(target);resources.Remove(candidate);return candidate;
        }
        internal HeightFieldGeometry TakeHeightField(string target,RoomHeightField source)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            if(!fields.TryGetValue(target,out var candidate))return null;
            if(candidate.Encoded!=JsonUtility.ToJson(source))throw new InvalidOperationException("Height source changed after preparation");
            fields.Remove(target);resources.Remove(candidate);return candidate;
        }
        internal RecipeVisual TakeRecipe(string target,RoomRecipe source)
        {
            if(ended)throw new InvalidOperationException("Room edit preparation has ended");
            if(!recipes.TryGetValue(target,out var candidate))return null;
            if(!candidate.Ready||candidate.Encoded!=JsonUtility.ToJson(source))throw new InvalidOperationException("Recipe source changed or preparation is incomplete");
            recipes.Remove(target);resources.Remove(candidate);return candidate;
        }
        internal static RoomEditPreparation TryCreate(RoomEditor editor,RoomJournal.PreparedEdit edit,out string error)
        {
            var prepared=new RoomEditPreparation();error=null;
            try {
                var changed=edit.ChangedObjects;
                foreach(var data in edit.Snapshot().objects) {
                    // Only this edit's objects can materialize. An unrelated lost view
                    // stays unavailable; cleanup must not retry broken peer geometry.
                    if(editor.NativeEntityDormant(data.id)||!changed.Contains(data.id))continue;
                    bool missing=!editor.Find(data.id)&&!data.IsBuiltIn;
                    var before=missing?null:editor.Read(data.id);
                    foreach(var step in prepared.PrepareGeometry(editor,data,before)){}
                }
                return prepared;
            }catch(Exception exception){prepared.Dispose();error=exception is ModelImportException or RoomResourceLimitException?exception.Message:"The room edit could not prepare its geometry; inspect the objects before retrying";return null;}
        }
        internal static async Task<RoomEditPreparation> PrepareNativeAsync(RoomEditor editor,RoomObjectData[] values,RoomPreparationBudget budget)
        {
            var result=new RoomEditPreparation();
            try{foreach(var data in values)await budget.Run(result.PrepareGeometry(editor,data,null));return result;}
            catch{result.Dispose();throw;}
        }
        IEnumerable<object> PrepareGeometry(RoomEditor editor,RoomObjectData data,RoomObjectData before)
        {
            if(!data.IsBuiltIn&&before==null){
                var lease=NativeObjectResources.Begin(data,new RoomResourceOwner(editor.WorldIdentity,data.id,"object"));resources.Add(lease);objects.Add(data.id,lease);yield return null;
            }
            var owner=new RoomResourceOwner(editor.WorldIdentity,data.id,"collision");
            if(data.kind==RoomObjectKind.Assembly&&(before==null?data.recipe!=null:JsonUtility.ToJson(before.recipe)!=JsonUtility.ToJson(data.recipe))){
                var candidate=RecipeVisual.BeginPreparation(data.recipe,new RoomResourceOwner(editor.WorldIdentity,data.id,"object"));resources.Add(candidate);recipes.Add(data.id,candidate);
                foreach(var step in candidate.BuildSteps())yield return step;
            }
            if((data.collision?.shapes.Length??0)>0&&(before==null||JsonUtility.ToJson(before.collision)!=JsonUtility.ToJson(data.collision))){
                var candidate=new CollisionGeometry(data.collision,null,owner);resources.Add(candidate);collisions.Add(data.id,candidate);yield return null;
            }
            var field=data.heightFields?.Length==1?data.heightFields[0]:null;var previous=before?.heightFields?.Length==1?before.heightFields[0]:null;
            if(field!=null&&(previous==null||JsonUtility.ToJson(field)!=JsonUtility.ToJson(previous))){
                var candidate=new HeightFieldGeometry(field,owner);resources.Add(candidate);fields.Add(data.id,candidate);yield return null;
            }
            var current=before!=null?editor.Find(data.id):null;var drawingView=current?current.GetComponent<CreatedRoomObject>():null;
            if(data.kind==RoomObjectKind.Drawing&&!ScanDrawingAnchor.Has(data)&&(!drawingView||!drawingView.DrawingMatches(data))){
                var points=(Vector3[])data.points.Clone();var candidate=PencilGeometry.Prepare(new[]{points},data.radius,new RoomResourceOwner(editor.WorldIdentity,data.id,"ink"),"spatial");resources.Add(candidate);drawings.Add(data.id,(points,data.radius,candidate));yield return null;
            }
            var surfaceView=current?current.GetComponent<DrawingSurfaceView>():null;
            if((data.surfaces?.Length??0)>0||surfaceView){
                var candidate=new PreparedDrawingSurfaces(editor.WorldIdentity,data.id,data.surfaces,surfaceView);resources.Add(candidate);surfaces.Add(data.id,candidate);yield return null;
            }
            var lightView=current?current.GetComponent<RoomLightView>():null;
            if(data.lights.Length>0||lightView){
                var candidate=new PreparedRoomLights(editor.WorldIdentity,data.id,data.lights,lightView);resources.Add(candidate);lights.Add(data.id,candidate);yield return null;
            }
            if(data.kind!=RoomObjectKind.ImportedModel)yield break;
            // New/unloaded model instances keep the established asynchronous
            // import/readiness path. An edit of existing geometry must be
            // prepared now; it cannot advance history and fail afterwards.
            if(before==null||before.modelGeometry.Same(data.modelGeometry))yield break;
            if(editor.PhysicsWorld&&editor.PhysicsWorld.Running)throw new ModelImportException("Pause physics before changing imported geometry");
            var item=editor.Find(data.id);var view=item?item.GetComponent<CreatedRoomObject>():null;
            if(!view)throw new ModelImportException("Wait for the imported geometry to load");
            if(!view.CanConfigureModelGeometry(data.modelGeometry,out var issue))throw new ModelImportException(issue);
            resources.Add(view.PrepareModelGeometry(data.modelGeometry));yield return null;
        }
        public void Dispose(){if(ended)return;ended=true;for(int i=resources.Count-1;i>=0;i--)resources[i].Dispose();resources.Clear();objects.Clear();collisions.Clear();fields.Clear();recipes.Clear();models.Clear();lights.Clear();drawings.Clear();surfaces.Clear();}
    }
}
