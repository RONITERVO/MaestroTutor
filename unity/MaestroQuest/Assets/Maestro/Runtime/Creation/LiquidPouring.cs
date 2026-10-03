// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Native physical effects cooperate with grips/animation, like gravity. Authoring
    // the affected contents is blocked until the short live episode is published.
    [DefaultExecutionOrder(400)]
    public sealed class LiquidPouring:MonoBehaviour {
        internal const int Segments=32;
        sealed class Vessel {
            internal string Id;internal RoomContainer Saved,Live;internal RoomItem Item;internal RigidRoomItem Rigid;internal Rigidbody Body;internal bool Ready;internal ContainerFlowGeometry.Opening Opening;
            internal LineRenderer Stream;internal Material Material;internal double Received,Spilled;internal readonly HashSet<string> Receivers=new();
        }
        readonly SortedDictionary<string,Vessel> vessels=new(StringComparer.Ordinal);
        readonly Dictionary<string,RoomContainer> original=new(),contents=new();
        readonly RaycastHit[] hits=new RaycastHit[64];readonly Vector3[] path=new Vector3[Segments+1];
        RoomEditor editor;RoomPhysicsWorld world;IDisposable write;bool publishing,blocked;float nextSample,lastSample,quiet,elapsed;string session=Guid.NewGuid().ToString("N"),error="";
        internal bool Active=>write!=null;
        internal bool Owns(string target)=>Active&&contents.ContainsKey(target);
        internal string Error=>error;
        internal void Initialize(RoomEditor owner){editor=owner;world=owner.PhysicsWorld;lastSample=Time.unscaledTime;if(world)world.Changed+=PhysicsChanged;}
        internal void Synchronize(RoomDocument document){
            var ids=new HashSet<string>();
            foreach(var data in document.objects){
                if(data.containers?.Length!=1)continue;ids.Add(data.id);
                if(!vessels.TryGetValue(data.id,out var v))vessels[data.id]=v=new Vessel{Id=data.id};
                if(Active&&!publishing&&original.TryGetValue(data.id,out var before)&&JsonUtility.ToJson(before)!=JsonUtility.ToJson(data.containers[0]))Cancel("A container changed; the unfinished pour was reverted");
                v.Saved=data.containers[0].Copy();if(!Active||!contents.ContainsKey(data.id))v.Live=v.Saved.Copy();v.Item=editor.Find(data.id);v.Rigid=v.Item?v.Item.GetComponent<RigidRoomItem>():null;v.Body=v.Item?v.Item.GetComponent<Rigidbody>():null;
                Preview(v);
            }
            foreach(var id in vessels.Keys.Where(x=>!ids.Contains(x)).ToArray()){
                if(Owns(id))Cancel("A container was removed; the unfinished pour was reverted");Release(vessels[id]);vessels.Remove(id);
            }
        }
        void LateUpdate(){float now=Time.unscaledTime;if(now<nextSample)return;nextSample=now+.05f;float elapsedSinceSample=Mathf.Min(now-lastSample,.1f);lastSample=now;Tick(elapsedSinceSample);}
        internal void Tick(float seconds){
            if(!editor||publishing)return;
            if(!float.IsFinite(seconds)||seconds<=0)return;seconds=Mathf.Min(seconds,.1f);
            var physics=editor.PhysicsWorld;
            if(!physics||!physics.Running||editor.Ownership.Suspended||editor.WriteGate.Frozen||!editor.CanSaveRoom){Finish(out _);return;}
            Vector3 gravity=Physics.gravity;if(!float.IsFinite(gravity.sqrMagnitude)||gravity.sqrMagnitude<.01f){Finish(out _);return;}var up=-gravity.normalized;
            if(blocked){HideStreams();return;}
            Physics.SyncTransforms();bool flowing=false;
            var authoring=editor.GetComponent<AnimationWorkshop>();foreach(var vessel in vessels.Values){vessel.Ready=Available(vessel)&&authoring?.ControlsTarget(vessel.Id)!=true;if(vessel.Ready)vessel.Opening=new ContainerFlowGeometry.Opening(vessel.Live,vessel.Item.transform);}
            foreach(var v in vessels.Values){
                if(v.Stream)v.Stream.enabled=false;
                if(!v.Ready||v.Live.amountMl<=0)continue;
                double excess=ContainerFlowGeometry.Excess(v.Live,v.Item.transform.rotation,up);
                double requested=Math.Min(excess,v.Live.capacityMl*.75*seconds*Math.Sqrt(excess/v.Live.capacityMl));if(requested<.000001)continue;
                var origin=ContainerFlowGeometry.Lip(v.Live,v.Item.transform,up,out var outward);if(!physics.CanSimulate(origin))continue;var velocity=outward*.15f;
                var body=v.Body;if(body&&!body.isKinematic)velocity+=Vector3.ClampMagnitude(body.GetPointVelocity(origin),3);
                if(!Trace(v,origin,velocity,gravity,up,out var receiver,out int count))continue;
                if(!Begin(out var issue)){error=issue;blocked=true;editor.ReportStatus(issue);return;}
                Touch(v);double moved=0;
                if(receiver!=null){Touch(receiver);RoomContainer.Transfer(v.Live,receiver.Live,requested,out moved,out _);if(moved>0){v.Received+=moved;v.Receivers.Add(receiver.Id);Preview(receiver);}}
                double spill=Math.Min(v.Live.amountMl,requested-moved);
                if(spill>0){v.Live.amountMl-=spill;v.Spilled+=spill;}
                if(moved+spill<=0)continue;
                contents[v.Id]=v.Live;if(receiver!=null)contents[receiver.Id]=receiver.Live;flowing=true;Preview(v);ShowStream(v,count,requested/seconds);
            }
            if(!Active)return;elapsed+=seconds;quiet=flowing?0:quiet+seconds;
            if(quiet>=.3f||elapsed>=10)Finish(out _);
        }
        bool Available(Vessel v)=>v.Item&&v.Item.isActiveAndEnabled&&v.Rigid&&v.Rigid.GeometryReady&&editor.PhysicsWorld.CanSimulate(v.Item.transform.position);
        bool Begin(out string issue){issue=null;if(Active)return true;write=editor.WriteGate.TryWrite(out issue);if(write==null)return false;session=Guid.NewGuid().ToString("N");quiet=elapsed=0;error="";return true;}
        void Touch(Vessel v){if(contents.ContainsKey(v.Id))return;original[v.Id]=v.Saved.Copy();contents[v.Id]=v.Live;}
        bool Trace(Vessel source,Vector3 origin,Vector3 velocity,Vector3 gravity,Vector3 up,out Vessel receiver,out int count){
            receiver=null;count=1;path[0]=origin;const float step=.025f;
            for(int i=1;i<=Segments;i++){
                float t=i*step;var next=origin+velocity*t+gravity*(.5f*t*t);var previous=path[count-1];var delta=next-previous;float length=delta.magnitude;
                if(length<1e-6f)continue;
                float closest=1;Vessel entering=null;
                foreach(var candidate in vessels.Values){
                    if(candidate==source||!candidate.Ready)continue;
                    if(candidate.Opening.Enters(previous,next,up,out float fraction)&&fraction<closest){closest=fraction;entering=candidate;}
                }
                int found=Physics.RaycastNonAlloc(previous,delta/length,hits,length,~0,QueryTriggerInteraction.Ignore);
                if(found==hits.Length)return false; // Saturated collision query cannot prove a clear path.
                foreach(var hit in hits.AsSpan(0,found)){
                    if(!hit.collider||i==1&&(hit.collider.attachedRigidbody==source.Body||hit.collider.transform.IsChildOf(source.Item.transform)))continue;
                    float fraction=hit.distance/length;if(fraction<=closest){closest=fraction;entering=null;}
                }
                path[count++]=Vector3.LerpUnclamped(previous,next,closest);
                if(closest<1){receiver=editor.PhysicsWorld.CanSimulate(path[count-1])?entering:null;return true;}
                if(!editor.PhysicsWorld.CanSimulate(next))return true;
            }
            return true; // The bounded stream ends as uncollected spill; no persistent pool is implied.
        }
        void Preview(Vessel v){if(v.Item)v.Item.GetComponent<ContainerFillView>()?.Apply(new[]{v.Live});}
        void ShowStream(Vessel v,int count,double rate){
            if(!v.Stream){var go=new GameObject("Liquid stream");go.transform.SetParent(transform,false);v.Stream=go.AddComponent<LineRenderer>();v.Stream.useWorldSpace=true;v.Stream.numCapVertices=2;v.Stream.numCornerVertices=2;v.Stream.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;v.Stream.receiveShadows=false;v.Material=IllustratedMaterials.Create(v.Live.color,0);v.Stream.sharedMaterial=v.Material;}
            v.Material.SetColor("_Color",v.Live.color);v.Stream.startColor=v.Stream.endColor=v.Live.color;v.Stream.startWidth=v.Stream.endWidth=Mathf.Clamp((float)Math.Sqrt(rate)*.0002f,.0015f,.008f);v.Stream.positionCount=count;for(int i=0;i<count;i++)v.Stream.SetPosition(i,path[i]);v.Stream.enabled=true;
        }
        internal bool Finish(out string issue){
            issue=null;if(!Active||publishing){HideStreams();return true;}
            publishing=true;bool ok=false;
            try{ok=editor&&editor.CommitLiquidPour(original,contents,out issue);if(!ok){error=issue??"The pour could not be saved; its liquid quantities were reverted";blocked=true;editor?.ReportStatus(error);}else{
                foreach(var v in vessels.Values.ToArray())if(v.Received+v.Spilled>0)editor.Poured(v.Id,v.Received,v.Spilled,v.Receivers.Count,v.Live.liquid);
            }}finally{ClearEpisode();publishing=false;}
            return ok;
        }
        void Cancel(string reason){error=reason;blocked=true;ClearEpisode();if(editor)editor.ReportStatus(reason);}
        void ClearEpisode(){
            write?.Dispose();write=null;original.Clear();contents.Clear();quiet=elapsed=0;
            foreach(var v in vessels.Values){v.Received=v.Spilled=0;v.Receivers.Clear();var saved=editor?editor.Read(v.Id)?.containers?.FirstOrDefault():null;if(saved!=null)v.Saved=saved.Copy();v.Live=v.Saved.Copy();Preview(v);}HideStreams();
        }
        void HideStreams(){foreach(var v in vessels.Values)if(v.Stream)v.Stream.enabled=false;}
        internal JObject Observe(string id){if(!vessels.TryGetValue(id,out var v))return null;return new JObject{["sessionId"]=session,["phase"]=blocked?"failed":Owns(id)?"flowing":"idle",["contents"]=new JObject{["amountMl"]=v.Live.amountMl,["savedAmountMl"]=v.Saved.amountMl,["capacityMl"]=v.Live.capacityMl,["transferredMl"]=v.Received,["spilledMl"]=v.Spilled},["temporary"]=editor.TemporaryRoom,["error"]=Maestro.Quest.Imports.ImportObservation.Text(error)};}

        void PhysicsChanged(){if(!editor||!world)return;if(!world.Running)Finish(out _);else{blocked=false;lastSample=Time.unscaledTime;nextSample=lastSample+.05f;}}
        void OnApplicationPause(bool paused){if(paused)Finish(out _);}
        void OnApplicationFocus(bool focused){if(!focused)Finish(out _);}
        void OnDisable(){Finish(out _);}
        static void Release(Vessel v){if(v.Stream)Destroy(v.Stream.gameObject);ArtResources.Release(v.Material);}
        void OnDestroy(){Finish(out _);if(world)world.Changed-=PhysicsChanged;foreach(var v in vessels.Values)Release(v);vessels.Clear();write?.Dispose();write=null;}
    }
}
