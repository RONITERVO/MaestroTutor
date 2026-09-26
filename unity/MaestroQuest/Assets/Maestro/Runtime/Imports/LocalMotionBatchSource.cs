// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Maestro.Quest.Imports
{
    // Explicit file list, not a recursive watcher. Reads originals without moving
    // or changing them; the Android source uses the document picker instead.
    public sealed class LocalMotionBatchSource : IMotionBatchSource
    {
        readonly string[] paths;
        bool disposed;
        public int Count => paths.Length;
        public string Name(int index) => Path.GetFileName(paths[index]);
        public LocalMotionBatchSource(string[] paths) { this.paths=(string[])(paths ?? throw new ArgumentNullException(nameof(paths))).Clone(); }
        public async Task<MotionBatchInput> ReadAsync(int index,CancellationToken cancellation)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalMotionBatchSource));
            cancellation.ThrowIfCancellationRequested();
            var bytes=await Task.Run(() => ModelLibrary.ReadBounded(paths[index]),cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (disposed) throw new OperationCanceledException();
            return new MotionBatchInput(Path.GetFileName(paths[index]),bytes);
        }
        public void Dispose() => disposed=true;
    }
}
