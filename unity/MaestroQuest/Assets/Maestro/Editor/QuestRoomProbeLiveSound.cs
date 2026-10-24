// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Editor {
    internal static class QuestRoomProbeLiveSound {
        internal const string Endpoint="https://radio.example.invalid/offline-fixture.mp3";
        internal static JObject Seed(){
            var data=File.ReadAllBytes(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/CompressedAudio/mono-cbr.mp3.bytes"));
            LiveAudioHttp.TestHandler=()=>new Handler(data);
            return new JObject{["endpoint"]=Endpoint,["boundary"]="Editor-only deterministic MP3 HTTP response. Real decoder, source ownership and native output; no sockets, provider or headset evidence."};
        }
        sealed class Handler:HttpMessageHandler {
            readonly byte[] data;internal Handler(byte[] data){this.data=data;}
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
                token.ThrowIfCancellationRequested();if(request.RequestUri.AbsoluteUri!=Endpoint)throw new InvalidOperationException("Only the explicit offline live-sound fixture endpoint is available");
                var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(new Body(data))};response.Content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");return Task.FromResult(response);
            }
        }
        sealed class Body:Stream {
            readonly List<byte[]> frames=new();readonly Stopwatch clock=Stopwatch.StartNew();readonly CancellationTokenSource closed=new();readonly double duration;
            int index,offset,disposed;long emitted;
            internal Body(byte[] data){
                int at=Mp3Packets.MetadataLength(data);at+=Mp3Packets.Read(data,at).Bytes;
                while(at<data.Length){var h=Mp3Packets.Read(data,at);var frame=new byte[h.Bytes];Array.Copy(data,at,frame,0,frame.Length);frames.Add(frame);at+=h.Bytes;duration=h.Samples/(double)h.Rate;}
            }
            public override async Task<int> ReadAsync(byte[] b,int at,int count,CancellationToken token){
                using var stop=CancellationTokenSource.CreateLinkedTokenSource(token,closed.Token);
                while(clock.Elapsed.TotalSeconds<Math.Max(0,emitted-6)*duration)await Task.Delay(5,stop.Token);stop.Token.ThrowIfCancellationRequested();
                var frame=frames[index];int n=Math.Min(count,frame.Length-offset);Array.Copy(frame,offset,b,at,n);offset+=n;if(offset==frame.Length){offset=0;index=(index+1)%frames.Count;emitted++;}return n;
            }
            public override int Read(byte[] b,int o,int n)=>ReadAsync(b,o,n,CancellationToken.None).GetAwaiter().GetResult();
            protected override void Dispose(bool disposing){if(Interlocked.Exchange(ref disposed,1)==0)closed.Cancel();base.Dispose(disposing);}
            public override bool CanRead=>Volatile.Read(ref disposed)==0;public override bool CanWrite=>false;public override bool CanSeek=>false;
            public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override void Flush()=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
        }
    }
}
