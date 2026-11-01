// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
namespace Maestro.Quest.Imports {
    // Validate each complete packet before the permissive decoder sees it. This
    // forward-only view neither seeks nor retains a growing frame index. The
    // source owner disposes the underlying stream to interrupt a blocked read.
    internal sealed class Mp3FrameStream:Stream {
        const int MaximumMetadata=65536;
        readonly Stream input;readonly CancellationToken cancel;
        readonly byte[] packet=new byte[MaximumMetadata+20];
        int position,length,rate,channels;long frames,bytesRead;bool started,ended;
        internal Mp3FrameStream(Stream input,CancellationToken cancel){this.input=input??throw new ArgumentNullException(nameof(input));this.cancel=cancel;}
        int Fill(int offset,int count){int total=0;while(total<count){cancel.ThrowIfCancellationRequested();int n=input.Read(packet,offset+total,count-total);if(n==0)break;if(n<0||n>count-total)throw new InvalidDataException("Invalid live audio read");total+=n;}return total;}
        void Require(int offset,int count){if(Fill(offset,count)!=count)throw new InvalidDataException("The live MP3 packet was truncated");}
        bool Next(){
            if(ended)return false;position=length=0;
            int n=Fill(0,4);if(n==0){if(frames<2)throw new InvalidDataException("The live MP3 has too few frames");ended=true;return false;}
            if(n!=4)throw new InvalidDataException("The live MP3 header was truncated");
            if(!started&&packet[0]=='I'&&packet[1]=='D'&&packet[2]=='3'){
                started=true;Require(4,6);
                length=Mp3Packets.MetadataLength(packet);
                int footer=packet[3]==4&&(packet[5]&16)!=0?10:0;
                if(length-10-footer>MaximumMetadata)throw new InvalidDataException("Live MP3 metadata exceeds 64 KiB");
                Require(10,length-10);return true;
            }
            started=true;var header=Mp3Packets.Read(packet,0);
            if(frames>0&&(rate!=header.Rate||channels!=header.Channels))throw new InvalidDataException("The live MP3 format changed");
            rate=header.Rate;channels=header.Channels;length=header.Bytes;
            Require(4,length-4);frames++;return true;
        }
        public override int Read(byte[] buffer,int offset,int count){
            if(buffer==null||offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException(nameof(count));
            cancel.ThrowIfCancellationRequested();if(count==0)return 0;
            if(position==length&&!Next())return 0;
            int take=Math.Min(count,length-position);Array.Copy(packet,position,buffer,offset,take);position+=take;bytesRead+=take;return take;
        }
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>bytesRead;set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();public override long Seek(long value,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
}
