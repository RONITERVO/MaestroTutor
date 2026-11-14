// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Imports {
    // At most two seconds per possible world voice, shared by exact source.
    // The renderer owns its separate bounded output queue. Disk and resampling
    // never run on Unity's main or audio callback threads.
    internal sealed class WavePcmAsset:AudioPcmAsset {
        internal const int BlockFrames=WaveAudio.OutputRate,MaximumBlocks=16;
        internal const long MaximumPcmBytes=(long)BlockFrames*MaximumBlocks*sizeof(short);
        sealed class Block {internal short[] Samples;internal long Used;}
        readonly object gate=new();readonly Stream input;readonly WaveAudio.Info info;readonly IDisposable retention;
        readonly CancellationTokenSource cancel;readonly SemaphoreSlim lane;
        readonly Dictionary<int,Block> blocks=new();readonly int frames;
        Task pending=Task.CompletedTask,closing;long clock;bool closed;string failure;
        internal WavePcmAsset(Stream owned,WaveAudio.Info metadata,short[] first,SemaphoreSlim lane,CancellationToken token,IDisposable retention){
            int length=WaveAudio.OutputFrames(in metadata);
            if(owned==null||lane==null||retention==null||length<1||first==null||first.Length!=Math.Min(length,BlockFrames))throw new ArgumentException("Invalid initial sound block");
            input=owned;info=metadata;this.retention=retention;frames=length;this.lane=lane;cancel=CancellationTokenSource.CreateLinkedTokenSource(token);
            blocks[0]=new Block{Samples=first,Used=++clock};
        }
        internal override int Frames=>frames;
        internal override long CapacityBytes=>MaximumPcmBytes;
        internal override long BufferedBytes {get{lock(gate)return blocks.Values.Sum(b=>b.Samples.LongLength*sizeof(short));}}
        internal override string Failure {get{lock(gate)return failure;}}
        internal override int Read(int frame,short[] destination,int count){
            if(frame<0||frame>=frames||count<1||destination==null||count>destination.Length)throw new ArgumentOutOfRangeException(nameof(frame));
            lock(gate){
                if(closed)throw new ObjectDisposedException(nameof(WavePcmAsset));
                if(failure!=null)throw new InvalidDataException(failure);
                int index=frame/BlockFrames,offset=frame%BlockFrames;
                if(blocks.TryGetValue(index,out var block)){
                    block.Used=++clock;int take=Math.Min(count,block.Samples.Length-offset);Array.Copy(block.Samples,offset,destination,0,take);return take;
                }
                if(pending.IsCompleted){
                    if(blocks.Count>=MaximumBlocks){int oldest=blocks.OrderBy(p=>p.Value.Used).First().Key;blocks.Remove(oldest);}
                    pending=Load(index);
                }
                return 0;
            }
        }
        async Task Load(int index){
            bool held=false;
            try{
                await lane.WaitAsync(cancel.Token).ConfigureAwait(false);held=true;
                var samples=await Task.Run(()=>WaveAudio.DecodeBlock(input,in info,index*BlockFrames,Math.Min(BlockFrames,frames-index*BlockFrames),cancel.Token),cancel.Token).ConfigureAwait(false);
                lock(gate){if(!closed&&!cancel.IsCancellationRequested)blocks[index]=new Block{Samples=samples,Used=++clock};}
            }catch(OperationCanceledException){lock(gate){if(!closed)failure="The sound source preparation was cancelled";}}
            catch(Exception){lock(gate){if(!closed)failure="The sound file could not supply its next samples";}}
            finally{if(held)lane.Release();}
        }
        internal override Task Close(){lock(gate){if(closing!=null)return closing;closed=true;cancel.Cancel();return closing=Drain(pending);}}
        async Task Drain(Task work){
            try{await work.ConfigureAwait(false);}finally{lock(gate){blocks.Clear();try{input.Dispose();}finally{cancel.Dispose();retention.Dispose();}}}
        }
    }
}
