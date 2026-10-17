// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // A hidden native model with immutable authored ownership. The saved entity
    // takes it once; abandoning preparation never publishes a placeholder.
    internal sealed class PreparedImportedModel:IDisposable
    {
        internal readonly string Target,Hash;
        readonly RoomModelGeometry settings;
        GameObject root;ImportedModel model;ImportedCollisionGeometry collision;
        bool ended;
        PreparedImportedModel(RoomObjectData data,RoomWorldIdentity world)
        {
            Target=data.id;Hash=data.modelHash;settings=data.modelGeometry.Copy();
            root=new GameObject("Prepared imported object");root.SetActive(false);
            model=root.AddComponent<ImportedModel>();model.ConfigureResourceOwner(world,Target,"object");
        }
        internal static async Task<PreparedImportedModel> Load(ModelAsset asset,RoomObjectData data,RoomWorldIdentity world,CancellationToken cancellation,Func<bool> current)
        {
            if(asset.Hash!=data.modelHash)throw new ModelImportException("The prepared model does not match the saved reference");
            var candidate=new PreparedImportedModel(data,world);Task load=null;
            try {
                Check();load=candidate.model.LoadAsync(asset);
                // Observe cancellation on Unity's context. A queued loader can
                // finish immediately; an active native importer drains separately
                // under its retiring lease, without holding this authored edit.
                while(!load.IsCompleted){Check();await Task.Yield();}
                await load;Check();
                if(!candidate.model.ApplyGeometry(candidate.settings,out var error))throw new ModelImportException(error);
                candidate.model.AttachObjectAcoustics();
                if(candidate.settings.meshCollision)candidate.collision=new ImportedCollisionGeometry(candidate.model,candidate.settings.walkable,new RoomResourceOwner(world,data.id,"collision"));
                return candidate;
            } catch {candidate.Dispose();if(load!=null)_=ObserveDrain(load);throw;}
            void Check(){cancellation.ThrowIfCancellationRequested();if(!current())throw new OperationCanceledException("The room changed while preparing the model");}
        }
        static async Task ObserveDrain(Task task){try{await task;}catch{/* The cancelled candidate no longer has a consumer. */}}
        internal void Validate(RoomObjectData data)
        {
            if(ended||!model||!model.Ready||model.AssetHash!=Hash||data==null||data.kind!=RoomObjectKind.ImportedModel||data.id!=Target||data.modelHash!=Hash||!settings.Same(data.modelGeometry))
                throw new ModelImportException("The imported model changed after preparation");
        }
        internal void Adopt(Transform parent,out ImportedModel imported,out ImportedCollisionGeometry colliders)
        {
            if(ended||!model||!model.Ready)throw new InvalidOperationException("The prepared model has ended");
            ended=true;root.transform.SetParent(parent,false);root.SetActive(true);imported=model;colliders=collision;root=null;model=null;collision=null;
        }
        public void Dispose(){if(ended)return;ended=true;collision?.Dispose();collision=null;model?.Dispose();model=null;if(root)ArtResources.Release(root);root=null;}
    }
}
