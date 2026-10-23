// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation {
 public sealed partial class RoomEditor {
  // Presentation state is not persisted. Retain the cold identities belonging to
  // the exact saved checkpoint so deleting a cold object in the fork cannot
  // turn Discard into a request to allocate it. A later live view takes priority.
  HashSet<string> savedDormantTargets=new(StringComparer.Ordinal),savingDormantTargets;
  HashSet<string> CaptureSavedDormancy(RoomDocument document)=>document.objects
   .Where(data=>!data.IsBuiltIn&&NativeEntityDormant(data.id)).Select(data=>data.id).ToHashSet(StringComparer.Ordinal);
  HashSet<string> RestorationDormancy(RoomDocument document)=>document.objects
   .Where(data=>!data.IsBuiltIn&&(NativeEntityDormant(data.id)||!HasSavedObject(data.id)&&savedDormantTargets.Contains(data.id)))
   .Select(data=>data.id).ToHashSet(StringComparer.Ordinal);
  void RestoreNativeResidency(RoomDocument document,HashSet<string> dormant){
   // The journal has already switched after preparation and memory acceptance.
   // Release old derived state; no temporary pose, bounds or broken-joint cache
   // is allowed to become the saved destination's cold representation.
   SynchronizeNativeResidency(document.objects.Select(data=>data.id).ToHashSet(StringComparer.Ordinal));
   InvalidateNativeDisplay();startupAutoplayTargets.Clear();suppressedDisplayTargets.Clear();
   // Drain an old asynchronous display attempt without allowing its failure or
   // deadline to suppress the restored identities. Its current guard is revoked.
   displayAttempt=Array.Empty<string>();displayDeadline=double.PositiveInfinity;
   InitializeDormantNative(document.objects.Where(data=>dormant.Contains(data.id)));
   savedDormantTargets.Clear();
  }
 }
}
