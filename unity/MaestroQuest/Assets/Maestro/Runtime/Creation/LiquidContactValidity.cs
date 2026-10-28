// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
 public sealed partial class LiquidPouring {
  const double ContactLifetime=.2,TrackingLifetime=.15;
  // Unlike frame-cached time, this also expires work during a slow callback.
  static double ContactNow=>Time.realtimeSinceStartupAsDouble;
  static bool FreshContact(double now,double at,double lifetime)=>now>=at&&now-at<=lifetime;
  static int ContactInputIndex(string id)=>id=="input:left"?0:id=="input:right"?1:id=="input:viewer"?2:-1;

  bool AvatarContactPose(RoomItem item,MaestroAvatar avatar,ContactState expected,bool exactPose,out uint revision,out int count){
   count=0;revision=0;
   if(!item||!item.isActiveAndEnabled||!avatar||!avatar.isActiveAndEnabled||!avatar.PoseRig||avatar.ModelBusy)return false;
   foreach(var joint in contactJoints){
    var bone=avatar.PoseRig.Bone(joint);if(!bone)continue;
    if(expected==null)contactPoints[count]=bone.position;
    else if(exactPose&&(count>=expected.Count||expected.AvatarPose==null||(bone.position-expected.AvatarPose[count]).sqrMagnitude>1e-10f))return false;
    count++;revision=unchecked(revision*31+(uint)bone.GetInstanceID());
   }
   revision=unchecked(revision+(item.GetComponent<RigidRoomItem>()?.MotionRevision??0));
   return count>0&&(expected==null||expected.Count==count&&expected.Revision==revision);
  }
  bool ContactParticipantCurrent(string id,ContactState state,bool exactPose,out string reason){
   reason=state?.Reason??"Not sampled";if(state?.Known!=true)return false;
   reason="A contact sampling opportunity was missed";if(SimulationWorkSchedule.Step-state.WorkStep>1)return false;
   reason="Contact sample expired";double now=ContactNow;if(!FreshContact(now,state.At,ContactLifetime))return false;
   int index=ContactInputIndex(id);
   if(index>=0){
    var input=contactInputs[index];reason="Tracking continuity changed";
    if(!input.Tracked||!FreshContact(now,input.At,TrackingLifetime)||input.Kind!=state.Kind)return false;
    if(exactPose&&input.Update!=state.InputUpdate){reason="A newer tracked pose replaced the sampled input";return false;}
    reason="";return true;
   }
   reason="Contact participant geometry or ownership changed";
   var item=editor?editor.Find(id):null;
   if(!item||!state.Item||item!=state.Item||!item.isActiveAndEnabled)return false;
   if(exactPose&&!state.Pose.Equals(item.transform.localToWorldMatrix)){reason="The participant moved after sampling";return false;}
   if(state.Kind=="avatar"){
    if(!AvatarContactPose(item,item.GetComponent<MaestroAvatar>(),state,exactPose,out _,out _)){reason="The sampled avatar pose is unavailable or changed";return false;}
   }else{
    var rigid=item.GetComponent<RigidRoomItem>();if(!rigid||!rigid.isActiveAndEnabled||!rigid.GeometryReady||rigid.MotionRevision!=state.Revision)return false;
   }
   reason="";return true;
  }

  readonly struct ContactDelivery {
   internal readonly LiquidContact Value;internal readonly ContactState Participant;
   internal readonly Vessel Source;internal readonly RoomItem Item;internal readonly RigidRoomItem Rigid;
   internal readonly uint Revision;internal readonly Matrix4x4 Pose;internal readonly RoomContainer Contents;internal readonly double Amount;
   internal ContactDelivery(LiquidContact value,ContactState participant,Vessel source){Value=value;Participant=participant;Source=source;Item=source.Item;Rigid=source.Rigid;Revision=Rigid?Rigid.MotionRevision:0;Pose=Item.transform.localToWorldMatrix;Contents=source.Live;Amount=source.Live.amountMl;}
  }
  bool ContactDeliveryCurrent(in ContactDelivery delivery){
   var value=delivery.Value;
   if(!ContactParticipantCurrent(value.Participant,delivery.Participant,true,out var reason)){
    Forget(delivery.Participant,reason);return false;
   }
   if(!vessels.TryGetValue(value.BodyId,out var source)||!ReferenceEquals(source,delivery.Source)||
      !delivery.Item||!delivery.Item.isActiveAndEnabled||source.Item!=delivery.Item||editor.Find(value.BodyId)!=delivery.Item||
      !delivery.Rigid||!delivery.Rigid.isActiveAndEnabled||!delivery.Rigid.GeometryReady||source.Rigid!=delivery.Rigid||delivery.Rigid.MotionRevision!=delivery.Revision||
      !delivery.Pose.Equals(delivery.Item.transform.localToWorldMatrix)||!ReferenceEquals(source.Live,delivery.Contents)||source.Live.amountMl!=delivery.Amount){
    Forget(delivery.Participant,"The sampled water geometry or contents changed");return false;
   }
   return true;
  }
 }
}
