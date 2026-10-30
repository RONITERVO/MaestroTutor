// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Imports
{
    public sealed partial class ModelLibrary
    {
        // Shares the library's inspection lane across room owners. No first-use
        // copy, Unity import, image decoding, native model reservation or save.
        internal async Task<ModelAcousticGeometry> PrepareAcousticsAsync(string hash,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!ValidHash(hash))throw new ModelImportException("This room has an invalid model reference.");
            using var pin=Assets.Pin(ImportedAssetKind.Model,hash);
            await boundsPreparation.WaitAsync(cancellation).ConfigureAwait(false);
            try {
                await writes.WaitAsync(cancellation).ConfigureAwait(false);
                try{return await Task.Run(()=>{
                    cancellation.ThrowIfCancellationRequested();
                    ColdReadCheckpoint?.Invoke("reading");
                    cancellation.ThrowIfCancellationRequested();
                    bool bundled=included!=null&&hash==included.Hash&&!File.Exists(Path.Combine(directory,hash+".glb"));
                    using var asset=bundled?included.Read():ReadLocalCore(hash,cancellation,true);
                    if(bundled)
                        asset.Inspection=ModelInspection.InspectAcoustics(asset.Bytes);
                    ColdReadCheckpoint?.Invoke("inspected");
                    cancellation.ThrowIfCancellationRequested();RememberBounds(asset);return asset.Inspection.Acoustics;
                },cancellation).ConfigureAwait(false);}finally{writes.Release();}
            }finally{boundsPreparation.Release();}
        }
    }
}
