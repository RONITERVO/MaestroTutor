// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Imports {
    public sealed class AudioAsset {
        public string Hash {get;} public string Name {get;}
        internal byte[] Content {get;} internal WaveAudio.Info Inspection {get;}
        internal AudioAsset(string name,byte[] owned,CancellationToken cancel){Content=owned;Inspection=WaveAudio.Inspect(owned,cancel);Hash=ModelLibrary.Hash(owned);Name=AudioLibrary.SafeName(name);}
    }
    /// <summary>Immutable imported clip payloads in the same private workspace as
    /// models and motions. Definitions reference content hashes, never source URIs.</summary>
    public sealed partial class AudioLibrary {
        internal const int MaximumFiles=32;internal const long MaximumTotalBytes=128L*1024*1024;
        static readonly SemaphoreSlim decoders=new(2,2);
        readonly string directory;readonly WorkspaceWriteGate workspace;readonly SemaphoreSlim writes=new(1,1);
        public AudioLibrary(string directory,WorkspaceWriteGate writeGate=null){this.directory=Path.GetFullPath(directory);workspace=writeGate??new();}
        internal WorkspaceAssetFence Assets=>workspace.Assets;
        internal Action<string> ReadCheckpoint;
        IDisposable Pin(string hash){_=AssetPath(hash);return Assets.Pin(ImportedAssetKind.Audio,hash);}
        internal static string SafeName(string value)=>new string((value??"Imported sound").Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(80).ToArray());
        internal static AudioAsset Inspect(string name,byte[] bytes,CancellationToken cancel=default)=>new(name,bytes==null?null:(byte[])bytes.Clone(),cancel);
        internal static AudioAsset ReadSelected(string name,string path,CancellationToken token)=>new(name,ReadBounded(path,token),token);
        internal static async Task<AudioAsset> ImportSelectedAsync(string name,string path,CancellationToken token) {
            await decoders.WaitAsync(token).ConfigureAwait(false);
            try{return await Task.Run(()=>new AudioAsset(name,CompressedAudio.Normalize(ReadBounded(path,token),token),token),token).ConfigureAwait(false);}
            finally{decoders.Release();}
        }
        internal static async Task<AudioAsset> ImportChatAsync(string name,byte[] owned,string originalHash,CancellationToken token) {
            await decoders.WaitAsync(token).ConfigureAwait(false);
            try{return await Task.Run(()=>{
                token.ThrowIfCancellationRequested();
                if(owned==null||owned.Length>WaveAudio.MaximumBytes||ModelLibrary.Hash(owned)!=originalHash)throw new InvalidDataException("The chat sound bytes do not match the selected sound.");
                return new AudioAsset(name,CompressedAudio.Normalize(owned,token),token);
            },token).ConfigureAwait(false);}finally{decoders.Release();}
        }
        static byte[] ReadBounded(string path,CancellationToken cancel,ImportedAssetReadBudget budget=null){
            WorkspaceArchive.NoLink(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            long length=input.Length;if(length<44||length>WaveAudio.MaximumBytes)throw new InvalidDataException("The sound copy is incomplete or exceeds 32 MB.");
            budget?.Payload(length+1);
            var bytes=new byte[(int)length];int at=0;
            while(at<bytes.Length){cancel.ThrowIfCancellationRequested();int read=input.Read(bytes,at,Math.Min(65536,bytes.Length-at));if(read==0)throw new EndOfStreamException("The sound copy changed.");at+=read;}
            if(input.ReadByte()!=-1)throw new IOException("The sound copy changed.");return bytes;
        }
        string AssetPath(string hash){if(!ModelLibrary.ValidHash(hash))throw new InvalidDataException("Choose an exact imported sound identity.");return Path.Combine(directory,hash+".wav");}
        AudioAsset ReadCore(string hash,CancellationToken cancel,ImportedAssetReadBudget budget=null){
            string path=AssetPath(hash);if(!Directory.Exists(directory)||!File.Exists(path))throw new FileNotFoundException("This sound's private copy is missing. Import the original sound again.");
            WorkspaceArchive.NoLink(directory);var asset=new AudioAsset(ReadName(hash,budget,cancel),ReadBounded(path,cancel,budget),cancel);
            if(asset.Hash!=hash)throw new InvalidDataException("This sound's private copy is damaged. Import the original sound again.");return asset;
        }
        string ReadName(string hash,ImportedAssetReadBudget budget=null,CancellationToken cancel=default){
            string path=Path.Combine(directory,hash+".txt");
            if(budget!=null){var bytes=budget.ReadMetadata(path,1024,cancel);if(bytes==null)return hash;using var checkedReader=new StreamReader(new MemoryStream(bytes,false),new UTF8Encoding(false,true),true);return SafeName(checkedReader.ReadLine());}
            if(!File.Exists(path))return hash;
            WorkspaceArchive.NoLink(path);if(new FileInfo(path).Length>1024)throw new InvalidDataException("The sound information file exceeds its limit.");
            using var reader=new StreamReader(path,new UTF8Encoding(false,true),true);return SafeName(reader.ReadLine());
        }
        public async Task<AudioAsset> ReadAsync(string hash,CancellationToken cancel=default){
            using var pin=Pin(hash);using var use=workspace.Write();await writes.WaitAsync(cancel).ConfigureAwait(false);
            try{return await Task.Run(()=>{ReadCheckpoint?.Invoke("reading");return ReadCore(hash,cancel);},cancel).ConfigureAwait(false);}finally{writes.Release();}
        }
        internal async Task<short[]> DecodeAsync(string hash,CancellationToken cancel=default,float? expectedSeconds=null){
            using var pin=Pin(hash);using var use=workspace.Write();await decoders.WaitAsync(cancel).ConfigureAwait(false);
            try{
                AudioAsset asset;await writes.WaitAsync(cancel).ConfigureAwait(false);
                try{asset=await Task.Run(()=>{ReadCheckpoint?.Invoke("reading");return ReadCore(hash,cancel);},cancel).ConfigureAwait(false);}finally{writes.Release();}
                if(expectedSeconds.HasValue&&Math.Abs(asset.Inspection.Seconds-expectedSeconds.Value)>.0001)throw new InvalidDataException("The saved sound duration differs from the exact clip; inspect and save the source again.");
                return await Task.Run(()=>{ReadCheckpoint?.Invoke("decoding");return WaveAudio.Decode(asset.Content,cancel);},cancel).ConfigureAwait(false);
            }finally{decoders.Release();}
        }
        internal async Task<AudioPcmAsset> PrepareAsync(string hash,float expectedSeconds,CancellationToken cancel=default){
            IDisposable pin=Pin(hash);
            try{
                AudioPcmAsset asset;
                if(expectedSeconds<=WaveAudio.ResidentSeconds)asset=AudioPcmAsset.Resident(await DecodeAsync(hash,cancel,expectedSeconds).ConfigureAwait(false));
                else{
                    using var opening=workspace.Write();await decoders.WaitAsync(cancel).ConfigureAwait(false);
                    try{
                        await writes.WaitAsync(cancel).ConfigureAwait(false);
                        try{asset=await Task.Run(()=>{ReadCheckpoint?.Invoke("opening");return OpenStream(hash,expectedSeconds,cancel);},cancel).ConfigureAwait(false);}
                        finally{writes.Release();}
                    }finally{decoders.Release();}
                }
                var result=AudioPcmAsset.Retained(asset,pin);pin=null;return result;
            }finally{pin?.Dispose();}
        }
        AudioPcmAsset OpenStream(string hash,float expectedSeconds,CancellationToken token){
            string path=AssetPath(hash);WorkspaceArchive.NoLink(directory);WorkspaceArchive.NoLink(path);
            FileStream input=null;IDisposable retention=null;
            try{
                retention=workspace.RetainRead();input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                var info=WaveAudio.Inspect(input,token);
                if(Math.Abs(info.Seconds-expectedSeconds)>.0001)throw new InvalidDataException("The saved sound duration differs from the exact clip; inspect and save the source again.");
                using(var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256)){
                    input.Position=0;var buffer=new byte[65536];int count;
                    while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();digest.AppendData(buffer,0,count);}
                    string actual=BitConverter.ToString(digest.GetHashAndReset()).Replace("-","").ToLowerInvariant();
                    if(actual!=hash)throw new InvalidDataException("This sound's private copy is damaged. Import the original sound again.");
                }
                var first=WaveAudio.DecodeBlock(input,in info,0,Math.Min(WavePcmAsset.BlockFrames,WaveAudio.OutputFrames(in info)),token);token.ThrowIfCancellationRequested();
                var result=new WavePcmAsset(input,info,first,decoders,token,retention);input=null;retention=null;return result;
            }finally{input?.Dispose();retention?.Dispose();}
        }
        public async Task SaveAsync(AudioAsset asset,CancellationToken cancel=default){
            if(asset==null)throw new ArgumentNullException(nameof(asset));using var use=workspace.Write();await writes.WaitAsync(cancel).ConfigureAwait(false);
            try{InvalidateReview(asset.Hash);await Task.Run(()=>SaveCore(asset,cancel),cancel).ConfigureAwait(false);}finally{writes.Release();}
        }
        void SaveCore(AudioAsset asset,CancellationToken cancel){
            cancel.ThrowIfCancellationRequested();_=WaveAudio.Inspect(asset.Content,cancel);
            if(ModelLibrary.Hash(asset.Content)!=asset.Hash)throw new InvalidDataException("The chosen sound changed before publication.");
            Directory.CreateDirectory(directory);WorkspaceArchive.NoLink(directory);string path=AssetPath(asset.Hash);bool exists=File.Exists(path);
            var files=new DirectoryInfo(directory).GetFiles("*.wav");foreach(var file in files)WorkspaceArchive.NoLink(file.FullName);
            if(files.Length+(exists?0:1)>MaximumFiles||files.Sum(f=>f.Length)-(exists?new FileInfo(path).Length:0)+asset.Content.Length>MaximumTotalBytes)throw new IOException("The sound library is full (32 files or 128 MB). Existing sounds remain available.");
            bool intact=exists&&new FileInfo(path).Length==asset.Content.Length&&ModelLibrary.Hash(ReadBounded(path,cancel))==asset.Hash;
            if(!intact)Publish(path,asset.Content,cancel);
            ImportedAssetSidecar.SaveAccepted(Path.Combine(directory,asset.Hash+".txt"),asset.Name,cancel);
        }
        static void Publish(string path,byte[] bytes,CancellationToken cancel){
            if(File.Exists(path))WorkspaceArchive.NoLink(path);string temp=path+"."+Guid.NewGuid().ToString("N")+".part";
            try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}cancel.ThrowIfCancellationRequested();if(File.Exists(path))FilePublication.Replace(temp,path,null);else File.Move(temp,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public sealed class Entry {public string Hash,Name;public long Bytes;public double Seconds;public int Rate,Channels;}
        public async Task<Entry[]> ListAsync(CancellationToken cancel=default)=>(await ScanAsync(cancel).ConfigureAwait(false)).RequireComplete();
        internal async Task<ImportedAssetInventory<Entry>> ScanAsync(CancellationToken cancel=default){
            using var use=workspace.Write();await writes.WaitAsync(cancel).ConfigureAwait(false);
            try{return await Task.Run(()=>{var snapshot=ImportedAssetInventory.Scan(directory,".wav",new ImportedAssetReadBudget(MaximumTotalBytes+MaximumFiles,ImportedAssetInventory.MediaMetadataBytes),
                (path,budget,token)=>{var asset=ReadCore(Path.GetFileNameWithoutExtension(path),token,budget);return new Entry{Hash=asset.Hash,Name=asset.Name,Bytes=asset.Content.Length,Seconds=asset.Inspection.Seconds,Rate=asset.Inspection.Rate,Channels=asset.Inspection.Channels};},cancel);snapshot.SourceRevision=InventoryRevision;return snapshot;},cancel).ConfigureAwait(false);}finally{writes.Release();}
        }
        internal bool TryCaptureArchive(out WorkspaceLibraryCapture capture){
            capture=null;if(!writes.Wait(0))return false;
            capture=new WorkspaceLibraryCapture(()=>writes.Release(),(documents,assets)=>{
                if(!Directory.Exists(directory))return;WorkspaceArchive.NoLink(directory);var files=new DirectoryInfo(directory).GetFiles("*.wav");
                if(files.Length>MaximumFiles||files.Sum(f=>f.Length)>MaximumTotalBytes)throw new InvalidDataException("The sound library exceeds its archive bounds.");
                foreach(var file in files){string hash=Path.GetFileNameWithoutExtension(file.Name);_=AssetPath(hash);string path=file.FullName;assets.Add("audio/"+hash+".wav",()=>WorkspaceLibraryCapture.Open(path));string info=Path.Combine(directory,hash+".txt");if(File.Exists(info))documents.Add("audio/"+hash+".txt",WorkspaceLibraryCapture.ReadDocument(info,1024));}
            });return true;
        }
    }
}
