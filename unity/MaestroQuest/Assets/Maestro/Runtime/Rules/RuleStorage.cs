// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Maestro.Quest.Rules
{
    public sealed class RuleStorage
    {
        const int MaximumBytes = 256 * 1024;
        readonly string directory, path;
        public RuleStorage(string directory) { this.directory = Path.GetFullPath(directory); path = Path.Combine(this.directory,"rules.v1.json"); }
        public RuleDocument Load(out string message)
        {
            message = null;
            if (Read(path,out var value)) return value;
            if (Read(path+".backup",out value)) { message = "Recovered rules from their backup"; return value; }
            if (File.Exists(path) || File.Exists(path+".backup")) message = "Saved rules could not be read; files are retained for recovery";
            return new RuleDocument();
        }
        bool Read(string file, out RuleDocument document)
        {
            document = null;
            try
            {
                if (!File.Exists(file) || new FileInfo(file).Length > MaximumBytes) return false;
                var candidate = JsonUtility.FromJson<RuleDocument>(File.ReadAllText(file,Encoding.UTF8));
                if (candidate == null || !candidate.Validate(out _)) return false;
                document = candidate; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { return false; }
        }
        public bool Save(RuleDocument document, out string error)
        {
            if (!document.Validate(out error)) return false;
            try
            {
                Directory.CreateDirectory(directory);
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(document));
                if (bytes.Length > MaximumBytes) { error = "The rule collection is too large to save"; return false; }
                using (var stream = new FileStream(path+".pending",FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if (File.Exists(path))
                {
                    File.Copy(path,Read(path,out _) ? path+".backup" : path+".unreadable",true);
                    File.Replace(path+".pending",path,null);
                }
                else File.Move(path+".pending",path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException) { error = "Rules could not be saved; the previous save is retained"; return false; }
        }
    }
}
