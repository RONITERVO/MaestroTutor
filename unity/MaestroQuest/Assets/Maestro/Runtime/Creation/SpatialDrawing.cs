// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
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
        float nextPreview;
        RoomObjectData retained;
        string roomSession,errorText="";
        public string SessionId {get;private set;}=Guid.NewGuid().ToString("N");
        public bool IsDrawing=>owner!=-1;
        public bool HasUnsavedStroke=>retained!=null;
        public JObject Observe()=>new() {["sessionId"]=SessionId,["phase"]=IsDrawing?"drawing":HasUnsavedStroke?"unsaved":"idle",["points"]=retained?.points.Length??points.Count,["temporary"]=Editor&&Editor.TemporaryRoom,["error"]=Maestro.Quest.Imports.ImportObservation.Text(errorText)};
        public void Begin(int id,Ray ray)
        {
            if(!Editor||!Editor.DrawingMode||IsDrawing)return;
            if(HasUnsavedStroke){Editor.ReportStatus("Save or discard the retained stroke first");return;}
            write=Editor.WriteGate.TryWrite(out var blocked);if(write==null){Editor.ReportStatus(blocked);return;}
            SessionId=Guid.NewGuid().ToString("N");roomSession=Editor.TemporarySessionId;owner=id;color=Editor.Paint;points.Clear();nextPreview=0;errorText="";
            var go=new GameObject("Pencil stroke in progress");go.transform.SetParent(transform,false);preview=go.AddComponent<PencilMarks>();Move(id,ray);
        }
        public void Move(int id,Ray ray)
        {
            if(owner!=id)return;var point=ray.GetPoint(.12f);if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z)){End(id);return;}
            if(points.Count>0&&Vector3.Distance(points[^1],point)<.005f)return;
            if(points.Count>0&&Vector3.Distance(points[^1],point)>.35f){End(id);return;}
            points.Add(point);if(points.Count>=RoomDocument.MaximumStrokePoints){End(id);return;}
            if(points.Count<2||Time.unscaledTime<nextPreview)return;nextPreview=Time.unscaledTime+1f/30;
            preview.SetPaths(new[]{points.Select(preview.transform.InverseTransformPoint).ToArray()},.003f);preview.SetColor(color);
        }
        public void End(int id)
        {
            if(owner!=id)return;owner=-1;
            if(!Editor||points.Count<2){Clear();return;}
            var origin=Editor.transform.InverseTransformPoint(points[0]);
            retained=new RoomObjectData {kind=RoomObjectKind.Drawing,position=origin,color=color,points=points.Select(p=>Editor.transform.InverseTransformPoint(p)-origin).ToArray()};
            if(preview){preview.SetPaths(new[]{points.Select(preview.transform.InverseTransformPoint).ToArray()},.003f);preview.SetColor(color);}
            points.Clear();Resolve(SessionId,false,out _,out _);
        }
        internal bool CanResolve(string session,bool discard,out string error)
        {
            error=null;if(!HasUnsavedStroke||session!=SessionId){error="Inspect the current unsaved stroke before resolving it";return false;}
            if(discard)return true;
            if(!Editor||roomSession!=Editor.TemporarySessionId){error="The stroke belongs to a different room session";return false;}
            return Editor.CanCreateDrawing(retained.points,retained.radius,out error);
        }
        internal bool Resolve(string session,bool discard,out JObject result,out string error)
        {
            result=null;string id="";int count=retained?.points.Length??0;
            if(!CanResolve(session,discard,out error)||!discard&&!Editor.CreateDrawing("",retained.position,1,retained.color,retained.radius,retained.points,out id,out error)){
                errorText=error;if(Editor)Editor.ReportStatus("Stroke not saved: "+error+". Retry stroke or Discard stroke.");return false;
            }
            result=new JObject {["sessionId"]=session,["phase"]=discard?"discarded":"saved",["objectId"]=id,["points"]=count,["temporary"]=Editor&&Editor.TemporaryRoom};Clear();
            if(!discard)Editor.Select(Editor.Find(id));Editor.ReportStatus(discard?"Unsaved stroke discarded":"Stroke saved");return true;
        }
        internal void ResolveManual(bool discard)=>Resolve(SessionId,discard,out _,out _);
        void Clear(){retained=null;points.Clear();owner=-1;errorText="";SessionId=Guid.NewGuid().ToString("N");if(preview)Destroy(preview.gameObject);preview=null;write?.Dispose();write=null;}
        public void Cancel(int id)=>End(id);
        void OnApplicationPause(bool paused){if(paused&&IsDrawing)End(owner);}
        void OnApplicationFocus(bool focused){if(!focused&&IsDrawing)End(owner);}
        void OnDisable(){if(IsDrawing)End(owner);}
        void OnDestroy()=>Clear();
    }
}
