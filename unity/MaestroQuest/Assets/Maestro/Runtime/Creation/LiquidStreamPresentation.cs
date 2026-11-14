// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class LiquidPouring {
        struct StreamContact { internal Vessel Target;internal Vector3 Mouth;internal int First; }
        // One reusable path per bounded vessel; never a second liquid inventory.
        sealed class StreamPresentation {
            internal readonly Vector3[] Points=new Vector3[Segments+1];
            internal StreamContact Contact;internal int Count;internal double Rate;internal bool Pending;
        }
        void RememberStream(Vessel source,int count,double rate,StreamContact contact){
            var stream=source.Stream;stream.Count=count;stream.Rate=rate;stream.Contact=contact;stream.Pending=true;
            for(int i=0;i<count;i++){stream.Points[i]=path[i];flowExecutionWork.PourPresentationPoints++;}
        }
        // Pour, scoop and rain all finish first. Every stream observes the same
        // final live quantity and the same plane rendered by ContainerFillView.
        // This clips only the already-proved suffix: no new PhysX/load/save work.
        void PresentStreams(Vector3 up){
            foreach(var source in vessels.Values){
                var stream=source.Stream;if(!stream.Pending)continue;stream.Pending=false;
                flowExecutionWork.PourPresentations++;
                if(!source.Item)continue;
                int count=stream.Count;var target=stream.Contact.Target;
                if(target!=null&&target.Item&&vessels.TryGetValue(target.Id,out var current)&&ReferenceEquals(target,current)){
                    var geometry=new LiquidStreamGeometry(target.Live,target.Item,up);flowExecutionWork.PourSurfaceCaptures++;
                    for(int i=stream.Contact.First;i<count;i++){
                        var from=i==stream.Contact.First?stream.Contact.Mouth:stream.Points[i-1];
                        flowExecutionWork.PourSurfaceQueries++;
                        if(!geometry.Medium.TryFreeSurfaceContact(from,stream.Points[i],out var point,out _))continue;
                        // A full cavity meets the stream exactly at its mouth.
                        // Retain the incoming grid point's evidence of entry;
                        // an already-wet mouth alone would suppress that impact.
                        var incoming=i==stream.Contact.First?stream.Points[i-1]:from;
                        bool impact=Vector3.Dot(incoming,up)>geometry.Medium.Level+1e-6f&&Mathf.Abs(Vector3.Dot(point,up)-geometry.Medium.Level)<=.00001f;
                        stream.Points[i]=point;count=i+1;
                        if(impact&&Time.unscaledTime>=source.NextStreamRipple){
                            target.Item.GetComponent<ContainerFillView>()?.AddRipple(point,1);
                            source.NextStreamRipple=Time.unscaledTime+.1f;
                        }
                        break;
                    }
                }
                source.Item.GetComponent<ContainerFillView>()?.ShowStream(stream.Points,count,stream.Rate,source.Live.color);
            }
        }
    }
}
