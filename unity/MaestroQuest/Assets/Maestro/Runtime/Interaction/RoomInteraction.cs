// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    /// <summary>Tracks movable content and explicitly designated recovery tools. Recall never moves the world.</summary>
    public sealed class RoomInteraction : MonoBehaviour
    {
        public Transform Viewer;
        Maestro.Quest.Persistence.WorkspaceWriteGate writes;
        internal RoomEditor RecoveryEditor;
        internal void ConfigureWrites(Maestro.Quest.Persistence.WorkspaceWriteGate gate){writes=gate;}
        internal void DetachWrites(Maestro.Quest.Persistence.WorkspaceWriteGate gate){if(ReferenceEquals(writes,gate)){writes=null;RecoveryEditor=null;}}
        readonly List<RoomItem> items = new();
        readonly Dictionary<RoomItem,Home> tools = new();
        internal int RegisteredCount=>items.Count;
        readonly struct Home
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float Scale;
            public Home(Vector3 position,Quaternion rotation,float scale){Position=position;Rotation=rotation;Scale=scale;}
        }
        internal readonly struct RecoveryPlacement
        {
            public readonly RoomItem Item;
            public readonly Vector3 Position,LocalPosition;
            public readonly Quaternion Rotation,LocalRotation;
            public readonly float Scale,LocalScale;
            public RecoveryPlacement(RoomItem item,Vector3 position,Quaternion rotation,float scale,RoomFrame parent){
                Item=item;Position=position;Rotation=rotation;Scale=scale;
                LocalPosition=parent.PointToRoom(position);LocalRotation=parent.RotationToRoom(rotation);LocalScale=scale/parent.MetresPerUnit;
            }
        }
        public void Register(RoomItem item,bool recoveryTool=false)
        {
            if(!item)return;
            if(!items.Contains(item))items.Add(item);
            // Capture the shipped tool layout once, before saved book placement is applied.
            // Ordinary objects, avatars and construction handles never opt in.
            if(recoveryTool&&!tools.ContainsKey(item)){
                var frame=new RoomFrame(transform);
                if(!frame.Read(item.transform,out var p,out var r,out var s))throw new System.ArgumentException("A recovery tool needs a uniform room frame");
                tools.Add(item,new Home(p,r,s));
            }
        }
        public void Unregister(RoomItem item){items.Remove(item);tools.Remove(item);}
        internal bool PlanRecovery(out List<RecoveryPlacement> plan,out string error)
        {
            plan=null;error="Book and tool recovery needs a valid tracked view";
            if(!Viewer||!RoomRecipe.Finite(Viewer.position)||!MotionFrame.ValidRotation(Viewer.rotation))return false;
            if(writes?.Frozen==true){error="Finish the workspace operation before recalling tools";return false;}
            var forward=Vector3.ProjectOnPlane(Viewer.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Viewer.up,Vector3.up);
            if(forward.sqrMagnitude<.01f)return false;
            var heading=Quaternion.LookRotation(forward.normalized,Vector3.up);
            var origin=Viewer.position-Vector3.up*1.55f;
            var candidates=new List<RecoveryPlacement>();
            foreach(var pair in tools){
                var item=pair.Key;if(!item)continue;
                if(item.PoseLocked||item.Grab&&item.Grab.isSelected){error="Release the book and tool trays before recalling them";return false;}
                var parent=new RoomFrame(item.transform.parent);
                if(!parent.Read(item.transform,out _,out _,out _)){error="A recovery tool has an unsupported coordinate frame";return false;}
                var home=pair.Value;
                candidates.Add(new RecoveryPlacement(item,origin+heading*home.Position,heading*home.Rotation,home.Scale,parent));
            }
            if(candidates.Count==0){error="No recovery tools are available";return false;}
            plan=candidates;error=null;return true;
        }
        internal static void ApplyRecovery(IEnumerable<RecoveryPlacement> plan)
        {
            foreach(var pose in plan){
                pose.Item.transform.SetLocalPositionAndRotation(pose.LocalPosition,pose.LocalRotation);
                pose.Item.transform.localScale=Vector3.one*pose.LocalScale;
                pose.Item.GetComponent<RigidRoomItem>()?.Teleported();
            }
            Physics.SyncTransforms();
        }
        public void RestoreInFrontOfViewer()
        {
            if(RecoveryEditor){
                if(!RecoveryEditor.RecallTools(null,0,true,out _,out var error))RecoveryEditor.ReportStatus(error);
                return;
            }
            using var write=writes?.TryWrite(out _);if(writes!=null&&write==null)return;
            if(PlanRecovery(out var plan,out _))ApplyRecovery(plan);
        }
    }
}
