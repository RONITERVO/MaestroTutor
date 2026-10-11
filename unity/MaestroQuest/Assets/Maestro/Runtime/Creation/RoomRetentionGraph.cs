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
        Navigation=512, WaterRoute=1024
    }
    /// <summary>Ephemeral dependency closure, never authored state or permission to unload.
    /// Area members activate together; enabled native connections retain both ends.
    /// A structure's remembered arrangement is not a physical connection.</summary>
    internal sealed class RoomRetentionGraph
    {
        readonly Dictionary<string,HashSet<string>> edges=new(StringComparer.Ordinal);
        readonly Dictionary<string,RoomRetentionReason> reasons=new(StringComparer.Ordinal);
        readonly Dictionary<string,string[]> areas=new(StringComparer.Ordinal);
        readonly HashSet<string> missingDependencies=new(StringComparer.Ordinal);
        internal IEnumerable<string> Targets=>edges.Keys;
        internal bool Contains(string target)=>target!=null&&edges.ContainsKey(target);
        internal RoomRetentionGraph(IEnumerable<RoomObjectData> objects,IEnumerable<RoomRegion> regions)
        {
            // Copy only dependency identities. Recipes, images, strokes and motion samples
            // stay in the canonical journal; reporting demand must not duplicate them.
            var values=objects.ToArray();
            foreach(var value in values){edges.Add(value.id,new(StringComparer.Ordinal));reasons.Add(value.id,RoomRetentionReason.None);}
            var assigned=new HashSet<string>(StringComparer.Ordinal);
            foreach(var region in regions){
                var members=region.members.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                areas.Add(region.id,members);foreach(var id in members)assigned.Add(id);ConnectArea(members);
            }
            var home=values.Where(x=>!x.IsBuiltIn&&!assigned.Contains(x.id)).Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            areas.Add("",home);ConnectArea(home);
            foreach(var value in values)foreach(var link in value.connections??Array.Empty<RoomConnection>())if(link.enabled){
                // Deleted connection peers remain meaningful saved references. They are
                // unavailable dependencies, never silently recreated runtime objects.
                if(Contains(link.connected))Connect(value.id,link.connected);else missingDependencies.Add(value.id);
            }
        }
        void ConnectArea(string[] members){for(int i=1;i<members.Length;i++)Connect(members[0],members[i]);}
        void Connect(string first,string second){edges[first].Add(second);edges[second].Add(first);}
        internal bool Retain(string target,RoomRetentionReason reason)
        {
            if(!Contains(target))return false;
            var pending=new Queue<string>();pending.Enqueue(target);
            while(pending.Count>0){
                string id=pending.Dequeue();var combined=reasons[id]|reason;
                if(combined==reasons[id])continue;
                reasons[id]=combined;foreach(var next in edges[id])pending.Enqueue(next);
            }
            return true;
        }
        internal string[] Closure(IEnumerable<string> targets)
        {
            var found=new HashSet<string>(StringComparer.Ordinal);var pending=new Queue<string>(targets);
            while(pending.Count>0){string id=pending.Dequeue();if(!Contains(id)||!found.Add(id))continue;foreach(var next in edges[id])pending.Enqueue(next);}
            return found.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        internal void RetainAll(RoomRetentionReason reason){foreach(var id in edges.Keys)Retain(id,reason);}
        internal RoomRetentionReason Reasons(string target)=>reasons.TryGetValue(target,out var value)?value:RoomRetentionReason.None;
        internal string[] Members(string area)=>areas.TryGetValue(area,out var members)?(string[])members.Clone():null;
        internal bool MissingDependency(string target)=>missingDependencies.Contains(target);
        internal static string[] Names(RoomRetentionReason flags)=>Enum.GetValues(typeof(RoomRetentionReason)).Cast<RoomRetentionReason>()
            .Where(value=>value!=RoomRetentionReason.None&&(flags&value)!=0).Select(value=>char.ToLowerInvariant(value.ToString()[0])+value.ToString().Substring(1)).ToArray();
    }
    public sealed partial class RoomJournal
    {
        internal RoomRetentionGraph RetentionGraph()=>new(items.Values,regions.Values);
    }
}
