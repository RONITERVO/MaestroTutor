// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    internal static class WorldIdentityFacts
    {
        internal const string Feature="worldIdentity.v1";
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.identity",
            ProgramDataType.Read(JObject.Parse("{\"record\":{\"worldId\":\"text\",\"regionId\":\"text\"}}")),
            "World and region identity",
            "Read the durable authored world and home/runtime region identities. Entity IDs and authored positions in this workspace are interpreted inside this scope. These identities survive reload, geometry edits, Undo, temporary keep/discard, viewpoint movement and archive restoration. They are not workspace-session guards, physical room anchors, action receipts or revisions. Authored area membership is available through world.regions and object.region. The runtime still uses one shared bounded scope; this fact does not claim regional streaming or change the 25-metre coordinate limit. Restoring an archive retains its world identity; it does not implicitly fork a new world. If identity cannot be saved or verified, this fact is unavailable. Observation never creates a world, writes a file, activates a region or starts programs.",
            null,null,(context,args)=>{
                var identity=context.Editor?context.Editor.WorldIdentity:null;
                return identity==null?null:ProgramValue.Literal(new JObject {["worldId"]=identity.worldId,["regionId"]=identity.regionId});
            },features:new[]{Feature});
    }
}
