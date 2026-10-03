// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareSurfaceEdit(string target,int revision,JObject args,out RoomObjectData data,out string strokeId,out string error)
        {
            data=null;strokeId="";if(!CanEditObject(target,true,out error))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; inspect its current surface revision";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object first";return false;}
            data=Pose(Read(target),Find(target).transform);var list=(data.surfaces??Array.Empty<DrawingSurface>()).ToList();string id=(string)args["surface"],op=(string)args["operation"];
            var surface=list.FirstOrDefault(s=>s.id==id);
            if(op=="configure") {
                var replacement=JsonUtility.FromJson<DrawingSurface>(args["definition"].ToString());replacement.id=id;replacement.version=1;replacement.strokes=surface?.strokes??Array.Empty<SurfaceStroke>();
                if(surface!=null)list[list.IndexOf(surface)]=replacement;else list.Add(replacement);
            }else {
                if(surface==null){error="This drawing surface was removed";return false;}
                if(op=="removeSurface")list.Remove(surface);
                else if(op=="clear")surface.strokes=Array.Empty<SurfaceStroke>();
                else {
                    var strokes=surface.strokes.ToList();strokeId=(string)args["stroke"];
                    string requestedStroke=strokeId;var stroke=strokes.FirstOrDefault(s=>s.id==requestedStroke);
                    if(op=="add") {
                        if(!surface.enabled){error="Enable drawing on this surface first";return false;}
                        if(stroke!=null){error="This stroke identity already exists";return false;}
                        if(string.IsNullOrEmpty(strokeId))strokeId=Guid.NewGuid().ToString("N");
                        strokes.Add(new SurfaceStroke {id=strokeId,color=new Color((float)args["red"],(float)args["green"],(float)args["blue"]),radius=(float)args["radius"],points=Programs.DrawingData.Points(args["points"])});
                    }else {
                        if(stroke==null){error="This surface stroke was removed";return false;}
                        if(op=="removeStroke")strokes.Remove(stroke);
                        else if(op=="splice") {
                            int index=(int)args["index"],remove=(int)args["deleteCount"];var points=Programs.DrawingData.Points(args["points"]);
                            if(index<0||index>stroke.points.Length||remove<0||remove>stroke.points.Length-index||remove==0&&points.Length==0){error="Choose an existing point range";return false;}
                            stroke.points=stroke.points.Take(index).Concat(points).Concat(stroke.points.Skip(index+remove)).ToArray();
                        }else {error="Unknown surface edit";return false;}
                    }
                    surface.strokes=strokes.ToArray();
                }
            }
            data.surfaces=list.ToArray();var candidate=Snapshot();var next=data;candidate.objects=candidate.objects.Select(x=>x.id==target?next:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditSurface(string target,int revision,JObject args,out string strokeId,out string error)
        {
            if(!PrepareSurfaceEdit(target,revision,args,out var data,out strokeId,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Surface drawing saved",false,out error);
        }
        internal void EraseSurfaceAt(string target,string surface,Vector3 point)
        {
            var patch=Read(target)?.surfaces?.FirstOrDefault(s=>s.id==surface);if(patch==null){SetStatus("The drawing patch was removed");return;}
            float nearest=Mathf.Max(.01f,DrawingRadius*2);string stroke=null;
            foreach(var mark in patch.strokes)for(int i=1;i<mark.points.Length;i++) {
                var a=mark.points[i-1];var delta=mark.points[i]-a;float along=delta.sqrMagnitude<.00000001f?0:Mathf.Clamp01(Vector3.Dot(point-a,delta)/delta.sqrMagnitude);
                float distance=Vector3.Distance(point,a+along*delta);if(distance<=nearest){nearest=distance;stroke=mark.id;}
            }
            if(stroke==null){SetStatus("Tap a surface stroke to erase it");return;}
            if(!Ownership.TryAcquire("surface-eraser", "Your surface eraser",RoomActorRole.Control,new[]{new Maestro.Quest.Programs.BehaviourCatalog.Claim(target,"wholeTarget")},null,out var lease,out var error,preservePlacement:true)){SetStatus(error);return;}
            using(lease){if(!EditSurface(target,ObjectRevision(target),new JObject {["operation"]="removeStroke",["surface"]=surface,["stroke"]=stroke},out _,out error))SetStatus(error);}
        }
        internal bool FindDrawingSurface(Ray ray,float maximum,out string target,out string surface,out Vector3 point,out float distance,float? radius=null,string exclude=null)
        {
            target=surface=null;point=default;distance=maximum;
            foreach(var pair in objects) {
                if(pair.Key==exclude)continue;
                var view=pair.Value.GetComponent<DrawingSurfaceView>();
                if(view&&view.Hit(ray,distance,out var hit,out var local,out float near,radius??DrawingRadius)){target=pair.Key;surface=hit;point=local;distance=near;}
            }
            return target!=null;
        }
    }
}
