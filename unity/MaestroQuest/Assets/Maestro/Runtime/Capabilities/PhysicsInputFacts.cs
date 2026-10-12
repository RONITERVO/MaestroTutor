// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class PhysicsInputFacts {
        static ProgramDataType Type()=>OutputType(Object(new JObject{
            ["state"]=Choice("inactive","ready","pending","unavailable"),["ready"]=new JObject{["type"]="boolean"},["reason"]=Text(null,128),
            ["dependencyCount"]=Number(0,66,true),["dependencies"]=List(Text(null,64),0,4),["momentumHeld"]=new JObject{["type"]="boolean"}}));
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.physics.inputs",Type(),"Object physics inputs",
            "Read current native gravity/obstacle/water input ownership for a loaded solid/bouncy object. Fixed, held, carried, paused and unavailable owners report inactive. pending means bounded native preparation; After fixing unavailable inputs, pause and then start physics to retry. ready also rechecks current local geometry; it is not a clear path, completed throw or regional-streaming guarantee. dependencyCount counts the current required input identities; dependencies lists only the first four stable IDs. momentumHeld is transient velocity retained only during uninterrupted preparation: Pause, grab, animation, teleport, source edits and workspace retirement discard it. Reads never acquire native objects, start physics, save or change velocity. The current whole-world collision gate still applies.",
            Object(new JObject{["target"]=Text("^[a-zA-Z0-9_-]{1,64}$",64)}),new JObject{["target"]="example-object"},
            (context,args)=>context.Editor?.Find((string)args["target"])?.GetComponent<RigidRoomItem>() is RigidRoomItem rigid?ProgramValue.Literal(rigid.ObservePhysicsInputs(),Type()):null);
    }
}
