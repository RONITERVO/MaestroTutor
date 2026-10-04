// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Creation {
    /// <summary>One bounded visual draft; release publishes the ordinary sculpt action once.</summary>
    public sealed partial class SpatialSculpting:MonoBehaviour {
        public RoomEditor Editor;
        public string Mode {get;private set;}="off";
        public float Radius {get;private set;}=.08f;
        public float Height {get;private set;}=.03f;
        public bool Enabled=>Mode!="off";
        public string SessionId {get;private set;}=Guid.NewGuid().ToString("N");
        public bool Active=>owner!=-1;
        public bool Retained {get;private set;}
        public bool Busy=>Active||Retained;
        readonly List<Vector2> points=new();
        readonly bool[] fingerBlocked={true,true};
        int owner=-1;string target,tool,mode,before,roomSession,errorText="";
        float radius,height,nextPreview,maximum;
        RoomHeightField source,draft;
        RoomActorRole role;
        RoomOwnership.Lease lease;IDisposable write;
        internal bool Owns(int id)=>Active&&owner==id;
        internal bool OwnsTool(string id)=>Active&&tool==id;
        internal bool CanConfigure(out string error){error=!Editor?"Sculpting is unavailable":Editor.DrawingInProgress?"Finish or discard the current drawing/sculpt gesture first":Editor.WriteGate.Frozen?"Finish the workspace operation first":Editor.Ownership.Suspended?"Room actions are paused":null;return error==null;}
        internal bool Configure(string operation,float size,float amount,out string error){
            if(!CanConfigure(out error))return false;
            if(!new[]{"off","raise","lower","level"}.Contains(operation)||!float.IsFinite(size)||size<.005f||size>2||!float.IsFinite(amount)||amount<0||amount>.5f){error="Choose off/raise/lower/level, radius 0.005–2 m and height 0–0.5 m";return false;}
            if(operation!="off"){if(!Editor.ConfigureDrawing("off",Editor.Paint,Editor.DrawingRadius,out error))return false;Editor.SuspendConstructionPicking();}
            Mode=operation;Radius=size;Height=amount;fingerBlocked[0]=fingerBlocked[1]=true;
            Editor.ReportStatus(operation=="off"?"Sculpt tool put away":"Sculpt "+operation+": trigger near the surface, or touch it with an index fingertip; release/lift to save");return true;
        }
        internal void PutAway(){Mode="off";fingerBlocked[0]=fingerBlocked[1]=true;}
        public void Begin(int id,Ray ray){if(Enabled&&Editor.FindHeightField(ray,.25f,null,out var at,out var p,out _))BeginAt(id,at,p,null,Mode,Radius,Height,.25f,RoomActorRole.Control);}
        public void Move(int id,Ray ray){if(!Owns(id))return;if(!Editor.FindHeightField(ray,maximum,tool,out var at,out var p,out _)||at!=target){End(id);return;}Sample(p);}
        public void End(int id){if(Owns(id))Finish(true);}
        public void Cancel(int id,IXRSelectInteractor lost=null){if(Owns(id)||Active&&tool!=null&&lost!=null&&Editor.Find(tool)?.Grab.interactorsSelecting.Any(i=>ReferenceEquals(i,lost))==true)Finish(false);if(id>=0&&id<2)fingerBlocked[id]=true;}
        internal void Finger(int id,Vector3? point){
            if(!point.HasValue){Cancel(id);return;}
            string at=null;Vector2 p=default;bool contact=Enabled&&Editor.FindHeightFieldNear(point.Value,null,out at,out p);
            if(!contact){End(id);fingerBlocked[id]=false;return;}
            if(Owns(id)){if(at!=target){End(id);fingerBlocked[id]=true;}else Sample(p);return;}
            if(fingerBlocked[id])return;fingerBlocked[id]=true;BeginAt(id,at,p,null,Mode,Radius,Height,.03f,RoomActorRole.Control);
        }
        internal void BeginTool(string id,Ray ray,string operation,float size,float amount,RoomActorRole actor){if(Editor.FindHeightField(ray,.02f,id,out var at,out var p,out _))BeginAt(-2,at,p,id,operation,size,amount,.02f,actor);}
        internal void MoveTool(string id,Ray ray){if(OwnsTool(id))Move(-2,ray);}
        internal void EndTool(string id,bool save=true){if(OwnsTool(id))Finish(save);}
        void BeginAt(int id,string at,Vector2 p,string toolId,string operation,float size,float amount,float max,RoomActorRole actor){
            if(!Editor||Busy||Editor.DrawingInProgress||Editor.Ownership.Suspended||Editor.WriteGate.Frozen)return;
            if(!Editor.CanEditObject(at,true,out var error)){Editor.ReportStatus(error);return;}
            write=Editor.WriteGate.TryWrite(out error);if(write==null){Editor.ReportStatus(error);return;}
            SessionId=Guid.NewGuid().ToString("N");target=at;tool=toolId;mode=operation;radius=size;height=amount;maximum=max;role=actor;roomSession=Editor.TemporarySessionId;
            source=Editor.Read(target)?.heightFields?.FirstOrDefault()?.Copy();if(source==null){Clear();return;}before=JsonUtility.ToJson(source);
            if(!Editor.Ownership.TryAcquire("sculpt:"+SessionId,tool==null?"Your sculpt tool":"Held sculpt tool",role,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},_=>Finish(false),out lease,out error,preservePlacement:true)){Editor.ReportStatus(error);Clear();return;}
            // A displaced owner's cleanup may have changed the saved field.
            if(!Unchanged(out error)){Editor.ReportStatus(error);Clear();return;}
            owner=id;points.Clear();errorText="";nextPreview=0;Sample(p,true);if(Active)Editor.ReportStatus("Sculpt preview — release or lift to save shape and collision");
        }
        bool Unchanged(out string error){
            error=null;var now=Editor?Editor.Read(target)?.heightFields?.FirstOrDefault():null;
            if(!Editor||roomSession!=Editor.TemporarySessionId||now==null||JsonUtility.ToJson(now)!=before){error="The surface or room changed; discard this sculpt draft";return false;}return MaterialUnchanged(out error);
        }
        void Sample(Vector2 point,bool force=false){
            if(!Active)return;
            if(!Unchanged(out var error)||Editor.Ownership.Suspended||lease?.Held!=true){errorText=error??"Sculpting interrupted";Finish(false);return;}
            if(points.Count>0){float distance=Vector2.Distance(points[^1],point);if(distance<Mathf.Max(.003f,radius*.12f))return;if(distance>.35f){Finish(true);return;}}
            points.Add(point);draft=source.Copy();draft.Sculpt(mode,points.ToArray(),radius,height,out _,out _);
            if(force||Time.unscaledTime>=nextPreview){nextPreview=Time.unscaledTime+1f/20;Editor.Find(target)?.GetComponent<HeightFieldView>()?.Preview(draft);}
            if(points.Count>=32)Finish(true);
        }
        void Finish(bool save){
            if(!Active)return;owner=-1;
            if(source==null||draft==null||source.heights.SequenceEqual(draft.heights)){Clear();return;}
            Retained=true;Editor.Find(target)?.GetComponent<HeightFieldView>()?.Preview(draft);
            if(save)Resolve(SessionId,false,out _,out _);else {errorText="Sculpting interrupted; retry or discard the retained draft";Editor.ReportStatus(errorText);}
        }
        internal bool CanResolve(string session,bool discard,out string error){
            error=null;if(!Retained||session!=SessionId){error="Inspect the current retained sculpt draft first";return false;}if(discard)return true;
            if(!Unchanged(out error)||!Editor.Ownership.CanAcquire("sculpt:"+SessionId,role,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},out error))return false;
            if(Material)return PrepareMaterial(out _,out error);
            return Editor.PrepareSculpt(target,Editor.ObjectRevision(target),mode,points.ToArray(),radius,height,out _,out _,out error);
        }
        internal bool Resolve(string session,bool discard,out JObject result,out string error){
            result=null;if(!CanResolve(session,discard,out error))return Failed(error);
            int changed=0;
            if(!discard){
                if(lease?.Held!=true&&!Editor.Ownership.TryAcquire("sculpt:"+SessionId,tool==null?"Your sculpt tool":"Held sculpt tool",role,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},_=>{},out lease,out error,preservePlacement:true))return Failed(error);
                if(!CanResolve(session,false,out error))return Failed(error);
                if(Material?!CommitMaterial(out changed,out error):!Editor.SculptHeightField(target,Editor.ObjectRevision(target),mode,points.ToArray(),radius,height,out changed,out error))return Failed(error);
            }
            result=new JObject{["sessionId"]=session,["phase"]=discard?"discarded":"saved",["target"]=discard?"":target,["points"]=points.Count,["changedVertices"]=changed,["temporary"]=Editor.TemporaryRoom};bool material=Material;Clear();Editor.ReportStatus(discard?"Surface draft discarded":material?"Material gesture saved — Undo restores both balances":"Sculpt gesture saved — Undo restores the surface");return true;
        }
        bool Failed(string error){errorText=error;Editor?.ReportStatus("Sculpt not saved: "+error+". Retry sculpt or Discard sculpt.");return false;}
        internal void ResolveManual(bool discard)=>Resolve(SessionId,discard,out _,out _);
        public Vector3 PointerPoint(Ray ray)=>Editor&&Editor.FindHeightField(ray,.25f,null,out _,out _,out var distance)?ray.GetPoint(distance):ray.GetPoint(.12f);
        internal JObject Observe()=>new(){["sessionId"]=SessionId,["phase"]=Active?"sculpting":Retained?"unsaved":"idle",["target"]=target??"",["brush"]=new JObject{["mode"]=mode??"none",["radius"]=Busy?radius:0,["height"]=Busy?height:0},["points"]=points.Count,["temporary"]=Editor&&Editor.TemporaryRoom,["error"]=Maestro.Quest.Imports.ImportObservation.Text(errorText)};
        internal JObject PathPage(string session,int offset){if(!Busy||session!=SessionId||offset<0||offset>points.Count)return null;return new JObject{["sessionId"]=SessionId,["offset"]=offset,["count"]=points.Count,["points"]=new JArray(points.Skip(offset).Take(8).Select(p=>new JObject{["x"]=p.x,["z"]=p.y}))};}
        void Clear(){ClearMaterial();if(Editor)Editor.Find(target)?.GetComponent<HeightFieldView>()?.Preview(null);lease?.Dispose();lease=null;write?.Dispose();write=null;owner=-1;Retained=false;target=tool=mode=before=roomSession=null;source=draft=null;points.Clear();errorText="";SessionId=Guid.NewGuid().ToString("N");}
        void OnApplicationPause(bool paused){if(paused&&Active)Finish(false);}
        void OnApplicationFocus(bool focused){if(!focused&&Active)Finish(false);}
        void OnDisable(){if(Active)Finish(false);}
        void OnDestroy()=>Clear();
    }
}
