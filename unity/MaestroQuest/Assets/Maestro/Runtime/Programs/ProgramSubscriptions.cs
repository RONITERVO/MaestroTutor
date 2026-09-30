// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Programs
{
    // Registered event producers read only the active room. They never acquire
    // ownership or turn an observed object ID into permission to edit it.
    public interface IProgramEventWorld {bool TryPosition(string id,out Vector3 position);}
    public interface IProgramEventWatch : IDisposable {
        bool Poll(float now,out ProgramValue value,out JObject fields,out string error);
    }
    public sealed class ProximitySubscription : IProgramEventWatch
    {
        readonly IProgramEventWorld world;
        readonly string source,target,transition;
        readonly float radius,hysteresis;
        bool inside,disposed;
        float nextSample;
        public const float SampleInterval=.1f;
        public ProximitySubscription(IProgramEventWorld world,JObject arguments,float now) {
            this.world=world;source=(string)arguments["source"];target=(string)arguments["target"];transition=(string)arguments["transition"];
            radius=(float)arguments["radius"];hysteresis=(float)arguments["hysteresis"];
            if(source==target)throw new ProgramFault("Proximity needs two different objects");
            if(!Distance(out float distance))throw new ProgramFault("A proximity object is missing, disabled or has an invalid position");
            inside=distance<=radius;nextSample=now+SampleInterval;
        }
        bool Distance(out float distance) {
            distance=0;
            if(world==null||!world.TryPosition(source,out var a)||!world.TryPosition(target,out var b))return false;
            distance=Vector3.Distance(a,b);return float.IsFinite(distance)&&distance<=1000000;
        }
        public bool Poll(float now,out ProgramValue value,out JObject fields,out string error) {
            value=default;fields=null;error=null;if(disposed||now<nextSample)return false;
            // At most one sample per tick, even after a long frame. No catch-up.
            nextSample=now+SampleInterval;
            if(!Distance(out float distance)) {error="A proximity object is missing, disabled or has an invalid position";return false;}
            bool next=inside?distance<radius+hysteresis:distance<=radius;
            if(next==inside)return false;inside=next;
            if(transition!="either"&&transition!=(inside?"enter":"exit"))return false;
            value=new ProgramValue(source);fields=new JObject {["otherId"]=target,["inside"]=inside,["distance"]=distance};return true;
        }
        public void Dispose() {disposed=true;}
    }
}
