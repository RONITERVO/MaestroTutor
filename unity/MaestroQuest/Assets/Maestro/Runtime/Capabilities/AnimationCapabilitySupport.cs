// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class AnimationTargets
    {
        public static ImportedModel ClipModel(RoomItem item)=>!item?null:item.GetComponent<MaestroAvatar>()?.CustomModel??item.GetComponent<CreatedRoomObject>()?.Model;
        public static JObject TargetSchema()=>Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32));
        public static JObject AvatarSchema()=>Resource(Choice("maestro"));
        public static JObject MovementSchema(){var value=Choice("inPlace","authored");value["x-requires"]=new JObject {["target"]="maestro"};value["x-features"]=new JArray("authoredMotion.v1");value["description"]="inPlace keeps navigation-adjusted horizontal hips. authored transfers the complete sampled body's planar displacement through scanned-floor and swept body-proxy checks; it stops at obstacles, personal space or lost alignment. Only Maestro supports authored travel. No loop or intent is inferred.";return value;}
        public static bool MovementReady(CapabilityContext context,JObject args,out string error){error=null;if((string)args["movement"]!="authored")return true;var avatar=context.Editor.Find((string)args["target"])?.GetComponent<MaestroAvatar>();if(!avatar){error="Authored travel needs Maestro";return false;}return avatar.CanPlayAuthored(out error);}
        public static JObject PropSchema() {var value=Prop();value["x-requires"]=new JObject {["target"]="maestro"};return value;}
        public static PropAttachment Attachment(JObject arguments) {
            if(arguments["prop"] is not JObject prop)return null;
            return new PropAttachment((string)prop["objectId"],(string)prop["avatarHash"],
                Enum.Parse<PropHand>((string)prop["hand"],true),Enum.Parse<PropRelease>((string)prop["release"],true),
                JsonUtility.FromJson<Vector3>(prop["offset"].ToString()),JsonUtility.FromJson<Quaternion>(prop["rotation"].ToString()),(float)prop["releaseAt"]);
        }
        public static bool PropReady(CapabilityContext context,JObject arguments,out string error) {
            var attachment=Attachment(arguments);error=null;if(attachment==null)return true;
            if((string)arguments["target"]!="maestro"||!HeldRoomProp.CanAttach(context.Editor,attachment,out error)) {error??="Only Maestro can carry this fitted prop";return false;}
            if(context.Workshop&&context.Workshop.ControlsTarget(attachment.ObjectId)) {error="Stop authoring the prop before running this action";return false;}
            if(context.Editor.Find(attachment.ObjectId).GetComponent<RigidRoomItem>().AnimationOwned) {error="Another animation owns this prop";return false;}
            return true;
        }
        public static string GestureName(JObject arguments) {
            var value=(string)arguments["gesture"];return char.ToUpperInvariant(value[0])+value.Substring(1);
        }
    }
    /// <summary>Common exclusive target/prop ownership, not an animation dispatcher.</summary>
    internal abstract class FullBodyCapability : CapabilityModule
    {
        public override string Duration=>"timed";
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override BehaviourCatalog.Claim[] Claims(JObject arguments) {
            var claims=new List<BehaviourCatalog.Claim>(base.Claims(arguments));
            if(arguments["prop"] is JObject prop)claims.Add(new BehaviourCatalog.Claim((string)prop["objectId"],"wholeTarget"));
            return claims.ToArray();
        }
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error)=>
            context.Target(arguments,out var item,out error)&&Movable(item,out error)&&AnimationTargets.PropReady(context,arguments,out error);
        static bool Movable(RoomItem item,out string error){error=item.PoseLocked?"A scanned ink layer follows its room anchor and cannot play object animations":null;return error==null;}
    }
    /// <summary>Releases exactly the resources acquired by one full-body execution.</summary>
    internal abstract class FullBodyOperation : CapabilityOperation
    {
        protected readonly CapabilityContext Context;
        protected readonly string TargetId;
        protected RoomItem Target;
        protected MaestroAvatar Avatar;
        protected bool Stopped {get;private set;}
        protected float Duration;
        readonly PropAttachment attachment;
        RigidRoomItem targetRigid,propReservation;
        HeldRoomProp prop;
        bool acquired;
        protected FullBodyOperation(CapabilityContext context,JObject arguments) {
            Context=context;TargetId=(string)arguments["target"];Duration=(float?)arguments["seconds"]??0;
            attachment=AnimationTargets.Attachment(arguments);
        }
        public override float Seconds=>Duration;
        protected bool Acquire(out string error) {
            error=null;Target=Context.Editor.Find(TargetId);Avatar=Target.GetComponent<MaestroAvatar>();
            acquired=true;
            if(Avatar) {Avatar.GetComponent<AvatarSpatialMotion>()?.Stop();Avatar.SetEditing(true);}
            targetRigid=Target.GetComponent<RigidRoomItem>();if(targetRigid)targetRigid.SetAnimationOwner(this,true);
            if(attachment==null)return true;
            propReservation=Context.Editor.Find(attachment.ObjectId).GetComponent<RigidRoomItem>();
            if(propReservation.AnimationOwned) {propReservation=null;error="Another animation owns this prop";return false;}
            propReservation.SetAnimationOwner(this,true);return true;
        }
        protected bool BeginProp(out string error) {
            error=null;if(attachment==null)return true;
            if(propReservation) {propReservation.SetAnimationOwner(this,false);propReservation=null;}
            prop=HeldRoomProp.Begin(Context.Editor,attachment,Duration,out error);return prop;
        }
        public override RuleActionState State(out string error) {
            error=null;return prop&&!prop.Valid(out error)?RuleActionState.Failed:RuleActionState.Ready;
        }
        public override bool Complete(out string error) {
            error=null;if(prop) {prop.Finish();error=prop.Error;}return error==null;
        }
        protected virtual void ReleasePlayback() {}
        protected virtual bool RetainPlacement=>false;
        public sealed override void Stop(bool preservePlacement) {
            if(Stopped)return;Stopped=true;
            try {
                if(propReservation) {propReservation.SetAnimationOwner(this,false);propReservation=null;}
                if(prop) {prop.End(preservePlacement);prop=null;}
                ReleasePlayback();
            } finally {
                if(acquired) {
                    if(Avatar)Avatar.SetEditing(false);
                    if(!preservePlacement&&!RetainPlacement&&Context.Editor&&Context.Editor.Find(TargetId)==Target)Context.Editor.RestorePose(TargetId);
                    if(targetRigid)targetRigid.SetAnimationOwner(this,false);
                }
            }
        }
    }
}
