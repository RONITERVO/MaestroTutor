// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Imports
{
    public sealed class IncludedMotionResult
    {
        public string ManifestHash;
        public int Added,Preserved,Restored;
    }
    public sealed partial class MotionLibrary
    {
        public BundledMotions Included {get;}
        public Task IncludedInitialization {get;private set;}=Task.CompletedTask;
        public bool InstallingIncluded=>Volatile.Read(ref installingIncluded)!=0;
        public string IncludedStatus {get;private set;}="";
        int installingIncluded;string includedNotice;
        async Task InitializeIncludedAsync()
        {
            try{await InstallIncludedAsync(Included.Hash).ConfigureAwait(false);}
            catch(Exception error){Notice=includedNotice=IncludedStatus=error is ModelImportException?error.Message:"Included animations could not be prepared. Add the included collection again when storage is available.";}
        }
        public async Task<IncludedMotionResult> InstallIncludedAsync(string manifestHash,string restoreId=null,CancellationToken cancellation=default)
        {
            if(Included==null||Included.Hash!=manifestHash)throw new ModelImportException("The included animation package changed. Inspect it before adding motions.");
            if(disposed)throw new ObjectDisposedException(nameof(MotionLibrary));
            if(Interlocked.CompareExchange(ref installingIncluded,1,0)!=0)throw new ModelImportException("Wait for the current included animation copy to finish.");
            try{
                using var write=workspaceWrites.Write();await writes.WaitAsync(cancellation).ConfigureAwait(false);
                try{
                    IncludedStatus="Preparing included animations…";
                    return await Task.Run(()=>{
                        cancellation.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException(nameof(MotionLibrary));if(readOnly)throw new ModelImportException(Notice);
                        MotionCatalogue candidate;lock(gate)candidate=catalogue.Copy();var source=Included.Catalogue;
                        var entries=candidate.entries.ToList();var sources=candidate.sources.ToList();var pending=new System.Collections.Generic.List<MotionEntry>();
                        var result=new IncludedMotionResult {ManifestHash=Included.Hash};
                        if(restoreId!=null){
                            var entry=entries.FirstOrDefault(x=>x.id==restoreId)??throw new ModelImportException("This motion identity is no longer in the library.");
                            var bundled=source.entries.FirstOrDefault(x=>x.hash==entry.hash)??throw new ModelImportException("This exact motion is not in the current included package. Import its original export.");
                            if(entry.rigHash!=bundled.rigHash||entry.bytes!=bundled.bytes||entry.curveValues!=bundled.curveValues||Math.Abs(entry.duration-bundled.duration)>.0001f)throw new ModelImportException("Saved motion metadata differs from the included payload. Its records are preserved for recovery.");
                            entry.removed=false;pending.Add(entry);result.Restored=1;
                        }else foreach(var bundled in source.entries){
                            if(entries.Any(x=>x.hash==bundled.hash)){result.Preserved++;continue;}
                            if(entries.Any(x=>x.id==bundled.id))throw new ModelImportException("An included motion ID conflicts with saved content. Existing choices are preserved.");
                            var entry=bundled.Copy();entries.Add(entry);pending.Add(entry);result.Added++;
                        }
                        var required=pending.SelectMany(x=>x.origins).Select(x=>x.sourceHash).ToHashSet();
                        foreach(var origin in source.sources.Where(x=>required.Contains(x.hash)))if(!sources.Any(x=>x.hash==origin.hash))sources.Add(origin.Copy());
                        candidate.entries=entries.ToArray();candidate.sources=sources.ToArray();Validate(candidate);
                        long total=Directory.Exists(directory)?new DirectoryInfo(directory).GetFiles("*.motion.glb").Sum(x=>x.Length):0;
                        foreach(var entry in pending){string path=PayloadPath(entry.hash);total=checked(total-(File.Exists(path)?new FileInfo(path).Length:0)+entry.bytes);}
                        if(total>MaximumDiskBytes)throw new ModelImportException("The included motions do not fit the remaining 128 MB library budget. Existing motions remain available.");
                        foreach(var entry in pending){
                            cancellation.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException(nameof(MotionLibrary));
                            var bytes=Included.Read(entry.hash);string path=PayloadPath(entry.hash);Directory.CreateDirectory(directory);
                            if(!File.Exists(path)||new FileInfo(path).Length!=bytes.Length||ModelLibrary.Hash(ReadBounded(path,MotionPack.MaximumBytes))!=entry.hash)WriteAtomic(path,bytes);
                        }
                        cancellation.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException(nameof(MotionLibrary));
                        // Publish the complete catalogue only after every new payload is verified and durable.
                        if(pending.Count>0){Save(candidate);lock(gate)catalogue=candidate;}
                        IncludedStatus=result.Added+" included motions added; "+result.Preserved+" saved choices preserved"+(result.Restored>0?"; exact download restored":"");
                        if(includedNotice!=null&&Notice==includedNotice)Notice=null;includedNotice=null;
                        return result;
                    }).ConfigureAwait(false);
                }finally{writes.Release();}
            }catch(Exception error){IncludedStatus=error is ModelImportException?error.Message:error is OperationCanceledException?"Included animation copy stopped. Previously saved choices remain.":"Included animation copy failed. Existing library choices are preserved.";throw;}
            finally{Interlocked.Exchange(ref installingIncluded,0);}
        }
    }
}
