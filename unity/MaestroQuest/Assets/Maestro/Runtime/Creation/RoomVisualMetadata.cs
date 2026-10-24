// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Creation {
 public sealed partial class RoomEditor {
  // Saved visual metadata does not require geometry. Do not race an in-flight
  // native snapshot, and do not treat an accidentally lost view as intentional retirement.
  bool CanEditDormantVisual(string target,out string error) {
   error="Wait for this object's loading or unloading to finish before editing its visual settings";
   if(!NativeEntityDormant(target)||NativePhase(target) is NativeEntityPhase.Preparing or NativeEntityPhase.Retiring)return false;
   error=null;return true;
  }
 }
}
