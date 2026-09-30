// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Interaction
{
    /// <summary>Native lifetime holds compose independently of app focus. Releasing a hold
    /// permits future intent; consumers cancel current effects and never replay them.</summary>
    public sealed class RoomRuntimeGate
    {
        sealed class Lease:IDisposable
        {
            RoomRuntimeGate owner;
            internal readonly string Reason;
            internal Lease(RoomRuntimeGate owner,string reason){this.owner=owner;Reason=reason;}
            public void Dispose(){var previous=owner;owner=null;previous?.Release(this);}
        }
        readonly List<Lease> holds=new();
        string failure;
        public bool Held=>failure!=null||holds.Count>0;
        public string Reason=>failure??(holds.Count>0?holds[0].Reason:null);
        public event Action Changed;
        // Native coordinators own the lease. Neither source programs nor wire arguments can clear it.
        internal IDisposable Hold(string reason)
        {
            if(string.IsNullOrWhiteSpace(reason)||reason.Length>120||reason.Any(char.IsControl))throw new ArgumentException("Use a short hold reason.");
            if(holds.Count>=16)throw new InvalidOperationException("Too many workspace lifetime holds.");
            var lease=new Lease(this,reason);holds.Add(lease);Notify();return lease;
        }
        void Release(Lease lease){if(holds.Remove(lease))Notify();}
        void Notify()
        {
            var listeners=Changed?.GetInvocationList();if(listeners==null)return;
            bool wasHeld=Held;
            foreach(Action listener in listeners)try{listener();}catch(Exception){failure="Workspace activity cleanup failed. Reopen the room before starting actions.";}
            // A failed release must also re-hold owners notified before that failure.
            if(!wasHeld&&Held)foreach(Action listener in listeners)try{listener();}catch(Exception){}
        }
    }
}
