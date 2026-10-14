// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
namespace Maestro.Quest.Imports {
    // Immutable source identity with independently addressed reads. A missing
    // block is preparation, never end-of-input or permission to advance a cursor.
    internal abstract class AudioPcmAsset {
        internal virtual bool IsLive=>false;
        internal abstract int Frames {get;}
        internal abstract long BufferedBytes {get;}
        internal abstract long CapacityBytes {get;}
        internal virtual short[] ResidentSamples=>null;
        internal virtual string Failure=>null;
        internal abstract int Read(int frame,short[] destination,int count);
        internal abstract Task Close();
        internal static AudioPcmAsset Resident(short[] samples)=>new Memory(samples);
        sealed class Memory:AudioPcmAsset {
            short[] samples;readonly int frames;
            internal Memory(short[] value){samples=value??throw new ArgumentNullException(nameof(value));frames=value.Length;}
            internal override int Frames=>frames;
            internal override long CapacityBytes=>(long)frames*sizeof(short);
            internal override long BufferedBytes=>(samples?.LongLength??0)*sizeof(short);
            internal override short[] ResidentSamples=>samples;
            internal override int Read(int frame,short[] destination,int count){
                if(samples==null)throw new ObjectDisposedException(nameof(AudioPcmAsset));
                if(frame<0||frame>=frames||count<1||destination==null||count>destination.Length)throw new ArgumentOutOfRangeException(nameof(frame));
                int take=Math.Min(count,frames-frame);Array.Copy(samples,frame,destination,0,take);return take;
            }
            internal override Task Close(){samples=null;return Task.CompletedTask;}
        }
    }
}
