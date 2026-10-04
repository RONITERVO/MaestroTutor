// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using System.Threading;
using Maestro.Quest.Imports;
using Meta.XR.MRUtilityKit;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    // The pinned platform implementation is below; tests substitute only the OS/SDK boundary.
    internal interface IRoomSceneSource
    {
        bool Supported {get;}
        Task<bool> Permission();
        Task<bool> Scan();
        Task<bool> Load();
    }
    public sealed partial class ScannedRoom
    {
        sealed class Request
        {
            public readonly string Id=Guid.NewGuid().ToString("N");
            public bool Cancelled,PlatformDialog;
            public TaskCompletionSource<bool> Return;
        }
        IRoomSceneSource source;
        Request worker;
        int testTimeout;
        string setupId=Guid.NewGuid().ToString("N"),requestId="",phase="idle";
        bool setupPaused,setupFocused=true,geometryAccepted;
        public event Action Changed;
        public bool Busy=>worker!=null;
        public bool CanCancel=>worker!=null&&!worker.Cancelled;
        public string Status=>Busy||phase=="failed"||phase=="cancelled"?SetupStatus:world?world.Status:SetupStatus;
        public string SetupStatus {get;private set;}="Load a saved room or request a new scan";
        bool SetupActive=>isActiveAndEnabled&&!setupPaused&&setupFocused;
        internal void SetSourceForTests(IRoomSceneSource value,int timeout=0){if(timeout<0)throw new ArgumentOutOfRangeException(nameof(timeout));if(Busy)throw new InvalidOperationException("A room request is still draining");testTimeout=timeout;source=value;NotifySetup();}
        void NotifySetup(){InvalidateLayout();setupId=Guid.NewGuid().ToString("N");Changed?.Invoke();}
        bool Current(Request request)=>this&&worker==request&&!request.Cancelled&&isActiveAndEnabled&&world&&!world.RuntimeHeld&&!virtualView;
        void Phase(string value,string message){phase=value;SetupStatus=message;NotifySetup();}
        void WorldChanged(){if(world.RuntimeHeld)CancelRequest(world.RuntimeHoldReason??"Room setup cancelled by workspace change");NotifySetup();}
        void LifecycleChanged(bool leaving)
        {
            // Android permission and Meta room setup intentionally take focus. Their explicit
            // request can return, but geometry loading cannot continue in the background.
            if(leaving){if(worker?.PlatformDialog!=true)CancelRequest("Room setup interrupted; request Load again");if(world)world.SetSurfaces(false,"Check room alignment after returning");}
            if(SetupActive&&worker!=null&&Current(worker))worker.Return?.TrySetResult(true);
            NotifySetup();
        }
        internal bool CanSetup(string expected,string operation,string expectedRequest,out string error)
        {
            error=null;
            if(expected!=setupId)error="Room setup changed; read room.environment again";
            else if(!world||!world.isActiveAndEnabled||!SetupActive)error="Return to the active room before changing room setup";
            else if(world.RuntimeHeld)error=world.RuntimeHoldReason;
            else if(operation=="cancel"){
                if(worker==null||worker.Cancelled||expectedRequest!=worker.Id)error="That room setup request is no longer active";
            }
            else if(operation=="show"||operation=="hide"){
                if(virtualView&&operation=="hide")error="Room surfaces stay visible in virtual view; return to mixed reality first";
            }
            else if(operation=="load"||operation=="scan"){
                if(virtualView)error="Return to mixed reality before loading or scanning the room";
                else if(source?.Supported!=true)error="Room scanning is available on Quest";
                else if(Busy)error="A room setup request is still pending or draining";
            }
            else error="Unknown room setup operation";
            return error==null;
        }
        internal bool SetSetup(string expected,string operation,string expectedRequest,out JObject result,out string error)
        {
            result=null;if(!CanSetup(expected,operation,expectedRequest,out error))return false;
            if(operation=="show"||operation=="hide")SetShowing(operation=="show");
            else if(operation=="cancel")CancelRequest("Room setup cancelled; an open system screen must still be closed there");
            else {
                var request=new Request();worker=request;requestId=request.Id;geometryAccepted=false;world.Contains=null;
                world.PausePhysics();world.SetSurfaces(false,"Room setup requested; physics remains paused");
                Phase("permission","Waiting for room access permission");
                _=RunRequest(request,operation=="scan");
            }
            result=ObserveSetup();return true;
        }
        void ManualRequest(bool rescan)
        {
            if(!SetSetup(setupId,rescan?"scan":"load",null,out _,out var error)){SetupStatus=error;NotifySetup();}
        }
        public void CancelSetup()=>CancelRequest("Room setup cancelled; an open system screen must still be closed there");
        void SetShowing(bool value)
        {
            if(showing==value)return;showing=value;if(surfaces)surfaces.HideMesh=!(showing||virtualView);NotifySetup();
        }
        void CancelRequest(string message)
        {
            if(worker==null||worker.Cancelled)return;
            worker.Cancelled=true;worker.Return?.TrySetResult(false);geometryAccepted=false;
            if(world){world.Contains=null;world.SetSurfaces(false,message);}
            Phase("cancelled",message);
        }
        async Task<bool> Returned(Request request)
        {
            if(!Current(request))return false;
            if(SetupActive)return true;
            Phase("returning","Finish the system screen and return to Maestro");
            request.Return=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool returned=await request.Return.Task;request.Return=null;return returned&&Current(request)&&SetupActive;
        }
        async Task<bool> PlatformResult(Request request,Task<bool> task,int milliseconds)
        {
            if(task==null)throw new InvalidOperationException("Missing platform task");
            using var deadline=new CancellationTokenSource();
            if(await Task.WhenAny(task,Task.Delay(testTimeout>0?testTimeout:milliseconds,deadline.Token))!=task){
                if(Current(request)){request.Cancelled=true;request.Return?.TrySetResult(false);Phase("failed","System request timed out; waiting for it to close before another request");}
                // Observation timeout is not cancellation of an OS/SDK operation. Keep admission
                // held until the actual task retires, and consume its eventual error/result.
                try{await task;}catch{}return false;
            }
            deadline.Cancel();return await task;
        }
        async Task RunRequest(Request request,bool rescan)
        {
            try{
                await Task.Yield();if(!Current(request)||!SetupActive)return;
                request.PlatformDialog=true;
                bool granted=await PlatformResult(request,source.Permission(),120000);
                if(!Current(request))return;
                if(!granted){Phase("failed","Room access was not granted; use Load or Scan to try again");return;}
                if(!await Returned(request))return;request.PlatformDialog=false;
                if(rescan){
                    Phase("scan","Complete room setup in the Meta system screen");request.PlatformDialog=true;
                    bool scanned=await PlatformResult(request,source.Scan(),600000);
                    if(!Current(request))return;
                    if(!scanned){Phase("cancelled","Room scan cancelled; Load can use an existing saved scan");return;}
                    if(!await Returned(request))return;request.PlatformDialog=false;
                }
                Phase("loading","Loading saved room geometry; physics remains paused");
                bool loaded=await PlatformResult(request,source.Load(),120000);
                if(!Current(request)||!SetupActive)return;
                if(!loaded){Phase("failed","No room loaded; request Scan, then check the displayed room");return;}
                geometryAccepted=true;world.SetSurfaces(false,"Waiting for a tracked room with floor and wall colliders");Phase("loaded","Room data loaded; show surfaces and check floor and wall alignment");
            }catch(Exception error){if(Current(request)){Debug.LogWarning("Room scene could not load: "+error.GetType().Name);Phase("failed","Room setup failed; try Load again");}}
            finally{if(this&&worker==request){if(phase is "permission" or "scan" or "returning" or "loading")Phase("cancelled","Room setup interrupted; request Load again");worker=null;NotifySetup();ValidateRoom();}}
        }
        internal JObject ObserveSetup()
        {
            bool canLoad=CanSetup(setupId,"load",null,out var reason);
            return new JObject {["stateId"]=setupId,["requestId"]=requestId,["phase"]=phase,["busy"]=Busy,
                ["status"]=ImportObservation.Text(SetupStatus),["reason"]=ImportObservation.Text(reason),
                ["availability"]=new JObject {["supported"]=source?.Supported==true,["active"]=SetupActive,["virtualView"]=virtualView,["canLoad"]=canLoad,["canScan"]=canLoad,["canCancel"]=CanCancel},
                ["surfaces"]=new JObject {["showing"]=showing,["visible"]=showing||virtualView,["ready"]=world&&world.SurfacesReady,["physicsRunning"]=world&&world.Running}};
        }
        sealed class DeviceRoomSource:IRoomSceneSource
        {
            readonly ScannedRoom owner;
            public DeviceRoomSource(ScannedRoom value){owner=value;}
#if UNITY_ANDROID && !UNITY_EDITOR
            public bool Supported=>owner&&owner.mruk;
            public Task<bool> Permission()=>ScenePermission(retainPending:true);
            public async Task<bool> Scan()=>await OVRScene.RequestSpaceSetup();
            public async Task<bool> Load()=>owner&&owner.mruk&&await owner.mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound:false)==MRUK.LoadDeviceResult.Success;
#else
            public bool Supported=>false;
            public Task<bool> Permission()=>Task.FromResult(false);
            public Task<bool> Scan()=>Task.FromResult(false);
            public Task<bool> Load()=>Task.FromResult(false);
#endif
        }
    }
}
