// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Creation
{
    public sealed class RoomStorage
    {
        readonly VersionedRoomFile<RoomDocument> file;
        public bool ReadOnly => file.ReadOnly;
        public RoomStorage(string directory) => file = new VersionedRoomFile<RoomDocument>(directory,"room",4*1024*1024,x => x.Validate(out _),x => x.Copy(),Normalize,x => x.version = 2);
        public RoomDocument Load(out string message) => file.Load(out message);
        public bool Save(RoomDocument room,out string error) => file.Save(room,out error);
        static void Normalize(RoomDocument room)
        {
            if (room.objects == null) return;
            foreach (var item in room.objects)
            {
                if (item == null) continue;
                if (room.version == 1 && item.mass == 0 && item.physics == Interaction.ItemPhysics.Fixed) item.mass = .5f;
                if (item.joints?.Length == 0) item.joints = null;
                if (item.motion?.frames?.Length == 0) item.motion = null;
            }
        }
    }
}
