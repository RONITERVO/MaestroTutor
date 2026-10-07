// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Per-workspace navigation bookmark. Tracking and real-room alignment
    /// remain physical; only the authored content frame is restored.</summary>
    internal sealed class WorkspaceViewpoint:MonoBehaviour
    {
        RoomEditor editor;
        VirtualRoomView view;
        Func<bool> tracked;
        RoomViewpoint pending;
        IDisposable restoring;
        bool recording,paused,focused=true;
        string lastError;float sampleAt;
        internal bool Pending=>pending!=null;
        internal void Initialize(RoomEditor owner,VirtualRoomView roomView,Func<bool> headTracked)
        {
            editor=owner;view=roomView;tracked=headTracked;view.Moved+=OnMoved;
            var saved=editor.Viewpoint;
            if(saved.active){pending=saved;recording=true;restoring=editor.RuntimeGate.Hold("Waiting for tracked view to restore your world location");}
            Tick();
        }
        bool HeadReady=>isActiveAndEnabled&&!paused&&focused&&tracked?.Invoke()==true;
        internal void Tick()
        {
            if(!HeadReady||!editor||!view)return;
            if(pending!=null){
                if(!view.RestoreViewpoint(pending,out var error)){
                    if(error!=lastError){lastError=error;editor.ReportStatus(error);}return;
                }
                pending=null;restoring?.Dispose();restoring=null;
                editor.ReportStatus("Saved world location restored; real-room alignment remains separate");
            }
            if(Time.unscaledTime>=sampleAt){sampleAt=Time.unscaledTime+1;Capture();}
        }
        void OnMoved(){if(HeadReady&&!recording){recording=true;Capture();}}
        internal void Capture()
        {
            if(!recording||Pending||!HeadReady||!editor||!view||editor.RuntimeGate.Held||editor.WriteGate.Frozen)return;
            if(view.ReadViewpoint(out var value))editor.RememberViewpoint(value);
        }
        void Update()=>Tick();
        void OnApplicationPause(bool value){if(value)Capture();paused=value;}
        void OnApplicationFocus(bool value){if(!value)Capture();focused=value;}
        void OnDestroy(){if(view)view.Moved-=OnMoved;restoring?.Dispose();restoring=null;}
    }
}
