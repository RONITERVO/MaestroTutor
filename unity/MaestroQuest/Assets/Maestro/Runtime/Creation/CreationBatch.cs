// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class CreationSource
    {
        public string kind,templateHash;
        public RoomRecipe recipe;
        public CollisionRecipe collision;
        public ObjectPhysicsSettings physics;
        internal bool Resolve(out RoomRecipe geometry,out CollisionRecipe shapes,out ObjectPhysicsSettings settings,out string defaultName,out string error)
        {
            geometry=null;shapes=null;settings=null;defaultName="Recipe piece";error="Choose an available template or an idle recipe";
            if(kind=="template") {
                var entry=CreationTemplates.Find(templateHash);if(entry==null)return false;
                geometry=entry.Recipe;shapes=entry.Collision;settings=entry.Physics;defaultName=entry.Name;
            } else if(kind=="recipe") {geometry=recipe;shapes=collision;settings=physics;}
            else return false;
            if(geometry==null||!geometry.Validate(out error))return false;
            if(geometry.playing){error="Create structure pieces idle, then explicitly start their animations";return false;}
            if(shapes!=null&&!shapes.Validate(out error))return false;
            if(settings!=null&&!RoomControls.ValidPhysics(settings)){error="Provide valid piece physics settings";return false;}
            error=null;return true;
        }
    }
    [Serializable] public sealed class CreationPiece
    {
        public string slot,name;
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public float scale=1;
        public CreationSource source;
    }
    [Serializable] public sealed class CreationBlueprint
    {
        public int version=1;
        public CreationPiece[] pieces;
        public bool Validate(out string error) {
            error="A blueprint needs version 1 and 1–16 distinct named pieces";
            if(version!=1||pieces==null||pieces.Length<1||pieces.Length>16)return false;
            var slots=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in pieces) {
                error="A blueprint needs distinct named pieces with valid local poses and scales";
                if(p==null||p.slot==null||!Regex.IsMatch(p.slot,"^[a-zA-Z][a-zA-Z0-9_]{0,23}$")||!slots.Add(p.slot)||p.name==null||p.name.Length>80||p.name.Any(char.IsControl)||
                    !float.IsFinite(p.position.sqrMagnitude)||p.position.sqrMagnitude>100||!MotionFrame.ValidRotation(p.rotation)||!float.IsFinite(p.scale)||p.scale<.1f||p.scale>4||p.source==null)return false;
                if(!p.source.Resolve(out _,out _,out _,out _,out error))return false;
            }
            error=null;return true;
        }
    }
    [Serializable] public sealed class CreationBatch
    {
        public CreationBlueprint blueprint;
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public float scale=1;
        public bool Prepare(out RoomObjectData[] objects,out string error) {
            objects=null;error="Provide a valid blueprint, room position, unit rotation and scale";
            if(blueprint==null||!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>625||!MotionFrame.ValidRotation(rotation)||!float.IsFinite(scale)||scale<.1f||scale>4)return false;
            if(!blueprint.Validate(out error))return false;
            var values=new List<RoomObjectData>();
            foreach(var piece in blueprint.pieces) {
                if(!piece.source.Resolve(out var recipe,out var collision,out var physics,out var defaultName,out error))return false;
                var p=position+rotation.normalized*(piece.position*scale);float size=scale*piece.scale;
                if(!float.IsFinite(p.sqrMagnitude)||p.sqrMagnitude>625||size<.1f||size>4){error="Every transformed piece must stay within room placement and scale limits";return false;}
                if(!RoomEditor.PrepareRecipeObject(string.IsNullOrEmpty(piece.name)?defaultName:piece.name,p,size,recipe,collision,physics,out var data,out error))return false;
                if(piece.source.kind=="template"){var template=CreationTemplates.Find(piece.source.templateHash);data.surfaces=template.Surfaces;data.drawingTips=template.DrawingTips;}
                data.rotation=(rotation.normalized*piece.rotation.normalized).normalized;values.Add(data);
            }
            objects=values.ToArray();error=null;return true;
        }
    }
    public sealed partial class RoomEditor
    {
        // Both single-object and multi-object creation expand into the same components.
        internal static bool PrepareRecipeObject(string name,Vector3 position,float scale,RoomRecipe recipe,CollisionRecipe collision,ObjectPhysicsSettings physics,out RoomObjectData item,out string error,DrawingSurface[] surfaces=null,DrawingTip[] drawingTips=null) {
            item=null;error="Provide a valid construction recipe";if(recipe==null||!recipe.Validate(out error))return false;
            if(collision!=null&&!collision.Validate(out error))return false;
            var data=new RoomObjectData {id=Guid.NewGuid().ToString("N"),name=name,kind=RoomObjectKind.Assembly,position=position,scale=scale,color=Color.white,physics=ItemPhysics.Fixed,recipe=recipe.Copy(),drawingTips=drawingTips?.Select(t=>t.Copy()).ToArray()??Array.Empty<DrawingTip>(),surfaces=surfaces?.Select(s=>s.Copy()).ToArray()??Array.Empty<DrawingSurface>(),collision=collision?.shapes.Length>0?collision.Copy():null};
            if(physics!=null&&!RoomControls.SetPhysics(data,physics,out error))return false;
            if(!DrawingSurface.ValidateCollection(data,out error)||!DrawingTip.ValidateCollection(data,out error))return false;item=data;error=null;return true;
        }
        bool PrepareCreationBatch(CreationBatch batch,out RoomObjectData[] objects,out string error) {
            objects=null;error="Provide a creation batch";if(batch==null||!CanCreatePrimitive(out error)||!batch.Prepare(out objects,out error))return false;
            var candidate=Snapshot();candidate.objects=candidate.objects.Concat(objects).ToArray();return candidate.Validate(out error);
        }
        public bool CanCreateBatch(CreationBatch batch,out string error)=>PrepareCreationBatch(batch,out _,out error);
        public bool CreateBatch(CreationBatch batch,out string[] ids,out string error) {
            ids=null;if(!PrepareCreationBatch(batch,out var objects,out error))return false;
            if(!CommitPersisted(objects,Array.Empty<string>(),"Structure created — one Undo removes its pieces",false,out error))return false;
            ids=objects.Select(o=>o.id).ToArray();return true;
        }
    }
}
