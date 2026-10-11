// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        RoomSpatialIndex spatialIndex;RoomJournal spatialJournal;
        void SynchronizeSpatialIndex(RoomDocument document)
        {
            if(!ReferenceEquals(spatialJournal,journal)){StopSpatialPreparation();spatialJournal=journal;}
            spatialIndex??=new RoomSpatialIndex(Models);spatialIndex.Synchronize(document);
        }
        void TickSpatialPreparation()
        {
            if(journal==null||Models==null)return;
            if(spatialIndex==null||!ReferenceEquals(spatialJournal,journal))SynchronizeSpatialIndex(journal.Snapshot());
            spatialIndex.Tick();
        }
        void StopSpatialPreparation(){spatialIndex?.Dispose();spatialIndex=null;spatialJournal=null;}
        internal JObject ObserveAuthoredBounds(string target)
        {
            var data=Read(target);if(data==null)return null;
            bool prepared=TryAuthoredSpatial(data,out var bounds);
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["prepared"]=prepared,["coordinates"]="object",["visual"]=SpatialRecord(bounds.Visual)};
        }
        bool TryAuthoredSpatial(RoomObjectData data,out RoomSpatialBounds bounds)
        {
            bounds=RoomSpatialBounds.Unknown;
            if(data==null||spatialIndex==null||!ReferenceEquals(spatialJournal,journal))return false;
            return spatialIndex.TryRead(data.id,SpatialSource(data.Copy()),out bounds);
        }
    }
}
