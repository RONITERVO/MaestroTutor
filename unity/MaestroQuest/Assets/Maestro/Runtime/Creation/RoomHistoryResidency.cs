// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation {
 public sealed partial class RoomJournal {
  internal sealed partial class PreparedEdit {
   string[] sourceDormant,destinationDormant;
   // Transient presentation hints belong to the existing bounded history entry.
   // A failed preparation must never mutate that shared entry.
   internal bool RestoresDormant(string id)=>(operation=="undo"?change.BeforeDormant:
    operation=="redo"?change.AfterDormant:Array.Empty<string>()).Contains(id);
   internal void CaptureDormancy(Func<string,bool> current,Func<string,bool> deferred) {
    CaptureDormancy(ChangedObjects.Where(id=>current(id)),ChangedObjects.Where(id=>current(id)||deferred(id)));
   }
   internal void CaptureDormancy(IEnumerable<string> source,IEnumerable<string> destination) {
    var changed=ChangedObjects;
    sourceDormant=(source??Array.Empty<string>()).Where(id=>changed.Contains(id)&&owner.items.ContainsKey(id)).Distinct().ToArray();
    destinationDormant=(destination??Array.Empty<string>()).Where(id=>changed.Contains(id)&&objectIndex.ContainsKey(id)).Distinct().ToArray();
   }
   void PublishDormancy() {
    if(sourceDormant==null)return;
    // Capture the state at each successful traversal. If a formerly cold view
    // was activated before Undo, Redo must not reinstate its old cold hint.
    if(operation=="undo"){change.AfterDormant=sourceDormant;change.BeforeDormant=destinationDormant;}
    else{change.BeforeDormant=sourceDormant;change.AfterDormant=destinationDormant;}
   }
  }
 }
}
