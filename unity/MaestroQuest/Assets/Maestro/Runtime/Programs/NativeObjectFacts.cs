// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    public static class NativeObjectFacts
    {
        public static BehaviourCatalog.FactDefinition Definition()=>new("object.definition",ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"kind\":\"text\",\"name\":\"text\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"rotation\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}},\"scale\":\"number\",\"content\":{\"record\":{\"points\":\"number\",\"parts\":\"number\",\"frames\":\"number\",\"modelHash\":\"text\",\"recipePlaying\":\"boolean\"}}}}")),"Saved object definition",
            "Canonical object metadata and exact authored revision, from the temporary fork when active. Position/rotation/scale are saved room-local values, not live physics, camera coordinates or the animated pose. Content counts describe stored drawings, recipe parts and recorded frames; modelHash is an exact asset reference, not proof of availability or completed loading. Name is bounded display text; an empty copy name retains the original full name. Reading grants no edit authority and takes no ownership. Use this revision for kind=copy; a missing source is unavailable.",
            Object(new JObject {["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32))}),new JObject {["target"]="book"},(context,args)=>{
                string id=(string)args["target"];if(!context.Editor||!context.Editor.Find(id))return null;var d=context.Editor.Read(id);if(d==null)return null;
                return ProgramValue.Literal(new JObject {["target"]=id,["revision"]=context.Editor.ObjectRevision(id),["kind"]=d.kind.ToString(),["name"]=Imports.ImportObservation.Text(d.name),
                    ["position"]=Interaction.ScannedRoom.Triple(d.position),["rotation"]=new JObject {["x"]=d.rotation.x,["y"]=d.rotation.y,["z"]=d.rotation.z,["w"]=d.rotation.w},["scale"]=d.scale,
                    ["content"]=new JObject {["points"]=d.points?.Length??0,["parts"]=d.recipe?.parts.Length??0,["frames"]=d.motion?.frames.Length??0,["modelHash"]=d.modelHash??"",["recipePlaying"]=d.recipe?.playing??false}});
            });
        public static BehaviourCatalog.FactDefinition Position()=>new("object.position",ProgramDataType.Read(JObject.Parse("{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}}")),"Object world position",
            "Current transform origin in Unity world metres as a record {x,y,z}. This is not a mesh centre, surface, floor height or navigation destination. Target may be the book, Maestro or an existing creation. Grabs, animations and physics all affect the reading; physics need not run. Missing/disabled targets, invalid positions and paused app runtime are unavailable. Reads take no ownership, grant no editing authority and do not subscribe. Save the result in a local when several calculations need the same snapshot. World coordinates may change after recentering/alignment; do not treat them as durable room anchors.",
            Object(new JObject {["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32))}),new JObject {["target"]="book"},
            (context,args)=>context.World!=null&&context.World.TryPosition((string)args["target"],out var position)?ReadPosition(position):null);
        static ProgramValue? ReadPosition(UnityEngine.Vector3 position) {
            if(!float.IsFinite(position.x)||!float.IsFinite(position.y)||!float.IsFinite(position.z)||System.Math.Abs(position.x)>1000000||System.Math.Abs(position.y)>1000000||System.Math.Abs(position.z)>1000000)return null;
            return ProgramValue.Literal(new JObject {["x"]=position.x,["y"]=position.y,["z"]=position.z});
        }
    }
}
