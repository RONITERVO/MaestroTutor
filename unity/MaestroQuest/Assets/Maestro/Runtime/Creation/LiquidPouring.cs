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
    public sealed partial class LiquidPouring:MonoBehaviour {
        internal const int Segments=LiquidResources.StreamPoints-1;
        sealed class Vessel {
            internal SimulationWorkSchedule.ServiceRecord WorkService=new();internal long AutomaticStep=-1;
            internal string Id;internal RoomContainer Saved,Live;internal RoomItem Item;internal RigidRoomItem Rigid;internal Rigidbody Body;internal bool Ready;internal ContainerFlowGeometry.Opening Opening;
            internal double Received,Spilled,Scooped,Drawn,DipBudget,Rain;internal float Exposure;internal bool RainReady;internal string RainReason="Physics is paused";internal readonly HashSet<string> Receivers=new(),Donors=new(),Dippers=new();
        }
        readonly SortedDictionary<string,Vessel> vessels=new(StringComparer.Ordinal);
        readonly Dictionary<string,RoomContainer> original=new(),contents=new();
        readonly Collider[] immersionHits=new Collider[64];readonly RaycastHit[] hits=new RaycastHit[64];readonly Vector3[] path=new Vector3[Segments+1];
        RoomEditor editor;RoomPhysicsWorld world;IDisposable write;bool publishing,blocked;float quiet,elapsed;string session=Guid.NewGuid().ToString("N"),error="";
        int weatherHolds;
        internal IDisposable HoldWeatherBoundary(){weatherHolds++;return new WeatherBoundary(this);}
        sealed class WeatherBoundary:IDisposable {
            LiquidPouring owner;
            internal WeatherBoundary(LiquidPouring owner)=>this.owner=owner;
            public void Dispose(){if(owner)owner.weatherHolds--;owner=null;}
        }
        internal bool Active=>write!=null;
        internal bool Owns(string target)=>Active&&contents.ContainsKey(target);
        internal string Error=>error;
        long automaticFlowStep=-1;
        internal void Initialize(RoomEditor owner){FlowWorkClock.Ensure();editor=owner;world=owner.PhysicsWorld;if(world)world.Changed+=PhysicsChanged;editor.RuntimeGate.Changed+=ContactGateChanged;}
        internal void Synchronize(RoomDocument document){
            flowEpoch++;ResetContacts();
            var ids=new HashSet<string>();
            foreach(var data in document.objects){
                if(data.containers?.Length!=1)continue;ids.Add(data.id);
                if(!vessels.TryGetValue(data.id,out var v))vessels[data.id]=v=new Vessel{Id=data.id};
                if(Active&&!publishing&&original.TryGetValue(data.id,out var before)&&JsonUtility.ToJson(before)!=JsonUtility.ToJson(data.containers[0]))Cancel("A container changed; the unfinished liquid flow was reverted");
                v.Saved=data.containers[0].Copy();if(!Active||!contents.ContainsKey(data.id))v.Live=v.Saved.Copy();v.Item=editor.Find(data.id);v.Rigid=v.Item?v.Item.GetComponent<RigidRoomItem>():null;v.Body=v.Item?v.Item.GetComponent<Rigidbody>():null;
                Preview(v);
            }
            SynchronizeMedia(document);
            foreach(var id in vessels.Keys.Where(x=>!ids.Contains(x)).ToArray()){
                if(Owns(id))Cancel("A container was removed; the unfinished liquid flow was reverted");Release(vessels[id]);vessels.Remove(id);
            }
        }
        void LateUpdate(){if(automaticFlowStep!=FlowWorkResources.Step){automaticFlowStep=FlowWorkResources.Step;TickPass(FlowWorkResources.StepSeconds,true);}TickAutomaticContacts();}
        internal void Tick(float seconds)=>TickPass(seconds,false);
        internal void TickPass(float seconds,bool automatic){
            if(!editor||publishing)return;
            editor.PollLiquidCheckpoints();
            if(!float.IsFinite(seconds)||seconds<=0)return;seconds=Mathf.Min(seconds,.1f);
            flowExecutionWork=default;discoveryWork=default;
            var physics=editor.PhysicsWorld;
            if(!FlowWorkEligible){ReleaseFlowInputs();FlowWorkResources.Release(this);InvalidateRain("Liquid flow is paused or unavailable");Finish(out _);return;}
            if(!PrepareWorkInputs(out var workIssue)){
                if(!FlowWorkResources.DiscoveryReady(this,out _))WaitForFlowWork(workIssue,discovery:true);
                return;
            }
            // A bounded storage queue throttles intake, not vessel body work.
            if(!Active&&!editor.LiquidCheckpointCapacity){HideStreams();InvalidateRain(editor.LiquidCheckpointWait);return;}
            PrepareGroupMotion();
            Vector3 gravity=Physics.gravity;var up=-gravity.normalized;bool flowing=false;
            foreach(var vessel in vessels.Values)vessel.DipBudget=vessel.Live.capacityMl*.75*seconds;
            if(!FlowWorkResources.TryExecute(this,out workIssue)){WaitForFlowWork(workIssue);return;}
            var groups=CaptureFlowExecution(automatic);if(groups.Length==0)return;
            HideStreams();
            // Keep pour, scoop and rain phase order across groups. The live
            // journal still publishes one coherent episode for the whole host.
            foreach(var members in groups)foreach(var v in members){
                if(!v.Ready||v.Live.amountMl<=0)continue;
                var terrain=RoomTerrainReservoir.Native(v.Live,v.Item.transform);
                double excess=ContainerFlowGeometry.Excess(v.Live,v.Item.transform.rotation,up,terrain);
                double requested=Math.Min(excess,v.Live.capacityMl*.75*seconds*Math.Sqrt(excess/v.Live.capacityMl));if(requested<.000001||Immersed(v,members,up))continue;
                var origin=ContainerFlowGeometry.Lip(v.Live,v.Item.transform,up,out var outward,terrain);if(!physics.CanSimulate(origin,v.Item))continue;var velocity=outward*.15f;
                if(v.Rigid)velocity+=Vector3.ClampMagnitude(v.Rigid.FlowPointVelocity(origin),3);
                if(!Trace(v,members,origin,velocity,gravity,up,out var receiver,out int count))continue;
                if(receiver!=null&&ChangesEpisodeIdentity(v,receiver)){Finish(out _);return;}
                if(!Begin(out var issue)){error=issue;blocked=true;editor.ReportStatus(issue);return;}
                Touch(v);double moved=0;
                if(receiver!=null){Touch(receiver);RoomContainer.Transfer(v.Live,receiver.Live,requested,out moved,out _);if(moved>0){v.Received+=moved;v.Receivers.Add(receiver.Id);Preview(receiver);}}
                double spill=Math.Min(v.Live.amountMl,requested-moved);
                if(spill>0){v.Live.amountMl-=spill;v.Spilled+=spill;}
                if(moved+spill<=0)continue;v.DipBudget=Math.Max(0,v.DipBudget-moved-spill);
                contents[v.Id]=v.Live;if(receiver!=null)contents[receiver.Id]=receiver.Live;flowing=true;Preview(v);ShowStream(v,count,requested/seconds);
            }
            foreach(var members in groups)foreach(var recipient in members){
                if(!recipient.Ready||recipient.Live.amountMl>=recipient.Live.capacityMl)continue;
                flowExecutionWork.ScoopRecipients++;
                Vessel donor=null;float smallest=float.PositiveInfinity;
                foreach(var candidate in members){
                    flowExecutionWork.ScoopCandidates++;
                    if(candidate==recipient||!candidate.Ready||candidate.DipBudget<=.000001)continue;
                    if(!ContainerScoopingGeometry.TryContact(candidate.Live,candidate.Item.transform,recipient.Live,recipient.Item.transform,up,out var contact)||contact.DonorFootprint>=smallest||!ClearScoopPath(contact,candidate.Item,recipient.Item))continue;
                    donor=candidate;smallest=contact.DonorFootprint;
                }
                if(donor==null)continue;
                double requested=Math.Min(donor.DipBudget,Math.Min(donor.Live.amountMl,Math.Min(recipient.Live.capacityMl-recipient.Live.amountMl,recipient.Live.capacityMl*.75*seconds)));
                if(requested<=.000001)continue;
                if(ChangesEpisodeIdentity(donor,recipient)){Finish(out _);return;}
                if(!Begin(out var issue)){error=issue;blocked=true;editor.ReportStatus(issue);return;}
                Touch(donor);Touch(recipient);
                if(!RoomContainer.Transfer(donor.Live,recipient.Live,requested,out var moved,out _))continue;
                donor.DipBudget-=moved;donor.Drawn+=moved;donor.Dippers.Add(recipient.Id);recipient.Scooped+=moved;recipient.Donors.Add(donor.Id);
                contents[donor.Id]=donor.Live;contents[recipient.Id]=recipient.Live;flowing=true;Preview(donor);Preview(recipient);
            }
            flowing|=CollectRain(groups,seconds,out bool published);if(published)return;
            if(!Active)return;elapsed+=seconds;quiet=flowing?0:quiet+seconds;
            if(quiet>=.3f||elapsed>=10)Finish(out _);
        }
        // An empty vessel may adopt new contents, but prior episode counters and
        // events must retain the old identity. Publish first and retry geometry
        // next tick. Unaccepted flow can revert; accepted saves retain live contents on failure.
        bool ChangesEpisodeIdentity(Vessel source,Vessel receiver)=>Owns(receiver.Id)&&receiver.Live.amountMl==0&&!source.Live.SameLiquid(receiver.Live);
        bool Immersed(Vessel vessel,Vessel[] members,Vector3 up){
            // An immersed full bucket must not endlessly pour below the reservoir's
            // surface and refill. Reuse the same bounded, unobstructed cavity proof;
            // lifting restores normal gravity-driven pouring on the next tick.
            flowExecutionWork.ImmersionChecks++;
            foreach(var reservoir in members){
                flowExecutionWork.ImmersionCandidates++;
                if(reservoir==vessel||!reservoir.Ready)continue;
                if(ContainerScoopingGeometry.TryImmersion(reservoir.Live,reservoir.Item.transform,vessel.Live,vessel.Item.transform,up,out var contact)&&ClearScoopPath(contact,reservoir.Item,vessel.Item))return true;
            }
            return false;
        }
        bool ClearScoopPath(ContainerScoopingGeometry.Contact contact,RoomItem source,RoomItem recipient){
            int mask=world.CollisionMask(~0,source);
            for(int sample=0;sample<5;sample++){
                if(!contact.Path(sample,out var from,out var to)||!world.CanSimulate(from,source)||!world.CanSimulate(to,recipient))continue;
                // Raycasts alone miss a ray starting inside a solid. Check both
                // ends too; saturation and any obstruction reject this path.
                if(Physics.OverlapSphereNonAlloc(from,.0005f,immersionHits,mask,QueryTriggerInteraction.Ignore)>0||Physics.OverlapSphereNonAlloc(to,.0005f,immersionHits,mask,QueryTriggerInteraction.Ignore)>0)continue;
                var delta=to-from;float distance=delta.magnitude;
                if(distance>1e-6f&&Physics.RaycastNonAlloc(from,delta/distance,hits,distance,mask,QueryTriggerInteraction.Ignore)==0)return true;
            }
            return false;
        }
        bool Available(Vessel v)=>v.Item&&v.Item.isActiveAndEnabled&&v.Item.GetComponent<ContainerFillView>()?.NativeReady==true&&v.Rigid&&v.Rigid.GeometryReady&&(!v.Live.TerrainFloor||RoomTerrainReservoir.Native(v.Live,v.Item.transform)!=null)&&editor.PhysicsWorld.CanSimulate(v.Item.transform.position,v.Item);
        bool Begin(out string issue){issue=null;if(Active)return true;write=editor.WriteGate.TryWrite(out issue);if(write==null)return false;flowEpoch++;session=Guid.NewGuid().ToString("N");quiet=elapsed=0;error="";return true;}
        void Touch(Vessel v){if(contents.ContainsKey(v.Id))return;original[v.Id]=v.Saved.Copy();contents[v.Id]=v.Live;}
        bool Trace(Vessel source,Vessel[] members,Vector3 origin,Vector3 velocity,Vector3 gravity,Vector3 up,out Vessel receiver,out int count){
            receiver=null;count=1;path[0]=origin;const float step=.025f;
            for(int i=1;i<=Segments;i++){
                float t=i*step;var next=origin+velocity*t+gravity*(.5f*t*t);var previous=path[count-1];var delta=next-previous;float length=delta.magnitude;
                if(length<1e-6f)continue;
                flowExecutionWork.PourSegments++;
                float closest=1;Vessel entering=null;
                foreach(var candidate in members){
                    flowExecutionWork.PourCandidates++;
                    if(candidate==source||!candidate.Ready)continue;
                    if(candidate.Opening.Enters(previous,next,up,out float fraction)&&fraction<closest){closest=fraction;entering=candidate;}
                }
                int found=Physics.RaycastNonAlloc(previous,delta/length,hits,length,world.CollisionMask(~0,source.Item),QueryTriggerInteraction.Ignore);
                if(found==hits.Length)return false; // Saturated collision query cannot prove a clear path.
                foreach(var hit in hits.AsSpan(0,found)){
                    if(!hit.collider||i==1&&(hit.collider.attachedRigidbody==source.Body||hit.collider.transform.IsChildOf(source.Item.transform)))continue;
                    float fraction=hit.distance/length;if(fraction<=closest){closest=fraction;entering=null;}
                }
                path[count++]=Vector3.LerpUnclamped(previous,next,closest);
                if(closest<1){receiver=entering!=null&&editor.PhysicsWorld.CanSimulate(path[count-1],entering.Item)&&editor.PhysicsWorld.CanSimulate(path[count-1],source.Item)?entering:null;return true;}
                if(!editor.PhysicsWorld.CanSimulate(next,source.Item))return true;
            }
            return true; // The bounded stream ends as uncollected spill; no persistent pool is implied.
        }
        void Preview(Vessel v){if(v.Item)v.Item.GetComponent<ContainerFillView>()?.ApplyLive(new[]{v.Live});}
        void ShowStream(Vessel v,int count,double rate){if(v.Item)v.Item.GetComponent<ContainerFillView>()?.ShowStream(path,count,rate,v.Live.color);}
        void Cancel(string reason){error=reason;blocked=true;ClearEpisode();if(editor)editor.ReportStatus(reason);}
        void ClearEpisode(){
            write?.Dispose();write=null;original.Clear();contents.Clear();quiet=elapsed=0;
            foreach(var v in vessels.Values){v.Received=v.Spilled=v.Scooped=v.Drawn=v.Rain=0;v.Receivers.Clear();v.Donors.Clear();v.Dippers.Clear();var saved=editor?editor.Read(v.Id)?.containers?.FirstOrDefault():null;if(saved!=null)v.Saved=saved.Copy();v.Live=v.Saved.Copy();Preview(v);}HideStreams();
        }
        void HideStreams(){foreach(var v in vessels.Values)if(v.Item)v.Item.GetComponent<ContainerFillView>()?.HideStream();}
        internal JObject Observe(string id){if(!vessels.TryGetValue(id,out var v))return null;return new JObject{["sessionId"]=session,["phase"]=FlowPhase(id),["contents"]=new JObject{["amountMl"]=v.Live.amountMl,["savedAmountMl"]=v.Saved.amountMl,["capacityMl"]=v.Live.capacityMl,["transferredMl"]=v.Received,["spilledMl"]=v.Spilled},["temporary"]=editor.TemporaryRoom,["error"]=Maestro.Quest.Imports.ImportObservation.Text(blocked?error:(!Active?editor.LiquidCheckpointWait:null)??FlowInputIssue(id)??FlowWorkIssue(id)??error)};}

        internal JObject ObserveScooping(string id){if(!vessels.TryGetValue(id,out var v))return null;return new JObject{["sessionId"]=session,["phase"]=FlowPhase(id),["scoopedMl"]=v.Scooped,["drawnMl"]=v.Drawn,["donors"]=v.Donors.Count,["recipients"]=v.Dippers.Count};}

        void PhysicsChanged(){
            flowEpoch++;ResetContacts();if(!editor||!world){ReleaseFlowInputs();return;}
            if(!world.Running){ReleaseFlowInputs();Finish(out _);return;}
            // A native readiness change is neither a new simulation request nor
            // permission to recover from a failed save. Keep its load and stop.
            if(flowInputIntent!=world.SimulationIntent){ReleaseFlowInputs();blocked=false;}
        }
        void OnApplicationPause(bool paused){contactsPaused=paused;if(paused){ReleaseFlowInputs();ResetContacts();Finish(out _);}}
        void OnApplicationFocus(bool focused){contactsFocused=focused;if(!focused){ReleaseFlowInputs();ResetContacts();Finish(out _);}}
        void OnDisable(){ReleaseFlowInputs();FlowWorkResources.Release(this);ContactWorkResources.Release(this);ResetContacts();Finish(out _);}
        static void Release(Vessel v){if(v.Item)v.Item.GetComponent<ContainerFillView>()?.HideStream();}
        void OnDestroy(){ReleaseFlowInputs();FlowWorkResources.Release(this);ContactWorkResources.Release(this);if(editor)editor.RuntimeGate.Changed-=ContactGateChanged;ResetContacts();Finish(out _);if(world)world.Changed-=PhysicsChanged;foreach(var v in vessels.Values)Release(v);vessels.Clear();write?.Dispose();write=null;}
    }
}
