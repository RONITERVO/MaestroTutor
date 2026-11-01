// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        // A membership generation identifies the read, including edits undone
        // back to the same set. Object revisions remain separate edit guards.
        internal string CheckEnvironmentSnapshot(string id,int revision,string membership,IReadOnlyDictionary<string,int> members) {
            if(members==null)return "Read every environment member and its saved revision";
            if(EnvironmentRevision(id)!=revision)return "The environment profile changed; read its current summary";
            if(revision==0)return ReadEnvironment(id)==null&&membership==""&&members.Count==0?null:"A new environment profile needs empty membership and no members";
            var page=ObserveResourceMembers("environment",id,"");
            if(page==null||membership!=(string)page["membership"])return "The environment membership changed; read its summary and member pages again";
            if(!EnvironmentMembers(id).ToHashSet().SetEquals(members.Keys))return "Include every current environment member exactly once";
            if(members.Any(pair=>pair.Value<1||ObjectRevision(pair.Key)!=pair.Value))return "An environment member changed; read its saved revision again";
            return null;
        }
        internal JObject ObserveEnvironmentSummary(string id) {
            var definition=ReadEnvironment(id);if(definition==null)return null;
            var page=ObserveResourceMembers("environment",id,"");
            return new JObject{["id"]=id,["revision"]=EnvironmentRevision(id),["name"]=definition.name,
                ["realCollisions"]=definition.realCollisions,["membership"]=page["membership"].DeepClone(),
                ["memberCount"]=page["total"].DeepClone(),["temporary"]=TemporaryRoom};
        }
    }
}
