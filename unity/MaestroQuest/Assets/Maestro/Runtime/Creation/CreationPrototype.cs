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
        public AppearanceBinding[] appearanceBindings=Array.Empty<AppearanceBinding>();
        public RoomAudioEmitter[] audioEmitters=Array.Empty<RoomAudioEmitter>();
        public string environmentProfile="",visibilityLayer="";
        public RoomWaterTraversal waterTraversal=new();
        public RoomModelGeometry modelGeometry=new();
        internal bool HasResources=>appearanceBindings.Length>0||audioEmitters.Length>0||environmentProfile!=""||visibilityLayer!="";
        public ObjectPhysicsSettings physics;
        public RoomRecipe recipe;
        public CollisionRecipe collision;
        public RoomWindow[] windows=Array.Empty<RoomWindow>();
        public DrawingSurface[] surfaces=Array.Empty<DrawingSurface>();
        public DrawingTip[] drawingTips=Array.Empty<DrawingTip>();
        public RoomSnapPoint[] snapPoints=Array.Empty<RoomSnapPoint>();
        public RoomContainer[] containers=Array.Empty<RoomContainer>();
        public RoomHeightField[] heightFields=Array.Empty<RoomHeightField>();
        public SculptTip[] sculptTips=Array.Empty<SculptTip>();
        public RoomMaterialStore[] materialStores=Array.Empty<RoomMaterialStore>();
        public Vector3[] points;
        public float radius=.003f;
        // Root-motion positions/rotations/scales are relative to the captured object's pose.
        public PrototypeMotion motion;
        static RoomObjectKind? Kind(string value)=>value switch {
            "block"=>RoomObjectKind.Block,"ball"=>RoomObjectKind.Ball,"cylinder"=>RoomObjectKind.Cylinder,
            "drawing"=>RoomObjectKind.Drawing,"model"=>RoomObjectKind.ImportedModel,"recipe"=>RoomObjectKind.Assembly,_=>null};
        internal RoomObjectData Instantiate(string name,Vector3 position,Quaternion rotation,float scale) {
            var data=new RoomObjectData {id=Guid.NewGuid().ToString("N"),name=name,kind=Kind(kind).Value,position=position,rotation=rotation,scale=scale,
                modelGeometry=modelGeometry.Copy(),waterTraversal=waterTraversal.Copy(),appearanceBindings=appearanceBindings.Select(b=>b.Copy()).ToArray(),audioEmitters=audioEmitters.Select(e=>e.Copy()).ToArray(),environmentProfile=environmentProfile,visibilityLayer=visibilityLayer,color=color,recipe=recipe?.Copy(),collision=collision?.Copy(),windows=windows.Select(w=>w.Copy()).ToArray(),surfaces=surfaces.Select(s=>s.Copy()).ToArray(),drawingTips=drawingTips.Select(t=>t.Copy()).ToArray(),snapPoints=snapPoints?.Select(p=>p.Copy()).ToArray()??Array.Empty<RoomSnapPoint>(),
                heightFields=heightFields?.Select(f=>f.Copy()).ToArray()??Array.Empty<RoomHeightField>(),sculptTips=sculptTips?.Select(t=>t.Copy()).ToArray()??Array.Empty<SculptTip>(),materialStores=materialStores?.Select(s=>s.Copy()).ToArray()??Array.Empty<RoomMaterialStore>(),containers=containers?.Select(c=>c.Copy()).ToArray()??Array.Empty<RoomContainer>(),points=points?.ToArray(),radius=radius,modelHash=modelHash};
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
            if(!value.ContainsKey("modelGeometry"))result.modelGeometry=new();
            if(!value.ContainsKey("windows"))result.windows=Array.Empty<RoomWindow>();
            if(!value.ContainsKey("waterTraversal"))result.waterTraversal=new();
            if(!value.ContainsKey("collision"))result.collision=null;if(!value.ContainsKey("motion"))result.motion=null;
            if(!value.ContainsKey("appearanceBindings"))result.appearanceBindings=Array.Empty<AppearanceBinding>();
            if(!value.ContainsKey("audioEmitters"))result.audioEmitters=Array.Empty<RoomAudioEmitter>();
            if(!value.ContainsKey("environmentProfile"))result.environmentProfile="";
            if(!value.ContainsKey("visibilityLayer"))result.visibilityLayer="";
            return result;
        }
        public bool Validate(out string error) {
            error="Provide a version-1, version-2, version-3, version-4 or version-5 creation prototype with idle geometry, valid components and local motion";
            if(modelGeometry==null||!modelGeometry.Valid||version<5&&!modelGeometry.Default||waterTraversal==null||!waterTraversal.Valid||version is not (1 or 2 or 3 or 4 or 5)||windows==null||version<4&&windows.Length>0||visibilityLayer==null||version<3&&visibilityLayer!=""||version==3&&visibilityLayer==""||appearanceBindings==null||audioEmitters==null||environmentProfile==null||appearanceBindings.Any(b=>b==null)||audioEmitters.Any(e=>e==null)||version==1&&HasResources||Kind(kind)==null||!RoomControls.ValidPhysics(physics)||surfaces==null||drawingTips==null||surfaces.Any(s=>s==null)||drawingTips.Any(t=>t==null)||
                recipe?.playing==true||motion!=null&&!motion.Valid)return false;
            if(modelGeometry.meshCollision&&motion!=null){error="Rigid mesh geometry cannot retain recorded root motion";return false;}
            if(recipe!=null&&!recipe.Validate(out error)||collision!=null&&!collision.Validate(out error))return false;
            var shallow=new RoomObjectData {kind=Kind(kind).Value,recipe=recipe,windows=windows,surfaces=surfaces,drawingTips=drawingTips,snapPoints=snapPoints,containers=containers,heightFields=heightFields,sculptTips=sculptTips,materialStores=materialStores};
            if(!RoomWindow.ValidateCollection(shallow,out error)||!DrawingSurface.ValidateCollection(shallow,out error)||!DrawingTip.ValidateCollection(shallow,out error)||!RoomSnapPoint.ValidateCollection(shallow,out error)||!RoomContainer.ValidateCollection(shallow,out error)||!RoomHeightField.ValidateCollection(shallow,out error)||!SculptTip.ValidateCollection(shallow,out error)||!RoomMaterialStore.ValidateCollection(shallow,out error))return false;
            var data=Instantiate("Prototype",Vector3.zero,Quaternion.identity,1);data.motion=null;
            // Local component shape is checked here; the containing blueprint must
            // close every definition reference before it can create anything.
            if(!CreationResources.ValidatePrototype(data,out error))return false;
            data.appearanceBindings=Array.Empty<AppearanceBinding>();data.audioEmitters=Array.Empty<RoomAudioEmitter>();data.environmentProfile="";data.visibilityLayer="";
            return ValidateObjects(new[]{data},out error);
        }
        internal static bool ValidateObjects(RoomObjectData[] objects,out string error,CreationResources resources=null)=>new RoomDocument {version=RoomDocument.CurrentVersion,appearances=resources?.appearances??Array.Empty<RoomAppearance>(),audioSources=resources?.audioSources??Array.Empty<RoomAudioDefinition>(),environmentProfiles=resources?.environmentProfiles??Array.Empty<RoomEnvironmentProfile>(),visibilityLayers=resources?.visibilityLayers??Array.Empty<RoomVisibilityLayer>(),objects=new[]{
            new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro}
        }.Concat(objects).ToArray()}.Validate(out error);
        internal static CreationPrototype Capture(RoomObjectData data) {
            if(data==null||data.IsBuiltIn)throw new ArgumentException("Choose a created object");
            var copy=data.Copy();var q=Quaternion.Inverse(copy.rotation);
            return new CreationPrototype {version=!copy.modelGeometry.Default?5:copy.windows.Length>0?4:copy.visibilityLayer!=""?3:CreationResources.Uses(copy)?2:1,appearanceBindings=copy.appearanceBindings,audioEmitters=copy.audioEmitters,environmentProfile=copy.environmentProfile,visibilityLayer=copy.visibilityLayer,kind=copy.kind switch {RoomObjectKind.ImportedModel=>"model",RoomObjectKind.Assembly=>"recipe",_=>copy.kind.ToString().ToLowerInvariant()},
                modelGeometry=copy.modelGeometry.Copy(),waterTraversal=copy.waterTraversal.Copy(),color=copy.color,physics=RoomControls.Physics(copy),recipe=copy.recipe,collision=copy.collision,windows=copy.windows,surfaces=copy.surfaces,drawingTips=copy.drawingTips,snapPoints=copy.snapPoints,containers=copy.containers,heightFields=copy.heightFields,sculptTips=copy.sculptTips,materialStores=copy.materialStores,
                points=copy.points,radius=copy.radius,modelHash=copy.modelHash,motion=copy.motion==null?null:new PrototypeMotion {loop=copy.motion.loop,frames=copy.motion.frames.Select(f=>new PrototypeFrame {
                    time=f.time,position=q*(f.position-copy.position)/copy.scale,rotation=(q*f.rotation).normalized,scale=f.scale/copy.scale}).ToArray()}};
        }
    }
}
