// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
 public sealed partial class RoomEditor {
  internal JObject ObserveCreationBudget(){
   int saved=System.Math.Max(0,(journal?.ObjectCount??0)-2);
   return new JObject{["saved"]=saved,["savedLimit"]=RoomDocument.MaximumObjects,
    ["remaining"]=System.Math.Max(0,RoomDocument.MaximumObjects-saved)};
  }
 }
}
