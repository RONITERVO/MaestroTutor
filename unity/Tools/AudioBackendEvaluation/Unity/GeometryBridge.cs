// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;

namespace Maestro.AudioEvaluation
{
    // Main-thread adapter for the app's passive active/retained geometry feed.
    // Preparation yields through the application's shared frame budget. Only
    // complete immutable values reach native workers; there is no saved scene.
    internal sealed class GeometryBridge : IDisposable
    {
        const string BudgetIssue="Acoustic scene budget reached";
        sealed class MeshEntry
        {
            internal string Id,Issue;
            internal AcousticGeometryBinding Definition;
            internal AcousticMeshData Mesh;
        }
        readonly OwnedLifecycle owner;
        readonly Func<AcousticGeometryCatalog> describe;
        readonly RoomPreparationBudget.Window window;
        Dictionary<AcousticGeometryKey,MeshEntry> cache=new();
        AcousticGeometryCatalog target,attempted,cachedDefinition,published;
        Task<Dictionary<AcousticGeometryKey,MeshEntry>> pending;
        int version,pendingVersion,nextId;
        bool closed;
        internal string Issue { get; private set; }
        internal int Omitted { get; private set; }
        internal int OmittedBoundaries { get; private set; }
        internal bool Preparing=>pending!=null;
        internal GeometryBridge(OwnedLifecycle owner,Func<AcousticGeometryCatalog> describe,RoomPreparationBudget.Window window=null)
        {
            this.owner=owner??throw new ArgumentNullException(nameof(owner));
            this.describe=describe??throw new ArgumentNullException(nameof(describe));
            this.window=window??new RoomPreparationBudget.Window();owner.SetGeometryReady(false);
        }
        internal void Poll()
        {
            if(closed)return;
            var latest=describe()??AcousticGeometryCatalog.Unavailable;
            if(target==null||!target.SameDefinitions(latest))
            {target=latest;version++;published=null;owner.SetGeometryReady(false);}
            if(pending!=null&&pending.IsCompleted)
            {
                var completed=pending;pending=null;
                if(completed.IsFaulted)
                {var error=completed.Exception.GetBaseException();if(pendingVersion==version)Issue=error.Message;else attempted=null;}
                else if(completed.IsCanceled||pendingVersion!=version)attempted=null;
                else {cache=completed.Result;cachedDefinition=attempted;}
                completed.Dispose();
            }
            if(!latest.Available)
            {
                owner.SetGeometryReady(false);cache.Clear();cachedDefinition=attempted=published=null;
                Issue="The room geometry owner is unavailable";Omitted=OmittedBoundaries=0;return;
            }
            if(pending==null&&(attempted==null||!attempted.SameDefinitions(latest)))
            {
                attempted=latest;pendingVersion=version;Issue=null;
                pending=Capture(latest,version,new Dictionary<AcousticGeometryKey,MeshEntry>(cache));
            }
            if(cachedDefinition!=null&&cachedDefinition.SameDefinitions(latest)&&
                (published==null||!published.SamePlacements(latest)))Publish(latest);
        }
        async Task<Dictionary<AcousticGeometryKey,MeshEntry>> Capture(AcousticGeometryCatalog catalog,int expected,
                                                                    Dictionary<AcousticGeometryKey,MeshEntry> previous)
        {
            void Validate()
            {
                if(closed||expected!=version||!catalog.SameDefinitions(describe()))throw new OperationCanceledException("Geometry changed during preparation");
            }
            var budget=new RoomPreparationBudget(Validate,window);
            var result=new Dictionary<AcousticGeometryKey,MeshEntry>();var admission=new AcousticGeometryBudget();
            for(int i=0;i<catalog.Count;i++)
            {
                var input=catalog.At(i);
                if(!input.Active)continue;
                previous.TryGetValue(input.Key,out var old);
                MeshEntry entry;
                if(!admission.HasRoom(input.Scanned))
                    entry=new MeshEntry{Id=old?.Id??"geometry/"+(++nextId),Definition=input,Issue=BudgetIssue};
                else if(old!=null&&old.Definition.SameDefinition(input)&&old.Issue!=BudgetIssue)entry=old;
                else
                {
                    await budget.Step();
                    entry=new MeshEntry{Id=old?.Id??"geometry/"+(++nextId),Definition=input};
                    try
                    {
                        if(!input.HasGeometry)throw new InvalidOperationException("Acoustic mesh is unavailable");
                        var mesh=AcousticMeshData.Capture(input.Input);
                        entry.Mesh=old?.Mesh?.SameContent(mesh)==true?old.Mesh:mesh;
                    }
                    catch(Exception error) {entry.Issue=error.Message;}
                }
                if(entry.Mesh!=null&&!admission.Add(entry.Mesh,input.Scanned))
                    entry=new MeshEntry{Id=entry.Id,Definition=input,Issue=BudgetIssue};
                result.Add(input.Key,entry);
            }
            Validate();return result;
        }
        void Publish(AcousticGeometryCatalog catalog)
        {
            // Attempt once per description. Stable failures require a definition
            // change or an explicit Retry; no frame-by-frame reread/upload loop.
            published=catalog;Omitted=0;OmittedBoundaries=catalog.PendingBoundaries;Issue=null;
            var meshes=new List<AcousticMeshPose>();
            try
            {
                for(int i=0;i<catalog.Count;i++)
                {
                    var input=catalog.At(i);if(!input.Active)continue;
                    var entry=cache[input.Key];
                    try
                    {
                        if(entry.Mesh==null)throw new InvalidOperationException(entry.Issue);
                        meshes.Add(new AcousticMeshPose(entry.Id,entry.Mesh,input.Pose,input.Scanned));
                    }
                    catch(Exception error) {Omitted++;if(input.Boundary)OmittedBoundaries++;Issue=error.Message;}
                }
                owner.UpdateGeometry(new AcousticSceneData(meshes.ToArray()));
                bool ready=OmittedBoundaries==0;owner.SetGeometryReady(ready);
                if(!ready&&Issue==null)Issue="A room boundary is still preparing";
            }
            catch(Exception error) {owner.SetGeometryReady(false);Issue=error.Message;}
        }
        internal void Retry()
        {
            if(closed)return;
            version++;attempted=cachedDefinition=published=null;cache.Clear();owner.SetGeometryReady(false);
        }
        public void Dispose()
        {
            if(closed)return;closed=true;version++;owner.SetGeometryReady(false);
            cache.Clear();target=attempted=cachedDefinition=published=null;
            // A yielded capture sees closed before its next unit. Observe a
            // late fault even if the owner is no longer polling this bridge.
            if(pending!=null)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
