// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class CreationSource
    {
        public string kind,templateHash;
        public RoomRecipe recipe;
        public CreationPrototype prototype;
        public CollisionRecipe collision;
        public ObjectPhysicsSettings physics;
        internal bool Prepare(string name,Vector3 position,Quaternion rotation,float scale,out RoomObjectData data,out string error) {
            data=null;
            if(kind=="prototype") {
                error="Provide a creation prototype";if(prototype==null||!prototype.Validate(out error))return false;
                data=prototype.Instantiate(name,position,rotation,scale);
                if(data.motion!=null&&!data.motion.Validate(data.kind)){data=null;error="Every transformed animation frame must stay within room placement and scale limits";return false;}
                error=null;return true;
            }
            if(!Resolve(out var recipe,out var collision,out var physics,out var defaultName,out error))return false;
            if(!RoomEditor.PrepareRecipeObject(string.IsNullOrEmpty(name)?defaultName:name,position,scale,recipe,collision,physics,out data,out error))return false;
            if(kind=="template"){var entry=CreationTemplates.Find(templateHash);data.surfaces=entry.Surfaces;data.drawingTips=entry.DrawingTips;data.snapPoints=entry.SnapPoints;data.containers=entry.Containers;data.heightFields=entry.HeightFields;data.sculptTips=entry.SculptTips;}
            data.rotation=rotation;return true;
        }
        internal bool Validate(out string error) {
            error="Provide an editable creation source";
            return kind=="prototype"?prototype!=null&&prototype.Validate(out error):Resolve(out _,out _,out _,out _,out error);
        }
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
    [Serializable] public sealed class BlueprintConnection
    {
        public string owner,connected;
        public ConnectionSettings definition;
    }
    [Serializable] public sealed class CreationBlueprint
    {
        public int version=1;
        public CreationPiece[] pieces;
        public BlueprintConnection[] connections=Array.Empty<BlueprintConnection>();
        public bool Validate(out string error) {
            error="A blueprint needs version 1 or 3 and 1–16 distinct named pieces";
            if(version is not (1 or 3)||pieces==null||pieces.Length<1||pieces.Length>16)return false;
            var slots=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in pieces) {
                error="A blueprint needs distinct named pieces with valid local poses and scales";
                if(p==null||p.slot==null||!Regex.IsMatch(p.slot,"\\A[a-zA-Z][a-zA-Z0-9_]{0,23}\\z")||!slots.Add(p.slot)||p.name==null||p.name.Length>80||p.name.Any(char.IsControl)||
                    !float.IsFinite(p.position.sqrMagnitude)||p.position.sqrMagnitude>100||!MotionFrame.ValidRotation(p.rotation)||!float.IsFinite(p.scale)||p.scale<.1f||p.scale>4||p.source==null)return false;
                if(!p.source.Validate(out error))return false;
            }
            var allLinks=connections??Array.Empty<BlueprintConnection>();
            error="Use version 3 for 1–15 connection links between distinct blueprint slots";
            if(version==1&&allLinks.Length>0||version==3&&(allLinks.Length<1||allLinks.Length>15))return false;
            var links=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var h in allLinks){
                error="Each connection needs two existing slots and a distinct owner; definitions cannot contain external object IDs";
                if(h==null||h.owner==null||h.connected==null||!slots.Contains(h.owner)||!slots.Contains(h.connected)||h.owner==h.connected||links.ContainsKey(h.owner)||h.definition==null)return false;
                if(!h.definition.ValidateDefinition(out error))return false;links.Add(h.owner,h.connected);
            }
            error="Blueprint connection links cannot form a cycle";if(!RoomConnection.Acyclic(links))return false;
            error=null;return true;
        }
    }
    [Serializable] public sealed class CreationBatch
    {
        public CreationBlueprint blueprint;
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public float scale=1;
        internal static CreationBatch Read(JObject value) {
            var batch=JsonUtility.FromJson<CreationBatch>(value.ToString());
            for(int i=0;i<batch.blueprint.pieces.Length;i++)if(batch.blueprint.pieces[i].source.kind=="prototype")
                batch.blueprint.pieces[i].source.prototype=CreationPrototype.Read((JObject)value["blueprint"]["pieces"][i]["source"]["prototype"]);
            return batch;
        }
        public bool Prepare(out RoomObjectData[] objects,out string error) {
            objects=null;error="Provide a valid blueprint, room position, unit rotation and scale";
            if(blueprint==null||!float.IsFinite(position.sqrMagnitude)||position.sqrMagnitude>625||!MotionFrame.ValidRotation(rotation)||!float.IsFinite(scale)||scale<.1f||scale>4)return false;
            if(!blueprint.Validate(out error))return false;
            var values=new List<RoomObjectData>();
            foreach(var piece in blueprint.pieces) {
                var p=position+rotation.normalized*(piece.position*scale);float size=scale*piece.scale;
                if(!float.IsFinite(p.sqrMagnitude)||p.sqrMagnitude>625||size<.1f||size>4){error="Every transformed piece must stay within room placement and scale limits";return false;}
                if(!piece.source.Prepare(piece.name,p,(rotation.normalized*piece.rotation.normalized).normalized,size,out var data,out error))return false;
                values.Add(data);
            }
            // Every new identity exists before resolving slot references. Never bind an existing room object by name.
            var slots=blueprint.pieces.Select((piece,index)=>(piece.slot,index)).ToDictionary(x=>x.slot,x=>values[x.index]);
            foreach(var link in blueprint.connections??Array.Empty<BlueprintConnection>()){
                var owner=slots[link.owner];var other=slots[link.connected];var hinge=link.definition.Bind(other.id);
                if(!hinge.Aligned(owner,other,out error))return false;owner.connections=new[]{hinge};
            }
            if(!RoomConnection.ValidateCollection(values.ToArray(),out error))return false;
            objects=values.ToArray();error=null;return true;
        }
    }
    public sealed partial class RoomEditor
    {
        // Both single-object and multi-object creation expand into the same components.
        internal static bool PrepareRecipeObject(string name,Vector3 position,float scale,RoomRecipe recipe,CollisionRecipe collision,ObjectPhysicsSettings physics,out RoomObjectData item,out string error,DrawingSurface[] surfaces=null,DrawingTip[] drawingTips=null,RoomSnapPoint[] snapPoints=null,RoomContainer[] containers=null,RoomHeightField[] heightFields=null,SculptTip[] sculptTips=null) {
            item=null;error="Provide a valid construction recipe";if(recipe==null||!recipe.Validate(out error))return false;
            if(collision!=null&&!collision.Validate(out error))return false;
            var data=new RoomObjectData {id=Guid.NewGuid().ToString("N"),name=name,kind=RoomObjectKind.Assembly,position=position,scale=scale,color=Color.white,physics=ItemPhysics.Fixed,recipe=recipe.Copy(),heightFields=heightFields?.Select(f=>f.Copy()).ToArray()??Array.Empty<RoomHeightField>(),sculptTips=sculptTips?.Select(t=>t.Copy()).ToArray()??Array.Empty<SculptTip>(),containers=containers?.Select(c=>c.Copy()).ToArray()??Array.Empty<RoomContainer>(),snapPoints=snapPoints?.Select(p=>p.Copy()).ToArray()??Array.Empty<RoomSnapPoint>(),drawingTips=drawingTips?.Select(t=>t.Copy()).ToArray()??Array.Empty<DrawingTip>(),surfaces=surfaces?.Select(s=>s.Copy()).ToArray()??Array.Empty<DrawingSurface>(),collision=collision?.shapes.Length>0?collision.Copy():null};
            if(physics!=null&&!RoomControls.SetPhysics(data,physics,out error))return false;
            if(!DrawingSurface.ValidateCollection(data,out error)||!DrawingTip.ValidateCollection(data,out error)||!RoomSnapPoint.ValidateCollection(data,out error)||!RoomContainer.ValidateCollection(data,out error))return false;item=data;error=null;return true;
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
