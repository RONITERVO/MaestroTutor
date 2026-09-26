// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    // v1 originals remain available to an older build. Once v2 exists, corruption
    // never silently rolls the user back to that older room or rule collection.
    internal sealed class VersionedRoomFile<T> where T : class
    {
        readonly string directory,primary,legacy,label;
        readonly int maximum;
        readonly Func<T,bool> validate;
        readonly Func<T,T> copy;
        readonly Action<T> normalize,upgrade;
        public bool ReadOnly { get; private set; }
        public VersionedRoomFile(string directory,string stem,int maximum,Func<T,bool> validate,Func<T,T> copy,Action<T> normalize,Action<T> upgrade)
        {
            this.directory = Path.GetFullPath(directory); primary = Path.Combine(this.directory,stem+".v2.json"); legacy = Path.Combine(this.directory,stem+".v1.json");
            label = stem; this.maximum = maximum; this.validate = validate; this.copy = copy; this.normalize = normalize; this.upgrade = upgrade;
        }
        public T Load(out string message)
        {
            message = null;
            string source = File.Exists(primary) || File.Exists(primary+".backup") ? primary : legacy;
            int expected = source == primary ? 2 : 1;
            if (!File.Exists(source) && !File.Exists(source+".backup")) return null;
            if (Read(source,expected,out var value,out bool newer)) { upgrade(value); return value; }
            if (!newer && Read(source+".backup",expected,out value,out _)) { upgrade(value); message = "Recovered "+label+" from its backup"; return value; }
            ReadOnly = true;
            message = newer ? "Saved "+label+" needs a different app version; its files are preserved" : "Saved "+label+" could not be read; saving is paused and its files are retained for recovery";
            return null;
        }
        bool Read(string path,int expected,out T value,out bool newer)
        {
            value = null; newer = false;
            try
            {
                if (!File.Exists(path)) return false;
                using var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                if (stream.Length < 1 || stream.Length > maximum) return false;
                var bytes = new byte[(int)stream.Length]; int at = 0;
                while (at < bytes.Length) { int count = stream.Read(bytes,at,bytes.Length-at); if (count == 0) return false; at += count; }
                if (stream.ReadByte() != -1) return false;
                string text = new UTF8Encoding(false,true).GetString(bytes);
                using var reader = new JsonTextReader(new StringReader(text)) { MaxDepth = 48,DateParseHandling = DateParseHandling.None };
                var json = JObject.Load(reader,new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || json["version"]?.Type != JTokenType.Integer) return false;
                if (json["version"].ToString() != expected.ToString()) { newer = true; return false; }
                var candidate = JsonUtility.FromJson<T>(text); if (candidate == null) return false;
                normalize?.Invoke(candidate); if (!validate(candidate)) return false;
                value = candidate; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is JsonException || e is OverflowException) { return false; }
        }
        public bool Save(T value,out string error)
        {
            error = "Saved "+label+" is unavailable for editing; its original files are preserved";
            if (ReadOnly) return false;
            Read(primary,2,out _,out bool newer); if (newer) { ReadOnly = true; return false; }
            if (value == null || !validate(value)) { error = "Invalid "+label+" data"; return false; }
            var candidate = copy(value); upgrade(candidate);
            if (!validate(candidate)) { error = "Invalid "+label+" data"; return false; }
            string pending = primary+".pending";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(candidate));
                if (bytes.Length > maximum) { error = "The "+label+" collection is too large to save"; return false; }
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if (File.Exists(primary))
                {
                    File.Copy(primary,Read(primary,2,out _,out _) ? primary+".backup" : primary+".unreadable",true);
                    File.Replace(pending,primary,null);
                }
                else File.Move(pending,primary);
                error = null; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException) { error = "Could not save "+label+"; the previous save is retained. Check available storage and try again."; return false; }
            finally { try { if (File.Exists(pending)) File.Delete(pending); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
