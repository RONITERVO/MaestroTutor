// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Creation
{
    public sealed class RoomStorage
    {
        public const string FileName="room.v8.json";
        readonly VersionedRoomFile<RoomDocument> file;
        readonly string directory;
        volatile string coordinationError;
        public bool ReadOnly => coordinationError!=null||file.ReadOnly;
        public RoomStorage(string directory) {
            this.directory=directory;file=new VersionedRoomFile<RoomDocument>(directory,"room",4*1024*1024,x => x.Validate(out _),x => x.Copy(),Normalize,x => x.version = RoomDocument.CurrentVersion,version:RoomDocument.CurrentVersion,newerDocument:x=>x.structures?.Any(s=>s!=null&&s.version>1)==true||x.objects?.Any(o=>o?.surfaces?.Any(s=>s!=null&&s.version>1)==true||o?.drawingTips?.Any(t=>t!=null&&t.version>1)==true||o?.connections?.Any(h=>h!=null&&h.version>1)==true||o?.snapPoints?.Any(p=>p!=null&&p.version>1)==true)==true);
        }
        public RoomDocument Load(out string message) {
            try {using var owner=RoomSnapshotTransaction.Enter(directory,recover:true,initialize:false);return file.Load(out message);}
            catch(Exception e){message=coordinationError="Saved room/memory snapshot is unavailable; its files are preserved. "+e.Message;return null;}
        }
        public bool Save(RoomDocument room,out string error) {
            error=coordinationError;if(error!=null)return false;
            try {using var owner=RoomSnapshotTransaction.Enter(directory);return file.Save(room,out error);}
            catch(Exception e){error=coordinationError="Room save was not confirmed; recover the saved room/memory snapshot before continuing. "+e.Message;return false;}
        }
        public bool RetainsMotion(string id,out bool uncertain,bool force=false) {
            uncertain=true;
            try {using var owner=RoomSnapshotTransaction.Inspect(directory,wait:false);return file.Retains(x => x.objects.Select(item => item.walkMotionId),id,out uncertain,force);}
            catch(Exception){uncertain=true;return false;}
        }
        internal static void Normalize(RoomDocument room)
        {
            if(room.version<3 && room.structures==null)room.structures=Array.Empty<RoomStructure>();
            if (room.objects == null) return;
            foreach (var item in room.objects)
            {
                if (item == null) continue;
                if (room.version == 1 && item.mass == 0 && item.physics == Interaction.ItemPhysics.Fixed) item.mass = .5f;
                // Unity serializes null nested classes as empty instances. Pre-recipe objects
                // must keep loading; only an empty non-assembly recipe is absent.
                if (item.kind != RoomObjectKind.Assembly && item.recipe != null && (item.recipe.parts?.Length ?? 0) == 0 && (item.recipe.tracks?.Length ?? 0) == 0) item.recipe=null;
                // Only the known empty representation means no custom collider. Preserve
                // future versions and malformed nonempty data for validation/recovery.
                if (item.collision != null && item.collision.version is 0 or 1 && item.collision.shapes?.Length == 0) item.collision=null;
                if (item.joints?.Length == 0) item.joints = null;
                if (item.motion?.frames?.Length == 0) item.motion = null;
            }
        }
    }
}
