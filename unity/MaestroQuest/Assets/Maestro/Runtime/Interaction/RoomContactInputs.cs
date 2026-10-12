// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Contact owns the latest swept corridor, never queued displacement. The
    // same bounded preparation/lifetime lane serves navigation and rigid bodies.
    internal sealed class RoomContactInputs:IDisposable {
        readonly RoomEditor editor;readonly string actor;readonly Func<bool> owned;
        readonly RoomSpatialInputs inputs;
        Bounds sweep;bool sampled,ready,disposed;string issue;
        internal RoomContactInputs(RoomEditor editor,string actor,Func<bool> owned){this.editor=editor;this.actor=actor;this.owned=owned;inputs=new RoomSpatialInputs(editor,actor,owned,false,purpose:"Contact");}
        internal bool SourceChanged=>inputs.SourceChanged;
        internal bool Pending=>inputs.Pending;
        internal bool Require(Bounds value,out string error){
            sweep=value;sampled=true;
            if(!Valid(value)){Dispose();issue=error="Contact bounds are unavailable";return false;}
            ready=inputs.Require(value,value,true,out issue);error=issue;return ready;
        }
        internal bool Current(out string error){
            error=issue;
            if(disposed||!sampled||!ready||!editor||!editor.isActiveAndEnabled||editor.RuntimeGate.Held||editor.WriteGate.Frozen||!owned()||inputs.SourceChanged)return false;
            var latest=editor.SpatialInputTargets(sweep,sweep,true,actor);
            if(latest.Any(id=>Array.BinarySearch(inputs.Targets,id,StringComparer.Ordinal)<0)){error="The contact input set changed";return false;}
            return editor.SpatialInputsReady(inputs.Targets,sweep,sweep,true,actor,out error)==RoomSpatialReadiness.Ready;
        }
        internal JObject Observe(){bool current=Current(out var error);return State(current?"ready":inputs.Pending?"pending":"unavailable",current,error,inputs.Targets);}
        internal static JObject State(string state,bool ready,string reason,string[] targets=null)=>new(){["state"]=state,["ready"]=ready,["reason"]=ImportObservation.Text(reason),["dependencyCount"]=targets?.Length??0,["dependencies"]=new JArray((targets??Array.Empty<string>()).Take(4))};
        internal static bool Valid(Bounds value)=>RoomRecipe.Finite(value.center)&&RoomRecipe.Finite(value.extents)&&RoomRecipe.Finite(value.size)&&value.extents.x>0&&value.extents.y>0&&value.extents.z>0;
        public void Dispose(){if(disposed)return;disposed=true;inputs.Dispose();ready=false;}
    }
    internal static class RoomContactSweep {
        internal static bool Item(RoomItem item,Pose target,Vector3 localScale,out Bounds sweep){
            sweep=default;
            if(!item||!item.Grab||!RoomRecipe.Finite(target.position)||!Creation.MotionFrame.ValidRotation(target.rotation)||!RoomRecipe.Finite(localScale)||localScale.x<=0||localScale.y<=0||localScale.z<=0)return false;
            foreach(var c in item.Grab.colliders)if(!c||!c.isTrigger&&(!c.enabled||!c.gameObject.activeInHierarchy))return false;
            var shape=RoomSpatialBounds.CaptureCollision(item,RoomObjectKind.Block);if(!shape.Known||!shape.HasBounds)return false;
            var parent=item.transform.parent?item.transform.parent.lossyScale:Vector3.one;
            var before=item.transform.lossyScale;var after=Vector3.Scale(parent,localScale);
            // Sphere about the object pivot covers every intermediate rotation,
            // offset collider and both scales, including two-handed resizing.
            var reach=Abs(shape.Bounds.center)+shape.Bounds.extents;
            float radius=Mathf.Max(Vector3.Scale(reach,Abs(before)).magnitude,Vector3.Scale(reach,Abs(after)).magnitude)+.002f;
            sweep=new Bounds(item.transform.position,Vector3.one*(radius*2));sweep.Encapsulate(new Bounds(target.position,Vector3.one*(radius*2)));
            return RoomContactInputs.Valid(sweep);
        }
        static Vector3 Abs(Vector3 value)=>new(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));
    }
}
