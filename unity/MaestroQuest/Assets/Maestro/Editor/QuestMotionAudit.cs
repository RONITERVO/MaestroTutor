// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    /// <summary>Read-only source audit through the runtime extractor; private output never enters Assets.</summary>
    public static class QuestMotionAudit
    {
        [Serializable] sealed class Source { public string file,hash,error; public int clips; public bool unchanged; }
        [Serializable] sealed class Report
        {
            public string version = "maestro-motion-audit-1";
            public int files,failures,uniqueMotions,shortHelpers,compatibleRigs;
            public long sourceBytes,payloadBytes;
            public List<Source> sources = new();
        }
        public static void Inspect()
        {
            string source = Path.GetFullPath(Environment.GetEnvironmentVariable("MAESTRO_MOTION_DIRECTORY") ?? throw new InvalidOperationException("Select a motion source directory"));
            string output = Path.GetFullPath(Environment.GetEnvironmentVariable("MAESTRO_MOTION_AUDIT") ?? throw new InvalidOperationException("Select a private audit output directory"));
            string sourcePrefix = source.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if (output.Equals(source,StringComparison.OrdinalIgnoreCase) || output.StartsWith(sourcePrefix,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Motion audit output must be outside the original collection");
            Directory.CreateDirectory(output); string storage = Path.Combine(output,"motions");
            using var library = new MotionLibrary(storage); var report = new Report();
            foreach (string path in Directory.GetFiles(source,"*.glb",SearchOption.AllDirectories).OrderBy(x => x,StringComparer.Ordinal))
            {
                var row = new Source { file = Path.GetRelativePath(source,path) }; report.sources.Add(row); report.files++;
                try
                {
                    var bytes = ModelLibrary.ReadBounded(path); row.hash = ModelLibrary.Hash(bytes); report.sourceBytes += bytes.Length;
                    var entries = library.ImportAsync(Path.GetFileName(path),bytes,Path.GetFileName(Path.GetDirectoryName(path))).GetAwaiter().GetResult();
                    row.clips = entries.Length; row.unchanged = row.hash == ModelLibrary.Hash(ModelLibrary.ReadBounded(path));
                    if (!row.unchanged) throw new InvalidOperationException("Source changed during audit");
                }
                catch (Exception error) { row.error = error.Message; report.failures++; }
            }
            var all = library.List(includeShort:true); report.uniqueMotions = all.Length; report.shortHelpers = all.Count(x => x.Short);
            report.compatibleRigs = all.Select(x => x.rigHash).Distinct().Count(); report.payloadBytes = all.Sum(x => (long)x.bytes);
            using var reopened = new MotionLibrary(storage);
            if (reopened.ReadOnly || !all.Select(x => x.id+":"+x.hash).OrderBy(x => x).SequenceEqual(reopened.List(includeShort:true).Select(x => x.id+":"+x.hash).OrderBy(x => x))) throw new InvalidOperationException("The saved catalogue did not reopen with the same stable identities");
            File.WriteAllText(Path.Combine(output,"audit.json"),JsonUtility.ToJson(report,true));
            if (report.files == 0 || report.failures > 0) throw new InvalidOperationException("Motion collection audit failed; see its private report");
            Debug.Log("MAESTRO_MOTION_AUDIT_PASSED files="+report.files+" unique="+report.uniqueMotions+" rigVariants="+report.compatibleRigs+" payloadBytes="+report.payloadBytes);
        }
    }
}
