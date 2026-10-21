// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool MovementBody(string target,out Bounds local){
            local=default;var item=Find(target);
            if(!item||!item.isActiveAndEnabled||journal==null||!journal.NativeCollisionMetadata(target,out var kind,out _))return false;
            bool solid=false;
            foreach(var c in item.Grab.colliders)if(c&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&c.gameObject.layer!=RoomPhysicsLayers.Pending){solid=true;break;}
            if(!solid)return false;
            var shape=RoomSpatialBounds.CaptureCollision(item,kind);
            if(!shape.Known||!shape.HasBounds||!RoomRecipe.Finite(shape.Bounds.center)||!RoomRecipe.Finite(shape.Bounds.extents))return false;
            local=shape.Bounds;return local.extents.x>0&&local.extents.y>0&&local.extents.z>0;
        }
        internal JObject ObserveHabitatPath(string target,string habitat,Vector3 destination){
            var item=Find(target);
            if(!item||!item.isActiveAndEnabled||!Frame.Valid||!RoomViewpoint.ValidPosition(destination)||!Liquids)return null;
            if(journal==null||!journal.NativeCollisionMetadata(target,out var kind,out _)||RuntimeGate.Held||WriteGate.Frozen||Ownership.Suspended)
                return LiquidPouring.HabitatUnavailable(habitat,"The actor or workspace is unavailable");
            var shape=RoomSpatialBounds.CaptureCollision(item,kind).Transform(item.transform.localToWorldMatrix);
            if(!shape.Known||!shape.HasBounds||shape.Bounds.extents.x<=0||shape.Bounds.extents.y<=0||shape.Bounds.extents.z<=0)
                return LiquidPouring.HabitatUnavailable(habitat,"The actor needs a known native collision envelope");
            var to=Frame.PointToWorld(destination)+shape.Bounds.center-item.transform.position;
            return Liquids.ObserveHabitatPath(item,habitat,shape.Bounds.center,to,shape.Bounds.extents);
        }
    }
    public sealed partial class LiquidPouring {
        internal static JObject HabitatUnavailable(string habitat,string reason)=>new(){
            ["bodyId"]=habitat??"",["contained"]=false,["reason"]=reason,["availability"]="unavailable",
            ["dependencyCount"]=0,["dependencies"]=new JArray()
        };
        internal JObject ObserveHabitatPath(RoomItem actor,string habitat,Vector3 from,Vector3 to,Vector3 extents){
            var admission=CheckHabitatPath(actor,habitat,from,to,extents);
            if(!admission.Captured)return HabitatUnavailable(habitat,admission.Reason);
            var bounds=new Bounds(from,extents*2);bounds.Encapsulate(new Bounds(to,extents*2));
            var result=DescribeMediumAvailability(bounds,editor.Identity(actor),habitat);
            result["bodyId"]=habitat;result["contained"]=admission.Contained;result["reason"]=admission.Reason??"";
            result["availability"]=admission.Availability.State??"ready";
            return result;
        }
        internal bool SurfaceDestination(RoomItem actor,string habitat,Vector3 hint,Vector3 centreOffset,Vector3 extents,float immersion,out Vector3 position,out string reason){
            position=default;reason="The actor or liquid workspace is unavailable";
            if(!editor||!actor||!editor.Frame.Valid||publishing||blocked||editor.RuntimeGate.Held||editor.WriteGate.Frozen||editor.Ownership.Suspended)return false;
            CaptureMedia(false);
            var available=CapturedMediumAvailability(new Bounds(actor.transform.position+centreOffset,extents*2),editor.Identity(actor),habitat);
            if(!available.Ready){reason=available.Reason;return false;}
            for(int i=0;i<mediaCount;i++)if(media[i].Id==habitat)return media[i].SurfacePosition(hint,centreOffset,extents,immersion,out position,out reason);
            reason="The chosen habitat has no available liquid surface";return false;
        }
        internal LiquidHabitatAdmission CheckHabitatPath(RoomItem actor,string habitat,Vector3 from,Vector3 to,Vector3 extents,bool surface=false){
            if(!editor||!actor||!editor.Frame.Valid||publishing||blocked||editor.RuntimeGate.Held||editor.WriteGate.Frozen||editor.Ownership.Suspended)
                return LiquidHabitatAdmission.Unavailable("The actor or liquid workspace is unavailable");
            if(habitat==editor.Identity(actor))return LiquidHabitatAdmission.Unavailable("An actor cannot use itself as its habitat");
            var bounds=new Bounds(from,extents*2);bounds.Encapsulate(new Bounds(to,extents*2));
            CaptureMedia(false);
            var available=CapturedMediumAvailability(bounds,editor.Identity(actor),habitat);
            if(!available.Ready)return new(available,false,available.Reason,true);
            for(int i=0;i<mediaCount;i++)if(media[i].Id==habitat){
                string reason;bool contained=surface?media[i].ContainsSurfaceBoxSweep(from,to,extents,out reason):media[i].ContainsBoxSweep(from,to,extents,out reason);
                return new(available,contained,reason,true);
            }
            // Empty is a known negative; another overlapping body never substitutes.
            vessels.TryGetValue(habitat,out var chosen);
            editor.MissingLiquidExtent(habitat,chosen?.Live,out var contents);
            if(contents!=null&&contents.amountMl<=0)return new(available,false,"The chosen habitat contains no liquid",true);
            return LiquidHabitatAdmission.Unavailable("The chosen habitat has no available liquid geometry");
        }
    }
    internal readonly struct LiquidHabitatAdmission {
        internal readonly MediumAvailability Availability;
        internal readonly bool Contained,Captured;
        internal readonly string Reason;
        internal LiquidHabitatAdmission(MediumAvailability availability,bool contained,string reason,bool captured){
            Availability=availability;Contained=contained;Reason=reason;Captured=captured;
        }
        internal static LiquidHabitatAdmission Unavailable(string reason)=>new(new("unavailable",reason,0),false,reason,false);
    }
}
