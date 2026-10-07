// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Editor
{
    // Explicit Editor fixture only. Replaces the platform boundary, never the
    // catalog, request state machine, planner, placement, saves or receipts.
    internal sealed class QuestRoomProbeScan:IRoomSceneSource,IRoomLayoutSource
    {
        readonly RoomPhysicsWorld world;readonly ScannedRoom scan;
        readonly string roomId=Guid.NewGuid().ToString("N"),floorId=Guid.NewGuid().ToString("N");
        public int Loads,Scans;
        public QuestRoomProbeScan(GameObject root)
        {
            world=root.GetComponent<RoomPhysicsWorld>();scan=root.GetComponent<ScannedRoom>();
            if(!world||!scan)throw new InvalidOperationException("Native room services must exist before installing the synthetic scan fixture.");
            scan.SetSourceForTests(this);scan.SetLayoutSourceForTests(this);world.PausePhysics();world.SetSurfaces(false,"Synthetic scan is initially unloaded");
        }
        public bool Supported=>true;
        public Task<bool> Permission()=>Task.FromResult(true);
        public Task<bool> Scan(){Scans++;return Task.FromResult(false);}
        public Task<bool> Load(){Loads++;return Task.FromResult(true);}
        public void Tick()
        {
            if(Loads>0&&!scan.Busy&&(string)scan.ObserveSetup()["phase"]=="loaded"&&!world.SurfacesReady)
                world.SetSurfaces(true,"Synthetic floor ready; no physical alignment proof");
        }
        public bool TryRead(Transform frame,out string room,out ScannedSurface[] entries,out int omitted,out string reason)
        {
            room=roomId;omitted=0;reason=null;
            // Same physical floor as QuestRoomProbe's synthetic collider, expressed
            // in the actual room frame. No implicit floor-height assumption by the agent.
            entries=new[]{new ScannedSurface{Id=floorId,Label="FLOOR",Position=frame.InverseTransformPoint(Vector3.zero),Rotation=Quaternion.Inverse(frame.rotation)*Quaternion.Euler(-90,0,0),Plane=new Rect(-10,-10,20,20)}};
            return Loads>0;
        }
    }
}
