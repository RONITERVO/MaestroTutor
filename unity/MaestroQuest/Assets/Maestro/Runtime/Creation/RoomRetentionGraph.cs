// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation
{
    [Flags] internal enum RoomRetentionReason
    {
        None=0, WorldOwned=1, Held=2, Ownership=4, Audio=8, Animation=16,
        Physics=32, CollisionEnvironment=64, Unavailable=128, WorkspaceBusy=256,
        Navigation=512, WaterRoute=1024, Observation=2048, ApproachingView=4096, RecentObservation=8192, Lighting=16384
    }
    /// <summary>Immutable authored dependencies, with no live demand or native references.
    /// Named regions and enabled connections form groups; unassigned objects are independent.
    /// Remembered structures are not physical connections.</summary>
    internal sealed class RoomRetentionTopology
    {
        readonly Dictionary<string,int> groupByTarget=new(StringComparer.Ordinal);
        readonly Dictionary<string,string[]> areas=new(StringComparer.Ordinal);
        readonly HashSet<string> missingDependencies=new(StringComparer.Ordinal);
        readonly string[][] groups;
        readonly IReadOnlyList<string> targets;
        internal IEnumerable<string> Targets=>targets;
        internal int GroupCount=>groups.Length;
        internal int GroupIndex(string target)=>target!=null&&groupByTarget.TryGetValue(target,out var index)?index:-1;
        internal bool Contains(string target)=>GroupIndex(target)>=0;
        internal RoomRetentionTopology(IEnumerable<RoomObjectData> objects,IEnumerable<RoomRegion> regions)
        {
            // Copy only identities. No recipes, motion samples or mutable journal arrays
            // are retained. Temporary build edges disappear after components are formed.
            var values=objects.ToArray();
            targets=Array.AsReadOnly(values.Select(value=>value.id).ToArray());
            var edges=values.ToDictionary(value=>value.id,_=>new HashSet<string>(StringComparer.Ordinal),StringComparer.Ordinal);
            void Connect(string first,string second){edges[first].Add(second);edges[second].Add(first);}
            var assigned=new HashSet<string>(StringComparer.Ordinal);
            foreach(var region in regions){
                var members=region.members.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                areas.Add(region.id,members);assigned.UnionWith(members);
                if(region.Loading=="together")for(int i=1;i<members.Length;i++)Connect(members[0],members[i]);
            }
            areas.Add("",values.Where(x=>!x.IsBuiltIn&&!assigned.Contains(x.id)).Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray());
            foreach(var value in values)foreach(var link in value.connections??Array.Empty<RoomConnection>())if(link.enabled){
                // A deleted peer is unavailable, never an invented entity.
                if(edges.ContainsKey(link.connected))Connect(value.id,link.connected);else missingDependencies.Add(value.id);
            }
            var components=new List<string[]>();var visited=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in edges.Keys.OrderBy(x=>x,StringComparer.Ordinal)){
                if(visited.Contains(id))continue;
                var members=new List<string>();var pending=new Queue<string>();pending.Enqueue(id);
                while(pending.Count>0){
                    var next=pending.Dequeue();if(!visited.Add(next))continue;
                    members.Add(next);foreach(var peer in edges[next])pending.Enqueue(peer);
                }
                var group=members.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                foreach(var member in group)groupByTarget.Add(member,components.Count);
                components.Add(group);
            }
            groups=components.ToArray();
        }
        internal string[] Closure(IEnumerable<string> seeds)
        {
            var included=new HashSet<int>();foreach(var target in seeds){int group=GroupIndex(target);if(group>=0)included.Add(group);}
            if(included.Count==0)return Array.Empty<string>();
            if(included.Count==1)return (string[])groups[included.First()].Clone();
            return included.SelectMany(index=>groups[index]).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        internal IEnumerable<string[]> Groups(){foreach(var group in groups)yield return (string[])group.Clone();}
        internal string[] Members(string area)=>areas.TryGetValue(area,out var members)?(string[])members.Clone():null;
        internal bool MissingDependency(string target)=>missingDependencies.Contains(target);
    }
    /// <summary>A fresh live retention observation over immutable authored topology.
    /// Queries never share mutable demand flags or permission to unload.</summary>
    internal sealed class RoomRetentionGraph
    {
        readonly RoomRetentionTopology topology;
        readonly RoomRetentionReason[] reasons;
        internal RoomRetentionGraph(IEnumerable<RoomObjectData> objects,IEnumerable<RoomRegion> regions):this(new RoomRetentionTopology(objects,regions)){}
        internal RoomRetentionGraph(RoomRetentionTopology topology){this.topology=topology;reasons=new RoomRetentionReason[topology.GroupCount];}
        internal IEnumerable<string> Targets=>topology.Targets;
        internal bool Contains(string target)=>topology.Contains(target);
        internal bool Retain(string target,RoomRetentionReason reason){int group=topology.GroupIndex(target);if(group<0)return false;reasons[group]|=reason;return true;}
        internal void RetainAll(RoomRetentionReason reason){for(int i=0;i<reasons.Length;i++)reasons[i]|=reason;}
        internal RoomRetentionReason Reasons(string target){int group=topology.GroupIndex(target);return group>=0?reasons[group]:RoomRetentionReason.None;}
        internal string[] Closure(IEnumerable<string> targets)=>topology.Closure(targets);
        internal IEnumerable<string[]> Groups()=>topology.Groups();
        internal string[] Members(string area)=>topology.Members(area);
        internal bool MissingDependency(string target)=>topology.MissingDependency(target);
        internal static string[] Names(RoomRetentionReason flags)=>Enum.GetValues(typeof(RoomRetentionReason)).Cast<RoomRetentionReason>()
            .Where(value=>value!=RoomRetentionReason.None&&(flags&value)!=0).Select(value=>char.ToLowerInvariant(value.ToString()[0])+value.ToString().Substring(1)).ToArray();
    }
    public sealed partial class RoomJournal
    {
        // Forks may share this immutable value. Only accepted dependency edits replace
        // it; retained observations keep their old snapshot and their own live flags.
        RoomRetentionTopology retentionTopology;
        internal RoomRetentionTopology RetentionTopology=>retentionTopology??=new(items.Values,regions.Values);
        internal RoomRetentionGraph RetentionGraph()=>new(RetentionTopology);
        static bool SameRetentionEntries<T>(T[] before,T[] after,Func<T,string> identity,Func<T,T,bool> same)
        {
            if(before.Length!=after.Length)return false;if(before.Length==0)return true;
            var next=after.ToDictionary(identity,StringComparer.Ordinal);
            return before.All(value=>next.TryGetValue(identity(value),out var changed)&&same(value,changed));
        }
        static IEnumerable<string> EnabledPeers(RoomObjectData value)=>(value.connections??Array.Empty<RoomConnection>()).Where(x=>x.enabled).Select(x=>x.connected);
        void InvalidateRetentionObjects(RoomObjectData[] before,RoomObjectData[] after)
        {
            if(retentionTopology!=null&&!SameRetentionEntries(before,after,x=>x.id,(a,b)=>a.IsBuiltIn==b.IsBuiltIn&&new HashSet<string>(EnabledPeers(a),StringComparer.Ordinal).SetEquals(EnabledPeers(b))))retentionTopology=null;
        }
        void InvalidateRetentionRegions(RoomRegion[] before,RoomRegion[] after)
        {
            if(retentionTopology!=null&&!SameRetentionEntries(before,after,x=>x.id,(a,b)=>a.Loading==b.Loading&&new HashSet<string>(a.members,StringComparer.Ordinal).SetEquals(b.members)))retentionTopology=null;
        }
    }
}
