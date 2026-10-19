// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        readonly RoomGroundQuery physicalFootGround=new(),virtualFootGround=new();
        int footGroundFrame=-1,footGroundRevision=-1;
        internal bool SampleFootGround(RoomItem actor,Vector3 expected,float band,out RaycastHit hit)
        {
            hit=default;
            if(!actor||!PhysicsWorld||!PhysicsWorld.SimulationActive||RuntimeGate.Held||
                !Frame.Valid||Mathf.Abs(Frame.MetresPerUnit-1)>.0001f||Vector3.Dot(transform.up,Vector3.up)<.99999f||
                !PhysicsWorld.EnvironmentReady(actor)||!GroundComplete(RoomGroundQuery.SupportEnvelope(expected,.025f,band,band)))return false;
            if(footGroundFrame!=Time.frameCount||footGroundRevision!=Revision){
                footGroundFrame=Time.frameCount;footGroundRevision=Revision;Physics.SyncTransforms();
                physicalFootGround.Capture(PhysicsWorld.transform,true);virtualFootGround.Capture(PhysicsWorld.transform,false);
            }
            return (PhysicsWorld.IncludesRealRoom(actor)?physicalFootGround:virtualFootGround).Sample(expected,band,band,out hit);
        }
    }
}
