// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomJournal
    {
        // The candidate and history delta have one owner and never expose mutable
        // source references. Preparation neither advances revisions nor consumes
        // history. Save/admission callers accept this exact candidate once.
        internal sealed class PreparedEdit:IDisposable
        {
            readonly RoomJournal owner;
            readonly Change change,undoTop,redoTop;
            readonly RoomDocument document;
            readonly int revision,undoCount,redoCount;
            readonly string viewpointState,timeState,operation;
            readonly bool baseline;
            bool ended;
            internal PreparedEdit(RoomJournal owner,Change change,RoomDocument document,string operation,bool baseline=false)
            {
                this.owner=owner;this.change=change;this.document=document.Copy();this.operation=operation;this.baseline=baseline;
                revision=owner.clock.Next;undoCount=owner.undo.Count;redoCount=owner.redo.Count;
                undoTop=undoCount>0?owner.undo[undoCount-1]:null;redoTop=redoCount>0?owner.redo[redoCount-1]:null;
                viewpointState=JsonUtility.ToJson(owner.viewpoint);timeState=JsonUtility.ToJson(owner.worldTime);
            }
            internal RoomDocument Snapshot()=>document.Copy();
            internal HashSet<string> ChangedObjects=>change.Before.Concat(change.After).Select(x=>x.id).ToHashSet();
            internal bool Current(RoomJournal expected)=>!ended&&ReferenceEquals(owner,expected)&&owner.clock.Next==revision&&owner.undo.Count==undoCount&&owner.redo.Count==redoCount&&
                (undoCount==0||ReferenceEquals(owner.undo[undoCount-1],undoTop))&&(redoCount==0||ReferenceEquals(owner.redo[redoCount-1],redoTop))&&
                JsonUtility.ToJson(owner.viewpoint)==viewpointState&&JsonUtility.ToJson(owner.worldTime)==timeState;
            internal bool Accept(RoomJournal expected,out string error)
            {
                error="The room changed while the edit was being prepared; inspect it before retrying";if(!Current(expected))return false;
                ended=true;
                if(operation=="baseline") {if(baseline)owner.Set(change.Before,change.After);}
                else if(operation=="undo") {owner.undo.RemoveAt(undoCount-1);owner.Publish(change,false);owner.redo.Add(change);}
                else if(operation=="redo") {owner.redo.RemoveAt(redoCount-1);owner.Publish(change,true);owner.undo.Add(change);}
                else {owner.Publish(change,true);owner.undo.Add(change);if(owner.undo.Count>32)owner.undo.RemoveAt(0);owner.redo.Clear();}
                error=null;return true;
            }
            public void Dispose()=>ended=true;
        }
        internal bool PrepareHistory(bool reverse,out PreparedEdit prepared,out string error)
        {
            prepared=null;error=reverse?"Nothing to undo":"Nothing to redo";var history=reverse?undo:redo;if(history.Count==0)return false;
            var change=history[history.Count-1];var candidate=HistoryCandidate(change,!reverse);if(!candidate.Validate(out error))return false;
            prepared=new PreparedEdit(this,change,candidate,reverse?"undo":"redo");error=null;return true;
        }
        static T[] Swap<T>(T[] source,T[] before,T[] after,Func<T,string> id)
        {
            var removed=before.Select(id).Concat(after.Select(id)).ToHashSet();return source.Where(x=>!removed.Contains(id(x))).Concat(after).ToArray();
        }
        RoomDocument HistoryCandidate(Change c,bool forward)
        {
            var d=Snapshot();
            d.objects=Swap(d.objects,forward?c.Before:c.After,forward?c.After:c.Before,x=>x.id).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
            d.regions=Swap(d.regions,forward?c.BeforeRegions:c.AfterRegions,forward?c.AfterRegions:c.BeforeRegions,x=>x.id);
            d.structures=Swap(d.structures,forward?c.BeforeStructures:c.AfterStructures,forward?c.AfterStructures:c.BeforeStructures,x=>x.id);
            d.audioSources=Swap(d.audioSources,forward?c.BeforeAudio:c.AfterAudio,forward?c.AfterAudio:c.BeforeAudio,x=>x.id);
            d.environmentProfiles=Swap(d.environmentProfiles,forward?c.BeforeEnvironments:c.AfterEnvironments,forward?c.AfterEnvironments:c.BeforeEnvironments,x=>x.id);
            d.appearances=Swap(d.appearances,forward?c.BeforeAppearances:c.AfterAppearances,forward?c.AfterAppearances:c.BeforeAppearances,x=>x.id);
            d.visibilityLayers=Swap(d.visibilityLayers,forward?c.BeforeVisibility:c.AfterVisibility,forward?c.AfterVisibility:c.BeforeVisibility,x=>x.id);
            d.lighting=(forward?c.AfterLighting:c.BeforeLighting)??d.lighting;d.worldTime=(forward?c.AfterWorldTime:c.BeforeWorldTime)??d.worldTime;d.weather=(forward?c.AfterWeather:c.BeforeWeather)??d.weather;return d;
        }
        void Publish(Change c,bool forward)
        {
            Set(forward?c.Before:c.After,forward?c.After:c.Before);
            SetRegions(forward?c.BeforeRegions:c.AfterRegions,forward?c.AfterRegions:c.BeforeRegions);
            SetStructures(forward?c.BeforeStructures:c.AfterStructures,forward?c.AfterStructures:c.BeforeStructures);
            SetAudio(forward?c.BeforeAudio:c.AfterAudio,forward?c.AfterAudio:c.BeforeAudio);
            SetEnvironments(forward?c.BeforeEnvironments:c.AfterEnvironments,forward?c.AfterEnvironments:c.BeforeEnvironments);
            SetAppearances(forward?c.BeforeAppearances:c.AfterAppearances,forward?c.AfterAppearances:c.BeforeAppearances);
            SetVisibility(forward?c.BeforeVisibility:c.AfterVisibility,forward?c.AfterVisibility:c.BeforeVisibility);
            SetLighting(forward?c.AfterLighting:c.BeforeLighting);SetWorldTime(forward?c.AfterWorldTime:c.BeforeWorldTime);SetWeather(forward?c.AfterWeather:c.BeforeWeather);
        }
    }
}
