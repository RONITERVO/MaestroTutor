// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Imports
{
    public sealed partial class ModelLibrary
    {
        // Shares the library's inspection lane across room owners. No first-use
        // copy, Unity import, image decoding, native model reservation or save.
        internal async Task<ModelAcousticGeometry> PrepareAcousticsAsync(string hash,CancellationToken cancellation)
        {
            await boundsPreparation.WaitAsync(cancellation).ConfigureAwait(false);
            try{return await Task.Run(()=>{
                cancellation.ThrowIfCancellationRequested();
                if(!ValidHash(hash))throw new ModelImportException("This room has an invalid model reference.");
                ModelAsset asset;
                if(included!=null&&hash==included.Hash&&!File.Exists(Path.Combine(directory,hash+".glb"))){
                    asset=included.Read();asset.Inspection=ModelInspection.InspectAcoustics(asset.Bytes);
                }else asset=ReadLocalCore(hash,cancellation,true);
                cancellation.ThrowIfCancellationRequested();RememberBounds(asset);return asset.Inspection.Acoustics;
            },cancellation).ConfigureAwait(false);}finally{boundsPreparation.Release();}
        }
    }
}
