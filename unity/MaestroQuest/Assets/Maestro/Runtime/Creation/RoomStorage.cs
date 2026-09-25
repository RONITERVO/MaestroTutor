// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public sealed class RoomStorage
    {
        const long MaximumBytes = 4 * 1024 * 1024;
        readonly string directory, primary, backup;
        public RoomStorage(string directory)
        {
            this.directory = Path.GetFullPath(directory);
            primary = Path.Combine(this.directory, "room.v1.json"); backup = primary + ".backup";
        }

        public RoomDocument Load(out string message)
        {
            message = null;
            if (!File.Exists(primary) && !File.Exists(backup)) return null;
            if (TryRead(primary, out var room)) return room;
            if (TryRead(backup, out room)) { message = "Recovered your room from its backup."; return room; }
            message = "Your saved room could not be read. Its files are kept for recovery.";
            return null;
        }

        public bool Save(RoomDocument room, out string error)
        {
            if (!room.Validate(out error)) return false;
            string temp = primary + ".pending";
            try
            {
                Directory.CreateDirectory(directory);
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(room));
                if (bytes.Length > MaximumBytes) { error = "This room is too large to save."; return false; }
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(primary))
                {
                    // Never replace the last good backup with an unreadable primary.
                    if (TryRead(primary, out _)) File.Copy(primary, backup, true);
                    else File.Copy(primary, primary + ".unreadable", true);
                    File.Replace(temp, primary, null);
                }
                else File.Move(temp, primary);
                error = null; return true;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is NotSupportedException)
            { error = "Room could not be saved. Your previous save is retained. Check available storage and try again."; return false; }
        }

        static bool TryRead(string path, out RoomDocument room)
        {
            room = null;
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > MaximumBytes) return false;
                var candidate = JsonUtility.FromJson<RoomDocument>(File.ReadAllText(path, Encoding.UTF8));
                // Unity serializes null inline classes as empty objects and null
                // arrays as empty arrays. Restore optional animation sentinels.
                if (candidate?.objects != null) foreach (var item in candidate.objects)
                {
                    if (item == null) continue;
                    // Pre-physics v1 files omit mass and retain fixed placement.
                    if (item.mass == 0 && item.physics == Interaction.ItemPhysics.Fixed) item.mass = .5f;
                    if (item.joints?.Length == 0) item.joints = null;
                    if (item.motion?.frames?.Length == 0) item.motion = null;
                }
                if (candidate == null || !candidate.Validate(out _)) return false;
                room = candidate; return true;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is ArgumentException) { return false; }
        }
    }
}
