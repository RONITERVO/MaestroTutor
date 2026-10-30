// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Maestro.Quest.Persistence;

namespace Maestro.Quest.Imports
{
    /// <summary>Content-addressed private copies; room/undo records never hold external paths.</summary>
    public sealed partial class ModelLibrary
    {
        readonly string directory;
        readonly BundledAvatar included;
        readonly SemaphoreSlim writes=new(1,1);
        readonly WorkspaceWriteGate workspaceWrites;
        public ModelLibrary(string directory,WorkspaceWriteGate writeGate=null,BundledAvatar includedAvatar=null) { this.directory = Path.GetFullPath(directory);workspaceWrites=writeGate??new();included=includedAvatar; }
        public static bool ValidHash(string hash) => hash != null && hash.Length == 64 && hash.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        public static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static ModelAsset Inspect(string name, byte[] bytes) => new() { Hash = Hash(bytes), Name = SafeName(name), Bytes = bytes, Inspection = ModelInspection.Inspect(bytes) };
        internal WorkspaceAssetFence Assets=>workspaceWrites.Assets;
        internal ModelAsset Admit(ModelAsset asset)=>(asset??throw new ArgumentNullException(nameof(asset))).Admit(Assets);
        internal Action<string> ReadCheckpoint;
        public Task<ModelAsset> ReadAsync(string hash,CancellationToken cancellation=default)
        {
            if(!ValidHash(hash))throw new ModelImportException("This room has an invalid model reference.");
            cancellation.ThrowIfCancellationRequested();
            // Admission precedes dispatch. A caller cancellation cannot release the
            // source while its actual read/inspection is still draining.
            IDisposable pin=Assets.Pin(ImportedAssetKind.Model,hash),write=null;
            try {if(included?.Hash==hash)write=workspaceWrites.Write();}
            catch {pin.Dispose();throw;}
            return Task.Run(async()=>{
                try {
                    await writes.WaitAsync(cancellation).ConfigureAwait(false);
                    try {
                        cancellation.ThrowIfCancellationRequested();ReadCheckpoint?.Invoke("reading");cancellation.ThrowIfCancellationRequested();
                        using var raw=ReadOrCopyIncluded(hash,cancellation);
                        ReadCheckpoint?.Invoke("inspected");cancellation.ThrowIfCancellationRequested();
                        RememberBounds(raw);var result=ModelAsset.Owned(raw,Assets,pin);pin=null;return result;
                    } finally {writes.Release();}
                } catch(OperationCanceledException){throw;}
                catch {ForgetBounds(hash);throw;}
                finally {pin?.Dispose();write?.Dispose();}
            });
        }
        ModelAsset ReadOrCopyIncluded(string hash,CancellationToken cancellation)
        {
            if(included?.Hash!=hash||File.Exists(Path.Combine(directory,hash+".glb")))return ReadLocalCore(hash,cancellation);
            var asset=included.Read();
            try {cancellation.ThrowIfCancellationRequested();SaveCore(asset);return asset;}
            catch {asset.Dispose();throw;}
        }
        ModelAsset ReadLocalCore(string hash,CancellationToken cancellation=default,bool acoustic=false)
        {
            if (!ValidHash(hash)) throw new ModelImportException("This room has an invalid model reference.");
            string path = Path.Combine(directory, hash + ".glb");
            if (!File.Exists(path)) throw new ModelImportException("This model's local copy is missing. Import the original file again.");
            WorkspaceArchive.NoLink(directory);WorkspaceArchive.NoLink(path);
            byte[] bytes = ReadBounded(path,cancellation);cancellation.ThrowIfCancellationRequested();var asset = acoustic ? new ModelAsset{Hash=Hash(bytes),Name=hash,Bytes=bytes,Inspection=ModelInspection.InspectAcoustics(bytes)} : Inspect(hash, bytes);
            if (asset.Hash != hash) throw new ModelImportException("The saved model copy is damaged. Import the original file again.");
            return asset;
        }
        public async Task SaveAsync(ModelAsset asset)
        {
            using var write=workspaceWrites.Write();using var admitted=Admit(asset);
            await writes.WaitAsync().ConfigureAwait(false);
            try {await Task.Run(()=>SaveCore(admitted)).ConfigureAwait(false);} finally {writes.Release();}
        }
        void SaveCore(ModelAsset asset)
        {
            // Revalidate at the persistence boundary; the caller cannot substitute a hash/path.
            using var check = Inspect(asset.Name, asset.Bytes); if (check.Hash != asset.Hash) throw new ModelImportException("The selected model changed during import.");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, check.Hash + ".glb");
            bool exists = File.Exists(path);
            bool intact = exists && new FileInfo(path).Length == asset.Bytes.Length && Hash(ReadBounded(path)) == check.Hash;
            if (!intact)
            {
                var files = new DirectoryInfo(directory).GetFiles("*.glb");
                if ((!exists && files.Length >= 32) || files.Sum(file => file.Length) - (exists ? new FileInfo(path).Length : 0) + asset.Bytes.Length > 256L * 1024 * 1024)
                    throw new ModelImportException("The model library is full (32 files or 256 MB). Existing models remain available.");
                string temporary = path + ".part";
                try { File.WriteAllBytes(temporary, asset.Bytes); if (exists) Maestro.Quest.Persistence.FilePublication.Replace(temporary, path, null); else File.Move(temporary, path); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            // Original metadata remains in the GLB; the sidecar also preserves package attribution.
            string attribution=check.Inspection.Attribution+(included?.Hash==check.Hash?"\n\n"+included.Attribution:"");
            File.WriteAllText(Path.Combine(directory, check.Hash + ".txt"), check.Name + "\nSHA256: " + check.Hash + "\n\n" + attribution, Encoding.UTF8);
            RememberBounds(check);
        }
        public sealed class Entry { public string Hash,Name;public long Bytes; }
        // Metadata discovery only. Selection re-reads, hashes and loads the chosen
        // GLB before committing; a listed file is not a claim of humanoid compatibility.
        public async Task<Entry[]> ListAsync()=>(await ScanAsync().ConfigureAwait(false)).RequireComplete();
        internal async Task<ImportedAssetInventory<Entry>> ScanAsync(CancellationToken cancel=default)
        {
            using var use=workspaceWrites.Write();await writes.WaitAsync(cancel).ConfigureAwait(false);
            try {return await Task.Run(()=>ImportedAssetInventory.Scan(directory,".glb",new ImportedAssetReadBudget(256L*1024*1024,ImportedAssetInventory.ModelMetadataBytes),
                (path,budget,token)=>{
                    WorkspaceArchive.NoLink(path);string hash=Path.GetFileNameWithoutExtension(path);long length=new FileInfo(path).Length;
                    if(!ValidHash(hash)||length<28||length>ModelInspection.MaximumBytes)throw new ModelImportException("The model library contains an invalid entry. Inspect the imported files.");
                    budget.Payload(length);string name=hash;
                    var metadata=budget.ReadMetadata(Path.Combine(directory,hash+".txt"),128*1024,token,()=>new ModelImportException("The model's information file is too large."));
                    if(metadata!=null){using var reader=new StreamReader(new MemoryStream(metadata,false),Encoding.UTF8,true);name=SafeName(reader.ReadLine());}
                    return new Entry {Hash=hash,Name=name,Bytes=length};
                },cancel),cancel).ConfigureAwait(false);} finally {writes.Release();}
        }
        internal bool TryCaptureArchive(out WorkspaceLibraryCapture capture)
        {
            capture=null;if(!writes.Wait(0))return false;
            capture=new WorkspaceLibraryCapture(()=>writes.Release(),(documents,assets)=>{
                if(!Directory.Exists(directory))return;WorkspaceArchive.NoLink(directory);
                var files=new DirectoryInfo(directory).GetFiles("*.glb");
                if(files.Length>32||files.Sum(x=>x.Length)>256L*1024*1024)throw new ModelImportException("Model library exceeds its archive budget.");
                foreach(var file in files){
                    string hash=Path.GetFileNameWithoutExtension(file.Name);if(!ValidHash(hash))throw new ModelImportException("Model library contains an invalid content identity.");
                    string path=file.FullName;assets.Add("models/"+hash+".glb",()=>WorkspaceLibraryCapture.Open(path));
                    string info=Path.Combine(directory,hash+".txt");if(File.Exists(info))documents.Add("models/"+hash+".txt",WorkspaceLibraryCapture.ReadDocument(info,128*1024));
                }
            });return true;
        }
        public static byte[] ReadBounded(string path,CancellationToken cancellation=default)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 28) throw new ModelImportException("This file is incomplete. Choose a complete GLB or VRM export.");
            if (stream.Length > ModelInspection.MaximumBytes) throw new ModelImportException("Choose a GLB or VRM file of 64 MB or smaller.");
            var bytes = new byte[(int)stream.Length]; int offset = 0;
            while (offset < bytes.Length) { cancellation.ThrowIfCancellationRequested();int read = stream.Read(bytes, offset, Math.Min(1024*1024,bytes.Length-offset)); if (read == 0) throw new IOException("Incomplete model copy"); offset += read; }
            if (stream.ReadByte() != -1) throw new IOException("Model copy changed"); return bytes;
        }
        public static string SafeName(string value) => new string((value ?? "Imported model").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(100).ToArray());
    }
}
