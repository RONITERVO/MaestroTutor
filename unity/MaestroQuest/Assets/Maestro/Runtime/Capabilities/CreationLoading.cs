// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs {
 internal static class CreationLoading {
  internal const string Feature="deferredCreation.v1";
  internal const string Description=" Optional loading chooses now (the default, with native preparation) or onDemand (save the exact definitions and IDs without allocating visible objects). On-demand completion confirms saved existence, not visibility, simulation or animation playback. Nearby observation or a later native action acquires the object through the normal bounded loader; a program must wait for that action's readiness. No quota, permission or resource check is bypassed. Undo/Redo preserves unloaded existence; stopping a completed creation never erases it.";
  internal static JObject Inputs(JObject schema) {
   var loading=CapabilitySchema.Choice("now","onDemand");loading["title"]="Load objects";
   loading["description"]="Now, or when nearby observation or a later action needs them";
   loading["x-enum-labels"]=new JObject{["now"]="Now",["onDemand"]="When needed"};
   loading["x-features"]=new JArray(Feature);
   ((JObject)schema["properties"])["loading"]=loading;return schema;
  }
  internal static bool Deferred(JObject args)=>(string)args["loading"]=="onDemand";
 }
}
