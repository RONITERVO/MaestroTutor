// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Public creation vocabulary has no saved-room enum ordinals or object identities.
    [Serializable] public sealed class PrototypeFrame
    {
        public float time; public Vector3 position; public Quaternion rotation=Quaternion.identity; public float scale=1;
    }
    [Serializable] public sealed class PrototypeMotion
    {
        public bool loop; public PrototypeFrame[] frames;
        public bool Valid => frames!=null && frames.Length>=1 && frames.Length<=301 && frames[0]!=null && frames[0].time==0 &&
            frames.Select((f,i)=>f!=null && float.IsFinite(f.time) && f.time>=0 && f.time<=30 && (i==0||frames[i-1]!=null&&f.time>frames[i-1].time) &&
                float.IsFinite(f.position.sqrMagnitude) && f.position.sqrMagnitude<=250001 && MotionFrame.ValidRotation(f.rotation) && float.IsFinite(f.scale) && f.scale>=.024999f && f.scale<=40.00001f).All(x=>x);
    }
    [Serializable] public sealed class CreationPrototype
    {
        public int version=1;
        public string kind,modelHash;
        public Color color=Color.white;
        public ObjectPhysicsSettings physics;
        public RoomRecipe recipe;
        public CollisionRecipe collision;
        public DrawingSurface[] surfaces=Array.Empty<DrawingSurface>();
        public DrawingTip[] drawingTips=Array.Empty<DrawingTip>();
        public RoomSnapPoint[] snapPoints=Array.Empty<RoomSnapPoint>();
        public RoomContainer[] containers=Array.Empty<RoomContainer>();
        public RoomHeightField[] heightFields=Array.Empty<RoomHeightField>();
        public Vector3[] points;
        public float radius=.003f;
        // Root-motion positions/rotations/scales are relative to the captured object's pose.
        public PrototypeMotion motion;
        static RoomObjectKind? Kind(string value)=>value switch {
            "block"=>RoomObjectKind.Block,"ball"=>RoomObjectKind.Ball,"cylinder"=>RoomObjectKind.Cylinder,
            "drawing"=>RoomObjectKind.Drawing,"model"=>RoomObjectKind.ImportedModel,"recipe"=>RoomObjectKind.Assembly,_=>null};
        internal RoomObjectData Instantiate(string name,Vector3 position,Quaternion rotation,float scale) {
            var data=new RoomObjectData {id=Guid.NewGuid().ToString("N"),name=name,kind=Kind(kind).Value,position=position,rotation=rotation,scale=scale,
                color=color,recipe=recipe?.Copy(),collision=collision?.Copy(),surfaces=surfaces.Select(s=>s.Copy()).ToArray(),drawingTips=drawingTips.Select(t=>t.Copy()).ToArray(),snapPoints=snapPoints?.Select(p=>p.Copy()).ToArray()??Array.Empty<RoomSnapPoint>(),
                heightFields=heightFields?.Select(f=>f.Copy()).ToArray()??Array.Empty<RoomHeightField>(),containers=containers?.Select(c=>c.Copy()).ToArray()??Array.Empty<RoomContainer>(),points=points?.ToArray(),radius=radius,modelHash=modelHash};
            RoomControls.SetPhysics(data,physics,out _);
            if(motion!=null)data.motion=new RoomMotion {loop=motion.loop,frames=motion.frames.Select(f=>new MotionFrame {
                time=f.time,position=position+rotation*(f.position*scale),rotation=(rotation*f.rotation).normalized,scale=scale*f.scale}).ToArray()};
            return data;
        }
        internal static CreationPrototype Read(JObject value) {
            var flat=(JObject)value.DeepClone();var geometry=(JObject)flat["geometry"];flat.Remove("geometry");foreach(var p in geometry.Properties())flat[p.Name]=p.Value.DeepClone();
            var result=JsonUtility.FromJson<CreationPrototype>(flat.ToString());
            // Unity creates default serializable reference objects for absent fields.
            result.recipe=result.kind=="recipe"?result.recipe:null;result.points=result.kind=="drawing"?result.points:null;
            result.modelHash=result.kind=="model"?result.modelHash:null;
            if(!value.ContainsKey("collision"))result.collision=null;if(!value.ContainsKey("motion"))result.motion=null;
            return result;
        }
        public bool Validate(out string error) {
            error="Provide a version-1 creation prototype with idle geometry, valid components and local motion";
            if(version!=1||Kind(kind)==null||!RoomControls.ValidPhysics(physics)||surfaces==null||drawingTips==null||surfaces.Any(s=>s==null)||drawingTips.Any(t=>t==null)||
                recipe?.playing==true||motion!=null&&!motion.Valid)return false;
            if(recipe!=null&&!recipe.Validate(out error)||collision!=null&&!collision.Validate(out error))return false;
            var shallow=new RoomObjectData {kind=Kind(kind).Value,recipe=recipe,surfaces=surfaces,drawingTips=drawingTips,snapPoints=snapPoints,containers=containers,heightFields=heightFields};
            if(!DrawingSurface.ValidateCollection(shallow,out error)||!DrawingTip.ValidateCollection(shallow,out error)||!RoomSnapPoint.ValidateCollection(shallow,out error)||!RoomContainer.ValidateCollection(shallow,out error)||!RoomHeightField.ValidateCollection(shallow,out error))return false;
            var data=Instantiate("Prototype",Vector3.zero,Quaternion.identity,1);data.motion=null;
            return ValidateObjects(new[]{data},out error);
        }
        internal static bool ValidateObjects(RoomObjectData[] objects,out string error)=>new RoomDocument {version=RoomDocument.CurrentVersion,objects=new[]{
            new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro}
        }.Concat(objects).ToArray()}.Validate(out error);
        internal static CreationPrototype Capture(RoomObjectData data) {
            if(data==null||data.IsBuiltIn)throw new ArgumentException("Choose a created object");
            var copy=data.Copy();var q=Quaternion.Inverse(copy.rotation);
            return new CreationPrototype {kind=copy.kind switch {RoomObjectKind.ImportedModel=>"model",RoomObjectKind.Assembly=>"recipe",_=>copy.kind.ToString().ToLowerInvariant()},
                color=copy.color,physics=RoomControls.Physics(copy),recipe=copy.recipe,collision=copy.collision,surfaces=copy.surfaces,drawingTips=copy.drawingTips,snapPoints=copy.snapPoints,containers=copy.containers,heightFields=copy.heightFields,
                points=copy.points,radius=copy.radius,modelHash=copy.modelHash,motion=copy.motion==null?null:new PrototypeMotion {loop=copy.motion.loop,frames=copy.motion.frames.Select(f=>new PrototypeFrame {
                    time=f.time,position=q*(f.position-copy.position)/copy.scale,rotation=(q*f.rotation).normalized,scale=f.scale/copy.scale}).ToArray()}};
        }
    }
}
