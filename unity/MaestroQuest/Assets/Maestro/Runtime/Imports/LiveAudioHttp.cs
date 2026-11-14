// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Imports {
    // Public media transport, never a provider client or credential store.
    internal static class LiveAudioHttp {
        internal const int HeaderTimeoutSeconds=10,ReadTimeoutSeconds=15;
#if UNITY_EDITOR
        internal static Func<HttpMessageHandler> TestHandler;
#endif
        internal static bool ValidEndpoint(string value){
            if(string.IsNullOrEmpty(value)||!value.StartsWith("https://",StringComparison.Ordinal)||value.Length>2048||value.IndexOfAny(new[]{'?','#','\\','@'})>=0)return false;
            foreach(char c in value)if(char.IsWhiteSpace(c)||char.IsControl(c))return false;
            return Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.IsWellFormedOriginalString()&&uri.Scheme==Uri.UriSchemeHttps&&uri.UserInfo.Length==0&&uri.Host.Length>0&&uri.Query.Length==0&&uri.Fragment.Length==0;
        }
        internal static async Task<AudioPcmAsset> Open(string endpoint,CancellationToken cancel){
            if(!ValidEndpoint(endpoint))throw new InvalidDataException("Use a direct public HTTPS MP3 URL without credentials, query or fragment");
            HttpClient client=null;HttpResponseMessage response=null;Stream input=null;
            try {
                cancel.ThrowIfCancellationRequested();
                HttpMessageHandler handler=null;
#if UNITY_EDITOR
                handler=TestHandler?.Invoke();
#endif
                handler??=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false,PreAuthenticate=false};
                client=new HttpClient(handler,true){Timeout=Timeout.InfiniteTimeSpan};
                using var request=new HttpRequestMessage(HttpMethod.Get,endpoint);
                request.Headers.TryAddWithoutValidation("Accept","audio/mpeg");request.Headers.TryAddWithoutValidation("Icy-MetaData","0");
                using var start=CancellationTokenSource.CreateLinkedTokenSource(cancel);start.CancelAfter(TimeSpan.FromSeconds(HeaderTimeoutSeconds));
                response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,start.Token).ConfigureAwait(false);
                if(response.StatusCode!=System.Net.HttpStatusCode.OK||!(string.Equals(response.Content.Headers.ContentType?.MediaType,"audio/mpeg",StringComparison.OrdinalIgnoreCase)||string.Equals(response.Content.Headers.ContentType?.MediaType,"audio/mp3",StringComparison.OrdinalIgnoreCase))||response.Content.Headers.ContentEncoding.Count!=0||(response.Headers.TryGetValues("icy-metaint",out var intervals)&&intervals.Any(v=>v!="0")))throw new InvalidDataException("The endpoint must return an unencoded MP3 stream directly, without redirects or interleaved metadata");
                input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false);cancel.ThrowIfCancellationRequested();
                var owned=new Connection(input,response,client,cancel);input=null;response=null;client=null;
                try{return new LivePcmAsset(LiveMp3Source.Open(owned,cancel));}catch{owned.Dispose();throw;}
            }catch(OperationCanceledException){throw;}
            catch(Exception){throw new InvalidDataException("Could not open the public MP3 stream; check its direct URL and network connection");}
            finally{if(response!=null)response.Dispose();else input?.Dispose();client?.Dispose();}
        }
        sealed class Connection:Stream {
            readonly Stream input;readonly HttpResponseMessage response;readonly HttpClient client;readonly CancellationToken lifetime;int closed;
            internal Connection(Stream input,HttpResponseMessage response,HttpClient client,CancellationToken token){this.input=input;this.response=response;this.client=client;lifetime=token;}
            public override int Read(byte[] buffer,int offset,int count){
                if(Volatile.Read(ref closed)!=0)throw new ObjectDisposedException(nameof(Connection));
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime);deadline.CancelAfter(TimeSpan.FromSeconds(ReadTimeoutSeconds));
                using var close=deadline.Token.Register(()=>Dispose());
                return input.ReadAsync(buffer,offset,count,deadline.Token).GetAwaiter().GetResult();
            }
            protected override void Dispose(bool disposing){if(Interlocked.Exchange(ref closed,1)==0){try{response.Dispose();}finally{client.Dispose();}}base.Dispose(disposing);}
            public override bool CanRead=>Volatile.Read(ref closed)==0;public override bool CanSeek=>false;public override bool CanWrite=>false;
            public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override void Flush()=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
        }
    }
    internal sealed class LivePcmAsset:AudioPcmAsset {
        readonly LiveMp3Source source;
        internal LivePcmAsset(LiveMp3Source source){this.source=source;}
        internal override bool IsLive=>true;
        internal override int Frames=>0; // Duration is unknown, not a fake infinite clip.
        internal override long CapacityBytes=>(long)LiveMp3Source.Capacity*sizeof(short);
        internal override long BufferedBytes=>(long)source.Observe().Buffered*sizeof(short);
        internal override string Failure=>source.Observe().Phase=="failed"?source.Observe().Error:null;
        internal LiveMp3Source.Snapshot State=>source.Observe();
        internal LiveMp3Source.ReadResult ReadLive(short[] destination,int count,bool latest)=>source.Read(destination,0,count,latest?2400:(int?)null);
        internal override int Read(int frame,short[] destination,int count)=>throw new NotSupportedException("A live feed cannot seek or loop");
        internal override Task Close()=>source.Close();
    }
}
