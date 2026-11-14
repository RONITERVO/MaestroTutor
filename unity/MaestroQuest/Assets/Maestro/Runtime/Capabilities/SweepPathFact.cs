// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class SweepPathFact {
        static readonly ProgramDataType ResultType=ProgramDataType.Read(JObject.Parse("{\"record\":{\"revision\":\"number\",\"part\":\"text\",\"offset\":\"number\",\"count\":\"number\",\"points\":{\"list\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}}}}}"));
        public static BehaviourCatalog.FactDefinition Definition()=>new("object.recipe.path",ResultType,
            "Recipe sweep path","Read up to four exact normalized XYZ path points at a fixed object revision. Read object.recipe.profile for the closed cross-section. Dimensions and placement come from object.recipe.part. Offset equal to count returns an empty page; stale, missing, non-sweep and later offsets are unavailable. This reads saved geometry and does not move an object.",
            Object(new JObject {["target"]=RecipeEditCapability.Target(),["revision"]=Revision(),["part"]=Text("^[a-zA-Z0-9_]{1,32}$",32),["offset"]=Number(0,16,true)}),
            new JObject {["target"]=new string('0',32),["revision"]=1,["part"]="Body",["offset"]=0},
            (context,args)=>{
                string target=(string)args["target"];if(!context.Editor||context.Editor.ObjectRevision(target)!=(int)args["revision"])return null;
                var part=context.Editor.Read(target)?.recipe?.parts.FirstOrDefault(p=>p.id==(string)args["part"]);int offset=(int)args["offset"];
                if(part?.shape!="sweep"||part.path==null||offset>part.path.Length)return null;
                return ProgramValue.Literal(new JObject {["revision"]=(int)args["revision"],["part"]=part.id,["offset"]=offset,["count"]=part.path.Length,["points"]=new JArray(part.path.Skip(offset).Take(4).Select(p=>new JObject {["x"]=p.x,["y"]=p.y,["z"]=p.z}))},ResultType);
            },features:new[]{RecipeSweep.Feature});
    }
}
