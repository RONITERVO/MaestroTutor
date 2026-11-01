// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Maestro.AudioEvaluation
{
    // ABI/ownership smoke probe. Native DSP renders into private memory; this is
    // not the Unity audio-device path or Quest perceptual/performance acceptance.
    internal static class NativePlayerProbe
    {
        [Serializable] internal sealed class Result
        {
            public string status, issue;
            public int voices, completedVoices, blocks, graphs, meshes, instances, nativeDiagnostics;
            public long submitted, played;
            public double energy;
            public bool drained, physicalPlayback, providerUsed;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Start()
        {
            string output=Path.Combine(Application.persistentDataPath,"native-audio-probe.json");
            var result=await Run();
            var lease=await PcmLeasePlayerProbe.Run();
            var reference=await EchoReferencePlayerProbe.Run();
            File.WriteAllText(Path.Combine(Application.persistentDataPath,"native-audio-reference-probe.json"),JsonUtility.ToJson(reference,true));
            UnityEngine.Debug.Log("MAESTRO_AUDIO_REFERENCE_PLAYER_PROBE "+JsonUtility.ToJson(reference));
            File.WriteAllText(Path.Combine(Application.persistentDataPath,"native-audio-lease-probe.json"),JsonUtility.ToJson(lease,true));
            UnityEngine.Debug.Log("MAESTRO_AUDIO_LEASE_PLAYER_PROBE "+JsonUtility.ToJson(lease));
            File.WriteAllText(output,JsonUtility.ToJson(result,true));
            UnityEngine.Debug.Log("MAESTRO_AUDIO_PLAYER_PROBE "+JsonUtility.ToJson(result));
            Application.Quit(result.status=="passed"&&lease.status=="passed"&&reference.status=="passed"?0:1);
        }
#endif
        internal static Task<Result> Run() => Task.Run(Execute);
        static Result Execute()
        {
            var result=new Result {status="failed", voices=9};
            int graphs=PreparedGraph.Opened, closedGraphs=PreparedGraph.Closed,
                meshes=PreparedGraph.MeshesOpened, closedMeshes=PreparedGraph.MeshesClosed,
                instances=PreparedGraph.InstancesOpened, closedInstances=PreparedGraph.InstancesClosed,
                diagnostics=PreparedGraph.Diagnostics;
            var owner=new OwnedLifecycle(result.voices);
            var timer=Stopwatch.StartNew();
            void Check(bool condition,string issue) { if(!condition) throw new InvalidOperationException(issue); }
            void Tick()
            {
                owner.Tick();
                if(owner.Failure!=null) throw owner.Failure;
                if(timer.Elapsed.TotalSeconds>20) throw new TimeoutException("Native player probe deadline");
            }
            try
            {
                owner.Configure(24000,256);
                while(owner.Active==null) { Tick(); Thread.Sleep(1); }
                var voices=new PcmVoice[result.voices]; var finished=new bool[result.voices];
                for(int v=0;v<voices.Length;v++)
                {
                    string id="abi/voice-"+v;
                    voices[v]=owner.BeginPcm(id,owner.Generation,0,.02);
                    Check(voices[v]!=null,"Voice admission failed");
                    var data=new short[4800];
                    for(int i=0;i<data.Length;i++) data[i]=(short)(700*Math.Sin(2*Math.PI*(440+v*40)*i/24000));
                    Check(voices[v].Write(1,data,0,out var issue),issue);
                    Check(voices[v].Complete(1,out issue),issue);
                    Check(owner.SetPcmGain(id,voices[v],.75f),"Mutable gain failed");
                    Check(owner.SetPcmPose(id,voices[v],AcousticFrame.Pose(1,0,v*.1f)),"Source pose failed");
                }
                var buffer=new float[512]; double time=0;
                while(result.completedVoices<voices.Length)
                {
                    Tick(); Check(owner.Render(buffer,2,time),"Native render rejected its owned format");
                    foreach(float sample in buffer)
                    {
                        Check(float.IsFinite(sample),"Native output is nonfinite");
                        result.energy+=sample*(double)sample;
                    }
                    time+=256.0/24000; result.blocks++;
                    for(int v=0;v<voices.Length;v++)
                        if(!finished[v] && owner.FinishPcm("abi/voice-"+v,voices[v],time,out var receipt))
                        {
                            Check(receipt.Submitted==4800 && receipt.Played==4800,"Inexact PCM completion receipt");
                            result.submitted+=receipt.Submitted; result.played+=receipt.Played;
                            finished[v]=true; result.completedVoices++;
                        }
                    Thread.Sleep(1);
                }
                Check(result.energy>1e-6,"Native output was empty");
            }
            catch(Exception error) { result.issue=error.ToString(); }
            finally
            {
                owner.Close(); var closeTimer=Stopwatch.StartNew();
                while(!owner.Drained && closeTimer.Elapsed.TotalSeconds<20) { owner.Tick(); Thread.Sleep(1); }
                result.drained=owner.Drained;
                result.graphs=PreparedGraph.Opened-graphs; result.meshes=PreparedGraph.MeshesOpened-meshes;
                result.instances=PreparedGraph.InstancesOpened-instances; result.nativeDiagnostics=PreparedGraph.Diagnostics-diagnostics;
                if(!result.drained || result.graphs!=PreparedGraph.Closed-closedGraphs ||
                    result.meshes!=PreparedGraph.MeshesClosed-closedMeshes || result.instances!=PreparedGraph.InstancesClosed-closedInstances)
                    result.issue=(result.issue??"")+" Native ownership did not drain.";
                if(result.nativeDiagnostics!=0) result.issue=(result.issue??"")+" Native validation diagnostics.";
                if(result.issue==null && result.completedVoices==result.voices) result.status="passed";
            }
            return result;
        }
    }
}
