// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Imports
{
    internal sealed class AudioFileReview
    {
        internal readonly string Hash,Revision,Name;
        internal readonly bool PayloadPresent,MetadataPresent,ContentValid;
        internal readonly long Bytes;
        internal AudioFileReview(string hash,string revision,string name,bool payload,bool metadata,bool valid,long bytes)
        {Hash=hash;Revision=revision;Name=name;PayloadPresent=payload;MetadataPresent=metadata;ContentValid=valid;Bytes=bytes;}
    }
    public sealed partial class AudioLibrary
    {
        readonly object reviewGate=new();readonly Dictionary<string,AudioFileReview> reviews=new(StringComparer.Ordinal);
        long inventoryRevision;
        internal long InventoryRevision=>Interlocked.Read(ref inventoryRevision);
        internal Action<string> MaintenanceCheckpoint;
        internal AudioFileReview Review(string hash){lock(reviewGate)return hash!=null&&reviews.TryGetValue(hash,out var value)?value:null;}
        void InvalidateReview(string hash){Interlocked.Increment(ref inventoryRevision);lock(reviewGate)reviews.Remove(hash);}
        void KeepReview(AudioFileReview value){lock(reviewGate){if(!reviews.ContainsKey(value.Hash)&&reviews.Count>=MaximumFiles)reviews.Remove(reviews.Keys.First());reviews[value.Hash]=value;}}
        static byte[] MaintenanceBytes(string path,int maximum,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();FileAttributes attributes;
            try{attributes=File.GetAttributes(path);}catch(FileNotFoundException){return null;}catch(DirectoryNotFoundException){return null;}
            WorkspaceArchive.NoLink(path);
            if((attributes&FileAttributes.Directory)!=0)throw new InvalidDataException("The sound storage entry is not a regular file.");
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            long length=input.Length;if(length<0||length>maximum)throw new InvalidDataException("The sound entry exceeds the bounded inspection limit. Its files are preserved.");
            var bytes=new byte[(int)length];int at=0;
            while(at<bytes.Length){token.ThrowIfCancellationRequested();int read=input.Read(bytes,at,Math.Min(65536,bytes.Length-at));if(read==0)throw new IOException("The sound entry changed during inspection.");at+=read;}
            if(input.ReadByte()!=-1)throw new IOException("The sound entry changed during inspection.");token.ThrowIfCancellationRequested();return bytes;
        }
        AudioFileReview InspectFile(string hash,CancellationToken token)
        {
            string path=AssetPath(hash);WorkspaceArchive.NoLink(directory);
            var payload=MaintenanceBytes(path,WaveAudio.MaximumBytes,token);
            var metadata=MaintenanceBytes(Path.Combine(directory,hash+".txt"),1024,token);
            string name=hash;
            if(metadata!=null)try{using var reader=new StreamReader(new MemoryStream(metadata,false),new UTF8Encoding(false,true),true);name=SafeName(reader.ReadLine());}catch(DecoderFallbackException){}
            string payloadHash=payload==null?"missing":ModelLibrary.Hash(payload);
            bool valid=false;
            if(payload!=null&&payloadHash==hash)try{_=WaveAudio.Inspect(payload,token);valid=true;}catch(InvalidDataException){}
            string revision=ModelLibrary.Hash(Encoding.UTF8.GetBytes("audio-maintenance.v1\n"+hash+"\n"+payloadHash+"\n"+(payload?.LongLength??-1).ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+(metadata==null?"missing":ModelLibrary.Hash(metadata))));
            return new(hash,revision,name,payload!=null,metadata!=null,valid,payload?.LongLength??0);
        }
        internal async Task<AudioFileReview> InspectFileAsync(string hash,CancellationToken token=default)
        {
            _=AssetPath(hash);using var use=workspace.Write();await writes.WaitAsync(token).ConfigureAwait(false);
            try{lock(reviewGate)reviews.Remove(hash);var value=await Task.Run(()=>InspectFile(hash,token),token).ConfigureAwait(false);token.ThrowIfCancellationRequested();KeepReview(value);return value;}
            finally{writes.Release();}
        }
        internal async Task<AudioFileReview> RemoveDownloadAsync(string hash,string revision,Func<Task<string>> protection,CancellationToken token=default)
        {
            _=AssetPath(hash);if(!ModelLibrary.ValidHash(revision)||protection==null)throw new ArgumentException("Inspect the sound and supply its exact reviewed revision.");
            using var freeze=workspace.TryFreeze(out var blocked)??throw new InvalidOperationException(blocked);
            using var lifetime=workspace.RetainRead();await writes.WaitAsync(token);
            try{
                using var retirement=workspace.Assets.TryRetire(ImportedAssetKind.Audio,hash,out blocked)??throw new InvalidOperationException(blocked);
                InvalidateReview(hash);
                var before=await Task.Run(()=>InspectFile(hash,token),token);
                if(before.Revision!=revision)throw new InvalidOperationException("This sound changed. Inspect it again before removing its download.");
                if(!before.PayloadPresent)throw new InvalidOperationException("This sound download is already absent. Refresh its inspection.");
                string reason=await protection();if(reason!=null)throw new InvalidOperationException(reason);
                MaintenanceCheckpoint?.Invoke("ready");token.ThrowIfCancellationRequested();
                // Authored writes, file serialization and new asset admissions stay
                // held through the worker. Stop after dispatch cannot undo a delete.
                return await Task.Run(()=>{
                    MaintenanceCheckpoint?.Invoke("deleting");
                    var final=InspectFile(hash,CancellationToken.None);
                    if(final.Revision!=revision)throw new InvalidOperationException("This sound changed before deletion. Its current files are preserved.");
                    File.Delete(AssetPath(hash));MaintenanceCheckpoint?.Invoke("deleted");
                    var after=InspectFile(hash,CancellationToken.None);
                    if(after.PayloadPresent)throw new IOException("Sound removal was not confirmed. Inspect its current files before retrying.");
                    KeepReview(after);return after;
                }).ConfigureAwait(false);
            }finally{writes.Release();}
        }
    }
}
