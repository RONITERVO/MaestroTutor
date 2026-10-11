// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // Explicit import acceptance promises a usable object. Restoration and
        // authored copies still acknowledge durable references independently of
        // residency; they do not claim that an asynchronous load is ready.
        internal async Task<string> CreateImportedModelAsync(string hash,CancellationToken cancellation)
        {
            using var pending=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var gate=RuntimeGate;
            void Interrupted(){if(gate.Held)pending.Cancel();}
            gate.Changed+=Interrupted;
            try{return await PrepareImportedModel(hash,pending.Token);}
            finally{gate.Changed-=Interrupted;}
        }
        async Task<string> PrepareImportedModel(string hash,CancellationToken cancellation)
        {
            if(!CanCreatePrimitive(out var error))throw new ModelImportException(error);
            if(!ModelLibrary.ValidHash(hash))throw new ModelImportException("Choose a verified model identity");
            using var write=WriteGate.TryWrite(out error);if(write==null)throw new ModelImportException(error);
            var expectedJournal=journal;string session=TemporarySessionId;
            var data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.ImportedModel,modelHash=hash,position=SpawnPosition()};
            bool Current(){
                if(!this||!ReferenceEquals(journal,expectedJournal)||TemporarySessionId!=session||RuntimeGate.Held||Ownership.Suspended||!CanSaveRoom||AnyHeld||DrawingInProgress)return false;
                var authoring=GetComponent<AnimationWorkshop>();return !authoring||!(authoring.ControlsTarget(SelectedId)||authoring.HasUnsavedPose||authoring.HasUnsavedRecording);
            }
            cancellation.ThrowIfCancellationRequested();if(!Current())throw new OperationCanceledException();
            var asset=await Models.ReadAsync(hash);
            cancellation.ThrowIfCancellationRequested();if(!Current())throw new OperationCanceledException();
            using var prepared=await PreparedImportedModel.Load(asset,data,WorldIdentity,cancellation,Current);
            cancellation.ThrowIfCancellationRequested();if(!Current())throw new OperationCanceledException();
            if(!CanCreatePrimitive(out error))throw new ModelImportException(error);
            // Capture the current journal only after asynchronous preparation.
            // Unrelated actors may have moved meanwhile; never save an old room
            // snapshot over their accepted state.
            if(!CommitPersisted(new[]{data},Array.Empty<string>(),"Model added",false,out error,modelPreparation:prepared))throw new ModelImportException(error);
            return data.id;
        }
    }
}
