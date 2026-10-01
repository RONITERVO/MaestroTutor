// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Imports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Persistence
{
    internal sealed class WorkspaceLocation
    {
        public string Generation {get;}
        public string ReceiptEpoch {get;}
        public bool ReviewRequired {get;}
        internal WorkspaceLocation(string generation,string epoch,bool review){Generation=generation;ReceiptEpoch=epoch;ReviewRequired=review;}
        internal JObject Json()=>new() {["generation"]=Generation,["receiptEpoch"]=ReceiptEpoch,["reviewRequired"]=ReviewRequired};
    }
    internal sealed class WorkspaceSelection
    {
        public string Revision {get;}
        public WorkspaceLocation Active {get;}
        public WorkspaceLocation Previous {get;}
        internal WorkspaceSelection(string revision,WorkspaceLocation active,WorkspaceLocation previous){Revision=revision;Active=active;Previous=previous;}
        internal JObject Json()=>new() {["version"]=1,["revision"]=Revision,["active"]=Active.Json(),["previous"]=Previous?.Json()??(JToken)JValue.CreateNull()};
    }
    internal sealed class WorkspacePrevious
    {
        internal readonly string Revision,Generation,ManifestHash;
        internal WorkspacePrevious(string revision,string generation,string hash){Revision=revision;Generation=generation;ManifestHash=hash;}
    }
    internal sealed class PreparedWorkspaceGeneration
    {
        public string Id {get;}
        public WorkspaceArchiveReceipt Receipt {get;}
        internal PreparedWorkspaceGeneration(string id,WorkspaceArchiveReceipt receipt){Id=id;Receipt=receipt;}
    }
    /// <summary>Filesystem boundary for reviewed restore. Preparing never changes the selected root;
    /// activation commits one pointer only after all content is verified and keeps the previous root.
    /// The host must quiesce/recreate its owners and enforce ReviewRequired before executing imports.
    /// No live runtime is switched by this store alone.</summary>
    internal sealed partial class WorkspaceGenerationStore
    {
        const string Original="original",Initial="initial";
        const int MaximumGenerations=64;
        readonly string appRoot,root,generations,staging,pointer;
        readonly Action<string> fault;
        static readonly UTF8Encoding Utf8=new(false,true);
        public WorkspaceGenerationStore(string applicationData,Action<string> fault=null)
        {
            appRoot=Path.GetFullPath(applicationData).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(appRoot.Length<=Path.GetPathRoot(Path.GetFullPath(applicationData)).Length)throw new ArgumentException("Use an app-owned data directory below the drive root.");
            root=Path.Combine(appRoot,"workspace-generations.v1");generations=Path.Combine(root,"generations");staging=Path.Combine(root,"staging");pointer=Path.Combine(root,"current.v1.json");this.fault=fault;
        }
        static bool Id(string value)=>value!=null&&value.Length==32&&value.All(c=>c>='a'&&c<='f'||c>='0'&&c<='9');
        static bool Exact(JObject value,params string[] keys)=>value!=null&&value.Count==keys.Length&&keys.All(value.ContainsKey);
        static bool TextId(JToken value)=>value?.Type==JTokenType.String&&Id((string)value);
        static InvalidDataException Invalid(string message)=>new(message);
        void CheckRoots()
        {
            foreach(string path in new[]{appRoot,root,generations,staging}){if(File.Exists(path))throw Invalid("A workspace storage directory is unavailable.");if(Directory.Exists(path))WorkspaceArchive.NoLink(path);}
            if(Directory.Exists(pointer))throw Invalid("Workspace selection is not a file.");
            if(Directory.Exists(appRoot)&&Directory.EnumerateDirectories(appRoot,"workspace-generations.v*").Any(x=>Path.GetFileName(x)!="workspace-generations.v1"))throw Invalid("A different workspace generation format exists; its data is preserved.");
            if(Directory.Exists(root)&&Directory.EnumerateFiles(root,"current.v*.json").Any(x=>Path.GetFileName(x)!="current.v1.json"))throw Invalid("A different workspace selection format exists; its data is preserved.");
        }
        FileStream Lease(bool initialize=true)
        {
            CheckRoots();Directory.CreateDirectory(generations);Directory.CreateDirectory(staging);CheckRoots();
            string path=Path.Combine(root,"writer.lock");if(File.Exists(path))WorkspaceArchive.NoLink(path);
            var lease=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            try {
                // Establish the original selection before any activation attempt. The first switch
                // then replaces an existing pointer and retains its baseline just like later switches.
                if(initialize&&!File.Exists(pointer)){var original=Load();Commit(original);}return lease;
            }catch{lease.Dispose();throw;}
        }
        static byte[] Bytes(string path,int limit)
        {
            WorkspaceArchive.NoLink(path);using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
            if(file.Length<1||file.Length>limit)throw Invalid("Workspace metadata exceeds its limit.");var bytes=new byte[(int)file.Length];int offset=0;
            while(offset<bytes.Length){int count=file.Read(bytes,offset,bytes.Length-offset);if(count==0)throw Invalid("Workspace metadata was truncated.");offset+=count;}
            if(file.ReadByte()!=-1)throw Invalid("Workspace metadata changed while reading.");return bytes;
        }
        static JObject Read(string path,int limit=4096)
        {
            using var reader=new JsonTextReader(new StringReader(Utf8.GetString(Bytes(path,limit)))) {MaxDepth=8,DateParseHandling=DateParseHandling.None};
            var value=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read())throw Invalid("Trailing workspace metadata.");return value;
        }
        static WorkspaceLocation Location(JToken value)
        {
            if(value is not JObject o||!Exact(o,"generation","receiptEpoch","reviewRequired")||o["generation"]?.Type!=JTokenType.String||o["receiptEpoch"]?.Type!=JTokenType.String||o["reviewRequired"]?.Type!=JTokenType.Boolean)throw Invalid("Invalid workspace location.");
            string generation=(string)o["generation"],epoch=(string)o["receiptEpoch"];
            if(generation!=Original&&!Id(generation)||epoch!=Original&&!Id(epoch)||generation!=Original&&epoch==Original)throw Invalid("Invalid workspace identity.");
            return new WorkspaceLocation(generation,epoch,(bool)o["reviewRequired"]);
        }
        static WorkspaceSelection Selection(JObject value)
        {
            if(!Exact(value,"version","revision","active","previous")||value["version"]?.Type!=JTokenType.Integer||(int)value["version"]!=1||value["revision"]?.Type!=JTokenType.String||(!TextId(value["revision"])&&(string)value["revision"]!=Initial))throw Invalid("Unsupported workspace selection.");
            var active=Location(value["active"]);var previous=value["previous"].Type==JTokenType.Null?null:Location(value["previous"]);
            if(previous?.Generation==active.Generation)throw Invalid("Current and previous workspaces must differ.");
            if((string)value["revision"]==Initial&&(active.Generation!=Original||active.ReceiptEpoch!=Original||active.ReviewRequired||previous!=null))throw Invalid("Invalid original workspace baseline.");
            return new WorkspaceSelection((string)value["revision"],active,previous);
        }
        string GenerationPath(string id){if(!Id(id))throw Invalid("Invalid prepared workspace identity.");return Path.Combine(generations,id);}
        JObject Metadata(string id)
        {
            string path=GenerationPath(id);if(!Directory.Exists(path))throw Invalid("The selected workspace directory is missing.");WorkspaceArchive.NoLink(path);var metadata=Read(Path.Combine(path,"generation.v1.json"));
            if(!(Exact(metadata,"version","id","manifestHash")||Exact(metadata,"version","id","manifestHash","recovery")&&metadata["recovery"]?.Type==JTokenType.Boolean&&(bool)metadata["recovery"]||Exact(metadata,"version","id","manifestHash","damagedRecovery")&&metadata["damagedRecovery"]?.Type==JTokenType.Boolean&&(bool)metadata["damagedRecovery"])||metadata["version"]?.Type!=JTokenType.Integer||(int)metadata["version"]!=1||metadata["id"]?.Type!=JTokenType.String||(string)metadata["id"]!=id||metadata["manifestHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)metadata["manifestHash"]))throw Invalid("Invalid prepared workspace metadata.");
            string data=Path.Combine(path,"data");if(!Directory.Exists(data))throw Invalid("The selected workspace directory is missing.");WorkspaceArchive.NoLink(data);return metadata;
        }
        void CheckLocation(WorkspaceLocation location)
        {
            if(location==null)return;Location(location.Json());
            if(location.Generation!=Original)Metadata(location.Generation);
            else {string path=Path.Combine(appRoot,"room");if(File.Exists(path))throw Invalid("Original workspace path is unavailable.");if(Directory.Exists(path))WorkspaceArchive.NoLink(path);}
        }
        public WorkspaceSelection Load()
        {
            CheckRoots();if(!File.Exists(pointer)) {
                // A replacement never deletes the pointer first. A backup without a pointer is damage,
                // not a fresh install; never silently reopen a different room.
                bool reserved=false;
                if(Directory.Exists(generations)){int count=0;foreach(string folder in Directory.EnumerateDirectories(generations)){
                    if(++count>MaximumGenerations)throw Invalid("Unexpected workspace retention count.");WorkspaceArchive.NoLink(folder);
                    if(new[]{"activation.v1.json",DamageCommit}.Any(name=>File.Exists(Path.Combine(folder,name))||Directory.Exists(Path.Combine(folder,name))))reserved=true;
                }}
                if(File.Exists(pointer+".previous")||reserved)throw Invalid("Workspace selection is missing; explicit recovery is required.");
                var first=new WorkspaceSelection(Initial,new WorkspaceLocation(Original,Original,false),null);CheckLocation(first.Active);return first;
            }
            var value=Selection(Read(pointer));CheckLocation(value.Active);if(value.Revision!=Initial&&value.Active.Generation==Original&&!Directory.Exists(Path.Combine(appRoot,"room")))throw Invalid("The selected original workspace is missing.");return value;
        }
        public string DataDirectory(WorkspaceLocation location)
        {if(location==null)throw new ArgumentNullException(nameof(location));CheckRoots();CheckLocation(location);return location.Generation==Original?Path.Combine(appRoot,"room"):Path.Combine(GenerationPath(location.Generation),"data");}
        public string ReceiptDirectory(WorkspaceLocation location)
        {
            string data=DataDirectory(location);if(location.ReceiptEpoch==Original)return data;
            string epochs=Path.Combine(data,"action-epochs"),selected=Path.Combine(epochs,location.ReceiptEpoch);foreach(string path in new[]{epochs,selected}){if(File.Exists(path))throw Invalid("The action history directory is unavailable.");if(Directory.Exists(path))WorkspaceArchive.NoLink(path);}return selected;
        }
        static void WriteNew(string path,byte[] bytes)
        {using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(bytes,0,bytes.Length);file.Flush(true);}
        static byte[] Json(JObject value)=>Utf8.GetBytes(value.ToString(Formatting.None));
        public PreparedWorkspaceGeneration Prepare(Stream archive,CancellationToken cancellation=default)=>PrepareArchive(archive,true,cancellation);
        // A file chooser preview must also work with missing/damaged selection metadata, and never
        // writes an implicit original pointer. Ordinary activation establishes its own baseline.
        internal PreparedWorkspaceGeneration PrepareImport(Stream archive,CancellationToken cancellation=default)=>PrepareArchive(archive,false,cancellation);
        PreparedWorkspaceGeneration PrepareArchive(Stream archive,bool initialize,CancellationToken cancellation)
        {
            using var lease=Lease(initialize);cancellation.ThrowIfCancellationRequested();
            if(Directory.EnumerateDirectories(generations).Take(MaximumGenerations).Count()>=MaximumGenerations)throw Invalid("Workspace retention is full. Review retained workspaces before importing another.");
            string id=Guid.NewGuid().ToString("N"),target=GenerationPath(id);bool created=false;
            try {
                using var staged=WorkspaceArchive.Stage(archive,staging,cancellation);fault?.Invoke("prepare.verified");
                Directory.CreateDirectory(target);created=true;Directory.Move(staged.DirectoryPath,Path.Combine(target,"data"));
                WriteNew(Path.Combine(target,"manifest.json"),staged.ManifestBytes);fault?.Invoke("prepare.contentMoved");
                WriteNew(Path.Combine(target,"generation.v1.json"),Json(new JObject {["version"]=1,["id"]=id,["manifestHash"]=staged.Receipt.ManifestHash}));
                cancellation.ThrowIfCancellationRequested();fault?.Invoke("prepare.ready");return new PreparedWorkspaceGeneration(id,staged.Receipt);
            }catch{if(created)DeleteOwned(target);throw;}
        }
        internal WorkspacePrevious Previous()
        {
            var current=Load();if(current.Previous==null)return null;
            if(current.Previous.Generation==Original)throw Invalid("The previous workspace has no verified retained snapshot. Explicit recovery is required.");
            var metadata=Metadata(current.Previous.Generation);
            return new WorkspacePrevious(current.Revision,current.Previous.Generation,(string)metadata["manifestHash"]);
        }
        // The previous slot is a retained, immutable accepted snapshot. Copy verified bytes into a
        // fresh preview so ordinary activation can retain today's accepted edits and use fresh receipts.
        internal PreparedWorkspaceGeneration PreparePrevious(string revision,string sourceId,string hash,CancellationToken cancellation=default)
        {
            using var lease=Lease();cancellation.ThrowIfCancellationRequested();var current=Load();Expected(current,revision);
            if(current.Previous?.Generation!=sourceId||sourceId==current.Active.Generation||!ModelLibrary.ValidHash(hash)||(string)Metadata(sourceId)["manifestHash"]!=hash)throw Invalid("The previous workspace identity changed. Inspect it again.");
            if(Directory.EnumerateDirectories(generations).Take(MaximumGenerations).Count()>=MaximumGenerations)throw Invalid("Workspace retention is full. Review retained workspaces before recovering another.");
            string source=GenerationPath(sourceId),id=Guid.NewGuid().ToString("N"),target=GenerationPath(id);bool created=false;
            byte[] manifest=Bytes(Path.Combine(source,"manifest.json"),WorkspaceArchive.MaximumManifestBytes);
            if(ModelLibrary.Hash(manifest)!=hash)throw Invalid("The previous workspace manifest changed. Its files are preserved.");
            try {
                Directory.CreateDirectory(target);created=true;string data=Path.Combine(target,"data");Directory.CreateDirectory(data);
                void Copy(string name,byte[] bytes){cancellation.ThrowIfCancellationRequested();string output=Path.GetFullPath(Path.Combine(data,name.Replace('/',Path.DirectorySeparatorChar)));if(!output.StartsWith(data+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw Invalid("Unsafe recovery path.");Directory.CreateDirectory(Path.GetDirectoryName(output));WriteNew(output,bytes);}
                var receipt=WorkspaceArchive.VerifyPreparedDirectory(Path.Combine(source,"data"),manifest,cancellation,Copy);fault?.Invoke("previous.copied");cancellation.ThrowIfCancellationRequested();
                WriteNew(Path.Combine(target,"manifest.json"),manifest);
                WriteNew(Path.Combine(target,"generation.v1.json"),Json(new JObject {["version"]=1,["id"]=id,["manifestHash"]=hash,["recovery"]=true}));
                WriteNew(Path.Combine(target,"recovery.v1.json"),Json(new JObject {["version"]=1,["origin"]=current.Json(),["source"]=new JObject {["generationId"]=sourceId,["manifestHash"]=hash}}));
                fault?.Invoke("previous.ready");cancellation.ThrowIfCancellationRequested();return new PreparedWorkspaceGeneration(id,receipt);
            }catch{if(created)DeleteOwned(target);throw;}
        }
        void CheckRecoveryOrigin(string id,WorkspaceSelection current=null)
        {
            if((bool?)Metadata(id)["damagedRecovery"]==true)throw Invalid("Use the damaged-workspace recovery operation for this preview.");
            string path=Path.Combine(GenerationPath(id),"recovery.v1.json");if(Directory.Exists(path))throw Invalid("Recovery identity is unavailable.");if(!File.Exists(path)){if((bool?)Metadata(id)["recovery"]==true)throw Invalid("The recovery preview identity is missing. Its files are preserved.");return;}
            current??=Load();
            var record=Read(path);
            if(!Exact(record,"version","origin","source")||record["version"]?.Type!=JTokenType.Integer||(int)record["version"]!=1||record["source"] is not JObject source||!Exact(source,"generationId","manifestHash")||!TextId(source["generationId"])||source["manifestHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)source["manifestHash"]))throw Invalid("Invalid saved recovery identity.");
            var origin=Selection(record["origin"] as JObject);
            if(origin.Previous?.Generation!=(string)source["generationId"]||(string)source["manifestHash"]!=(string)Metadata(id)["manifestHash"]||!JToken.DeepEquals(origin.Json(),current.Json()))throw Invalid("The recovery preview belongs to an earlier workspace selection. Select the current previous workspace again.");
        }
        WorkspaceArchiveReceipt Verify(string id,string hash,CancellationToken cancellation)
        {
            if(!ModelLibrary.ValidHash(hash)||(string)Metadata(id)["manifestHash"]!=hash)throw Invalid("The selected workspace does not match its preview.");
            string path=GenerationPath(id);byte[] manifest=Bytes(Path.Combine(path,"manifest.json"),WorkspaceArchive.MaximumManifestBytes);
            if(ModelLibrary.Hash(manifest)!=hash)throw Invalid("The prepared manifest changed after inspection.");
            return WorkspaceArchive.VerifyPreparedDirectory(Path.Combine(path,"data"),manifest,cancellation);
        }
        public PreparedWorkspaceGeneration InspectPrepared(string id,string hash,CancellationToken cancellation=default)
        {
            using var lease=Lease();if(Reserved(id))throw Invalid("This workspace was already reserved for activation. Inspect the current selection.");CheckRecoveryOrigin(id);
            return new PreparedWorkspaceGeneration(id,Verify(id,hash,cancellation));
        }
        void Expected(WorkspaceSelection value,string revision)
        {if(value.Revision!=revision)throw Invalid("The active workspace changed. Review the current selection before continuing.");}
        void Commit(WorkspaceSelection next)
        {
            string pending=pointer+"."+Guid.NewGuid().ToString("N")+".pending";
            try {
                WriteNew(pending,Json(next.Json()));fault?.Invoke("pointer.beforeCommit");
                if(File.Exists(pointer)){WorkspaceArchive.NoLink(pointer);if(File.Exists(pointer+".previous"))WorkspaceArchive.NoLink(pointer+".previous");File.Replace(pending,pointer,pointer+".previous");}
                else File.Move(pending,pointer);
                fault?.Invoke("pointer.afterCommit");
            }finally{if(File.Exists(pending))File.Delete(pending);}
        }
        // Both generations are verified before one pointer commit. The retained generation must
        // be captured from accepted live owners under WorkspaceEditHold, not copied from autosaves.
        // Its exact identity and the origin selection remain bound to this activation on retry.
        public WorkspaceSelection Activate(string id,string hash,string expectedRevision,string retainedId,string retainedHash,CancellationToken cancellation=default)
        {
            using var lease=Lease();cancellation.ThrowIfCancellationRequested();var current=Load();
            if(id==retainedId||!Id(retainedId)||!ModelLibrary.ValidHash(retainedHash))throw Invalid("Choose a separate retained workspace snapshot.");
            if(!ModelLibrary.ValidHash(hash)||(string)Metadata(id)["manifestHash"]!=hash)throw Invalid("The selected workspace does not match its preview.");
            string attempt=Path.Combine(GenerationPath(id),"activation.v1.json");WorkspaceSelection next;
            if(File.Exists(attempt)) {
                var record=ActivationRecord(attempt);var origin=Selection(record["origin"] as JObject);
                if((string)record["from"]!=expectedRevision||(string)record["manifestHash"]!=hash||(string)record["retained"]["generation"]!=retainedId||(string)record["retained"]["manifestHash"]!=retainedHash)throw Invalid("The activation identity belongs to a different request.");
                next=Selection(record["next"] as JObject);
                if(next.Active.Generation!=id)throw Invalid("Invalid saved activation attempt.");
                if(current.Revision==next.Revision){if(!JToken.DeepEquals(current.Json(),next.Json()))throw Invalid("The activation outcome does not match its saved identity.");return current;}
                Expected(current,expectedRevision);if(!JToken.DeepEquals(origin.Json(),current.Json()))throw Invalid("Saved activation origin does not match.");
                CheckRecoveryOrigin(id,current);Verify(id,hash,cancellation);Verify(retainedId,retainedHash,cancellation);
            }else {
                Expected(current,expectedRevision);
                if(current.Active.Generation==id||current.Previous?.Generation==id||current.Active.Generation==retainedId||current.Previous?.Generation==retainedId||Reserved(id)||Reserved(retainedId))throw Invalid("Activation requires two unused, verified workspace generations.");
                CheckRecoveryOrigin(id,current);Verify(id,hash,cancellation);Verify(retainedId,retainedHash,cancellation);
                next=new WorkspaceSelection(Guid.NewGuid().ToString("N"),new WorkspaceLocation(id,Guid.NewGuid().ToString("N"),true),new WorkspaceLocation(retainedId,Guid.NewGuid().ToString("N"),true));
                WriteNew(attempt,Json(new JObject {["version"]=1,["from"]=expectedRevision,["origin"]=current.Json(),["manifestHash"]=hash,
                    ["retained"]=new JObject {["generation"]=retainedId,["manifestHash"]=retainedHash},["next"]=next.Json()}));fault?.Invoke("activation.reserved");
            }
            cancellation.ThrowIfCancellationRequested();Commit(next);return next;
        }
        // Read-only reconciliation never retries a commit after an edit hold was released.
        internal WorkspaceSelection CommittedActivation(string id,string hash,string revision,string retainedId,string retainedHash)
        {
            using var lease=Lease();var current=Load();string path=Path.Combine(GenerationPath(id),"activation.v1.json");
            if(!File.Exists(path))return null;
            var record=ActivationRecord(path);
            if((string)record["from"]!=revision||(string)record["manifestHash"]!=hash||(string)record["retained"]["generation"]!=retainedId||(string)record["retained"]["manifestHash"]!=retainedHash)throw Invalid("The activation belongs to a different retained snapshot.");
            var next=Selection(record["next"] as JObject);if(next.Active.Generation!=id)throw Invalid("Invalid activation generation.");
            return JToken.DeepEquals(current.Json(),next.Json())?current:null;
        }
        static JObject ActivationRecord(string path)
        {
            var record=Read(path);
            if(!Exact(record,"version","from","origin","manifestHash","retained","next")||record["version"]?.Type!=JTokenType.Integer||(int)record["version"]!=1||record["from"]?.Type!=JTokenType.String||record["manifestHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)record["manifestHash"])||record["retained"] is not JObject retained||!Exact(retained,"generation","manifestHash")||!TextId(retained["generation"])||retained["manifestHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)retained["manifestHash"]))throw Invalid("Invalid workspace activation identity.");
            var origin=Selection(record["origin"] as JObject);var next=Selection(record["next"] as JObject);
            if(origin.Revision!=(string)record["from"]||next.Revision==origin.Revision||next.Previous?.Generation!=(string)retained["generation"]||!next.Active.ReviewRequired||!next.Previous.ReviewRequired||next.Active.Generation==Original||next.Active.ReceiptEpoch==next.Previous.ReceiptEpoch||next.Active.Generation==origin.Active.Generation||next.Previous.Generation==origin.Active.Generation)throw Invalid("Invalid saved workspace activation boundary.");
            return record;
        }
        bool Reserved(string id,string except=null)
        {
            int count=0;
            foreach(string folder in Directory.EnumerateDirectories(generations)) {
                if(++count>MaximumGenerations)throw Invalid("Unexpected workspace retention count.");WorkspaceArchive.NoLink(folder);if(Path.GetFileName(folder)==except)continue;
                if(DamageReferences(folder,id))return true;
                string path=Path.Combine(folder,"activation.v1.json");if(!File.Exists(path))continue;
                var record=ActivationRecord(path);if((string)record["next"]["active"]["generation"]!=Path.GetFileName(folder))throw Invalid("Invalid activation reservation location.");
                if(Path.GetFileName(folder)==id||(string)record["retained"]["generation"]==id)return true;
            }
            return false;
        }
        public WorkspaceSelection RestorePrevious(string expectedRevision)
        {
            using var lease=Lease();var current=Load();Expected(current,expectedRevision);if(current.Previous==null)throw Invalid("There is no previous workspace to restore.");CheckLocation(current.Previous);if(current.Previous.Generation==Original&&!Directory.Exists(Path.Combine(appRoot,"room")))throw Invalid("The previous original workspace is missing.");
            var next=new WorkspaceSelection(Guid.NewGuid().ToString("N"),new WorkspaceLocation(current.Previous.Generation,Guid.NewGuid().ToString("N"),true),current.Active);Commit(next);return next;
        }
        // capturedHash comes from a fresh native capture under WorkspaceEditHold after accepted
        // documents were flushed. It is never accepted from the action's wire arguments.
        public WorkspaceSelection CompleteReview(string reviewId,string generation,string inspectedHash,string expectedRevision,string capturedHash,CancellationToken cancellation=default)
        {
            if(!Id(reviewId)||!ModelLibrary.ValidHash(inspectedHash)||capturedHash!=inspectedHash)throw Invalid("Workspace contents changed. Prepare and inspect a new review.");
            using var lease=Lease();cancellation.ThrowIfCancellationRequested();var current=Load();string path=Path.Combine(root,"review.v1.json");
            JObject record=File.Exists(path)?ReviewRecord(path):null;WorkspaceSelection next;
            if(record!=null&&(string)record["reviewId"]==reviewId) {
                var origin=Selection(record["origin"] as JObject);next=Selection(record["next"] as JObject);
                if(origin.Revision!=expectedRevision||origin.Active.Generation!=generation||(string)record["manifestHash"]!=inspectedHash)throw Invalid("This review identity belongs to different contents.");
                if(JToken.DeepEquals(current.Json(),next.Json()))return current;
                if(!JToken.DeepEquals(current.Json(),origin.Json()))throw Invalid("The reviewed workspace selection changed.");
            }else {
                Expected(current,expectedRevision);
                if(!current.Active.ReviewRequired||current.Active.Generation!=generation)throw Invalid("Inspect the selected workspace that requires review.");
                next=new WorkspaceSelection(Guid.NewGuid().ToString("N"),new WorkspaceLocation(current.Active.Generation,current.Active.ReceiptEpoch,false),current.Previous);
                var proof=new JObject {["version"]=1,["reviewId"]=reviewId,["manifestHash"]=inspectedHash,["origin"]=current.Json(),["next"]=next.Json()};
                string pending=path+"."+Guid.NewGuid().ToString("N")+".pending";
                try {
                    WriteNew(pending,Json(proof));if(File.Exists(path)){WorkspaceArchive.NoLink(path);if(File.Exists(path+".previous"))WorkspaceArchive.NoLink(path+".previous");File.Replace(pending,path,path+".previous");}else File.Move(pending,path);
                }finally{if(File.Exists(pending))File.Delete(pending);}
                fault?.Invoke("review.reserved");
            }
            cancellation.ThrowIfCancellationRequested();Commit(next);return next;
        }
        internal WorkspaceSelection CommittedReview(string reviewId,string generation,string hash,string revision)
        {
            using var lease=Lease();var current=Load();string path=Path.Combine(root,"review.v1.json");if(!File.Exists(path))return null;
            var record=ReviewRecord(path);var origin=Selection(record["origin"] as JObject);var next=Selection(record["next"] as JObject);
            if((string)record["reviewId"]!=reviewId||(string)record["manifestHash"]!=hash||origin.Active.Generation!=generation||origin.Revision!=revision)return null;
            return JToken.DeepEquals(current.Json(),next.Json())?current:null;
        }
        static JObject ReviewRecord(string path)
        {
            var record=Read(path);
            if(!Exact(record,"version","reviewId","manifestHash","origin","next")||record["version"]?.Type!=JTokenType.Integer||(int)record["version"]!=1||!TextId(record["reviewId"])||record["manifestHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)record["manifestHash"]))throw Invalid("Invalid saved workspace review.");
            var origin=Selection(record["origin"] as JObject);var next=Selection(record["next"] as JObject);
            if(!origin.Active.ReviewRequired||next.Active.ReviewRequired||origin.Revision==next.Revision||origin.Active.Generation!=next.Active.Generation||origin.Active.ReceiptEpoch!=next.Active.ReceiptEpoch||!JToken.DeepEquals(origin.Previous?.Json(),next.Previous?.Json()))throw Invalid("Invalid saved review boundary.");
            return record;
        }
        public void DiscardPrepared(string id,string hash)
        {
            using var lease=Lease();var current=Load();string path=GenerationPath(id);
            if(current.Active.Generation==id||current.Previous?.Generation==id||Reserved(id))throw Invalid("An activated or reserved workspace cannot be discarded as a preview.");
            if((string)Metadata(id)["manifestHash"]!=hash)throw Invalid("The prepared identity does not match.");DeleteOwned(path);
        }
        void DeleteOwned(string path)
        {
            if(Path.GetDirectoryName(Path.GetFullPath(path))!=generations||!Id(Path.GetFileName(path)))throw Invalid("Unsafe workspace cleanup path.");
            if(!Directory.Exists(path))return;int visited=0;
            void Check(string folder,int depth=0){if(depth>8)throw Invalid("Cleanup paths are too deep.");WorkspaceArchive.NoLink(folder);foreach(var entry in Directory.EnumerateFileSystemEntries(folder)){if(++visited>WorkspaceArchive.MaximumEntries*2+10)throw Invalid("Too many cleanup paths.");WorkspaceArchive.NoLink(entry);if(Directory.Exists(entry))Check(entry,depth+1);}}
            Check(path);Directory.Delete(path,true);
        }
    }
}
