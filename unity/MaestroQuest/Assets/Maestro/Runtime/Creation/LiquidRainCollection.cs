// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class LiquidPouring
    {
        readonly WeatherCover rainCover=new();
        readonly RoomContainer rainSource=new(){capacityMl=1000000};
        bool CollectRain(float seconds){
            var weather=editor.CurrentWeather;var velocity=WeatherCover.Velocity(editor);bool flowing=false;
            foreach(var v in vessels.Values){
                v.Exposure=0;v.RainReady=false;v.RainReason="";
                if(!v.Ready){v.RainReason="Container is unavailable for physics";continue;}
                if(weather.Rain<=0){v.RainReason="There is no rain";continue;}
                if(v.Live.amountMl>=v.Live.capacityMl){v.RainReason="Container is full";continue;}
                if(v.Live.amountMl>0&&!v.Live.SameLiquid(rainSource)){v.RainReason="Existing contents cannot mix with Water";continue;}
                var t=v.Item.transform;var c=v.Live;var rotation=t.rotation*c.frame.rotation;
                var normal=rotation*Vector3.up;float flux=Mathf.Max(0,Vector3.Dot(normal,-velocity)/6);
                if(flux<=.001f){v.RainReason="Opening faces away from the rain";continue;}
                int open=0,unknown=0;
                for(int sample=0;sample<5;sample++){
                    float x=sample==1?.5f:sample==2?-.5f:0,z=sample==3?.5f:sample==4?-.5f:0;
                    var offset=new Vector3(x*(c.IsRectangular?c.rectangle.width*.5f:c.radius),c.height,z*(c.IsRectangular?c.rectangle.depth*.5f:c.radius));
                    var point=t.TransformPoint(c.frame.position+c.frame.rotation*offset)+normal*.001f;
                    var exposure=rainCover.Exposure(editor,point,velocity,v.Item);
                    if(exposure==RainExposure.Open)open++;else if(exposure==RainExposure.Unknown)unknown++;
                }
                v.Exposure=open/5f;
                if(open==0){v.RainReason=unknown>0?"Rain exposure is unknown":"Opening is covered";continue;}
                double scale=t.lossyScale.x,area=c.FootprintArea*scale*scale;
                double requested=Math.Min(c.capacityMl-c.amountMl,weather.Rain*area*flux*v.Exposure*seconds*1000/3600);
                if(requested<=.000001){v.RainReason="Rain intake is below simulation precision";continue;}
                // Preserve the identity of an earlier pour/scoop episode before an empty vessel adopts rainwater.
                if(Owns(v.Id)&&c.amountMl==0&&!c.SameLiquid(rainSource)){Finish(out _);return flowing;}
                v.RainReady=true;
                if(!Begin(out var issue)){error=issue;blocked=true;v.RainReady=false;v.RainReason=issue;editor.ReportStatus(issue);return flowing;}
                Touch(v);rainSource.amountMl=requested;
                if(!RoomContainer.Transfer(rainSource,c,requested,out var moved,out var failure)){v.RainReason=failure;v.RainReady=false;continue;}
                v.Rain+=moved;contents[v.Id]=c;flowing|=moved>0;Preview(v);
            }
            return flowing;
        }
        internal JObject ObserveRain(string id){
            if(!vessels.TryGetValue(id,out var v))return null;
            bool running=world&&world.Running&&!editor.Ownership.Suspended&&!editor.RuntimeGate.Held;
            return new JObject{["sessionId"]=session,["phase"]=blocked?"failed":Owns(id)?"flowing":"idle",["collectedMl"]=v.Rain,["exposure"]=v.Exposure,["ready"]=running&&!blocked&&v.RainReady,["reason"]=blocked?Maestro.Quest.Imports.ImportObservation.Text(error):!running?"Physics is paused":v.RainReason};
        }
    }
}
