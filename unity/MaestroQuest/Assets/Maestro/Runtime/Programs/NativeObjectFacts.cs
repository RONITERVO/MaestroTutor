// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    public static class NativeObjectFacts
    {
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
