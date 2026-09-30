// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    [Serializable] public sealed class MotionOrigin
    {
        public string sourceHash;
        public int clipIndex;
        public MotionOrigin Copy() => (MotionOrigin)MemberwiseClone();
    }
    [Serializable] public sealed class MotionSource
    {
        public string hash,name,attribution;
        public MotionSource Copy() => (MotionSource)MemberwiseClone();
    }
    [Serializable] public sealed class MotionEntry
    {
        public string id,hash,rigHash,name;
        public string[] tags = Array.Empty<string>();
        public MotionOrigin[] origins = Array.Empty<MotionOrigin>();
        public float duration;
        public int bytes,curveValues;
        public bool favourite,archived,removed;
        public bool Short => duration < .1f;
        public MotionEntry Copy() { var copy = (MotionEntry)MemberwiseClone(); copy.tags = (string[])tags.Clone(); copy.origins = origins.Select(x => x.Copy()).ToArray(); return copy; }
    }
    [Serializable] sealed class MotionCatalogue
    {
        public int version = 2;
        public MotionEntry[] entries = Array.Empty<MotionEntry>();
        public MotionSource[] sources = Array.Empty<MotionSource>();
        public MotionCatalogue Copy() => new() { version = version,entries = entries.Select(x => x.Copy()).ToArray(),sources = sources.Select(x => x.Copy()).ToArray() };
    }
    /// <summary>Private versioned catalogue and motion payloads; all Unity clip methods run on the main thread.</summary>
    public sealed class MotionLibrary : IDisposable
    {
        public const int MaximumEntries = 1024, MaximumResidentClips = 8, MaximumResidentCurveValues = 800000;
        public const long MaximumDiskBytes = 128L*1024*1024;
        const int MaximumCatalogueBytes = 16*1024*1024;
        readonly string directory,primary,backup;
        readonly object gate = new();
        readonly SemaphoreSlim writes = new(1,1);
        MotionCatalogue catalogue;
        bool disposed,readOnly;
        readonly HashSet<string> removing = new();
        public string Notice { get; private set; }
        public bool ReadOnly => readOnly;
        internal sealed class Cached
        {
            public MotionEntry Entry;
            public AnimationClip Clip;
            public Task Load;
            public int Users;
            public long Used;
        }
        readonly Dictionary<string,Cached> cache = new();
        long clock;
        public int ResidentClipCount => cache.Count;
        public int ResidentCurveValues => cache.Values.Sum(x => x.Entry.curveValues);
        public sealed class Lease : IDisposable
        {
            MotionLibrary owner; Cached value;
            public AnimationClip Clip => value?.Clip;
            public string RigHash => value?.Entry.rigHash;
            public string Id => value?.Entry.id;
            internal Lease(MotionLibrary owner,Cached value) { this.owner = owner; this.value = value; }
            public void Dispose() { if (value == null) return; value.Users = Math.Max(0,value.Users-1); value.Used = ++owner.clock; value = null; owner = null; }
        }
        public MotionLibrary(string directory)
        {
            this.directory = Path.GetFullPath(directory); primary = Path.Combine(this.directory,"motions.v2.json"); backup = primary+".backup";
            string source=File.Exists(primary) || File.Exists(backup) ? primary : Path.Combine(this.directory,"motions.v1.json");
            int expected=source == primary ? 2 : 1;
            if (TryRead(source,out catalogue,out bool unsupported,expected)) { catalogue.version=2; return; }
            if (unsupported) { readOnly = true; Notice = "This motion catalogue needs a different app version. Its files are preserved."; catalogue = new MotionCatalogue(); return; }
            if (TryRead(source+".backup",out catalogue,out _,expected)) { catalogue.version=2; Notice = "Recovered the motion library from its backup"; return; }
            if (File.Exists(source) || File.Exists(source+".backup")) { readOnly = true; Notice = "The motion catalogue is unreadable. Its files are retained for recovery; imports are paused."; }
            catalogue = new MotionCatalogue();
        }
        internal bool TryCaptureArchive(out WorkspaceLibraryCapture capture)
        {
            capture=null;if(!writes.Wait(0))return false;
            try {
                if(disposed||readOnly)throw new ModelImportException(Notice??"Motion library is unavailable.");
                MotionCatalogue copy;lock(gate)copy=catalogue.Copy();
                capture=new WorkspaceLibraryCapture(()=>writes.Release(),(documents,assets)=>{
                    if(Directory.Exists(directory))WorkspaceArchive.NoLink(directory);
                    documents.Add("motions/motions.v2.json",new UTF8Encoding(false,true).GetBytes(JsonConvert.SerializeObject(copy,Formatting.None)));
                    foreach(var entry in copy.entries.Where(x=>!x.removed)){string path=PayloadPath(entry.hash);assets.Add("motions/"+entry.hash+".motion.glb",()=>WorkspaceLibraryCapture.Open(path));}
                });return true;
            }catch{writes.Release();throw;}
        }
        public MotionEntry Inspect(string id)
        {
            lock (gate) { var entry=catalogue.entries.FirstOrDefault(x => x.id == id)?.Copy(); if (entry != null && removing.Contains(id)) entry.removed=true; return entry; }
        }
        public MotionEntry Find(string id) { var entry=Inspect(id); return entry?.removed == false ? entry : null; }
        public bool PayloadPresent(string id) { var entry=Inspect(id); return entry != null && File.Exists(PayloadPath(entry.hash)); }
        public bool Downloaded(string id) { var entry=Inspect(id); return entry != null && !entry.removed && PayloadPresent(id); }
        public bool Pinned(string id) => cache.TryGetValue(id,out var value) && (value.Users > 0 || !value.Load.IsCompleted);
        public MotionSource[] Sources() { lock (gate) return catalogue.sources.Select(x => x.Copy()).ToArray(); }
        public MotionEntry[] List(string query = "",string rigHash = null,bool includeShort = false,bool favouritesOnly = false,bool archivedOnly = false)
        {
            lock (gate) return catalogue.entries.Where(x => x.archived == archivedOnly && (archivedOnly || !x.removed) && (includeShort || !x.Short) && (!favouritesOnly || x.favourite) &&
                (rigHash == null || x.rigHash == rigHash) && (string.IsNullOrWhiteSpace(query) || x.name.IndexOf(query,StringComparison.OrdinalIgnoreCase) >= 0 || x.tags.Any(t => t.IndexOf(query,StringComparison.OrdinalIgnoreCase) >= 0)))
                .OrderByDescending(x => x.favourite).ThenBy(x => x.name,StringComparer.OrdinalIgnoreCase).ThenBy(x => x.id,StringComparer.Ordinal).Select(x => x.Copy()).ToArray();
        }
        public const int SearchPageSize=12;
        public sealed class SearchPage { public MotionEntry[] Entries; public int Offset,Total; }
        public SearchPage Search(string query,string rigHash,bool includeShort,bool favouritesOnly,bool archivedOnly,int offset)
        {
            var matches=List(query,rigHash,includeShort,favouritesOnly,archivedOnly);
            int page=matches.Length==0 ? 0 : Math.Min(Math.Max(0,offset)/SearchPageSize,(matches.Length-1)/SearchPageSize)*SearchPageSize;
            return new SearchPage {Entries=matches.Skip(page).Take(SearchPageSize).ToArray(),Offset=page,Total=matches.Length};
        }
        public async Task ArchiveAsync(string id,bool archived)
        {
            await writes.WaitAsync().ConfigureAwait(false);
            try { await Task.Run(() => {
                if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
                if (readOnly) throw new ModelImportException(Notice);
                MotionCatalogue candidate; lock (gate) candidate=catalogue.Copy();
                var entry=candidate.entries.FirstOrDefault(x => x.id == id) ?? throw new ModelImportException("This motion is missing from the catalogue.");
                if (!archived && entry.removed) throw new ModelImportException("Import its original export again to restore this motion and its existing identity.");
                entry.archived=archived; Save(candidate); lock (gate) catalogue=candidate;
            }).ConfigureAwait(false); } finally { writes.Release(); }
        }
        // Call on the Unity thread. The final guard runs after waiting for other
        // writes; hiding the entry prevents new assignments/leases during IO.
        public Task RemoveDownloadAsync(string id,Func<Task<string>> protection) => MaintainAsync(id,protection,false);
        public Task ForgetAsync(string id,Func<Task<string>> protection) => MaintainAsync(id,protection,true);
        async Task MaintainAsync(string id,Func<Task<string>> protection,bool forget)
        {
            if (protection == null) throw new ArgumentNullException(nameof(protection));
            await writes.WaitAsync();
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
                if (readOnly) throw new ModelImportException(Notice);
                var entry=Inspect(id) ?? throw new ModelImportException("This motion is missing from the catalogue.");
                if (forget && (!entry.removed || PayloadPresent(id))) throw new ModelImportException("Remove the local download before forgetting its details.");
                if (!entry.archived) throw new ModelImportException("Archive this motion before removing its download.");
                if (Pinned(id)) throw new ModelImportException("Stop this motion and wait for loading to finish before removing its download.");
                lock (gate) removing.Add(id);
                string reason=await protection(); if (reason != null) throw new ModelImportException(reason);
                if (cache.TryGetValue(id,out var cached)) Remove(cached);
                await Task.Run(() => {
                    if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
                    MotionCatalogue candidate; lock (gate) candidate=catalogue.Copy();
                    if (forget)
                    {
                        candidate.entries=candidate.entries.Where(x => x.id != id).ToArray();
                        var needed=candidate.entries.SelectMany(x => x.origins).Select(x => x.sourceHash).ToHashSet();
                        candidate.sources=candidate.sources.Where(x => needed.Contains(x.hash)).ToArray();
                    }
                    else candidate.entries.First(x => x.id == id).removed=true;
                    // Publish the tombstone before removing bytes. An interrupted
                    // delete can be retried, and a reimport keeps the same GUID.
                    Save(candidate); lock (gate) catalogue=candidate;
                    string path=PayloadPath(entry.hash);
                    if (!forget && File.Exists(path)) File.Delete(path);
                });
            }
            finally { lock (gate) removing.Remove(id); writes.Release(); }
        }
        public async Task<MotionEntry[]> ImportAsync(string fileName,byte[] bytes,string category = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
            await writes.WaitAsync().ConfigureAwait(false);
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
                if (readOnly) throw new ModelImportException(Notice);
                // Serialize extraction too: a future bulk picker must not multiply
                // peak decoded memory. This creates no Unity models or textures.
                var packs = await Task.Run(() => MotionPack.Extract(fileName,bytes)).ConfigureAwait(false);
                return await Task.Run(() => {
                    if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
                    if (readOnly) throw new ModelImportException(Notice);
                    MotionCatalogue candidate; lock (gate) candidate = catalogue.Copy();
                    var entries = candidate.entries.ToList(); var sources = candidate.sources.ToList(); var added = new List<MotionEntry>();
                    string sourceHash = packs[0].SourceHash;
                    if (!sources.Any(x => x.hash == sourceHash)) sources.Add(new MotionSource { hash = sourceHash,name = ModelLibrary.SafeName(fileName),attribution = packs[0].Attribution });
                    foreach (var pack in packs)
                    {
                        var entry = entries.FirstOrDefault(x => x.hash == pack.Hash);
                        if (entry == null)
                        {
                            entry = new MotionEntry { id = Guid.NewGuid().ToString("N"),hash = pack.Hash,rigHash = pack.RigHash,name = pack.Name,duration = pack.Duration,bytes = pack.Bytes.Length,curveValues = pack.CurveValues };
                            entries.Add(entry);
                        }
                        entry.archived=false; entry.removed=false;
                        if (!entry.origins.Any(x => x.sourceHash == sourceHash && x.clipIndex == pack.SourceClip)) entry.origins = entry.origins.Append(new MotionOrigin { sourceHash = sourceHash,clipIndex = pack.SourceClip }).ToArray();
                        if (!string.IsNullOrWhiteSpace(category)) entry.tags = entry.tags.Append(CleanTag(category)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        added.Add(entry);
                    }
                    candidate.entries = entries.ToArray(); candidate.sources = sources.ToArray(); Validate(candidate);
                    Directory.CreateDirectory(directory);
                    long total = new DirectoryInfo(directory).GetFiles("*.motion.glb").Sum(x => x.Length);
                    foreach (var pack in packs.GroupBy(x => x.Hash).Select(x => x.First()))
                    {
                        string path = PayloadPath(pack.Hash); long old = File.Exists(path) ? new FileInfo(path).Length : 0;
                        total = total-old+pack.Bytes.Length;
                        if (total > MaximumDiskBytes) throw new ModelImportException("The motion library has reached its 128 MB disk budget. Existing motions remain available.");
                    }
                    foreach (var pack in packs.GroupBy(x => x.Hash).Select(x => x.First()))
                    {
                        string path = PayloadPath(pack.Hash);
                        if (File.Exists(path) && new FileInfo(path).Length == pack.Bytes.Length && ModelLibrary.Hash(ReadBounded(path,MotionPack.MaximumBytes)) == pack.Hash) continue;
                        WriteAtomic(path,pack.Bytes);
                    }
                    Save(candidate); lock (gate) catalogue = candidate;
                    return added.Select(x => x.Copy()).ToArray();
                }).ConfigureAwait(false);
            }
            finally { writes.Release(); }
        }
        public async Task UpdateAsync(string id,string name,string[] tags,bool favourite)
        {
            await writes.WaitAsync().ConfigureAwait(false);
            try { await Task.Run(() => {
                if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
                if (readOnly) throw new ModelImportException(Notice);
                MotionCatalogue candidate; lock (gate) candidate = catalogue.Copy();
                var entry = candidate.entries.FirstOrDefault(x => x.id == id) ?? throw new ModelImportException("This motion is missing from the library.");
                entry.name = ModelLibrary.SafeName(name); entry.tags = (tags ?? Array.Empty<string>()).Select(CleanTag).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); entry.favourite = favourite;
                Validate(candidate); Save(candidate); lock (gate) catalogue = candidate;
            }).ConfigureAwait(false); } finally { writes.Release(); }
        }
        static string CleanTag(string value) => ModelLibrary.SafeName(value).Trim().Substring(0,Math.Min(32,ModelLibrary.SafeName(value).Trim().Length));
        public async Task<Lease> AcquireAsync(string id,string rigHash)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
            var entry = Find(id) ?? throw new ModelImportException("This motion is missing. Import its original file again.");
            if (entry.rigHash != rigHash) throw new ModelImportException("This motion uses a different rig or rest pose. Choose a compatible motion.");
            if (!cache.TryGetValue(id,out var cached))
            {
                while (cache.Count >= MaximumResidentClips || ResidentCurveValues+entry.curveValues > MaximumResidentCurveValues)
                {
                    var unused = cache.Values.Where(x => x.Users == 0 && x.Load.IsCompleted).OrderBy(x => x.Used).FirstOrDefault();
                    if (unused == null) throw new ModelImportException("Active animations fill the playback budget. Stop a motion before previewing another.");
                    Remove(unused);
                }
                cached = new Cached { Entry = entry,Used = ++clock }; cache.Add(id,cached); cached.Load = Load(cached);
            }
            cached.Users++;
            try { await cached.Load; if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary)); return new Lease(this,cached); }
            catch { cached.Users--; if (cached.Users == 0) Remove(cached); throw; }
        }
        async Task Load(Cached cached)
        {
            var pack = await Task.Run(() => {
                var bytes = ReadBounded(PayloadPath(cached.Entry.hash),MotionPack.MaximumBytes);
                if (ModelLibrary.Hash(bytes) != cached.Entry.hash) throw new ModelImportException("This saved motion is damaged. Import the original file again.");
                var value = MotionPack.Read(bytes);
                if (value.RigHash != cached.Entry.rigHash || value.CurveValues != cached.Entry.curveValues || Math.Abs(value.Duration-cached.Entry.duration) > .0001f || bytes.Length != cached.Entry.bytes)
                    throw new ModelImportException("The saved motion does not match its catalogue entry.");
                return value;
            });
            if (disposed) throw new ObjectDisposedException(nameof(MotionLibrary));
            cached.Clip = await MotionClipCompiler.CompileAsync(pack,() => disposed);
            cached.Clip.name = cached.Entry.name;
        }
        void Remove(Cached value) { if (cache.TryGetValue(value.Entry.id,out var current) && current == value) cache.Remove(value.Entry.id); ArtResources.Release(value.Clip); value.Clip = null; }
        public void Dispose() { if (disposed) return; disposed = true; foreach (var value in cache.Values.ToArray()) Remove(value); }
        string PayloadPath(string hash) { if (!ModelLibrary.ValidHash(hash)) throw new ModelImportException("Invalid motion content identity."); return Path.Combine(directory,hash+".motion.glb"); }
        static byte[] ReadBounded(string path,int maximum)
        {
            if (!File.Exists(path)) throw new ModelImportException("The local motion copy is missing. Import its original file again.");
            using var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if (stream.Length < 1 || stream.Length > maximum) throw new ModelImportException("The saved motion file exceeds its size limit.");
            var bytes = new byte[(int)stream.Length]; int at = 0;
            while (at < bytes.Length) { int read = stream.Read(bytes,at,bytes.Length-at); if (read == 0) throw new IOException("Incomplete motion copy"); at += read; }
            if (stream.ReadByte() != -1) throw new IOException("Motion copy changed"); return bytes;
        }
        static bool Text(string value,int maximum) => value != null && value.Length <= maximum && value.All(c => !char.IsControl(c));
        static void Validate(MotionCatalogue value)
        {
            void Check(bool valid) { if (!valid) throw new ModelImportException("The motion catalogue contains invalid or excessive data."); }
            Check(value != null && (value.version == 1 || value.version == 2) && value.entries != null && value.sources != null && value.entries.Length <= MaximumEntries && value.sources.Length <= MaximumEntries);
            var sources = new HashSet<string>();
            foreach (var source in value.sources) { Check(source != null && ModelLibrary.ValidHash(source.hash) && sources.Add(source.hash) && Text(source.name,100) && source.attribution != null && source.attribution.Length <= 65536); }
            var ids = new HashSet<string>(); var hashes = new HashSet<string>(); int origins = 0;
            foreach (var entry in value.entries)
            {
                Check(entry != null && Guid.TryParseExact(entry.id,"N",out _) && ids.Add(entry.id) && ModelLibrary.ValidHash(entry.hash) && hashes.Add(entry.hash) && ModelLibrary.ValidHash(entry.rigHash));
                Check((value.version >= 2 || !entry.archived && !entry.removed) && (!entry.removed || entry.archived));
                Check(Text(entry.name,100) && !string.IsNullOrWhiteSpace(entry.name) && entry.tags != null && entry.tags.Length <= 16 && entry.tags.All(x => Text(x,32) && !string.IsNullOrWhiteSpace(x)) && entry.tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() == entry.tags.Length);
                Check(float.IsFinite(entry.duration) && entry.duration > 0 && entry.duration <= 3600 && entry.bytes >= 28 && entry.bytes <= MotionPack.MaximumBytes && entry.curveValues > 0 && entry.curveValues <= MaximumResidentCurveValues);
                Check(entry.origins != null && entry.origins.Length > 0 && entry.origins.Length <= MaximumEntries);
                var unique = new HashSet<string>();
                foreach (var origin in entry.origins) { Check(origin != null && sources.Contains(origin.sourceHash) && origin.clipIndex >= 0 && origin.clipIndex < 32 && unique.Add(origin.sourceHash+":"+origin.clipIndex)); }
                origins += entry.origins.Length; Check(origins <= 32768);
            }
            Check(value.entries.Where(x => !x.removed).Sum(x => (long)x.bytes) <= MaximumDiskBytes);
        }
        // Archive snapshots use the same catalogue validation without opening a live library.
        internal static MotionCatalogue DecodeSnapshot(byte[] bytes)
        {
            if(bytes==null||bytes.Length<1||bytes.Length>MaximumCatalogueBytes)throw new ModelImportException("Invalid motion snapshot size.");
            using var reader=new JsonTextReader(new StringReader(new UTF8Encoding(false,true).GetString(bytes))) {MaxDepth=16,DateParseHandling=DateParseHandling.None};
            var json=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            if(reader.Read()||json.Count!=3||json["version"]?.Type!=JTokenType.Integer||(int)json["version"]!=2||json["entries"] is not JArray||json["sources"] is not JArray)throw new ModelImportException("Unsupported motion snapshot.");
            var value=json.ToObject<MotionCatalogue>();Validate(value);return value;
        }
        static bool TryRead(string path,out MotionCatalogue value) => TryRead(path,out value,out _);
        static bool TryRead(string path,out MotionCatalogue value,out bool unsupported,int expected = 2)
        {
            value = null; unsupported = false;
            try
            {
                if (!File.Exists(path)) return false;
                using var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false,true).GetString(ReadBounded(path,MaximumCatalogueBytes)))) { MaxDepth = 16,DateParseHandling = DateParseHandling.None };
                var json = JObject.Load(reader,new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || json["version"]?.Type != JTokenType.Integer) return false;
                if (json["version"].ToString() != expected.ToString()) { unsupported = true; return false; }
                var candidate = json.ToObject<MotionCatalogue>(); Validate(candidate); value = candidate; return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is ArgumentException || error is ModelImportException || error is NullReferenceException || error is InvalidCastException || error is OverflowException) { return false; }
        }
        void Save(MotionCatalogue candidate)
        {
            TryRead(primary,out _,out bool unsupported); if (unsupported) { readOnly=true; throw new ModelImportException("The motion catalogue version changed; its files are preserved."); }
            Validate(candidate); byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(candidate,Formatting.None));
            if (bytes.Length > MaximumCatalogueBytes) throw new ModelImportException("The motion catalogue has reached its metadata budget.");
            Directory.CreateDirectory(directory);
            if (File.Exists(primary)) File.Copy(primary,TryRead(primary,out _) ? backup : primary+".unreadable",true);
            WriteAtomic(primary,bytes);
        }
        static void WriteAtomic(string path,byte[] bytes)
        {
            string pending = path+".pending";
            try
            {
                using (var stream = new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(pending,path,null); else File.Move(pending,path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
