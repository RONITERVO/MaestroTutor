// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Tests {
    // No sockets or user media. Exercises the real HTTP admission, decoder and PCM path.
    public sealed class LiveAudioTestNetwork:IDisposable {
        public readonly List<PacedStream> Bodies=new();
        public int Requests,DisposedHandlers;public string RequestUrl,Authentication,Cookie;
        public HttpStatusCode Status=HttpStatusCode.OK;public string MediaType="audio/mpeg",Metadata,Encoding;
        public Func<CancellationToken,Task<HttpResponseMessage>> Headers;
        public LiveAudioTestNetwork(){LiveAudioHttp.TestHandler=()=>new Handler(this);}
        public void Dispose(){LiveAudioHttp.TestHandler=null;foreach(var body in Bodies)body.Dispose();}
        sealed class Handler:HttpMessageHandler {
            readonly LiveAudioTestNetwork owner;public Handler(LiveAudioTestNetwork owner){this.owner=owner;}
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){
                owner.Requests++;owner.RequestUrl=request.RequestUri.AbsoluteUri;owner.Authentication=request.Headers.Authorization?.ToString();owner.Cookie=request.Headers.Contains("Cookie")?"present":null;
                if(owner.Headers!=null)return await owner.Headers(cancel);
                var body=new PacedStream();owner.Bodies.Add(body);var result=new HttpResponseMessage(owner.Status){Content=new StreamContent(body)};
                result.Content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue(owner.MediaType);
                if(owner.Metadata!=null)result.Headers.TryAddWithoutValidation("icy-metaint",owner.Metadata);
                if(owner.Encoding!=null)result.Content.Headers.ContentEncoding.Add(owner.Encoding);
                return result;
            }
            protected override void Dispose(bool disposing){Interlocked.Increment(ref owner.DisposedHandlers);base.Dispose(disposing);}
        }
        public sealed class PacedStream:Stream {
            readonly List<byte[]> frames=new();readonly Stopwatch clock=Stopwatch.StartNew();readonly CancellationTokenSource closed=new();
            readonly double duration;int index,offset;long emitted;int disposed;
            public volatile bool Starve,End,Corrupt;public int Disposals=>Volatile.Read(ref disposed);
            public PacedStream(){
                var bytes=File.ReadAllBytes(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/CompressedAudio/mono-cbr.mp3.bytes"));
                int at=Mp3Packets.MetadataLength(bytes);at+=Mp3Packets.Read(bytes,at).Bytes;
                while(at<bytes.Length){var h=Mp3Packets.Read(bytes,at);var frame=new byte[h.Bytes];Array.Copy(bytes,at,frame,0,frame.Length);frames.Add(frame);at+=h.Bytes;duration=h.Samples/(double)h.Rate;}
            }
            public override async Task<int> ReadAsync(byte[] b,int at,int count,CancellationToken cancel){
                using var stop=CancellationTokenSource.CreateLinkedTokenSource(cancel,closed.Token);
                while(Starve||clock.Elapsed.TotalSeconds<Math.Max(0,emitted-6)*duration)await Task.Delay(5,stop.Token);
                stop.Token.ThrowIfCancellationRequested();if(End&&offset==0)return 0;
                var frame=frames[index];int n=Math.Min(count,frame.Length-offset);Array.Copy(frame,offset,b,at,n);
                if(Corrupt)Array.Clear(b,at,n);offset+=n;if(offset==frame.Length){offset=0;index=(index+1)%frames.Count;emitted++;}return n;
            }
            public override int Read(byte[] b,int o,int n)=>ReadAsync(b,o,n,CancellationToken.None).GetAwaiter().GetResult();
            protected override void Dispose(bool disposing){if(Interlocked.Exchange(ref disposed,1)==0)closed.Cancel();base.Dispose(disposing);}
            public override bool CanRead=>Disposals==0;public override bool CanWrite=>false;public override bool CanSeek=>false;
            public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override void Flush()=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
        }
    }
}
#endif
