// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using UnityEngine;
namespace Maestro.AudioEvaluation
{
    // Main-thread player entry for the real application lease and native adapter.
    // DSP is measured in memory; physical/device audio acceptance is separate.
    internal static class PcmLeasePlayerProbe
    {
        internal static async Task<NativePlayerProbe.Result> Run()
        {
            var result=new NativePlayerProbe.Result {status="failed",voices=1};
            int opened=PreparedGraph.Opened,closed=PreparedGraph.Closed,meshes=PreparedGraph.MeshesOpened,
                meshesClosed=PreparedGraph.MeshesClosed,instances=PreparedGraph.InstancesOpened,
                instancesClosed=PreparedGraph.InstancesClosed,diagnostics=PreparedGraph.Diagnostics;
            var owner=new OwnedLifecycle();var timer=Stopwatch.StartNew();
            GameObject scope=null;PcmLeaseAdapter adapter=null;PcmOutputLease lease=null;
            void Check(bool condition,string issue){if(!condition)throw new InvalidOperationException(issue);}
            void Tick(){owner.Tick();if(owner.Failure!=null)throw owner.Failure;if(timer.Elapsed.TotalSeconds>20)throw new TimeoutException("Application lease probe deadline");}
            try{
                var format=AudioSettings.GetConfiguration();owner.Configure(format.sampleRate,format.dspBufferSize);
                while(owner.Active==null){Tick();await Task.Yield();}
                scope=new GameObject("Application lease player probe");adapter=new PcmLeaseAdapter(scope.AddComponent<PcmOutputBackend>(),owner);
                double time=AudioSettings.dspTime;var input=new SpeechPcmStream(time,.02);var samples=new short[4800];
                for(int i=0;i<samples.Length;i++)samples[i]=(short)(900*Math.Sin(2*Math.PI*440*i/24000));
                Check(input.TryWrite(1,samples,time,out var issue),issue);Check(input.TryComplete(1,out issue),issue);
                lease=PcmOutputLease.Open(scope.transform,"Probe speech",input,true,.75f,96,1,15);
                lease.SetPose(new Vector3(1,0,0),Quaternion.identity);lease.Start();
                var block=new float[format.dspBufferSize*2];
                while(!lease.IsDrained(time)){
                    Tick();Check(lease.Available,"Lost application lease");Check(owner.Render(block,2,time),"Native render rejected its format");
                    foreach(float sample in block){Check(float.IsFinite(sample),"Nonfinite output");result.energy+=sample*(double)sample;}
                    time+=(double)format.dspBufferSize/format.sampleRate;result.blocks++;await Task.Yield();
                }
                var receipt=input.Read(time);result.submitted=receipt.Submitted;result.played=receipt.Played;
                Check(receipt.SourceDrained&&receipt.Played==4800&&receipt.Submitted==4800,"Inexact lease receipt");
                Check(result.energy>1e-6,"Lease output was empty");lease.Dispose();
                Check(input.Read(time).Closed&&!lease.IsDrained(time+60),"Closed lease kept claiming playback");result.completedVoices=1;
            }catch(Exception error){result.issue=error.ToString();}
            finally{
                lease?.Dispose();adapter?.Dispose();if(scope)UnityEngine.Object.Destroy(scope);owner.Close();var drain=Stopwatch.StartNew();
                while(!owner.Drained&&drain.Elapsed.TotalSeconds<20){owner.Tick();await Task.Yield();}
                result.drained=owner.Drained;result.graphs=PreparedGraph.Opened-opened;result.meshes=PreparedGraph.MeshesOpened-meshes;
                result.instances=PreparedGraph.InstancesOpened-instances;result.nativeDiagnostics=PreparedGraph.Diagnostics-diagnostics;
                if(!result.drained||result.graphs!=PreparedGraph.Closed-closed||result.meshes!=PreparedGraph.MeshesClosed-meshesClosed||
                    result.instances!=PreparedGraph.InstancesClosed-instancesClosed||result.nativeDiagnostics!=0)
                    result.issue=(result.issue??"")+" Native lease ownership did not drain cleanly.";
            }
            if(result.issue==null)result.status="passed";return result;
        }
    }
}
