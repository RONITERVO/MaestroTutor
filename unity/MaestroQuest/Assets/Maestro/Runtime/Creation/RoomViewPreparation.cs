// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal const float ViewPreparationTimeout=120;
        internal VirtualRoomCapture BeginViewCapture(out string error,Func<bool> admission=null){
            if(!CanCaptureView(out error))return null;
            var source=journal;var viewer=Viewer;int generation=viewGeneration,revision=Revision;
            bool Current()=>this&&ViewSourceAvailable&&Viewer==viewer&&Frame.Valid&&
                RoomRecipe.Finite(viewer.position)&&RoomRecipe.Finite(viewer.forward)&&
                ReferenceEquals(journal,source)&&generation==viewGeneration&&revision==Revision&&(admission?.Invoke()??true);
            capturingView=true;nextViewCapture=Time.unscaledTime+1;
            return new VirtualRoomCapture(Current,token=>PrepareViewCapture(Current,token));
        }
        async Task<JObject> PrepareViewCapture(Func<bool> current,CancellationToken token){
            Camera camera=null;IDisposable observation=null;Task<string> load=null;
            double deadline=Time.realtimeSinceStartupAsDouble+ViewPreparationTimeout;
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);
            void GateChanged(){if(RuntimeGate.Held)cancellation.Cancel();}
            RuntimeGate.Changed+=GateChanged;
            bool Current()=>!cancellation.IsCancellationRequested&&current()&&Time.realtimeSinceStartupAsDouble<deadline;
            void Check(){
                if(Time.realtimeSinceStartupAsDouble>=deadline)throw new InvalidOperationException("Preparing the virtual room view timed out; inspect object loading and request a new view");
                if(!Current())throw new OperationCanceledException();
            }
            try{
                Check();camera=CreateViewCamera();observation=ObserveNativeCamera(camera,sourceLayers:-1);
                while(true){
                    Check();camera.transform.SetPositionAndRotation(Viewer.position,Viewer.rotation);
                    var missing=MissingViewEntities(camera);
                    if(missing.Length>0){
                        // Share the display/action lane and connected closure. A
                        // second observer cannot create a second importer budget.
                        load=AcquireNativeEntities(missing,Current,cancellation.Token,displayDuringHold:true);
                        while(!load.IsCompleted){
                            Check();camera.transform.SetPositionAndRotation(Viewer.position,Viewer.rotation);await Task.Yield();
                        }
                        string issue=await load;load=null;Check();if(issue!=null)throw new InvalidOperationException(issue);
                        continue; // The viewer may have turned toward another area.
                    }
                    if(ViewModelsLoading(camera)||LightingViewLoading(camera)){await Task.Yield();continue;}
                    Check();if(!RenderCameraView(camera,out var image,out var error))throw new InvalidOperationException(error);
                    return image;
                }
            }finally{
                cancellation.Cancel();
                if(load!=null)try{await load;}catch(Exception){}
                RuntimeGate.Changed-=GateChanged;observation?.Dispose();
                if(camera)Destroy(camera.gameObject);capturingView=false;
            }
        }
        string[] MissingViewEntities(Camera camera){
            if(!Frame.Valid)throw new InvalidOperationException("The room coordinate frame is unavailable");
            var view=new RoomObservationView(camera,sourceLayers:-1);var missing=new List<string>();
            var lighting=LightViewDependencies(new[]{view});
            if(!view.Reliable)throw new InvalidOperationException("The virtual camera projection is unavailable");
            foreach(var pair in dormantNative){
                if(pair.Key=="book")continue;
                var bounds=ReadDormantSpatial(pair.Key,pair.Value,out _,out var prepared,out var placed);
                // Missing scan anchors intentionally hide anchored ink in the
                // viewer too. Unknown geometry remains a capture dependency.
                if(prepared&&!placed)continue;
                if(lighting.Contains(pair.Key)||view.Includes(bounds.Transform(transform.localToWorldMatrix)))missing.Add(pair.Key);
            }
            return missing.ToArray();
        }
        bool ViewModelsLoading(Camera camera){
            var view=new RoomObservationView(camera,sourceLayers:-1);
            foreach(var pair in objects){
                if(pair.Key=="book"||!pair.Value||!pair.Value.isActiveAndEnabled)continue;
                var item=pair.Value;var created=item.GetComponent<CreatedRoomObject>();var avatar=item.GetComponent<Avatar.MaestroAvatar>();
                if(!(avatar&&avatar.ModelBusy)&&!(created&&regionModels.ContainsKey(pair.Key)&&!created.ModelGeometryReady))continue;
                if(!view.Includes(ReadSpatialBounds(pair.Key,out _).Transform(transform.localToWorldMatrix)))continue;
                if(created&&!created.ModelLoading&&!string.IsNullOrEmpty(created.ModelGeometryIssue))throw new InvalidOperationException("A visible model could not load: "+created.ModelGeometryIssue);
                return true;
            }
            return false;
        }
    }
}
