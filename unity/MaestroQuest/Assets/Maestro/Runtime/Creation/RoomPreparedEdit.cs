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
            readonly Dictionary<string,RoomObjectData> objectIndex;
            readonly int revision,undoCount,redoCount;
            readonly long runtimePlacements;
            readonly Dictionary<string,int> targetRevisions,environmentRevisions,audioRevisions,appearanceRevisions,regionRevisions,structureRevisions,regionMemberRevisions,visibilityRevisions;
            int acceptedLightingRevision;
            readonly string viewpointState,timeState,operation;
            readonly bool baseline;
            bool ended,accepted;
            internal PreparedEdit(RoomJournal owner,Change change,RoomDocument document,string operation,bool baseline=false,IEnumerable<string> regionReceiptIds=null)
            {
                this.owner=owner;this.change=change;this.document=document.Copy();this.operation=operation;this.baseline=baseline;
                objectIndex=this.document.objects.ToDictionary(x=>x.id,StringComparer.Ordinal);
                revision=owner.clock.Next;runtimePlacements=owner.runtimePlacementAdvances;
                targetRevisions=change.Before.Concat(change.After).Select(x=>x.id).Distinct().ToDictionary(id=>id,owner.ObjectRevision);
                environmentRevisions=change.BeforeEnvironments.Concat(change.AfterEnvironments).Select(x=>x.id).Distinct().ToDictionary(id=>id,owner.EnvironmentRevision);
                audioRevisions=change.BeforeAudio.Concat(change.AfterAudio).Select(x=>x.id).Distinct().ToDictionary(id=>id,owner.AudioRevision);
                appearanceRevisions=change.BeforeAppearances.Concat(change.AfterAppearances).Select(x=>x.id).Distinct().ToDictionary(id=>id,owner.AppearanceRevision);
                // Region deltas omit unchanged definitions. Keep explicitly requested IDs
                // separately so no-op saves still return their accepted revision.
                regionRevisions=change.BeforeRegions.Concat(change.AfterRegions).Select(x=>x.id).Concat(regionReceiptIds??Array.Empty<string>()).Distinct().ToDictionary(id=>id,owner.RegionRevision);
                structureRevisions=change.BeforeStructures.Concat(change.AfterStructures).Select(x=>x.id).Distinct().ToDictionary(id=>id,owner.StructureRevision);
                // Membership revisions are receipts, not pose write guards. Metadata
                // edits must not reject unrelated runtime placement advances.
                regionMemberRevisions=change.BeforeRegions.Concat(change.AfterRegions).SelectMany(x=>x.members).Distinct().ToDictionary(id=>id,owner.ObjectRevision);
                visibilityRevisions=change.BeforeVisibility.Concat(change.AfterVisibility).Select(x=>x.id).Distinct().ToDictionary(id=>id,owner.VisibilityRevision);
                undoCount=owner.undo.Count;redoCount=owner.redo.Count;
                undoTop=undoCount>0?owner.undo[undoCount-1]:null;redoTop=redoCount>0?owner.redo[redoCount-1]:null;
                viewpointState=JsonUtility.ToJson(owner.viewpoint);timeState=JsonUtility.ToJson(owner.worldTime);
            }
            internal RoomDocument Snapshot()=>document.Copy();
            internal bool VisualOnly=>change.Scope==RoomEditScope.Visual;
            internal bool LiquidContentsOnly=>change.Scope==RoomEditScope.LiquidContents;
            internal bool PreservesPlacement=>change.Scope!=RoomEditScope.Full;
            internal bool HasTemporalCheckpoint=>change.TimeEdit!=WorldTimeEdit.Exact||change.WeatherTransition;
            // Capture once immediately after native acceptance, before observers run.
            // Later clock/placement progress remains dirty for its normal checkpoint.
            internal RoomDocument AcceptedTemporalSnapshot() {
                if(!accepted||!HasTemporalCheckpoint)throw new InvalidOperationException("Temporal edit has not been accepted");
                var value=document.Copy();value.worldTime=owner.WorldTime;value.weather=owner.Weather;return value;
            }
            internal int AcceptedLightingRevision=>accepted?acceptedLightingRevision:0;
            internal int AcceptedVisibilityRevision(string id)=>accepted&&visibilityRevisions.TryGetValue(id,out var value)?value:0;
            internal int AcceptedObjectRevision(string id)=>accepted&&(targetRevisions.TryGetValue(id,out var value)||regionMemberRevisions.TryGetValue(id,out value))?value:0;
            internal int AcceptedEnvironmentRevision(string id)=>accepted&&environmentRevisions.TryGetValue(id,out var value)?value:0;
            internal int AcceptedAudioRevision(string id)=>accepted&&audioRevisions.TryGetValue(id,out var value)?value:0;
            internal int AcceptedAppearanceRevision(string id)=>accepted&&appearanceRevisions.TryGetValue(id,out var value)?value:0;
            internal int AcceptedRegionRevision(string id)=>accepted&&regionRevisions.TryGetValue(id,out var value)?value:0;
            internal int AcceptedStructureRevision(string id)=>accepted&&structureRevisions.TryGetValue(id,out var value)?value:0;
            // Native preparation reads detached copies of its targets. Unrelated saved
            // geometry remains owned by the candidate instead of being copied again.
            internal RoomObjectData ReadObject(string id)=>id!=null&&objectIndex.TryGetValue(id,out var value)?value.Copy():null;
            internal HashSet<string> ChangedObjects=>change.Before.Concat(change.After).Select(x=>x.id).ToHashSet();
            // A background save can checkpoint an earlier runtime clock/viewpoint.
            // Publication applies only the authored delta, preserving newer runtime
            // state. Explicit temporal edits resolve their own delta at acceptance.
            internal bool RuntimeProgressChanged=>owner.runtimePlacementAdvances!=runtimePlacements||JsonUtility.ToJson(owner.viewpoint)!=viewpointState||JsonUtility.ToJson(owner.worldTime)!=timeState;
            // Only explicitly recorded runtime placements may advance the global
            // clock. Authored edits and another fork remain stale. Scoped field deltas
            // can preserve newer target placement; physical edits require an unchanged target.
            // Apply only this delta; newer unrelated poses remain dirty for a later save.
            bool RevisionCurrent(bool allowRuntimeProgress)=>owner.clock.Next==revision||allowRuntimeProgress&&
                (long)owner.clock.Next-revision==owner.runtimePlacementAdvances-runtimePlacements&&
                (PreservesPlacement||targetRevisions.All(x=>owner.ObjectRevision(x.Key)==x.Value));
            internal bool Current(RoomJournal expected,bool allowRuntimeProgress=false)=>!ended&&ReferenceEquals(owner,expected)&&RevisionCurrent(allowRuntimeProgress)&&owner.undo.Count==undoCount&&owner.redo.Count==redoCount&&
                (undoCount==0||ReferenceEquals(owner.undo[undoCount-1],undoTop))&&(redoCount==0||ReferenceEquals(owner.redo[redoCount-1],redoTop))&&
                (allowRuntimeProgress||JsonUtility.ToJson(owner.viewpoint)==viewpointState)&&
                (allowRuntimeProgress&&(change.TimeEdit!=WorldTimeEdit.Exact||change.AfterWorldTime==null&&change.BeforeWorldTime==null)||JsonUtility.ToJson(owner.worldTime)==timeState);
            internal bool Accept(RoomJournal expected,out string error,bool allowRuntimeProgress=false)
            {
                error="The room changed while the edit was being prepared; inspect it before retrying";if(!Current(expected,allowRuntimeProgress))return false;
                ended=true;
                if(operation=="baseline") {if(baseline)owner.Set(change.Before,change.After);}
                else if(operation=="undo") {owner.undo.RemoveAt(undoCount-1);owner.Publish(change,false);owner.redo.Add(change);}
                else if(operation=="redo") {owner.redo.RemoveAt(redoCount-1);owner.Publish(change,true);owner.undo.Add(change);}
                else {
                    // An initially equal seek still carries intent: time may have ticked
                    // before admission. Refresh only a new edit, never history entries.
                    if(change.TimeEdit!=WorldTimeEdit.Exact)change.BeforeWorldTime=owner.WorldTime;
                    if(change.WeatherTransition)change.BeforeWeather=owner.Weather;
                    bool noOp=HasTemporalCheckpoint&&owner.TemporalNoOp(change);
                    if(!noOp){owner.Publish(change,true);owner.undo.Add(change);if(owner.undo.Count>32)owner.undo.RemoveAt(0);owner.redo.Clear();}
                }
                foreach(var id in targetRevisions.Keys.ToArray())targetRevisions[id]=owner.ObjectRevision(id);
                foreach(var id in environmentRevisions.Keys.ToArray())environmentRevisions[id]=owner.EnvironmentRevision(id);
                foreach(var id in audioRevisions.Keys.ToArray())audioRevisions[id]=owner.AudioRevision(id);
                foreach(var id in appearanceRevisions.Keys.ToArray())appearanceRevisions[id]=owner.AppearanceRevision(id);
                foreach(var id in regionRevisions.Keys.ToArray())regionRevisions[id]=owner.RegionRevision(id);
                foreach(var id in structureRevisions.Keys.ToArray())structureRevisions[id]=owner.StructureRevision(id);
                foreach(var id in regionMemberRevisions.Keys.ToArray())regionMemberRevisions[id]=owner.ObjectRevision(id);
                foreach(var id in visibilityRevisions.Keys.ToArray())visibilityRevisions[id]=owner.VisibilityRevision(id);
                acceptedLightingRevision=owner.LightingRevision;
                accepted=true;error=null;return true;
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
            var after=forward?c.After:c.Before;
            d.objects=Swap(d.objects,forward?c.Before:c.After,c.Scope!=RoomEditScope.Full?MergeFields(after,c.Scope):after,x=>x.id).OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
            d.regions=Swap(d.regions,forward?c.BeforeRegions:c.AfterRegions,forward?c.AfterRegions:c.BeforeRegions,x=>x.id);
            d.structures=Swap(d.structures,forward?c.BeforeStructures:c.AfterStructures,forward?c.AfterStructures:c.BeforeStructures,x=>x.id);
            d.audioSources=Swap(d.audioSources,forward?c.BeforeAudio:c.AfterAudio,forward?c.AfterAudio:c.BeforeAudio,x=>x.id);
            d.environmentProfiles=Swap(d.environmentProfiles,forward?c.BeforeEnvironments:c.AfterEnvironments,forward?c.AfterEnvironments:c.BeforeEnvironments,x=>x.id);
            d.appearances=Swap(d.appearances,forward?c.BeforeAppearances:c.AfterAppearances,forward?c.AfterAppearances:c.BeforeAppearances,x=>x.id);
            d.visibilityLayers=Swap(d.visibilityLayers,forward?c.BeforeVisibility:c.AfterVisibility,forward?c.AfterVisibility:c.BeforeVisibility,x=>x.id);
            d.lighting=(forward?c.AfterLighting:c.BeforeLighting)??d.lighting;d.worldTime=ResolveTime(forward?c.AfterWorldTime:c.BeforeWorldTime,c.TimeEdit)??d.worldTime;d.weather=ResolveWeather(forward?c.AfterWeather:c.BeforeWeather,c.WeatherTransition)??d.weather;return d;
        }
        // Field history owns only its declared component values. Current placement
        // and all other components survive publication and Undo/Redo previews.
        RoomObjectData[] MergeFields(RoomObjectData[] values,RoomEditScope scope)=>values.Select(value=>
            items.TryGetValue(value.id,out var current)?RoomEditFields.Apply(current,value,scope):value.Copy()).ToArray();
        RoomWorldTime ResolveTime(RoomWorldTime requested,WorldTimeEdit mode) {
            if(requested==null)return null;
            if(mode==WorldTimeEdit.Exact)return requested.Copy();
            var value=WorldTime;
            if(mode==WorldTimeEdit.Settings)value.settings=requested.settings?.Copy();
            else {value.day=requested.day;value.second=requested.second;}
            return value;
        }
        RoomWeather ResolveWeather(RoomWeather requested,bool transition) {
            if(requested==null)return null;
            if(!transition)return requested.Copy();
            if(weather.SameTarget(requested))return Weather; // Never restart an unchanged fade.
            var value=requested.Copy();value.from=new Art.WeatherProjection(weather).Sample(WorldSeconds).Settings();
            value.startDay=worldTime.day;value.startSecond=worldTime.second;return value;
        }
        bool TemporalNoOp(Change c)=>c.WeatherTransition?weather.SameTarget(c.AfterWeather):
            c.TimeEdit==WorldTimeEdit.Settings?worldTime.settings.Same(c.AfterWorldTime.settings):
            worldTime.day==c.AfterWorldTime.day&&worldTime.second==c.AfterWorldTime.second;
        void Publish(Change c,bool forward)
        {
            var after=forward?c.After:c.Before;
            Set(forward?c.Before:c.After,c.Scope!=RoomEditScope.Full?MergeFields(after,c.Scope):after);
            SetRegions(forward?c.BeforeRegions:c.AfterRegions,forward?c.AfterRegions:c.BeforeRegions);
            SetStructures(forward?c.BeforeStructures:c.AfterStructures,forward?c.AfterStructures:c.BeforeStructures);
            SetAudio(forward?c.BeforeAudio:c.AfterAudio,forward?c.AfterAudio:c.BeforeAudio);
            SetEnvironments(forward?c.BeforeEnvironments:c.AfterEnvironments,forward?c.AfterEnvironments:c.BeforeEnvironments);
            SetAppearances(forward?c.BeforeAppearances:c.AfterAppearances,forward?c.AfterAppearances:c.BeforeAppearances);
            SetVisibility(forward?c.BeforeVisibility:c.AfterVisibility,forward?c.AfterVisibility:c.BeforeVisibility);
            SetLighting(forward?c.AfterLighting:c.BeforeLighting);SetWorldTime(ResolveTime(forward?c.AfterWorldTime:c.BeforeWorldTime,c.TimeEdit));SetWeather(ResolveWeather(forward?c.AfterWeather:c.BeforeWeather,c.WeatherTransition));
        }
    }
}
