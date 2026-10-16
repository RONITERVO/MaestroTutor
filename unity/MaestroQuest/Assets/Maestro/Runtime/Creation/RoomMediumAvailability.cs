// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal readonly struct MediumAvailability
    {
        internal readonly string State,Reason;
        internal readonly int Count;
        internal bool Ready=>State==null||State=="ready";
        internal MediumAvailability(string state,string reason,int count=0)
        {State=state;Reason=reason;Count=count;}
    }
    public sealed partial class RoomJournal
    {
        internal string[] LiquidIds()=>items.Values.Where(x=>(x.containers?.Length??0)==1).Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        internal bool LiquidMetadata(string id,out RoomContainer contents,out Vector3 position,out Quaternion rotation,out float scale,out ScanSpatialPlacement anchor)
        {
            contents=null;position=default;rotation=Quaternion.identity;scale=1;anchor=null;
            if(!items.TryGetValue(id,out var data)||data.containers?.Length!=1)return false;
            contents=data.containers[0].Copy();position=data.position;rotation=data.rotation;scale=data.scale;anchor=ScanSpatialPlacement.From(data);return true;
        }
    }
    public sealed partial class RoomEditor
    {
        internal string[] LiquidIds()=>journal?.LiquidIds()??Array.Empty<string>();
        // A cavity can extend beyond the object's artwork and solid colliders.
        // Project its own accepted dimensions, using the same retained idle pose
        // as native activation. Do not load geometry or clone a recipe/animation.
        internal RoomSpatialExtent MissingLiquidExtent(string id,RoomContainer live,out RoomContainer contents)=>MissingLiquidExtent(id,live,out contents,out _,out _);
        internal RoomSpatialExtent MissingLiquidExtent(string id,RoomContainer live,out RoomContainer contents,out Matrix4x4 placement,out Quaternion worldRotation)
        {
            placement=Matrix4x4.identity;worldRotation=Quaternion.identity;contents=null;if(journal==null||!journal.LiquidMetadata(id,out var saved,out var position,out var rotation,out var scale,out var anchor))return default;
            contents=live??saved;
            var item=Find(id);
            if(item){if(!Frame.Read(item.transform,out position,out rotation,out scale))return default;}
            else if(dormantNative.TryGetValue(id,out var entry)&&entry.SavedPosition==position&&entry.SavedRotation==rotation&&entry.SavedScale==scale){position=entry.Position;rotation=entry.Rotation;scale=entry.Scale;}
            var size=new Vector3(contents.IsRectangular?contents.rectangle.width:contents.radius*2,contents.height,contents.IsRectangular?contents.rectangle.depth:contents.radius*2);
            var extent=RoomSpatialExtent.Empty;extent.Include(new Bounds(Vector3.up*(contents.height*.5f),size),Matrix4x4.TRS(contents.frame.position,contents.frame.rotation,Vector3.one));
            var local=new RoomSpatialBounds{Collision=extent};
            var authoredPlacement=Matrix4x4.TRS(position,rotation,Vector3.one*scale);
            if(anchor!=null&&!anchor.TryPlacement(this,out authoredPlacement,out rotation))return default;
            placement=transform.localToWorldMatrix*authoredPlacement;worldRotation=transform.rotation*rotation;
            return local.Transform(authoredPlacement).Transform(transform.localToWorldMatrix).Collision;
        }
        internal bool LiquidNativePending(string id)=>NativeContentPending(id);
    }
    public sealed partial class LiquidPouring
    {
        readonly struct MissingMedium
        {
            internal readonly string Id,State;internal readonly RoomSpatialExtent Extent;
            internal MissingMedium(string id,string state,RoomSpatialExtent extent){Id=id;State=state;Extent=extent;}
            internal bool Relevant(Bounds body,string ignore)=>Id!=ignore&&(!Extent.Known||!Extent.HasBounds||Extent.Bounds.Intersects(body));
        }
        readonly List<MissingMedium> missingMedia=new();
        string mediumQueryIssue;
        void CaptureMissingMedia()
        {
            missingMedia.Clear();mediumQueryIssue=null;
            if(!editor||!editor.isActiveAndEnabled||!editor.Frame.Valid){mediumQueryIssue="The liquid workspace is unavailable";return;}
            foreach(var id in editor.LiquidIds()){
                vessels.TryGetValue(id,out var vessel);
                var item=editor.Find(id);
                if(item&&item.isActiveAndEnabled&&vessel!=null&&vessel.Item==item&&item.GetComponent<ContainerFillView>()?.NativeReady==true&&vessel.Rigid&&(!vessel.Live.TerrainFloor||RoomTerrainReservoir.Native(vessel.Live,item.transform)!=null))continue;
                var extent=editor.MissingLiquidExtent(id,vessel?.Live,out var contents);
                // An accepted empty vessel contains no medium, regardless of its view.
                if(contents!=null&&contents.amountMl<=0)continue;
                missingMedia.Add(new(id,editor.LiquidNativePending(id)?"pending":"unavailable",extent));
            }
        }
        internal static Bounds MediumSweep(Vector3 from,Vector3 to,float radius,float height)
        {
            var body=new Bounds(from+Vector3.up*(height*.5f),new Vector3(radius*2,height,radius*2));
            body.Encapsulate(new Bounds(to+Vector3.up*(height*.5f),body.size));return body;
        }
        internal MediumAvailability CapturedMediumAvailability(Bounds body,string ignore=null)
        {
            int count=0;string state="ready";
            foreach(var value in missingMedia)if(value.Relevant(body,ignore)){
                count++;if(value.State=="unavailable"||state=="ready")state=value.State;
            }
            if(mediumQueryIssue!=null)return new("unavailable",mediumQueryIssue,count);
            return new(state,state=="pending"?"Liquid is still loading; inspect dependencies before moving":state=="unavailable"?"Liquid geometry is unavailable; inspect dependencies before moving":null,count);
        }
        internal JObject DescribeMediumAvailability(Bounds body,string ignore=null)
        {
            var availability=CapturedMediumAvailability(body,ignore);
            var dependencies=new JArray();foreach(var value in missingMedia)if(value.Relevant(body,ignore)&&dependencies.Count<4)
                dependencies.Add(new JObject{["target"]=value.Id,["state"]=value.State,["boundsKnown"]=value.Extent.Known&&value.Extent.HasBounds});
            return new JObject{["availability"]=availability.State??"ready",["dependencyCount"]=availability.Count,["dependencies"]=dependencies};
        }
    }
}
