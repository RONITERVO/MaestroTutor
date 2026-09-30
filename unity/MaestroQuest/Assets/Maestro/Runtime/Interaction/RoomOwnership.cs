// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Programs;
namespace Maestro.Quest.Interaction
{
    // These roles are assigned by trusted native entry points, never program arguments.
    public enum RoomActorRole { Ambient=0, Program=10, Reflex=20, Control=30, Grab=40 }
    [Serializable] public sealed class RoomOwnerClaim { public string target,channel; }
    [Serializable] public sealed class RoomOwnerView { public string id,label,role; public bool allowsGrab; public RoomOwnerClaim[] claims; }
    [Serializable] public sealed class RoomOwnershipView { public bool suspended; public string error; public RoomOwnerView[] owners; }
    /// <summary>One all-or-nothing channel arbiter for programs, manual tools and ambient actors.</summary>
    public sealed class RoomOwnership
    {
        public const int MaximumOwners=64,MaximumClaims=128;
        public readonly struct Interruption {
            public readonly string By,Label;public readonly bool PreservePlacement;
            public string Message=>"Interrupted by "+Label;
            public Interruption(string by,string label,bool preserve) {By=by;Label=label;PreservePlacement=preserve;}
        }
        public sealed class Lease : IDisposable {
            readonly RoomOwnership service;
            internal readonly Action<Interruption> Interrupted;
            internal readonly BehaviourCatalog.Claim[] Claims;
            public string Id {get;} public string Label {get;} public RoomActorRole Role {get;}
            public bool Held {get;internal set;}=true;
            public string EndReason {get;internal set;}
            public bool AllowsGrab {get;internal set;}
            public bool SetAllowsGrab(bool value)=>service.SetAllowsGrab(this,value);
            internal Lease(RoomOwnership service,string id,string label,RoomActorRole role,BehaviourCatalog.Claim[] claims,Action<Interruption> interrupted) {
                this.service=service;Id=id;Label=label;Role=role;Claims=claims;Interrupted=interrupted;
            }
            public void Dispose()=>service.Release(this);
        }
        readonly Dictionary<string,Lease> owners=new();bool changing;
        public bool Suspended {get;private set;}
        public string Error {get;private set;}
        static bool Cooperates(Lease owner,RoomActorRole incoming)=>incoming==RoomActorRole.Grab&&owner.Role==RoomActorRole.Control&&owner.AllowsGrab;
        static bool Conflict(Lease owner,IEnumerable<BehaviourCatalog.Claim> claims)=>claims.Any(a=>owner.Claims.Any(a.Conflicts));
        static bool Text(string value,int max)=>!string.IsNullOrEmpty(value)&&value.Length<=max&&!value.Any(c=>c<32);
        static bool Valid(string id,RoomActorRole role,IReadOnlyList<BehaviourCatalog.Claim> claims)=>
            Text(id,128)&&Enum.IsDefined(typeof(RoomActorRole),role)&&claims!=null&&claims.Count<=MaximumClaims&&
            claims.All(c=>Text(c.Target,128)&&Text(c.Channel,64));
        public bool CanAcquire(string id,RoomActorRole role,IReadOnlyList<BehaviourCatalog.Claim> claims,out string error,bool replaceControl=false) {
            error=null;if(!Valid(id,role,claims)){error="Invalid room ownership request";return false;}
            if(Error!=null||Suspended||changing){error=Error??(Suspended?"Room actions are paused":"Room ownership is changing");return false;}
            var blocked=owners.Values.FirstOrDefault(x=>x.Id!=id&&Conflict(x,claims)&&!Cooperates(x,role)&&(x.Role>role||x.Role==role&&!(replaceControl&&role==RoomActorRole.Control)));
            if(blocked!=null){error=blocked.Label+" owns a required channel or object";return false;}
            if(!owners.ContainsKey(id)&&owners.Count-owners.Values.Count(x=>Conflict(x,claims)&&!Cooperates(x,role))>=MaximumOwners){error="Room ownership capacity reached";return false;}
            return true;
        }
        public bool Covers(string id,IReadOnlyList<BehaviourCatalog.Claim> claims)=>owners.TryGetValue(id,out var owner)&&
            claims.All(c=>owner.Claims.Any(x=>x.Target==c.Target&&(x.Channel==c.Channel||x.Channel=="wholeTarget")));
        public bool TryAcquire(string id,string label,RoomActorRole role,IReadOnlyList<BehaviourCatalog.Claim> claims,Action<Interruption> interrupted,
            out Lease lease,out string error,bool preservePlacement=false,bool replaceControl=false,bool allowsGrab=false) {
            lease=null;if(!CanAcquire(id,role,claims,out error,replaceControl))return false;
            if(owners.ContainsKey(id)){error="This room owner is already registered";return false;}
            if(!Text(label,120)||allowsGrab&&role!=RoomActorRole.Control){error="Invalid room owner label";return false;}
            var exact=claims.Distinct().ToArray();var displaced=owners.Values.Where(x=>Conflict(x,exact)&&!Cooperates(x,role)).ToArray();
            var incoming=new Lease(this,id,label,role,exact,interrupted){AllowsGrab=allowsGrab};
            // Publish the complete replacement before callbacks. A callback cannot
            // reacquire a half-released set or expose a momentary empty claim table.
            changing=true;
            try {
                foreach(var old in displaced){owners.Remove(old.Id);old.Held=false;old.EndReason="Interrupted by "+label;}
                owners.Add(id,incoming);
                var notice=new Interruption(id,label,preservePlacement);
                Notify(displaced,notice);
                if(Error!=null){incoming.Dispose();Suspend(true);error=Error;return false;}
                if(!incoming.Held){error="Room ownership changed during takeover";return false;}
                lease=incoming;return true;
            } catch {incoming.Dispose();throw;}
            finally {changing=false;}
        }
        bool SetAllowsGrab(Lease lease,bool value) {
            if(!lease.Held||lease.Role!=RoomActorRole.Control||changing)return false;
            if(!value&&owners.Values.Any(x=>x.Role==RoomActorRole.Grab&&Conflict(x,lease.Claims)))return false;
            lease.AllowsGrab=value;return true;
        }
        void Notify(Lease[] previous,Interruption notice) {
            // Every displaced actor must receive cleanup even if another callback fails.
            foreach(var owner in previous)try {owner.Interrupted?.Invoke(notice);}
            catch(Exception) {Error="Room actions stopped after an ownership cleanup failure; reopen the room";}
        }
        void Release(Lease lease) {
            if(owners.TryGetValue(lease.Id,out var current)&&ReferenceEquals(current,lease))owners.Remove(lease.Id);
            lease.Held=false;
        }
        public void Suspend(bool value) {
            if(changing&&!value)return;
            Suspended=value;if(!value)return;
            bool wasChanging=changing;changing=true;
            try {
                var previous=owners.Values.ToArray();owners.Clear();
                foreach(var owner in previous){owner.Held=false;owner.EndReason="Room actions paused";}
                Notify(previous,new Interruption("lifecycle","room pause",false));
            } finally {changing=wasChanging;}
        }
        public RoomOwnershipView Observe()=>new() {suspended=Suspended,error=Error??"",owners=owners.Values.Select(x=>new RoomOwnerView {
            id=x.Id,label=x.Label,role=x.Role.ToString().ToLowerInvariant(),allowsGrab=x.AllowsGrab,claims=x.Claims.Select(c=>new RoomOwnerClaim {target=c.Target,channel=c.Channel}).ToArray()
        }).ToArray()};
    }
}
