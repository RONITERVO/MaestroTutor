// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        void ApplyConnection(RoomObjectData data,RoomItem item,RoomEditPreparation preparation){
            using var candidate=preparation?.TakeConnection(data);var view=item.GetComponent<RoomConnectionView>();
            if(candidate!=null){if(!view)view=item.gameObject.AddComponent<RoomConnectionView>();view.ApplyPrepared(this,data,candidate);}
            else view?.Refresh(); // Unrelated reconciliation never recreates a missing peer connection.
        }
        internal void NotifyConnectionBroken(RoomItem item,RoomConnection definition){
            if(applying||Ownership.Suspended||RuntimeGate.Held||!PhysicsWorld||!PhysicsWorld.Running)return;var id=Identity(item);if(id!=null)ConnectionBroken?.Invoke(id,definition.connected,definition.kind,definition.breakForce,definition.breakTorque);
        }
        public event Action<string,string,string,float,float> ConnectionBroken;
        internal static bool ConnectionPlaces(string operation)=>operation is "attach" or "align" or "slide";
        internal bool CanEditConnectionSources(string target,int revision,string connected,int connectedRevision,out string error,object physicsOwner=null)
        {
            if(!CanChangeObject(target,true,true,false,out error,physicsOwner))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; inspect its connection revision";return false;}
            if(RuntimeGate.Held||Ownership.Suspended){error="Resume room interaction before editing connections";return false;}
            var workshop=GetComponent<AnimationWorkshop>();
            if(workshop?.ControlsTarget(target)==true){error="Finish authoring this object first";return false;}
            if(connected!=null){
                if(!CanChangeObject(connected,true,true,false,out error,physicsOwner))return false;
                if(connectedRevision>0&&ObjectRevision(connected)!=connectedRevision){error="The connected object changed; inspect both members before retrying";return false;}
                if(workshop?.ControlsTarget(connected)==true){error="Finish authoring the connected object first";return false;}
            }
            error=null;return true;
        }
        internal bool PrepareConnection(string target,int revision,string operation,string connected,RoomConnection definition,float angle,out RoomObjectData data,out string error)
        {
            data=null;if(operation!="remove"&&connected==null){error="Choose the exact connected object";return false;}if(!CanEditConnectionSources(target,revision,operation=="remove"?null:connected,0,out error))return false;
            data=Pose(Read(target),Find(target).transform);
            if(operation=="remove")data.connections=Array.Empty<RoomConnection>();
            else {
                if(operation=="attach"){
                    if(PhysicsWorld&&PhysicsWorld.Running){error="Pause room physics before joining the current poses";return false;}
                    if(definition==null){error="Provide break limits for the fixed join";return false;}
                    var a=Find(target).transform;var b=Find(connected).transform;definition=definition.Copy();definition.kind="fixed";definition.connected=connected;definition.ownerFrame=new ConnectionFrame();definition.connectedFrame=new ConnectionFrame {position=b.InverseTransformPoint(a.position),rotation=(Quaternion.Inverse(b.rotation)*a.rotation).normalized};data.connections=new[]{definition.Copy()};
                }else if(operation=="rearm"){
                    var h=data.connections?.FirstOrDefault();error="Read a configured connection and its exact other member before rearming";
                    if(h==null||h.connected!=connected||!h.enabled)return false;
                    if(!h.Aligned(Find(target).transform,Find(connected).transform,out error))return false;
                }else if(operation=="configure"){if(definition==null){error="Provide a connection definition";return false;}data.connections=new[]{definition.Copy()};data.connections[0].connected=connected;}
                else if(operation=="slide"){
                    var h=data.connections?.FirstOrDefault();error="Read a configured slider and choose a distance within its saved limits";
                    if(h==null||h.kind!="slider"||h.connected!=connected||!float.IsFinite(angle)||angle<h.slide.minimum||angle>h.slide.maximum)return false;
                    h.Slide(data,Pose(Read(connected),Find(connected).transform),angle);
                }else if(operation=="align"){
                    var h=data.connections?.FirstOrDefault();error="Inspect an existing connection and its exact connected object before alignment";
                    if(h==null||h.kind=="slider"||h.connected!=connected||!float.IsFinite(angle)||angle<-170||angle>170||h.kind=="fixed"&&angle!=0||h.kind=="hinge"&&h.limits.enabled&&(angle<h.limits.minimum||angle>h.limits.maximum))return false;
                    // Compute in room coordinates, without touching the live transforms during admission or a failed save.
                    var other=Pose(Read(connected),Find(connected).transform);
                    h.Align(data,other,angle);
                } else {error="Unknown connection operation";return false;}
            }
            var candidate=Snapshot();var next=data;candidate.objects=candidate.objects.Select(x=>x.id==target?next:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditConnection(string target,int revision,string operation,string connected,RoomConnection definition,float angle,out string error)
        {
            if(!PrepareConnection(target,revision,operation,connected,definition,angle,out var data,out error))return false;
            if(operation=="rearm"){Find(target).GetComponent<RigidRoomItem>()?.StopVelocity();Find(connected).GetComponent<RigidRoomItem>()?.StopVelocity();Find(target).GetComponent<RoomConnectionView>()?.ResetBreak();error=null;return true;}
            bool placement=ConnectionPlaces(operation);
            var replacements=placement?new[]{data,Pose(Read(connected),Find(connected).transform)}:new[]{data};
            var before=placement?new RoomLayout {placements=replacements.Select(x=>Frame.Placement(x.id,Find(x.id).transform)).ToArray()}:null;
            if(!CommitPersisted(replacements,Array.Empty<string>(),"Connection "+operation,placement,out error,before,scope:placement?RoomEditScope.Full:RoomEditScope.Content))return false;
            Find(target)?.GetComponent<RoomConnectionView>()?.ResetBreak();return true;
        }
    }
}
