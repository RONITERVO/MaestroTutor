// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class LiquidPouring {
        readonly LiquidPath observedPath=new();
        // A complete segment needs every relevant cavity, unlike first-contact
        // queries which can stop at their first proven hit. No loading or physics.
        internal MediumAvailability MeasureLiquidPath(Vector3 from,Vector3 to,LiquidPath result){
            result.Clear();CaptureMedia(true);
            if(!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!float.IsFinite((to-from).sqrMagnitude))
                return new("unavailable","Liquid path endpoints are invalid");
            var bounds=new Bounds(from,Vector3.zero);bounds.Encapsulate(to);
            var availability=CapturedMediumAvailability(bounds);
            if(!availability.Ready)return availability;
            return result.Measure(media,mediaCount,from,to)?availability:new("unavailable","The complete finite liquid path could not be measured");
        }
        internal JObject ObserveLiquidPath(Vector3 from,Vector3 to,string target){
            if(!editor||!editor.Frame.Valid||!RoomViewpoint.ValidPosition(from)||!RoomViewpoint.ValidPosition(to))return null;
            var start=editor.Frame.PointToWorld(from);var end=editor.Frame.PointToWorld(to);
            var availability=MeasureLiquidPath(start,end,observedPath);
            var bounds=new Bounds(start,Vector3.zero);bounds.Encapsulate(end);
            var result=DescribeMediumAvailability(bounds);
            result["availability"]=availability.State??"ready";
            result["reason"]=availability.Reason??"";
            result["pathMetres"]=observedPath.Metres;result["wetMetres"]=observedPath.WetMetres;
            result["dryMetres"]=observedPath.Complete?Math.Max(0,observedPath.Metres-observedPath.WetMetres):0;
            result["bodyCount"]=observedPath.WetBodies;result["targetMetres"]=observedPath.MetresIn(target);
            return result;
        }
    }
}
