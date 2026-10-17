// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool BeginCreatePrimitive(RoomObjectKind kind,string name,Vector3 position,float scale,Color color,
            out string id,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)
        {
            string created=null;
            bool started=BeginContentEdit(()=>CanCreatePrimitive(out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{
                    values=null;if(!PreparePrimitiveObject(kind,name,position,scale,color,out var data,out issue))return false;
                    values=new[]{data};created=data.id;return true;
                },Array.Empty<string>(),kind+" added",out pending,out error,publicationCheckpoint);
            id=started?created:null;return started;
        }
        internal bool BeginCreateRecipe(string name,Vector3 position,float scale,RoomRecipe recipe,CollisionRecipe collision,ObjectPhysicsSettings physics,
            out string id,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null,
            DrawingSurface[] surfaces=null,DrawingTip[] drawingTips=null,RoomSnapPoint[] snapPoints=null,RoomContainer[] containers=null,RoomHeightField[] heightFields=null,SculptTip[] sculptTips=null,RoomMaterialStore[] materialStores=null)
        {
            id=null;pending=null;
            if(!CanCreateRecipe(recipe,collision,physics,out error,surfaces,drawingTips,snapPoints,containers,heightFields,sculptTips,materialStores))return false;
            string created=null;
            bool started=BeginContentEdit(()=>CanCreatePrimitive(out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{
                    values=null;if(!PrepareRecipeObject(name,position,scale,recipe,collision,physics,out var data,out issue,surfaces,drawingTips,snapPoints,containers,heightFields,sculptTips,materialStores))return false;
                    values=new[]{data};created=data.id;return true;
                },Array.Empty<string>(),"Assembly added",out pending,out error,publicationCheckpoint);
            id=started?created:null;return started;
        }
        internal bool BeginCreateDrawing(string name,Vector3 position,float scale,Color color,float radius,Vector3[] points,
            out string id,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)
        {
            id=null;pending=null;if(!CanCreateDrawing(points,radius,out error))return false;
            string created=null;
            bool started=BeginContentEdit(()=>CanCreatePrimitive(out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{
                    var data=PrepareDrawingObject(name,position,scale,color,radius,points);
                    values=new[]{data};created=data.id;issue=null;return true;
                },Array.Empty<string>(),"Drawing added",out pending,out error,publicationCheckpoint);
            id=started?created:null;return started;
        }
        internal bool BeginCopyObject(string source,int revision,string name,Vector3 position,
            out string id,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)
        {
            string created=null;
            bool started=BeginContentEdit(()=>CanCopyObject(source,revision,out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{
                    values=null;if(!PrepareCopyObject(source,revision,name,position,out var data,out issue))return false;
                    values=new[]{data};created=data.id;return true;
                },Array.Empty<string>(),"Object copied",out pending,out error,publicationCheckpoint);
            id=started?created:null;return started;
        }
    }
}
