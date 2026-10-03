// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    // Older originals remain available. Once a newer file exists, corruption
    // never silently rolls the user back to an earlier room or rule collection.
    internal sealed class VersionedRoomFile<T> where T : class
    {
        readonly string directory,primary,stem,label;
        readonly int version,minimumVersion;
        readonly int maximum;
        readonly Func<T,bool> validate,newerDocument;
        readonly Func<JObject,bool> validWire;
        readonly Func<T,T> copy;
        readonly Action<T> normalize,upgrade;
        sealed class Retained { public long Length,Stamp; public bool Uncertain; public HashSet<string> Ids; }
        readonly object retainedGate=new();
        readonly Dictionary<string,Retained> retained=new();
        public bool ReadOnly { get; private set; }
        public VersionedRoomFile(string directory,string stem,int maximum,Func<T,bool> validate,Func<T,T> copy,Action<T> normalize,Action<T> upgrade,int version = 2,Func<T,bool> newerDocument = null,Func<JObject,bool> validWire = null,int minimumVersion = 1)
        {
            if(minimumVersion<1 || minimumVersion>version)throw new ArgumentOutOfRangeException(nameof(minimumVersion));
            this.minimumVersion=minimumVersion;this.directory = Path.GetFullPath(directory); this.stem=stem; this.version=version; primary = Path.Combine(this.directory,stem+".v"+version+".json");
            this.newerDocument=newerDocument;this.validWire=validWire;
            label = stem; this.maximum = maximum; this.validate = validate; this.copy = copy; this.normalize = normalize; this.upgrade = upgrade;
        }
        // A newer app writes a different filename. Do not load an older save
        // merely because its contents are still valid after a downgrade.
        internal static bool HasNewerFiles(string directory,string stem,int version)
        {
            try
            {
                if (!Directory.Exists(directory)) return false;
                string prefix=stem+".v";
                foreach (string path in Directory.EnumerateFiles(directory,stem+".v*.json*"))
                {
                    string name=Path.GetFileName(path);
                    if (!name.StartsWith(prefix,StringComparison.Ordinal)) continue;
                    int end=name.IndexOf(".json",prefix.Length,StringComparison.Ordinal);
                    if (end < 0) continue;
                    string suffix=name.Substring(end+5);
                    if (suffix != "" && suffix != ".backup" && suffix != ".pending" && suffix != ".unreadable") continue;
                    string digits=name.Substring(prefix.Length,end-prefix.Length);
                    if (digits.Length == 0 || digits.Any(c => c < '0' || c > '9')) continue;
                    if (!int.TryParse(digits,out int savedVersion) || savedVersion > version) return true;
                }
                return false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return true; }
        }
        public T Load(out string message)
        {
            message = null;
            if (HasNewerFiles(directory,stem,version))
            {
                ReadOnly=true; message="Saved "+label+" needs a different app version; its files are preserved"; return null;
            }
            string source=primary; int expected=version;
            for (; expected > minimumVersion; expected--)
            {
                source=Path.Combine(directory,stem+".v"+expected+".json");
                if (File.Exists(source) || File.Exists(source+".backup")) break;
            }
            source=Path.Combine(directory,stem+".v"+expected+".json");
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
                if(newerDocument?.Invoke(candidate)==true) {newer=true;return false;}
                if(validWire!=null && !validWire(json))return false;
                normalize?.Invoke(candidate); if (!validate(candidate)) return false;
                value = candidate; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is JsonException || e is OverflowException) { return false; }
        }
        public bool Retains(Func<T,IEnumerable<string>> identities,string id,out bool uncertain,bool force=false,Func<T,bool> unknownReferences=null)
        {
            lock (retainedGate)
            {
                bool found=false; uncertain=HasNewerFiles(directory,stem,version);
                for (int v=minimumVersion;v<=version;v++)
                    foreach (string suffix in new[] { "", ".backup", ".pending", ".unreadable" })
                    {
                        string path=Path.Combine(directory,stem+".v"+v+".json"+suffix);
                        try
                        {
                            var info=new FileInfo(path);
                            if (!info.Exists) { retained.Remove(path); continue; }
                            if (force || !retained.TryGetValue(path,out var cached) || cached.Length != info.Length || cached.Stamp != info.LastWriteTimeUtc.Ticks)
                            {
                                cached=new Retained { Length=info.Length,Stamp=info.LastWriteTimeUtc.Ticks };
                                if (Read(path,v,out var document,out _)) {cached.Ids=identities(document).Where(x => x != null).ToHashSet();cached.Uncertain=unknownReferences?.Invoke(document)==true;} else cached.Uncertain=true;
                                retained[path]=cached;
                            }
                            uncertain |= cached.Uncertain; found |= cached.Ids?.Contains(id) == true;
                        }
                        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { uncertain=true; }
                    }
                return found;
            }
        }
        public bool Save(T value,out string error)
        {
            // Library usage scans read primary, backup and staging files on a worker.
            // Serialize publication with those reads: Windows cannot rename a file
            // while the same store's retention reader still holds it open.
            lock(retainedGate)return SaveExclusive(value,out error);
        }
        bool SaveExclusive(T value,out string error)
        {
            error = "Saved "+label+" is unavailable for editing; its original files are preserved";
            if (ReadOnly || HasNewerFiles(directory,stem,version)) { ReadOnly=true; return false; }
            Read(primary,version,out _,out bool newer); if (newer) { ReadOnly = true; return false; }
            if (value == null || !validate(value)) { error = "Invalid "+label+" data"; return false; }
            var candidate = copy(value); upgrade(candidate);
            if (!validate(candidate)) { error = "Invalid "+label+" data"; return false; }
            string pending = primary+".pending", phase="stage";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(candidate));
                if (bytes.Length > maximum) { error = "The "+label+" collection is too large to save"; return false; }
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if (File.Exists(primary))
                {
                    phase="backup";File.Copy(primary,Read(primary,version,out _,out _) ? primary+".backup" : primary+".unreadable",true);
                    phase="publish";Maestro.Quest.Persistence.FilePublication.Replace(pending,primary,null);
                }
                else {phase="publish";File.Move(pending,primary);}
                error = null; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException) {
                // Diagnostic codes identify sharing/permission/disk failures without
                // exposing private paths or document contents in device logs.
                Debug.LogWarning($"Maestro {label} save failed during {phase} ({e.GetType().Name}, 0x{e.HResult:X8})");
                error = "Could not save "+label+"; the previous save is retained. Check available storage and try again."; return false;
            }
            finally { try { if (File.Exists(pending)) File.Delete(pending); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
