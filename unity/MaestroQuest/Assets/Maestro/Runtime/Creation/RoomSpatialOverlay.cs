// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Owned value snapshots only. Reuses the base tree until a bounded set of
    // exact identity replacements/tombstones is compacted. No journal/native
    // references, callbacks or query scratch are retained by a snapshot.
    internal sealed class RoomSpatialOverlay<T> where T:struct {
        internal const int MaximumOverrides=32;
        internal readonly struct Entry {
            internal readonly string Id;internal readonly RoomSpatialExtent Extent;internal readonly T Value;
            internal Entry(string id,RoomSpatialExtent extent,T value){
                if(string.IsNullOrEmpty(id))throw new ArgumentException("Spatial entry needs an identity",nameof(id));
                Id=id;Extent=extent.Known&&extent.HasBounds&&!RoomSpatialLookup.Valid(extent.Bounds)?default:extent;Value=value;
            }
        }
        internal readonly struct Change {
            internal readonly string Id;internal readonly bool Present;internal readonly Entry Replacement;
            internal Change(Entry value){Id=value.Id;Present=true;Replacement=value;}
            internal Change(string removed){if(string.IsNullOrEmpty(removed))throw new ArgumentException("Spatial removal needs an identity",nameof(removed));Id=removed;Present=false;Replacement=default;}
        }
        sealed class Basis {
            internal readonly Entry[] Values;internal readonly Dictionary<string,int> Slots;internal readonly RoomSpatialLookup Lookup;
            internal Basis(IReadOnlyList<Entry> source){
                if(source==null)throw new ArgumentNullException(nameof(source));
                if(source.Count>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(source));
                Values=new Entry[source.Count];Slots=new(StringComparer.Ordinal);
                for(int i=0;i<source.Count;i++){var entry=source[i];if(string.IsNullOrEmpty(entry.Id))throw new ArgumentException("Spatial entry needs an identity",nameof(source));Values[i]=entry;}
                Array.Sort(Values,(a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id));
                var bounds=new RoomSpatialExtent[Values.Length];
                for(int i=0;i<Values.Length;i++){Slots.Add(Values[i].Id,i);bounds[i]=Values[i].Extent;}
                Lookup=new RoomSpatialLookup(bounds);
            }
        }
        readonly Basis basis;readonly Dictionary<string,Change> overrides;
        internal int Count {get;}internal int OverrideCount=>overrides.Count;
        internal int BuildWork=>basis.Lookup.BuildWork;
        internal RoomSpatialOverlay(IReadOnlyList<Entry> source){basis=new(source);overrides=new(StringComparer.Ordinal);Count=basis.Values.Length;}
        RoomSpatialOverlay(Basis basis,Dictionary<string,Change> overrides,int count){this.basis=basis;this.overrides=overrides;Count=count;}
        internal bool Contains(string id)=>id!=null&&(overrides.TryGetValue(id,out var change)?change.Present:basis.Slots.ContainsKey(id));
        internal RoomSpatialOverlay<T> Apply(IReadOnlyList<Change> changes,out int updates,out int compactions,out int buildWork){
            if(changes==null)throw new ArgumentNullException(nameof(changes));
            updates=compactions=buildWork=0;if(changes.Count==0)return this;
            var next=new Dictionary<string,Change>(overrides,StringComparer.Ordinal);var dirty=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<changes.Count;i++){
                var value=changes[i];if(string.IsNullOrEmpty(value.Id)||value.Present&&value.Replacement.Id!=value.Id)throw new ArgumentException("Invalid spatial change",nameof(changes));
                // Removing an identity absent from both layers is a no-op. This
                // keeps unrelated live changes out of the dormant overlay.
                if(!value.Present&&!basis.Slots.ContainsKey(value.Id))next.Remove(value.Id);else next[value.Id]=value;
                dirty.Add(value.Id);
            }
            int count=basis.Values.Length;
            foreach(var pair in next){if(basis.Slots.ContainsKey(pair.Key))count--;if(pair.Value.Present)count++;}
            if(count>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(changes),"Spatial lookup capacity exceeded");
            updates=dirty.Count;
            if(next.Count<=MaximumOverrides)return new(basis,next,count);
            var values=new List<Entry>(count);
            foreach(var entry in basis.Values)if(!next.ContainsKey(entry.Id))values.Add(entry);
            foreach(var value in next.Values)if(value.Present)values.Add(value.Replacement);
            var compacted=new RoomSpatialOverlay<T>(values);compactions=1;buildWork=compacted.BuildWork;return compacted;
        }
        internal int Query(Bounds body,Bounds? support,List<Entry> result,List<int> scratch){
            if(result==null||scratch==null)throw new ArgumentNullException(result==null?nameof(result):nameof(scratch));
            result.Clear();int nodes=basis.Lookup.Query(body,support,scratch);
            foreach(int index in scratch){var entry=basis.Values[index];if(!overrides.ContainsKey(entry.Id))result.Add(entry);}
            bool all=!RoomSpatialLookup.Valid(body)||support.HasValue&&!RoomSpatialLookup.Valid(support.Value);
            foreach(var change in overrides.Values){
                if(!change.Present)continue;var entry=change.Replacement;var extent=entry.Extent;
                if(!extent.Known||!extent.HasBounds&&support.HasValue||extent.HasBounds&&(all||extent.Bounds.Intersects(body)||support.HasValue&&extent.Bounds.Intersects(support.Value)))result.Add(entry);
            }
            result.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id));return nodes;
        }
    }
}
