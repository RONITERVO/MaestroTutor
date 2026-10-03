// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class SnapConstructionCapability:CapabilityModule {
        public override string Id=>"object.layout.snap";
        public override string Label=>"Snap a construction to a point";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","source.revision.current","room.physics.paused","authoring.inactive","storage.writable"};
        public override string Description=>"Align the chosen point of the FIRST moving member with an exact destination point of the same family. Both local frames coincide, with turn in degrees about destination +Y. Move 1–15 distinct idle creations as one rigid arrangement, without resizing; include every connected member. The destination must be outside that list. Use current revisions for all members and destination. Pause physics and stop member/destination playback, grips and authoring. place only changes poses; join also adds an ordinary fixed connection from the first member to the destination, using explicit break limits (zero means unbreakable). The first member must then use solid/bouncy physics and have no outgoing connection; never silently replace an existing link. One save/Undo includes the live destination pose and the whole moving arrangement. Failed admission/save changes nothing. Temporary edits stay in the fork; Stop does not undo completed edits. Snap points do not enforce occupancy, collision-free placement, nearest-point selection or chess rules. Subsequent point edits do not alter the accepted connection frames. Book controls, user programs and the agent call this same operation.";
        static JObject Member(bool destination=false){var fields=new JObject{["target"]=DrawingData.Target(),["revision"]=Revision()};if(destination)fields["point"]=SnapPointCapability.PointId();return CurrentInputs(Object(fields),"object.placement","revision",new JObject{["target"]="target"});}
        static JObject Variant(string mode){var fields=new JObject{["mode"]=Choice(mode),["members"]=List(Member(),1,15),["point"]=SnapPointCapability.PointId(),["destination"]=Member(true),["turn"]=Number(-180,180)};fields["mode"]["x-static"]=true;if(mode=="join"){fields["breakForce"]=Number(0,10000);fields["breakTorque"]=Number(0,10000);}var schema=Object(fields);schema["title"]=mode=="place"?"Place at point":"Place and join";schema["format"]="snapPlacement";schema["x-features"]=mode=="join"?new JArray(SnapPointCapability.Feature,ConnectionCapability.Feature):new JArray(SnapPointCapability.Feature);return schema;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("mode"),["oneOf"]=new JArray(Variant("place"),Variant("join"))};
        public override JObject OutputSchema=>Object(new JObject{["count"]=Number(1,15,true),["joined"]=new JObject{["type"]="boolean"},["temporary"]=new JObject{["type"]="boolean"}});
        public override JObject Example=>new(){["mode"]="place",["members"]=new JArray(new JObject{["target"]=new string('0',32),["revision"]=1}),["point"]="Bottom",["destination"]=new JObject{["target"]=new string('1',32),["revision"]=1,["point"]="Top"},["turn"]=0};
        static RoomSnapPlacement Read(JObject a)=>JsonUtility.FromJson<RoomSnapPlacement>(a.ToString());
        public override BehaviourCatalog.Claim[] Claims(JObject a){var r=Read(a);return r.members.Select(m=>m.target).Append(r.destination.target).Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareSnap(Read(a),out _,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;var request=Read(a);if(!c.Editor.SnapConstruction(request,out error))return false;operation=new CompletedCapability(new JObject{["count"]=request.members.Length,["joined"]=request.mode=="join",["temporary"]=c.Editor.TemporaryRoom});return true;}
    }
}
