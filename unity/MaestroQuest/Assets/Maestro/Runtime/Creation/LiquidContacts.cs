// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal readonly struct LiquidContact {
        internal readonly string BodyId,Participant,Kind,Phase;
        internal readonly Vector3 Point;internal readonly float Speed,Depth;
        internal LiquidContact(string body,string participant,string kind,string phase,Vector3 point,float speed,float depth){BodyId=body;Participant=participant;Kind=kind;Phase=phase;Point=point;Speed=speed;Depth=depth;}
    }
    public sealed partial class RoomEditor {
        internal event Action<LiquidContact> MediumContact;
        internal void EmitMediumContact(in LiquidContact value)=>MediumContact?.Invoke(value);
    }
    public sealed partial class LiquidPouring {
        sealed class ContactState {
            internal readonly Vector3[] Previous=new Vector3[MediumDisplacement.Samples];
            internal Vector3[] AvatarPose;
            internal RoomItem Item;internal string Kind="",BodyId="",Liquid="",Reason="Not sampled";
            internal bool Known;internal uint Revision;internal int Count;
            internal double At;internal float Depth,Speed,RippleAt=-10;internal Vector3 Point;internal Matrix4x4 Pose;internal uint InputUpdate;
        }
        sealed class ContactInput {internal Vector3 Point;internal string Kind="controller";internal bool Tracked;internal double At;internal uint Update;}
        readonly ContactInput[] contactInputs={new(),new()};
        readonly Dictionary<string,ContactState> contacts=new(StringComparer.Ordinal);
        readonly List<ContactDelivery> contactEvents=new();
        readonly Vector3[] contactPoints=new Vector3[MediumDisplacement.Samples];
        static readonly PoseJoint[] contactJoints={PoseJoint.LeftFoot,PoseJoint.RightFoot,PoseJoint.LeftHand,PoseJoint.RightHand,PoseJoint.Hips,PoseJoint.Head};
        uint contactEpoch;
        bool contactsPaused,contactsFocused=true;
        bool ContactReady=>!contactsPaused&&contactsFocused&&editor&&world&&world.Running&&!blocked&&!editor.RuntimeGate.Held&&!editor.Ownership.Suspended&&!editor.WriteGate.Frozen&&editor.Frame.Valid;
        internal void SetContactInput(int index,Vector3? point,bool hand){
            if(index<0||index>=contactInputs.Length)return;var input=contactInputs[index];double now=ContactNow;string kind=hand?"hand":"controller";
            bool continuous=input.Tracked&&FreshContact(now,input.At,TrackingLifetime)&&input.Kind==kind;
            input.Update++;input.Tracked=point.HasValue&&RoomRecipe.Finite(point.Value);input.Kind=kind;
            if(input.Tracked)input.Point=point.Value;input.At=now;
            if((!continuous||!input.Tracked)&&contacts.TryGetValue(InputId(index),out var state))Forget(state,input.Tracked?"Tracking continuity changed":"Tracking is unavailable");
        }
        static string InputId(int index)=>index==0?"input:left":"input:right";
        static void Forget(ContactState state,string reason){state.Known=false;state.BodyId=state.Liquid="";state.Depth=state.Speed=0;state.Reason=reason;}
        void ResetContacts(){contactEpoch++;contacts.Clear();foreach(var v in vessels.Values)if(v.Item)v.Item.GetComponent<ContainerFillView>()?.ClearRipples();}
        void ContactGateChanged(){if(editor.RuntimeGate.Held)ResetContacts();}
        internal void TickContacts(float seconds){
            if(!ContactWorkEligible||!float.IsFinite(seconds)||seconds<=0||!float.IsFinite(Physics.gravity.sqrMagnitude)||Physics.gravity.sqrMagnitude<.01f){ResetContacts();ContactWorkResources.Release(this);return;}
            if(!ContactWorkResources.Ready(this,out var changedWork))WaitForContactWork(changedWork);
            if(!ContactWorkResources.TryAdmit(this,out var workIssue)){WaitForContactWork(workIssue);return;}
            var epoch=++contactEpoch;int roomRevision=editor.Revision;var roomMatrix=editor.transform.localToWorldMatrix;
            Physics.SyncTransforms();CaptureMedia(false);contactEvents.Clear();
            using(var environment=mediumEnvironment.Begin(world)){
                foreach(var b in mediumBodies.Values){
                    if(!b.Item||!b.Item.isActiveAndEnabled||!b.Rigid||!b.Rigid.GeometryReady){if(contacts.TryGetValue(b.Id,out var unavailable))Forget(unavailable,"Object geometry is unavailable");continue;}
                    if(b.Revision!=b.Rigid.MotionRevision){b.Displacement.Capture(b.Item);b.Revision=b.Rigid.MotionRevision;}
                    for(int i=0;i<b.Displacement.Count;i++)contactPoints[i]=b.Item.transform.TransformPoint(b.Displacement.Points[i]);
                    SampleContact(b.Id,"object",b.Item,b.Rigid.MotionRevision,contactPoints,b.Displacement.Count,true,seconds,in environment);
                }
                var item=editor.Find("maestro");var avatar=item?item.GetComponent<MaestroAvatar>():null;
                if(AvatarContactPose(item,avatar,null,false,out uint revision,out int count)){
                    SampleContact("maestro","avatar",item,revision,contactPoints,count,true,seconds,in environment);
                }else if(contacts.TryGetValue("maestro",out var missing))Forget(missing,"Avatar pose is unavailable");
                for(int i=0;i<contactInputs.Length;i++){
                    var input=contactInputs[i];if(!input.Tracked||!FreshContact(ContactNow,input.At,TrackingLifetime)){if(contacts.TryGetValue(InputId(i),out var missingInput))Forget(missingInput,"Tracking is unavailable");continue;}
                    contactPoints[0]=input.Point;SampleContact(InputId(i),input.Kind,null,0,contactPoints,1,false,seconds,in environment);
                }
            }
            // Consumers queue programs. Copy this rare edge batch before invoking
            // callbacks, since an edit in a listener may replace the live room.
            if(contactEvents.Count>0)foreach(var delivery in contactEvents.ToArray()){
                // A listener may replace/edit/rebase the room. Remaining events
                // belong to the sampled source, even if IDs are reused afterward.
                if(!ContactWorkResources.Ready(this,out _)||contactEpoch!=epoch||editor.Revision!=roomRevision||!roomMatrix.Equals(editor.transform.localToWorldMatrix))break;
                if(!ContactDeliveryCurrent(in delivery))continue;
                var value=delivery.Value;Ripple(value.BodyId,value.Point,value.Speed,delivery.Participant);
                editor.EmitMediumContact(in value);
            }
        }
        void SampleContact(string id,string kind,RoomItem item,uint revision,Vector3[] points,int count,bool authored,float seconds,in RoomEnvironmentQueries.Batch environment){
            if(!contacts.TryGetValue(id,out var state))contacts[id]=state=new();
            if(state.Item!=item||state.Revision!=revision||state.Kind!=kind||state.Count!=count)Forget(state,"Contact baseline changed");
            double now=ContactNow;if(state.Known&&!FreshContact(now,state.At,ContactLifetime))Forget(state,"Contact sample expired");
            state.Item=item;state.Revision=revision;state.Kind=kind;state.Count=count;state.At=now;state.Pose=item?item.transform.localToWorldMatrix:default;
            int inputIndex=ContactInputIndex(id);state.InputUpdate=inputIndex>=0?contactInputs[inputIndex].Update:0;
            // Preserve the sampled world pose directly; roundtripping room coordinates
            // loses precision after a large translation or non-unit world scale.
            if(kind=="avatar"){state.AvatarPose??=new Vector3[contactJoints.Length];Array.Copy(points,state.AvatarPose,count);}
            if(count==0){Forget(state,"No supported contact samples");return;}
            bool previousKnown=state.Known,found=false,unknown=false,crossed=false;float depth=0,speed=0,volume=float.PositiveInfinity;Vector3 point=points[0],crossPoint=default;LiquidMediumGeometry chosen=default,crossMedium=default;
            for(int i=0;i<count;i++){
                var current=points[i];if(!RoomRecipe.Finite(current)||!environment.CanSimulate(current,item,item)){unknown=true;break;}
                var previous=authored?editor.Frame.PointToWorld(state.Previous[i]):state.Previous[i];
                var coverage=new Bounds(current,Vector3.zero);if(previousKnown)coverage.Encapsulate(previous);
                if(!CapturedMediumAvailability(coverage,id).Ready){unknown=true;break;}
                if(previousKnown)speed=Mathf.Max(speed,Mathf.Min(30,Vector3.Distance(previous,current)/seconds));
                if(SampleMedium(current,item,id,in environment,out var medium,out var d,out bool ready)){
                    if(!ready){unknown=true;break;}
                    if(!found||medium.Volume<volume||medium.Id==chosen.Id&&d>depth){found=true;chosen=medium;depth=d;volume=medium.Volume;point=current;}
                }
                if(previousKnown&&!crossed)for(int j=0;j<mediaCount;j++){
                    var candidate=media[j];if(candidate.Id==id||!candidate.CrossesSurface(previous,current,out var crossing))continue;
                    if(!candidate.Item.GetComponent<RigidRoomItem>().GeometryReady||!environment.CanSimulate(crossing,item,candidate.Item)){unknown=true;break;}
                    crossed=true;crossPoint=crossing;crossMedium=candidate;break;
                }
            }
            for(int i=0;i<count;i++)state.Previous[i]=authored?editor.Frame.PointToRoom(points[i]):points[i];
            if(unknown){Forget(state,"Contact geometry or participant environment is unavailable");return;}
            string before=state.BodyId,after=found?chosen.Id:"";
            if(previousKnown){
                if(before!=after){
                    if(before!="")PublishContact(before,id,kind,"exited",editor.Frame.PointToWorld(state.Point),speed,0,state);
                    if(after!="")PublishContact(after,id,kind,"entered",crossed&&crossMedium.Id==after?crossPoint:point+chosen.Up*depth,speed,depth,state);
                }else if(after==""&&crossed)PublishContact(crossMedium.Id,id,kind,"crossed",crossPoint,speed,0,state);
                else if(after!=""&&speed>.03f&&Time.unscaledTime-state.RippleAt>=.18f)Ripple(after,point+chosen.Up*depth,speed,state);
            }
            state.Known=true;state.BodyId=after;state.Liquid=found?chosen.Contents.liquid:"";state.Depth=depth;state.Speed=speed;state.Point=editor.Frame.PointToRoom(found?point+chosen.Up*depth:point);state.Reason="";
        }
        void PublishContact(string body,string id,string kind,string phase,Vector3 point,float speed,float depth,ContactState state){
            if(!vessels.TryGetValue(body,out var vessel)||!vessel.Item)return;
            contactEvents.Add(new(new LiquidContact(body,id,kind,phase,point,speed,depth),state,vessel));
        }
        void Ripple(string body,Vector3 point,float speed,ContactState state){if(vessels.TryGetValue(body,out var v)&&v.Item){v.Item.GetComponent<ContainerFillView>()?.AddRipple(point,speed);state.RippleAt=Time.unscaledTime;}}
        internal JObject ObserveContact(string id){
            bool input=id=="input:left"||id=="input:right";if(!input&&(!editor||!editor.Find(id)))return null;
            contacts.TryGetValue(id,out var state);bool workReady=ContactWorkResources.Ready(this,out var workIssue);
            bool participantReady=ContactParticipantCurrent(id,state,false,out var participantIssue);bool known=workReady&&participantReady;
            if(known&&state.BodyId!="")known=vessels.TryGetValue(state.BodyId,out var v)&&v.Item&&v.Item.isActiveAndEnabled&&v.Rigid&&v.Rigid.GeometryReady&&world.CanSimulate(editor.Frame.PointToWorld(state.Point),state.Item)&&world.CanSimulate(editor.Frame.PointToWorld(state.Point),v.Item);
            if(known){
                CaptureMissingMedia();
                for(int i=0;i<state.Count&&known;i++){
                    var sample=input?state.Previous[i]:editor.Frame.PointToWorld(state.Previous[i]);
                    known=CapturedMediumAvailability(new Bounds(sample,Vector3.zero),id).Ready;
                }
            }
            var point=known?state.Point:Vector3.zero;
            return new JObject{["known"]=known,["immersed"]=known&&state.BodyId!="",["bodyId"]=known?state.BodyId:"",["kind"]=state?.Kind??(input?"input":"object"),["liquid"]=known?state.Liquid:"",["depthMetres"]=known?state.Depth:0,["sampleSpeedMetresPerSecond"]=known?state.Speed:0,["surface"]=WorkspaceViewpoint.Point(point),["reason"]=known?"":!workReady?workIssue:!participantReady?participantIssue:"Contact geometry or participant environment is unavailable"};
        }
    }
}
