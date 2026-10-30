// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class ObjectSearchFact
    {
        internal const string Feature="objectSearch.v1";
        internal static BehaviourCatalog.FactDefinition Fact()
        {
            var entry=Object(new JObject{["id"]=Text(null,32),["name"]=Text(null,80),["kind"]=Text(null,16)});
            var output=Object(new JObject{["worldId"]=Text(null,32),["offset"]=Number(0,int.MaxValue,true),["total"]=Number(0,int.MaxValue,true),["next"]=Text(null,65),["entries"]=List(entry,0,RoomObjectSearch.PageSize)});
            var kind=Text(null,16);kind["enum"]=new JArray(new[]{"all"});foreach(var value in Enum.GetNames(typeof(RoomObjectKind)))((JArray)kind["enum"]).Add(value);
            return new("world.objects",OutputType(output),"Find saved objects",
                "Search every saved top-level object, including dormant creations, without loading, selecting or moving anything. Match all space-separated words against saved names, exact IDs or kinds, ignoring case. kind filters one exact kind or all. Results are ordered by stable ID, up to three per page. Start with empty cursor; pass next unchanged with the same query/kind until it is empty. A cursor becomes unavailable when searchable metadata or workspace/temporary scope changes; restart from empty cursor. Movement and native loading do not change this directory. An object absent from a page is not deleted. Read object.definition for a known ID and its current edit revision; object.presence distinguishes saved existence from native readiness. Internal recipe parts are not separate objects.",
                Object(new JObject{["query"]=Text(null,80),["kind"]=kind,["cursor"]=Text(null,65)}),new JObject{["query"]="",["kind"]="all",["cursor"]=""},
                (c,a)=>c.Editor&&c.Editor.SearchObjects((string)a["query"],(string)a["kind"],(string)a["cursor"]) is JObject page?ProgramValue.Literal(page,OutputType(output)):null,features:new[]{Feature});
        }
    }
}
