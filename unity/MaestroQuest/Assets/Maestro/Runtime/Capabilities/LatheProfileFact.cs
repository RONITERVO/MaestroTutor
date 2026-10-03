// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class LatheProfileFact
    {
        public static BehaviourCatalog.FactDefinition Definition()=>new("object.recipe.profile",ProgramDataType.Read(JObject.Parse("{\"record\":{\"revision\":\"number\",\"part\":\"text\",\"segments\":\"number\",\"offset\":\"number\",\"count\":\"number\",\"vertices\":\"number\",\"points\":{\"list\":{\"record\":{\"x\":\"number\",\"y\":\"number\"}}}}}")),
            "Lathe profile points","Read up to four exact profile points for an existing lathe part at a fixed object revision. x is radius and y is height in normalized part coordinates. segments is the angular subdivision count. vertices is a conservative generated vertex cost for the whole recipe; the room limit is 262144. Offset equal to count returns an empty page. Missing/non-lathe parts, stale revisions and later offsets are unavailable. The profile is geometry, not a fluid container or a hollow collider.",
            Object(new JObject {["target"]=RecipeEditCapability.Target(),["revision"]=Revision(),["part"]=Text("^[a-zA-Z0-9_]{1,32}$",32),["offset"]=Number(0,16,true)}),
            new JObject {["target"]=new string('0',32),["revision"]=1,["part"]="Body",["offset"]=0},
            (context,args)=>{
                string target=(string)args["target"];if(!context.Editor||context.Editor.ObjectRevision(target)!=(int)args["revision"])return null;
                var recipe=context.Editor.Read(target)?.recipe;var part=recipe?.parts.FirstOrDefault(p=>p.id==(string)args["part"]);int offset=(int)args["offset"];
                if(part?.shape!="lathe"||offset>part.profile.Length)return null;
                return ProgramValue.Literal(new JObject {["revision"]=(int)args["revision"],["part"]=part.id,["segments"]=part.segments,["offset"]=offset,["count"]=part.profile.Length,["vertices"]=RecipeLathe.VertexCost(recipe),["points"]=new JArray(part.profile.Skip(offset).Take(4).Select(p=>new JObject {["x"]=p.x,["y"]=p.y}))});
            },features:new[]{RecipeLathe.Feature});
    }
}
