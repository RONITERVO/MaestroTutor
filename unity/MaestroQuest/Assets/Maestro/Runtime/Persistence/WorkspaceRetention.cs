// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    internal sealed class WorkspaceRetention
    {
        readonly WorkspaceHost host;readonly WorkspaceGenerationStore store;Job latest;JObject inventory;
        string inspectionId="";bool disposed,paused,focused=true;internal Action<string> Fault;
        internal sealed class Job
        {
            internal readonly string Id;internal Job(string id){Id=id??Guid.NewGuid().ToString("N");}internal string Mode,Phase,Error;internal bool Pending;
            internal JObject Source;
            internal JObject Result;internal Task<Outcome> Worker;internal CancellationTokenSource Cancellation;
        }
        internal sealed class Outcome {internal JObject Result;internal JObject Inventory;internal string Error;internal bool Cancelled;}
        internal WorkspaceRetention(WorkspaceHost host,string applicationData){this.host=host;store=new WorkspaceGenerationStore(applicationData,p=>Fault?.Invoke(p));}
        internal bool Busy=>latest?.Pending==true;
        internal bool Ready(out string error)
        {
            error=null;if(disposed||paused||!focused||!host||!host.isActiveAndEnabled||!host.Ready||host.Retiring){error="Resume Maestro and wait for previous owners to finish.";return false;}
            if(Busy||host.Switching||host.Activation.Busy||host.Review.Busy||host.Recovery.BlocksOtherOperations||host.History.Busy||host.Evidence.Busy||host.Import.Occupied||host.Export.Busy){error="Finish or cancel the current workspace operation first.";return false;}return true;
        }
        JObject Find(JObject args)=>(string)args["inspectionId"]==inspectionId?(inventory?["entries"] as JArray)?.OfType<JObject>().FirstOrDefault(e=>(string)e["generationId"]==(string)args["generationId"]&&(string)e["originalManifestHash"]==(string)args["originalManifestHash"]):null;
        internal bool CanStart(string mode,JObject args,out string error)
        {
            if(!Ready(out error))return false;if(mode=="inspect")return true;
            var source=Find(args);if(mode!="export"||source==null||(bool)source["metadataAvailable"]!=true||(bool?)inventory?["selectionReadable"]!=true){error="Inspect an available retained workspace before exporting.";return false;}
            if((string)source["role"]=="active"||(string)source["generationId"]==host.Selection?.Active.Generation){error="Use workspace.archive.export for the live workspace.";return false;}
            return host.Export.TryPublisher(out _,out _,out error);
        }
        internal Job Start(string mode,JObject args,string runId=null)
        {
            if(!CanStart(mode,args,out var error))throw new InvalidOperationException(error);var source=Find(args);string cache=null;Func<string,string> publish=null;
            if(mode=="export"&&!host.Export.TryPublisher(out cache,out publish,out error))throw new InvalidOperationException(error);
            string origin=(string)inventory?["originHash"],live=host.Selection?.Active.Generation;
            var job=new Job(runId) {Source=source,Mode=mode,Phase=mode=="inspect"?"inspecting":"exporting",Pending=true,Cancellation=new CancellationTokenSource()};latest=job;
            if(mode=="inspect"){inventory=null;inspectionId="";}var token=job.Cancellation.Token;
            job.Worker=Task.Run(()=>{
                try {
                    Fault?.Invoke("retention.worker");token.ThrowIfCancellationRequested();
                    if(mode=="inspect"){
                        var found=store.InspectRetention(token);return new Outcome {Inventory=found,Result=new JObject {["inspectionId"]=job.Id,["count"]=((JArray)found["entries"]).Count,["selectionReadable"]=found["selectionReadable"].DeepClone()}};
                    }
                    using var bundle=store.ExportRetained(origin,(string)source["generationId"],(string)source["originalManifestHash"],live,cache,token);Fault?.Invoke("retention.beforePublish");token.ThrowIfCancellationRequested();string location=publish(bundle.Path);
                    if(!WorkspaceExport.ValidLocation(location))throw new System.IO.IOException();
                    return new Outcome {Result=new JObject {["generationId"]=source["generationId"].DeepClone(),["originalManifestHash"]=source["originalManifestHash"].DeepClone(),["manifestHash"]=bundle.Receipt.ManifestHash,["sourceFingerprint"]=bundle.SourceFingerprint,["location"]=location,["archiveHash"]=bundle.ArchiveHash,["sizeKiB"]=bundle.Bytes/1024d,["excludedFiles"]=bundle.ExcludedFiles,["missingModels"]=bundle.Receipt.Summary.MissingModels.Length,["missingMotions"]=bundle.Receipt.Summary.MissingMotions.Length,["missingControllerPrograms"]=bundle.Receipt.Summary.MissingControllerPrograms.Length,["unavailablePrograms"]=bundle.Receipt.Summary.UnavailablePrograms}};
                }catch(OperationCanceledException){return new Outcome {Cancelled=true,Error="Retained export cancelled before publication. Check Downloads after an interrupted publication."};}
                catch(Exception){return new Outcome {Error="Retained content could not be verified or published. Files are preserved. Inspect again; check Downloads after uncertain publication."};}
            });return job;
        }
        internal void Poll()
        {
            var job=latest;if(disposed||job?.Pending!=true||!job.Worker.IsCompleted)return;var result=job.Worker.GetAwaiter().GetResult();job.Error=result.Error;job.Result=result.Result;
            job.Phase=result.Error!=null?result.Cancelled?"cancelled":"failed":job.Mode=="inspect"?"inspected":"exported";
            if(result.Inventory!=null){inventory=result.Inventory;inspectionId=job.Id;}
            job.Cancellation.Dispose();job.Cancellation=null;job.Pending=false;
        }
        internal JObject Entry(string request,int index)=>request==inspectionId&&inventory?["entries"] is JArray entries&&index>=0&&index<entries.Count?(JObject)entries[index].DeepClone():null;
        internal JObject Status()=>new() {["requestId"]=latest?.Id??"",["phase"]=latest?.Phase??"idle",["status"]=latest?.Error??"Portable exports preserve saved content; private recovery material remains on this device.",["inspectionId"]=inspectionId,["generationId"]=(string)latest?.Source?["generationId"]??"",["manifestHash"]=(string)latest?.Result?["manifestHash"]??"",["location"]=(string)latest?.Result?["location"]??"",["archiveHash"]=(string)latest?.Result?["archiveHash"]??""};
        internal void Cancel(Job job){if(job?.Pending==true)job.Cancellation?.Cancel();}
        internal void Pause(bool value){paused=value;if(value)Cancel(latest);}
        internal void Focus(bool value){focused=value;if(!value)Cancel(latest);}
        internal void Disable()=>Cancel(latest);
        internal async Task Dispose(){disposed=true;Cancel(latest);if(latest?.Worker!=null)await latest.Worker;if(latest!=null){latest.Cancellation?.Dispose();latest.Cancellation=null;latest.Pending=false;}}
    }
}
