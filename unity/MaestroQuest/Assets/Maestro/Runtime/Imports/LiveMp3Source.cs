// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Imports {
    // Decoder side of a live adapter. It has no network, credentials, saved room,
    // Unity objects or provider access. Admission transfers exclusive stream
    // ownership; a rejected Open leaves it with the caller (including aliases).
    // Live samples expire rather than accumulating an unbounded delayed replay.
    internal sealed class LiveMp3Source {
        internal const int MaximumSources=2,Capacity=2*WaveAudio.OutputRate;
        static readonly object admission=new();static readonly List<LiveMp3Source> sources=new();
        readonly object gate=new();readonly Stream input;readonly CancellationTokenSource cancel=new();
        readonly short[] ring=new short[Capacity];readonly Task completion;
        CancellationTokenRegistration registration;Task disposal;bool closed,ended,finished;string failure;
        long produced,consumed;
        internal readonly struct Snapshot {
            internal readonly string Phase,Error;internal readonly long Produced,Consumed,Dropped;internal readonly int Buffered;
            internal Snapshot(string phase,string error,long produced,long consumed,long dropped,int buffered){Phase=phase;Error=error;Produced=produced;Consumed=consumed;Dropped=dropped;Buffered=buffered;}
        }
        internal readonly struct ReadResult {
            internal readonly long FirstFrame,Dropped;internal readonly int Count;internal readonly Snapshot State;
            internal ReadResult(long first,int count,long dropped,Snapshot state){FirstFrame=first;Count=count;Dropped=dropped;State=state;}
        }
        long dropped;
        LiveMp3Source(Stream input,CancellationToken token){
            this.input=input;
            registration=token.Register(()=>{Stop();});
            completion=Run();
        }
        internal static LiveMp3Source Open(Stream owned,CancellationToken token=default){
            if(owned==null||!owned.CanRead)throw new ArgumentException("A readable live audio stream is required");
            token.ThrowIfCancellationRequested();
            lock(admission){
                if(sources.Any(s=>ReferenceEquals(s.input,owned)))throw new InvalidOperationException("This live audio stream already has an owner");
                if(sources.Count>=MaximumSources)throw new InvalidOperationException("The live audio decoder budget is full");
                var source=new LiveMp3Source(owned,token);sources.Add(source);return source;
            }
        }
        internal static int ActiveSources{get{lock(admission)return sources.Count;}}
        internal Task Completion=>completion;
        Snapshot ObserveLocked(){
            long first=Math.Max(consumed,Math.Max(0,produced-Capacity));int buffered=(int)(produced-first);
            return new Snapshot(closed?"closed":failure!=null?"failed":buffered>0?"available":ended?"ended":"buffering",failure??"",produced,consumed,dropped+first-consumed,closed||failure!=null?0:buffered);
        }
        internal Snapshot Observe(){lock(gate)return ObserveLocked();}
        internal ReadResult Read(short[] destination,int offset,int count){
            if(destination==null||offset<0||count<1||offset>destination.Length-count)throw new ArgumentOutOfRangeException(nameof(count));
            lock(gate){
                if(closed||failure!=null)return new ReadResult(consumed,0,0,ObserveLocked());
                long first=Math.Max(consumed,Math.Max(0,produced-Capacity)),lost=first-consumed;
                int take=(int)Math.Min(count,produced-first);
                for(int i=0;i<take;i++)destination[offset+i]=ring[(first+i)%Capacity];
                consumed=first+take;dropped+=lost;return new ReadResult(first,take,lost,ObserveLocked());
            }
        }
        void Produce(){
            using var packets=new Mp3FrameStream(input,cancel.Token);
            using var decoder=new NLayer.MpegFile(packets);
            var convert=new AudioSampleReader(decoder.ReadSamples,decoder.SampleRate,decoder.Channels,()=>cancel.Token.ThrowIfCancellationRequested(),long.MaxValue);
            var block=new short[480];int count;
            while((count=convert.Read(block,0,block.Length))>0){
                lock(gate){if(closed||cancel.IsCancellationRequested)return;for(int i=0;i<count;i++)ring[produced++%Capacity]=block[i];}
            }
            lock(gate){if(!closed)ended=true;}
        }
        async Task Run(){
            try{await Task.Run(Produce).ConfigureAwait(false);}
            catch(OperationCanceledException){lock(gate){if(!closed)failure="The live audio source was cancelled";}}
            catch(Exception){lock(gate){if(!closed)failure="The live MP3 source disconnected, changed format or could not be decoded";}}
            finally{
                try{await DisposeInput().ConfigureAwait(false);}catch(Exception){lock(gate){if(!closed)failure="The live audio connection could not close cleanly";}}
                lock(gate){finished=true;cancel.Dispose();}
                ReleaseClosed();
            }
        }
        Task DisposeInput(){lock(gate)return disposal??=Task.Run(()=>input.Dispose());}
        void Stop(){
            lock(gate){if(closed)return;closed=true;Array.Clear(ring,0,ring.Length);if(!finished)cancel.Cancel();}
            _ = DisposeInput();ReleaseClosed();
        }
        void ReleaseClosed(){
            lock(gate){if(!closed||!finished)return;}
            registration.Dispose();lock(admission)sources.Remove(this);
        }
        internal async Task Close(){Stop();await completion.ConfigureAwait(false);ReleaseClosed();}
    }
}
