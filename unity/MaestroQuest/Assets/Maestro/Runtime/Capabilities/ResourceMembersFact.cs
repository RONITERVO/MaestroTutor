// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class ResourceMembersFact
    {
        internal const string Feature="resourceMembers.v1";
        internal static BehaviourCatalog.FactDefinition Fact() {
            var kind=Choice("environment","appearance","visibility","region");
            var id=Text("^[a-f0-9]{32}$",32);
            var output=Object(new JObject{["kind"]=kind,["id"]=id,["revision"]=Revision(),
                ["membership"]=Text("^[a-f0-9]{32}$",32),["offset"]=Number(0,RoomDocument.MaximumObjects+2,true),
                ["total"]=Number(0,RoomDocument.MaximumObjects+2,true),["next"]=Text(null,36),
                ["members"]=List(Text("^(book|maestro|[a-fA-F0-9]{32})$",32),0,RoomResourceMembers.PageSize)});
            return new("resource.members",OutputType(output),"Saved resource members",
                "Read the exact saved top-level object IDs bound to an environment collision profile, appearance, visibility layer or authored region. Includes unloaded objects without loading native views. Start cursor empty; pass next unchanged with the same kind/id until it is empty. Pages share the membership generation and definition revision. Any membership or definition edit, Undo/Redo of such an edit, or workspace/temporary scope change makes continuation unavailable: discard collected pages and restart. Object motion, unrelated edits and native loading do not invalidate this directory. Appearance membership counts each object once even with multiple bindings. Missing definitions are unavailable; an existing empty group has total 0. This read neither reserves objects nor freezes their edit revisions. Before a shared edit inspect current object revisions, claim every affected member and submit the exact definition revision; the action still checks current membership, ownership, physical admission and its declared resource limits. Paging does not raise collision profile, saved-world, native-view or program budgets.",
                Object(new JObject{["kind"]=kind.DeepClone(),["id"]=id.DeepClone(),["cursor"]=Text(null,36)}),
                new JObject{["kind"]="environment",["id"]=new string('a',32),["cursor"]=""},
                (c,a)=>c.Editor&&c.Editor.ObserveResourceMembers((string)a["kind"],(string)a["id"],(string)a["cursor"]) is JObject page?ProgramValue.Literal(page,OutputType(output)):null,
                features:new[]{Feature});
        }
    }
}
