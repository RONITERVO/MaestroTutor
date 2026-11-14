// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using UnityEngine;

namespace Maestro.AudioEvaluation
{
    // Last listener filter: this diagnostic measures audio without playing it.
    public sealed class EchoProbeMute : MonoBehaviour
    {
        void OnAudioFilterRead(float[] data,int channels) => Array.Clear(data,0,data.Length);
    }
    internal static class EchoReferencePlayerProbe
    {
        [Serializable] internal sealed class Result
        {
            public string status,issue;
            public int frames,rate,channels;
            public double energy;
            public bool referenceClosed,physicalPlayback,providerUsed;
        }
        // The production listener component must survive High stripping and run
        // on the actual Unity audio thread, independently of the private ABI probe.
        internal static async Task<Result> Run()
        {
            var result=new Result{status="failed"};GameObject root=null;AudioClip clip=null;
            EchoReferenceStream reference=null;AudioSource source=null;
            var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabled=new bool[listeners.Length];float volume=AudioListener.volume;bool pause=AudioListener.pause;
            try
            {
                for(int i=0;i<listeners.Length;i++){enabled[i]=listeners[i].enabled;listeners[i].enabled=false;}
                root=new GameObject("Measured and muted reference probe");root.AddComponent<AudioListener>();
                var tap=root.AddComponent<RoomAudioTap>();root.AddComponent<EchoProbeMute>();
                AudioListener.volume=1;AudioListener.pause=false;
                reference=tap.BeginReference();result.rate=reference.Rate;result.channels=reference.Channels;
                var tone=new float[result.rate];for(int i=0;i<tone.Length;i++)tone[i]=(float)(.05*Math.Sin(2*Math.PI*440*i/result.rate));
                clip=AudioClip.Create("Reference probe tone",result.rate,1,result.rate,false);clip.SetData(tone,0);
                var emitter=new GameObject("Reference probe emitter");emitter.transform.SetParent(root.transform,false);
                source=emitter.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.Play();
                var frame=new float[reference.FrameSamples];double deadline=Time.realtimeSinceStartupAsDouble+3;
                while(result.frames<25 && Time.realtimeSinceStartupAsDouble<deadline)
                {
                    EchoReferenceRead state;
                    while((state=reference.Read(frame,out _))==EchoReferenceRead.Frame)
                    {
                        result.frames++;foreach(float value in frame)result.energy+=value*(double)value;
                    }
                    if(state!=EchoReferenceRead.Waiting)throw new InvalidOperationException("Listener reference interrupted: "+state);
                    await Task.Yield();
                }
                if(result.frames<25 || result.energy<.001)throw new InvalidOperationException("No measured Unity listener output");
            }
            catch(Exception error){result.issue=error.ToString();}
            finally
            {
                reference?.Dispose();result.referenceClosed=reference!=null && reference.Closed;
                if(source)source.Stop();if(root){root.GetComponent<AudioListener>().enabled=false;UnityEngine.Object.Destroy(root);}
                if(clip)UnityEngine.Object.Destroy(clip);await Task.Yield();
                for(int i=0;i<listeners.Length;i++)if(listeners[i])listeners[i].enabled=enabled[i];
                AudioListener.volume=volume;AudioListener.pause=pause;
            }
            if(result.issue==null && result.referenceClosed)result.status="passed";return result;
        }
    }
}
