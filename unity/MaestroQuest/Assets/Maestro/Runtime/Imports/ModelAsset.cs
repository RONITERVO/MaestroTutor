// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Imports
{
    /// <summary>A validated byte view with independently owned source admission.
    /// Disposal closes admission, but keeps the validated byte view available to an importer
    /// that already retained its own pin and is still draining.</summary>
    public sealed class ModelAsset : IDisposable
    {
        public string Hash, Name;
        public byte[] Bytes;
        public ModelInspection Inspection;
        readonly object sync=new();
        WorkspaceAssetFence source;
        string sourceHash;
        IDisposable pin;
        bool disposed;

        internal static ModelAsset Owned(ModelAsset value,WorkspaceAssetFence source,IDisposable pin)
            =>new() {Hash=value.Hash,Name=value.Name,Bytes=value.Bytes,Inspection=value.Inspection,
                source=source,sourceHash=value.Hash,pin=pin};
        void Validate(WorkspaceAssetFence expected,bool requireBound)
        {
            if(disposed)throw new ObjectDisposedException(nameof(ModelAsset));
            if(!ModelLibrary.ValidHash(Hash)||Bytes==null||Inspection==null)
                throw new ModelImportException("Choose a validated model before loading it.");
            if(source!=null&&(source!=expected||sourceHash!=Hash)||requireBound&&source==null&&expected!=null)
                throw new InvalidOperationException("The model source does not belong to this workspace. Read or admit it from the current model library.");
        }
        internal ModelAsset Admit(WorkspaceAssetFence expected)
        {
            if(expected==null)throw new ArgumentNullException(nameof(expected));
            lock(sync){Validate(expected,false);return Owned(this,expected,expected.Pin(ImportedAssetKind.Model,Hash));}
        }
        internal IDisposable RetainForLoad(WorkspaceAssetFence expected)
        {
            lock(sync){
                Validate(expected,true);
                // Raw standalone assets are used by isolated import/format tests.
                // Production native owners always configure their exact library.
                return source?.Pin(ImportedAssetKind.Model,sourceHash);
            }
        }
        public void Dispose()
        {
            IDisposable release;
            lock(sync){if(disposed)return;disposed=true;release=pin;pin=null;}
            release?.Dispose();
        }
    }
}
