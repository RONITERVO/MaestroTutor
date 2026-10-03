// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>A trigger/pinch draws a stroke; failed saves retain a frozen draft.</summary>
    public sealed class SpatialDrawing:MonoBehaviour
    {
        public RoomEditor Editor;
        readonly List<Vector3> points=new();
        int owner=-1;
        IDisposable write;
        PencilMarks preview;
        Color color;
        float nextPreview,radius;
        RoomObjectData retained;
        string roomSession,errorText="",surfaceTarget,surfaceId,surfaceBefore;
        RoomOwnership.Lease surfaceOwner;
        bool attached;
        public string SessionId {get;private set;}=Guid.NewGuid().ToString("N");
        public bool IsDrawing=>owner!=-1;
        public bool HasUnsavedStroke=>retained!=null;
        public JObject Observe()=>new() {["sessionId"]=SessionId,["phase"]=IsDrawing?"drawing":HasUnsavedStroke?"unsaved":"idle",["points"]=retained?.points.Length??points.Count,["temporary"]=Editor&&Editor.TemporaryRoom,["error"]=Maestro.Quest.Imports.ImportObservation.Text(errorText)};
        public void Begin(int id,Ray ray)
        {
            if(!Editor||!Editor.DrawingMode||IsDrawing)return;
            if(HasUnsavedStroke){Editor.ReportStatus("Save or discard the retained stroke first");return;}
            write=Editor.WriteGate.TryWrite(out var blocked);if(write==null){Editor.ReportStatus(blocked);return;}
            SessionId=Guid.NewGuid().ToString("N");roomSession=Editor.TemporarySessionId;owner=id;color=Editor.Paint;radius=Editor.DrawingRadius;points.Clear();nextPreview=0;errorText="";
            Vector3 initialPoint=default;attached=Editor.DrawingOnSurfaces&&Editor.FindDrawingSurface(ray,.25f,out surfaceTarget,out surfaceId,out initialPoint,out _);
            if(Editor.DrawingOnSurfaces&&!attached){Editor.ReportStatus("Point at an enabled drawing patch within 25 cm");Clear();return;}
            if(attached) {
                if(!Editor.CanEditObject(surfaceTarget,true,out var error)){Editor.ReportStatus(error);Clear();return;}
                if(Editor.SurfaceErasing){Editor.EraseSurfaceAt(surfaceTarget,surfaceId,initialPoint);Clear();return;}
                surfaceBefore=JsonUtility.ToJson(Editor.Read(surfaceTarget).surfaces.First(s=>s.id==surfaceId));
                if(!Editor.Ownership.TryAcquire("surface-pencil:"+SessionId,"Your surface pencil",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(surfaceTarget,"wholeTarget")},_=>End(id,false),out surfaceOwner,out error,preservePlacement:true)){Editor.ReportStatus(error);Clear();return;}
            }
            var go=new GameObject("Pencil stroke in progress");go.transform.SetParent(transform,false);preview=go.AddComponent<PencilMarks>();if(attached)go.transform.SetParent(Editor.Find(surfaceTarget).GetComponent<DrawingSurfaceView>().Surface(surfaceId),false);Move(id,ray);
        }
        public void Move(int id,Ray ray)
        {
            if(owner!=id)return;var point=ray.GetPoint(.12f);
            if(attached) {
                if(!Editor.FindDrawingSurface(ray,.25f,out var target,out var surface,out point,out _)||target!=surfaceTarget||surface!=surfaceId){End(id);return;}
            }
            if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z)){End(id);return;}
            if(points.Count>0&&Vector3.Distance(points[^1],point)<.005f)return;
            if(points.Count>0&&Vector3.Distance(points[^1],point)>.35f){End(id);return;}
            points.Add(point);if(points.Count>=(attached?DrawingSurface.MaximumPoints:RoomDocument.MaximumStrokePoints)){End(id);return;}
            if(points.Count<2||Time.unscaledTime<nextPreview)return;nextPreview=Time.unscaledTime+1f/30;
            preview.SetPaths(new[]{PreviewPoints()},radius);preview.SetColor(color);
        }
        public Vector3 PointerPoint(Ray ray)=>Editor&&Editor.DrawingOnSurfaces&&Editor.FindDrawingSurface(ray,.25f,out _,out _,out _,out var distance)?ray.GetPoint(distance):ray.GetPoint(.12f);
        Vector3[] PreviewPoints()=>attached?points.Select(p=>p-Vector3.forward*radius).ToArray():points.Select(preview.transform.InverseTransformPoint).ToArray();
        public void End(int id)=>End(id,true);
        void End(int id,bool save)
        {
            if(owner!=id)return;owner=-1;
            if(!Editor||points.Count<2){Clear();return;}
            var origin=attached?Vector3.zero:Editor.transform.InverseTransformPoint(points[0]);
            retained=new RoomObjectData {kind=RoomObjectKind.Drawing,position=origin,color=color,radius=radius,points=attached?points.ToArray():points.Select(p=>Editor.transform.InverseTransformPoint(p)-origin).ToArray()};
            if(preview){preview.SetPaths(new[]{PreviewPoints()},radius);preview.SetColor(color);}
            points.Clear();if(save)Resolve(SessionId,false,out _,out _);else {errorText="Surface drawing interrupted; release the object, then retry or discard the retained stroke";Editor.ReportStatus(errorText);}
        }
        internal bool CanResolve(string session,bool discard,out string error)
        {
            error=null;if(!HasUnsavedStroke||session!=SessionId){error="Inspect the current unsaved stroke before resolving it";return false;}
            if(discard)return true;
            if(!Editor||roomSession!=Editor.TemporarySessionId){error="The stroke belongs to a different room session";return false;}
            if(attached) {
                var now=Editor.Read(surfaceTarget)?.surfaces?.FirstOrDefault(s=>s.id==surfaceId);
                if(now==null||JsonUtility.ToJson(now)!=surfaceBefore){error="The surface changed; discard this retained stroke instead of applying it to a different patch";return false;}
                if(!Editor.Ownership.CanAcquire("surface-pencil:"+SessionId,RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(surfaceTarget,"wholeTarget")},out error))return false;
                return Editor.PrepareSurfaceEdit(surfaceTarget,Editor.ObjectRevision(surfaceTarget),SurfaceArguments(),out _,out _,out error);
            }
            return Editor.CanCreateDrawing(retained.points,retained.radius,out error);
        }
        internal bool Resolve(string session,bool discard,out JObject result,out string error)
        {
            result=null;string id="";int count=retained?.points.Length??0;
            if(!CanResolve(session,discard,out error)||!discard&&!SaveRetained(out id,out error)){
                errorText=error;if(Editor)Editor.ReportStatus("Stroke not saved: "+error+". Retry stroke or Discard stroke.");return false;
            }
            result=new JObject {["sessionId"]=session,["phase"]=discard?"discarded":"saved",["objectId"]=id,["points"]=count,["temporary"]=Editor&&Editor.TemporaryRoom};Clear();
            if(!discard)Editor.Select(Editor.Find(id));Editor.ReportStatus(discard?"Unsaved stroke discarded":"Stroke saved");return true;
        }
        internal void ResolveManual(bool discard)=>Resolve(SessionId,discard,out _,out _);
        JObject SurfaceArguments()=>new() {["operation"]="add",["surface"]=surfaceId,["stroke"]="",["red"]=retained.color.r,["green"]=retained.color.g,["blue"]=retained.color.b,["radius"]=retained.radius,["points"]=new JArray(retained.points.Select(p=>new JObject {["x"]=p.x,["y"]=p.y,["z"]=0}))};
        bool SaveRetained(out string id,out string error) {
            id=surfaceTarget;
            if(attached) {
                // Retrying after a grab/pause must reclaim ownership, not merely check
                // that a takeover would be allowed while another program keeps running.
                if(surfaceOwner?.Held!=true&&!Editor.Ownership.TryAcquire("surface-pencil:"+SessionId,"Your surface pencil",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(surfaceTarget,"wholeTarget")},_=>{},out surfaceOwner,out error,preservePlacement:true))return false;
                // A displaced owner's cleanup may change the patch. Recheck afterward.
                if(!CanResolve(SessionId,false,out error))return false;
                return Editor.EditSurface(surfaceTarget,Editor.ObjectRevision(surfaceTarget),SurfaceArguments(),out _,out error);
            }
            return Editor.CreateDrawing("",retained.position,1,retained.color,retained.radius,retained.points,out id,out error);
        }
        void Clear(){surfaceOwner?.Dispose();surfaceOwner=null;attached=false;surfaceTarget=surfaceId=surfaceBefore=null;retained=null;points.Clear();owner=-1;errorText="";SessionId=Guid.NewGuid().ToString("N");if(preview)Destroy(preview.gameObject);preview=null;write?.Dispose();write=null;}
        public void Cancel(int id)=>End(id);
        void OnApplicationPause(bool paused){if(paused&&IsDrawing)End(owner);}
        void OnApplicationFocus(bool focused){if(!focused&&IsDrawing)End(owner);}
        void OnDisable(){if(IsDrawing)End(owner);}
        void OnDestroy()=>Clear();
    }
}
