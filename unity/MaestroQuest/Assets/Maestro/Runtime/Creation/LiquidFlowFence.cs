// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
 public sealed partial class RoomEditor {
  // A cheap synchronous fence, not another spatial discovery or retained query.
  // Loading may publish callbacks which move an unrelated receiver/obstacle.
  internal Func<bool> CaptureFlowPlacementFence(){
   var source=journal;int revision=Revision,generation=nativeGeneration;
   var matrix=transform.localToWorldMatrix;
   var views=objects.Select(pair=>(id:pair.Key,item:pair.Value,
    matrix:pair.Value?pair.Value.transform.localToWorldMatrix:default,
    active:pair.Value&&pair.Value.isActiveAndEnabled)).ToArray();
   return ()=>this&&ReferenceEquals(source,journal)&&revision==Revision&&generation==nativeGeneration&&
    matrix.Equals(transform.localToWorldMatrix)&&views.Length==objects.Count&&views.All(view=>
     objects.TryGetValue(view.id,out var item)&&ReferenceEquals(item,view.item)&&
     (item&&item.isActiveAndEnabled)==view.active&&(!item||item.transform.localToWorldMatrix.Equals(view.matrix)));
  }
 }
 public sealed partial class LiquidPouring {
  Func<bool> flowPlacementFence;
  bool FlowPlacementCurrent()=>flowPlacementFence?.Invoke()==true;
  void CaptureFlowPlacementFence(){
   var placement=editor.CaptureFlowPlacementFence();var gravity=Physics.gravity;
   var motion=vessels.Values.Where(v=>v.Rigid).Select(v=>(v.Rigid,motion:v.Rigid.FlowMotion)).ToArray();
   flowPlacementFence=()=>Physics.gravity.Equals(gravity)&&placement()&&
    motion.All(v=>v.Rigid&&v.Rigid.FlowMotion.Equals(v.motion));
  }
 }
}
