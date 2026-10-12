// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal Bounds NavigationRoomBounds(Bounds world){
            var extent=RoomSpatialExtent.Empty;extent.Include(world,transform.worldToLocalMatrix);return extent.Bounds;
        }
        internal string[] NavigationTargets(Bounds body,Bounds ground,bool water,string actor){
            var values=SpatialMovementTargets(NavigationRoomBounds(body),NavigationRoomBounds(ground)).AsEnumerable();
            if(water&&Liquids)values=values.Concat(Liquids.NavigationCavities(body));
            return values.Where(id=>id!=actor).Distinct(StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        }
        internal RoomSpatialReadiness NavigationReady(string[] targets,Bounds body,Bounds ground,bool water,string actor,out string error){
            if(!Frame.Valid){error="The navigation frame is unavailable";return RoomSpatialReadiness.Unavailable;}
            var state=SpatialMovementReady(targets,NavigationRoomBounds(ground),out error);if(state!=RoomSpatialReadiness.Ready)return state;
            if(water&&Liquids){
                var missing=Liquids.NavigationAvailability(body,actor);
                if(!missing.Ready){error=missing.Reason;return missing.State=="pending"?RoomSpatialReadiness.Preparing:RoomSpatialReadiness.Unavailable;}
            }
            error=null;return RoomSpatialReadiness.Ready;
        }
    }
    public sealed partial class LiquidPouring {
        internal string[] NavigationCavities(Bounds world){
            var result=new System.Collections.Generic.List<string>();
            foreach(var id in editor.LiquidIds()){
                vessels.TryGetValue(id,out var vessel);
                var extent=editor.MissingLiquidExtent(id,vessel?.Live,out var contents);
                if(contents!=null&&contents.amountMl<=0)continue;
                if(!extent.Known||!extent.HasBounds||extent.Bounds.Intersects(world))result.Add(id);
            }
            return result.ToArray();
        }
        internal MediumAvailability NavigationAvailability(Bounds world,string actor){CaptureMissingMedia();return CapturedMediumAvailability(world,actor);}
    }
}
