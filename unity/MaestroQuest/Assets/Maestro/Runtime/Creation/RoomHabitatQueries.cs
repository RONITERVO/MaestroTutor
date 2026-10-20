// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
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
            if(!editor||!actor||!editor.Frame.Valid||publishing||blocked||editor.RuntimeGate.Held||editor.WriteGate.Frozen||editor.Ownership.Suspended)
                return HabitatUnavailable(habitat,"The actor or liquid workspace is unavailable");
            if(habitat==editor.Identity(actor))return HabitatUnavailable(habitat,"An actor cannot use itself as its habitat");
            var bounds=new Bounds(from,extents*2);bounds.Encapsulate(new Bounds(to,extents*2));
            CaptureMedia(false);
            var available=CapturedMediumAvailability(bounds,editor.Identity(actor),habitat);
            var result=DescribeMediumAvailability(bounds,editor.Identity(actor),habitat);
            result["bodyId"]=habitat;result["contained"]=false;result["reason"]=available.Reason??"";
            if(!available.Ready)return result;
            for(int i=0;i<mediaCount;i++)if(media[i].Id==habitat){
                result["contained"]=media[i].ContainsBoxSweep(from,to,extents,out var reason);result["reason"]=reason??"";return result;
            }
            // Empty is a known negative, including an accepted dormant empty
            // vessel. Never substitute another overlapping or larger water body.
            vessels.TryGetValue(habitat,out var chosen);
            editor.MissingLiquidExtent(habitat,chosen?.Live,out var contents);
            if(contents!=null&&contents.amountMl<=0){result["reason"]="The chosen habitat contains no liquid";return result;}
            return HabitatUnavailable(habitat,"The chosen habitat has no available liquid geometry");
        }
    }
}
