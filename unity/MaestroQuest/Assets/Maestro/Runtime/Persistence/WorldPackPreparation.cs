// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Persistence
{
    /// <summary>Pure detached new-world preparation. No files, native roots, programs or audio are
    /// created here. Retain this exact snapshot through preview/staging; never expand again on accept.</summary>
    public static class WorldPackPreparation
    {
        public sealed class Summary
        {
            public int CreatedObjects {get;} public int RecipeParts {get;} public int CollisionPieces {get;}
            public int CustomGeometryVertices {get;} public int ResourceDefinitions {get;} public int Files {get;}
            public long Bytes {get;} public string ManifestHash {get;}
            internal Summary(RoomObjectData[] objects,CreationBatchMapping mapping,WorkspaceArchiveReceipt receipt) {
                CreatedObjects=objects.Length;RecipeParts=objects.Sum(o=>o.recipe?.parts.Length??0);
                CollisionPieces=objects.Sum(CollisionRecipe.ReservedPieces);
                CustomGeometryVertices=objects.Sum(o=>RecipeGeometry.VertexCost(o.recipe));
                ResourceDefinitions=mapping.Resources.Count;Files=receipt.Summary.Files;Bytes=receipt.Summary.Bytes;ManifestHash=receipt.ManifestHash;
            }
        }
        public sealed class Result
        {
            public WorkspaceArchiveSnapshot Snapshot {get;} public Summary Summary {get;}
            public CreationBatchMapping Mapping {get;} public string WorldId {get;} public string InstanceId {get;}
            internal Result(WorkspaceArchiveSnapshot snapshot,Summary summary,CreationBatchMapping mapping,string world,string instance) {
                Snapshot=snapshot;Summary=summary;Mapping=mapping;WorldId=world;InstanceId=instance;
            }
        }
        static readonly UTF8Encoding Utf8=new(false,true);
        static InvalidDataException Invalid(string message)=>new(message);
        internal static void RequireClosed(WorkspaceArchiveSummary summary) {
            if(summary==null||summary.UnavailablePrograms!=0||summary.MissingImages.Length!=0||summary.MissingModels.Length!=0||summary.MissingSounds.Length!=0||summary.MissingMotions.Length!=0||summary.MissingControllerPrograms.Length!=0)
                throw Invalid("A bundled world must include every required asset, module and program dependency.");
        }
        public static Result Prepare(WorldPackCatalog.Entry entry,WorkspaceArchiveSnapshot baseline,CancellationToken cancellation=default) {
            if(entry==null||baseline==null)throw new ArgumentNullException();
            cancellation.ThrowIfCancellationRequested();RequireClosed(baseline.Metadata.Summary);
            // The caller supplies WorkspaceDefaults.Snapshot with the actual bundled avatar/motions.
            // Never reinterpret an occupied current room as a fresh baseline or append a second pack.
            if(!baseline.Documents.TryGetValue(RoomStorage.FileName,out var bytes)||baseline.Documents.ContainsKey(WorldPackDocument.FileName))
                throw Invalid("Choose the current fresh workspace baseline for a new bundled world.");
            var wire=JObject.Parse(Utf8.GetString(bytes));var room=JsonUtility.FromJson<RoomDocument>(wire.ToString());
            RoomLiquidCodec.Decode(room,wire);RoomStorage.Normalize(room);
            if(room.version!=RoomDocument.CurrentVersion||room.objects.Length!=2||room.objects.Any(o=>!o.IsBuiltIn)||room.structures.Length!=0||room.regions.Length!=0||
                room.appearances.Length!=0||room.audioSources.Length!=0||room.environmentProfiles.Length!=0||room.visibilityLayers.Length!=0)
                throw Invalid("Bundled preparation only accepts a fresh book-and-Maestro baseline.");
            // Validate actual baseline payload bytes before assigning any retained pack identities.
            RequireClosed(WorkspaceArchive.Fingerprint(baseline,cancellation).Summary);
            cancellation.ThrowIfCancellationRequested();var batch=CreationBatch.Read(entry.Creation);
            if(!batch.Prepare(out var objects,out var resources,out var mapping,out var error))throw Invalid(error);
            room.world=RoomWorldIdentity.Create();room.objects=room.objects.Concat(objects).ToArray();
            room.appearances=resources.appearances;room.audioSources=resources.audioSources;
            room.environmentProfiles=resources.environmentProfiles;room.visibilityLayers=resources.visibilityLayers;
            if(!room.Validate(out error))throw Invalid(error);
            string instance=Guid.NewGuid().ToString("N");
            var provenance=new WorldPackDocument {worldId=room.world.worldId,instances=new[]{new WorldPackInstance {
                instanceId=instance,packId=entry.Id,packVersion=entry.Version,sourceHash=entry.Hash,entryId=entry.EntryId,
                objects=mapping.Objects.Select(p=>new WorldPackSlot {slot=p.Key,id=p.Value}).ToArray(),
                resources=mapping.Resources.Select(p=>new WorldPackResourceSlot {kind=p.Kind,slot=p.Slot,id=p.Id}).ToArray()
            }}};
            var documents=baseline.Documents.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
            documents[RoomStorage.FileName]=Utf8.GetBytes(RoomLiquidCodec.Encode(room));documents.Add(WorldPackDocument.FileName,provenance.Encode());
            var snapshot=new WorkspaceArchiveSnapshot(documents,baseline.Assets);RequireClosed(snapshot.Metadata.Summary);
            var receipt=WorkspaceArchive.Fingerprint(snapshot,cancellation);RequireClosed(receipt.Summary);
            return new Result(snapshot,new Summary(objects,mapping,receipt),mapping,room.world.worldId,instance);
        }
    }
}
