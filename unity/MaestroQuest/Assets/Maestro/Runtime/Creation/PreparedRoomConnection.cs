// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Reserve a potential joint before accepting an enabled connection. Actual
    // PhysX creation still waits for aligned, owned, grounded native members.
    internal sealed class PreparedRoomConnection:IDisposable
    {
        readonly string target,encoded;readonly RoomConnectionView previous;
        internal readonly NativePhysicsResources.Lease Lease;
        readonly bool created;bool ended;
        internal static string Encode(RoomObjectData data)=>string.Join("|",data.connections.Select(JsonUtility.ToJson));
        internal PreparedRoomConnection(RoomWorldIdentity world,RoomObjectData data,RoomConnectionView view){
            target=data.id;encoded=Encode(data);previous=view;
            if(data.connections.Length==0||!data.connections[0].enabled)return;
            Lease=view?view.ReusableReservation:null;
            if(Lease==null){Lease=NativePhysicsResources.Connection(new RoomResourceOwner(world,target,"physics"));created=true;}
        }
        internal void Validate(RoomObjectData data){if(ended||data.id!=target||encoded!=Encode(data)||Lease!=null&&!Lease.Live)throw new InvalidOperationException("Connection source changed or preparation has ended");}
        internal NativePhysicsResources.Lease Take(RoomConnectionView view,RoomObjectData data){Validate(data);if(!view||previous&&previous!=view)throw new InvalidOperationException("Connection owner changed after preparation");if(created)Lease.BindJointHost(view);ended=true;return Lease;}
        public void Dispose(){if(ended)return;ended=true;if(created)Lease.Dispose();}
    }
}
