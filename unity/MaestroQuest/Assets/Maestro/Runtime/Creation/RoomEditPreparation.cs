// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Admission owns prepared resources until the exact journal candidate has
    // been persisted/accepted and reconciliation takes them. Failed preparation
    // or storage releases all candidates and preserves the active scene.
    // Compound and terrain candidates do not need a published entity component.
    // Transfer them by stable target identity; cancellation destroys every untaken
    // candidate. Regional activation and global cost limits remain separate work.
    internal sealed class RoomEditPreparation:IDisposable
    {
        readonly List<IDisposable> resources=new();
        readonly Dictionary<string,CollisionGeometry> collisions=new();
        readonly Dictionary<string,HeightFieldGeometry> fields=new();
        readonly Dictionary<string,RecipeVisual> recipes=new();
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
            if(candidate.Encoded!=JsonUtility.ToJson(source))throw new InvalidOperationException("Recipe source changed after preparation");
            recipes.Remove(target);resources.Remove(candidate);return candidate;
        }
        internal static RoomEditPreparation TryCreate(RoomEditor editor,RoomJournal.PreparedEdit edit,out string error)
        {
            var prepared=new RoomEditPreparation();error=null;
            try {
                var changed=edit.ChangedObjects;
                foreach(var data in edit.Snapshot().objects) {
                    if(!changed.Contains(data.id))continue;
                    var before=editor.Read(data.id);var owner=new RoomResourceOwner(editor.WorldIdentity,data.id,"collision");
                    if(data.kind==RoomObjectKind.Assembly&&JsonUtility.ToJson(before?.recipe)!=JsonUtility.ToJson(data.recipe)){
                        var candidate=new RecipeVisual(data.recipe,new RoomResourceOwner(editor.WorldIdentity,data.id,"object"));prepared.resources.Add(candidate);prepared.recipes.Add(data.id,candidate);
                    }
                    if((data.collision?.shapes.Length??0)>0&&JsonUtility.ToJson(before?.collision)!=JsonUtility.ToJson(data.collision)){
                        var candidate=new CollisionGeometry(data.collision,null,owner);prepared.resources.Add(candidate);prepared.collisions.Add(data.id,candidate);
                    }
                    var field=data.heightFields?.Length==1?data.heightFields[0]:null;var previous=before?.heightFields?.Length==1?before.heightFields[0]:null;
                    if(field!=null&&JsonUtility.ToJson(field)!=JsonUtility.ToJson(previous)){
                        var candidate=new HeightFieldGeometry(field,owner);prepared.resources.Add(candidate);prepared.fields.Add(data.id,candidate);
                    }
                    if(data.kind!=RoomObjectKind.ImportedModel)continue;
                    // New/unloaded model instances keep the established asynchronous
                    // import/readiness path. An edit of existing geometry must be
                    // prepared now; it cannot advance history and fail afterwards.
                    if(before==null||before.modelGeometry.Same(data.modelGeometry))continue;
                    if(editor.PhysicsWorld&&editor.PhysicsWorld.Running)throw new ModelImportException("Pause physics before changing imported geometry");
                    var item=editor.Find(data.id);var view=item?item.GetComponent<CreatedRoomObject>():null;
                    if(!view)throw new ModelImportException("Wait for the imported geometry to load");
                    if(!view.CanConfigureModelGeometry(data.modelGeometry,out error))throw new ModelImportException(error);
                    prepared.resources.Add(view.PrepareModelGeometry(data.modelGeometry));
                }
                return prepared;
            }catch(Exception exception){prepared.Dispose();error=exception is ModelImportException?exception.Message:"The room edit could not prepare its geometry; inspect the objects before retrying";return null;}
        }
        public void Dispose(){if(ended)return;ended=true;for(int i=resources.Count-1;i>=0;i--)resources[i].Dispose();resources.Clear();collisions.Clear();fields.Clear();recipes.Clear();}
    }
}
