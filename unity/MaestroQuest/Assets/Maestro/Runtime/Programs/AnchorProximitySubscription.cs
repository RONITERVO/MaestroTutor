// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    public readonly struct AnchorProximitySample {
        public readonly Vector3 Position,Centre;public readonly bool Available;public readonly uint TargetRevision,HolderRevision;
        public AnchorProximitySample(Vector3 position,Vector3 centre,bool available,uint targetRevision=0,uint holderRevision=0){Position=position;Centre=centre;Available=available;TargetRevision=targetRevision;HolderRevision=holderRevision;}
    }
    public interface IProgramAnchorProbe:IDisposable {bool Read(out AnchorProximitySample sample,out string error);}
    public interface IProgramAnchorWorld {IProgramAnchorProbe OpenAnchorProbe(string target,JObject holder,Vector3 offset,bool requirePhysics);}
    // One private, bounded observation per active wait. No collision objects, IO or ownership.
    public sealed class AnchorProximitySubscription:IProgramEventWatch
    {
        public const float SampleInterval=.05f,MaximumSampleGap=.2f;
        readonly IProgramAnchorProbe probe;readonly string target,holder,part,transition,initial;readonly float radius,hysteresis;
        bool disposed,hasSample;int state=-1;uint targetRevision,holderRevision;float nextSample,lastSample;
        static JObject Input(){var schema=Object(new JObject {
            ["target"]=AnimationTargets.TargetSchema(),["holder"]=HoldObjectCapability.AnchorSchema(),["offset"]=Vector(),
            ["radius"]=Number(.02,10),["hysteresis"]=Number(.005,2),["transition"]=Choice("enter","exit","either"),["initial"]=Choice("baseline","report"),
            ["physics"]=new JObject {["type"]="boolean",["title"]="Require freely simulating physics",["description"]="Only a free solid/bouncy target qualifies. Paused, held or animation-owned time resets the baseline."}});
            schema["x-features"]=new JArray("anchorZones.v1");return schema;}
        public static BehaviourCatalog.EventDefinition Definition()=>new("object.anchor.proximity.changed","Object entered or left an anchor zone",
            "Observe one explicit object's transform origin around a moving recipe part, Maestro hand or object-root anchor. offset uses holder-root-scale metres, matching object.hold; radius/hysteresis use world metres. Initial baseline suppresses the current side; report also reports it on the first sample. Enter at distance <= radius; exit at >= radius+hysteresis. At most 20 samples/second per wait; gaps over 0.2 seconds, rigid-body motion discontinuities and eligibility changes reset the baseline and reapply initial. physics=true requires a solid/bouncy target; held, carried, animation-owned or paused/unready physics emits nothing. Held anchors also suspend observation. Missing/replaced objects or sockets and invalid readings fail. The exact anchor revision/avatarHash is checked on admission; subsequent root/part motion is observed on those same instances. Value is the target ID; fields include holderId, part, inside, distance and the sampled zone centre x/y/z. This is sampled origin distance, not swept contact, mesh intersection, line of sight, navigation or a catch guarantee. Fast crossings can be missed. It does not own objects or authorize mutation. Before pickup use object.hold reach with a suitable radius to recheck current distance and eligibility; an old event never permits teleporting a departed object. Stop/pause/reload discard watches without replay.",
            Object(new JObject {["holderId"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["inside"]=new JObject {["type"]="boolean"},["distance"]=Number(0,1000000),["x"]=Number(-1000000,1000000),["y"]=Number(-1000000,1000000),["z"]=Number(-1000000,1000000)}),
            input:Input(),example:new JObject {["target"]=new string('0',32),["holder"]=new JObject {["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=""},["offset"]=new JObject {["x"]=0,["y"]=0,["z"]=.25},["radius"]=.15,["hysteresis"]=.03,["transition"]="enter",["initial"]="baseline",["physics"]=true},
            watch:(world,args,now)=>new AnchorProximitySubscription(world,args,now));
        public AnchorProximitySubscription(IProgramEventWorld world,JObject args,float now){
            if(world is not IProgramAnchorWorld anchors)throw new ProgramFault("Anchor observations are unavailable");
            target=(string)args["target"];var anchor=(JObject)args["holder"];holder=(string)anchor["objectId"];part=(string)anchor["part"]??(string)anchor["hand"]??"";
            if(target==holder)throw new ProgramFault("Choose different target and anchor objects");
            radius=(float)args["radius"];hysteresis=(float)args["hysteresis"];transition=(string)args["transition"];initial=(string)args["initial"];
            probe=anchors.OpenAnchorProbe(target,anchor,args["offset"].ToObject<Vector3>(),(bool)args["physics"]);
            if(!Read(out var sample,out var distance,out var error)){probe?.Dispose();throw new ProgramFault(error);}
            if(sample.Available)Reset(sample,distance);lastSample=now;nextSample=now+SampleInterval;
        }
        bool Read(out AnchorProximitySample sample,out float distance,out string error){
            sample=default;distance=0;error="The anchor observation is unavailable";if(probe==null||!probe.Read(out sample,out error))return false;
            distance=Vector3.Distance(sample.Position,sample.Centre);
            if(!float.IsFinite(distance)||distance>1000000||!Valid(sample.Position)||!Valid(sample.Centre)){error="Anchor observation contains an invalid position";return false;}return true;
        }
        static bool Valid(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&Mathf.Abs(v.x)<=1000000&&Mathf.Abs(v.y)<=1000000&&Mathf.Abs(v.z)<=1000000;
        void Reset(AnchorProximitySample sample,float distance){hasSample=true;targetRevision=sample.TargetRevision;holderRevision=sample.HolderRevision;state=initial=="report"?-1:distance<=radius?1:0;}
        public bool Poll(float now,out ProgramValue value,out JObject fields,out string error){
            value=default;fields=null;error=null;if(disposed||now<nextSample)return false;nextSample=now+SampleInterval;
            if(!Read(out var sample,out float distance,out error))return false;
            bool gap=now-lastSample>MaximumSampleGap;lastSample=now;
            if(!sample.Available){hasSample=false;return false;}
            if(!hasSample||gap||sample.TargetRevision!=targetRevision||sample.HolderRevision!=holderRevision)Reset(sample,distance);
            int next=(state==1?distance<radius+hysteresis:distance<=radius)?1:0;
            if(next==state)return false;state=next;
            if(transition!="either"&&transition!=(state==1?"enter":"exit"))return false;
            value=new ProgramValue(target);fields=new JObject {["holderId"]=holder,["part"]=part,["inside"]=state==1,["distance"]=distance,["x"]=sample.Centre.x,["y"]=sample.Centre.y,["z"]=sample.Centre.z};return true;
        }
        public void Dispose(){if(disposed)return;disposed=true;probe?.Dispose();}
    }
}
