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
    internal sealed class ModelFileReview
    {
        internal readonly string Hash,Revision,Name;
        internal readonly bool PayloadPresent,MetadataPresent,ContentValid,Included;
        internal readonly long Bytes;
        internal ModelFileReview(string hash,string revision,string name,bool payload,bool metadata,bool valid,bool included,long bytes)
        {Hash=hash;Revision=revision;Name=name;PayloadPresent=payload;MetadataPresent=metadata;ContentValid=valid;Included=included;Bytes=bytes;}
    }
    public sealed partial class ModelLibrary
    {
        internal const int MaximumFiles=32,MaximumMetadataBytes=128*1024;
        readonly object reviewGate=new();readonly Dictionary<string,ModelFileReview> reviews=new(StringComparer.Ordinal);
        long inventoryRevision;
        internal long InventoryRevision=>Interlocked.Read(ref inventoryRevision);
        internal Action<string> MaintenanceCheckpoint;
        internal bool IsIncluded(string hash)=>included!=null&&included.Hash==hash;
        internal ModelFileReview Review(string hash){lock(reviewGate)return hash!=null&&reviews.TryGetValue(hash,out var value)?value:null;}
        void InvalidateReview(string hash){Interlocked.Increment(ref inventoryRevision);lock(reviewGate)reviews.Remove(hash);ForgetBounds(hash);}
        void KeepReview(ModelFileReview value){lock(reviewGate){if(!reviews.ContainsKey(value.Hash)&&reviews.Count>=MaximumFiles)reviews.Remove(reviews.Keys.First());reviews[value.Hash]=value;}}
        string AssetPath(string hash){if(!ValidHash(hash))throw new InvalidDataException("Choose an exact imported model identity.");return Path.Combine(directory,hash+".glb");}
        bool InspectDirectory()
        {
            WorkspaceFileInventory.Parents(Path.GetDirectoryName(directory));
            FileAttributes attributes;
            try{attributes=File.GetAttributes(directory);}catch(FileNotFoundException){return false;}catch(DirectoryNotFoundException){return false;}
            WorkspaceArchive.NoLink(directory);
            if((attributes&FileAttributes.Directory)==0)throw new IOException("The model library path is not a directory.");
            return true;
        }
        static byte[] MaintenanceBytes(string path,int maximum,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();FileAttributes attributes;
            try{attributes=File.GetAttributes(path);}catch(FileNotFoundException){return null;}catch(DirectoryNotFoundException){return null;}
            WorkspaceArchive.NoLink(path);
            if((attributes&FileAttributes.Directory)!=0)throw new InvalidDataException("The model storage entry is not a regular file.");
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            long length=input.Length;if(length<0||length>maximum)throw new InvalidDataException("The model entry exceeds the bounded inspection limit. Its files are preserved.");
            var bytes=new byte[(int)length];int at=0;
            while(at<bytes.Length){token.ThrowIfCancellationRequested();int read=input.Read(bytes,at,Math.Min(65536,bytes.Length-at));if(read==0)throw new IOException("The model entry changed during inspection.");at+=read;}
            if(input.ReadByte()!=-1)throw new IOException("The model entry changed during inspection.");token.ThrowIfCancellationRequested();return bytes;
        }
        ModelFileReview InspectFile(string hash,CancellationToken token)
        {
            string path=AssetPath(hash);token.ThrowIfCancellationRequested();bool exists=InspectDirectory();
            var payload=exists?MaintenanceBytes(path,ModelInspection.MaximumBytes,token):null;
            var metadata=exists?MaintenanceBytes(Path.Combine(directory,hash+".txt"),MaximumMetadataBytes,token):null;
            string name=hash;
            if(metadata!=null)try{using var reader=new StreamReader(new MemoryStream(metadata,false),new UTF8Encoding(false,true),true);name=SafeName(reader.ReadLine());}catch(DecoderFallbackException){}
            string payloadHash=payload==null?"missing":Hash(payload);bool valid=false;
            if(payload!=null&&payloadHash==hash)try{_=ModelInspection.Inspect(payload);valid=true;}catch(ModelImportException){}
            token.ThrowIfCancellationRequested();
            string revision=Hash(Encoding.UTF8.GetBytes("model-maintenance.v1\n"+hash+"\n"+payloadHash+"\n"+(payload?.LongLength??-1).ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+(metadata==null?"missing":Hash(metadata))));
            return new(hash,revision,name,payload!=null,metadata!=null,valid,IsIncluded(hash),payload?.LongLength??0);
        }
        internal async Task<ModelFileReview> InspectFileAsync(string hash,CancellationToken token=default)
        {
            _=AssetPath(hash);using var use=workspaceWrites.Write();await writes.WaitAsync(token).ConfigureAwait(false);
            try{lock(reviewGate)reviews.Remove(hash);var value=await Task.Run(()=>InspectFile(hash,token),token).ConfigureAwait(false);token.ThrowIfCancellationRequested();KeepReview(value);return value;}
            finally{writes.Release();}
        }
        internal async Task<ModelFileReview> RemoveDownloadAsync(string hash,string revision,Func<Task<string>> protection,CancellationToken token=default)
        {
            _=AssetPath(hash);if(!ValidHash(revision)||protection==null)throw new ArgumentException("Inspect the model and supply its exact reviewed revision.");
            if(IsIncluded(hash))throw new InvalidOperationException("The included Maestro avatar is protected. Its private copy may be restored automatically when used.");
            using var freeze=workspaceWrites.TryFreeze(out var blocked)??throw new InvalidOperationException(blocked);
            using var lifetime=workspaceWrites.RetainRead();await writes.WaitAsync(token);
            try{
                using var retirement=Assets.TryRetire(ImportedAssetKind.Model,hash,out blocked)??throw new InvalidOperationException(blocked);
                InvalidateReview(hash);
                var before=await Task.Run(()=>InspectFile(hash,token),token);
                if(before.Revision!=revision)throw new InvalidOperationException("This model changed. Inspect it again before removing its download.");
                if(!before.PayloadPresent)throw new InvalidOperationException("This model download is already absent. Refresh its inspection.");
                string reason=await protection();if(reason!=null)throw new InvalidOperationException(reason);
                MaintenanceCheckpoint?.Invoke("ready");token.ThrowIfCancellationRequested();
                // Freeze, serialization and exact-source retirement remain held
                // until the dispatched worker actually closes, even after Stop.
                return await Task.Run(()=>{
                    MaintenanceCheckpoint?.Invoke("deleting");
                    var final=InspectFile(hash,CancellationToken.None);
                    if(final.Revision!=revision)throw new InvalidOperationException("This model changed before deletion. Its current files are preserved.");
                    File.Delete(AssetPath(hash));MaintenanceCheckpoint?.Invoke("deleted");
                    var after=InspectFile(hash,CancellationToken.None);
                    if(after.PayloadPresent)throw new IOException("Model removal was not confirmed. Inspect its current files before retrying.");
                    KeepReview(after);return after;
                }).ConfigureAwait(false);
            }finally{writes.Release();}
        }
    }
}
