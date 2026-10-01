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
    public sealed class ModelAsset
    {
        public string Hash, Name;
        public byte[] Bytes;
        public ModelInspection Inspection;
    }

    /// <summary>Content-addressed private copies; room/undo records never hold external paths.</summary>
    public sealed class ModelLibrary
    {
        readonly string directory;
        readonly SemaphoreSlim writes=new(1,1);
        readonly WorkspaceWriteGate workspaceWrites;
        public ModelLibrary(string directory,WorkspaceWriteGate writeGate=null) { this.directory = Path.GetFullPath(directory);workspaceWrites=writeGate??new(); }
        public static bool ValidHash(string hash) => hash != null && hash.Length == 64 && hash.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        public static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static ModelAsset Inspect(string name, byte[] bytes) => new() { Hash = Hash(bytes), Name = SafeName(name), Bytes = bytes, Inspection = ModelInspection.Inspect(bytes) };
        public Task<ModelAsset> ReadAsync(string hash) => Task.Run(() =>
        {
            if (!ValidHash(hash)) throw new ModelImportException("This room has an invalid model reference.");
            string path = Path.Combine(directory, hash + ".glb");
            if (!File.Exists(path)) throw new ModelImportException("This model's local copy is missing. Import the original file again.");
            byte[] bytes = ReadBounded(path); var asset = Inspect(hash, bytes);
            if (asset.Hash != hash) throw new ModelImportException("The saved model copy is damaged. Import the original file again.");
            return asset;
        });
        public async Task SaveAsync(ModelAsset asset)
        {
            using var write=workspaceWrites.Write();
            await writes.WaitAsync().ConfigureAwait(false);
            try { await Task.Run(() => {
                // Revalidate at the persistence boundary; the caller cannot substitute a hash/path.
                var check = Inspect(asset.Name, asset.Bytes); if (check.Hash != asset.Hash) throw new ModelImportException("The selected model changed during import.");
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
                    try { File.WriteAllBytes(temporary, asset.Bytes); if (exists) File.Replace(temporary, path, null); else File.Move(temporary, path); }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                // Original metadata remains in the GLB. This readable sidecar is also useful for exports/support.
                File.WriteAllText(Path.Combine(directory, check.Hash + ".txt"), check.Name + "\nSHA256: " + check.Hash + "\n\n" + check.Inspection.Attribution, Encoding.UTF8);
            }).ConfigureAwait(false); } finally { writes.Release(); }
        }
        public sealed class Entry { public string Hash,Name;public long Bytes; }
        // Metadata discovery only. Selection re-reads, hashes and loads the chosen
        // GLB before committing; a listed file is not a claim of humanoid compatibility.
        public async Task<Entry[]> ListAsync()
        {
            using var use=workspaceWrites.Write();await writes.WaitAsync().ConfigureAwait(false);
            try {return await Task.Run(()=>{
                if(!Directory.Exists(directory))return Array.Empty<Entry>();WorkspaceArchive.NoLink(directory);
                var files=new DirectoryInfo(directory).GetFiles("*.glb").OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray();
                if(files.Length>32||files.Sum(f=>f.Length)>256L*1024*1024)throw new ModelImportException("The model library exceeds its discovery budget.");
                return files.Select(file=>{
                    WorkspaceArchive.NoLink(file.FullName);string hash=Path.GetFileNameWithoutExtension(file.Name);
                    if(!ValidHash(hash)||file.Length<28||file.Length>ModelInspection.MaximumBytes)throw new ModelImportException("The model library contains an invalid entry. Inspect the imported files.");
                    string name=hash,info=Path.Combine(directory,hash+".txt");
                    if(File.Exists(info)){
                        WorkspaceArchive.NoLink(info);if(new FileInfo(info).Length>128*1024)throw new ModelImportException("The model's information file is too large.");
                        using var reader=new StreamReader(info,Encoding.UTF8,true);name=SafeName(reader.ReadLine());
                    }
                    return new Entry {Hash=hash,Name=name,Bytes=file.Length};
                }).ToArray();
            }).ConfigureAwait(false);} finally {writes.Release();}
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
        public static byte[] ReadBounded(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 28) throw new ModelImportException("This file is incomplete. Choose a complete GLB or VRM export.");
            if (stream.Length > ModelInspection.MaximumBytes) throw new ModelImportException("Choose a GLB or VRM file of 64 MB or smaller.");
            var bytes = new byte[(int)stream.Length]; int offset = 0;
            while (offset < bytes.Length) { int read = stream.Read(bytes, offset, bytes.Length - offset); if (read == 0) throw new IOException("Incomplete model copy"); offset += read; }
            if (stream.ReadByte() != -1) throw new IOException("Model copy changed"); return bytes;
        }
        public static string SafeName(string value) => new string((value ?? "Imported model").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(100).ToArray());
    }
}
