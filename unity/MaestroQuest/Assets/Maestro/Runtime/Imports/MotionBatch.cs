// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Maestro.Quest.Imports
{
    // A source opens one user-selected document at a time. It must release any
    // provider copy before returning, and never preload the remaining files.
    public interface IMotionBatchSource : IDisposable
    {
        int Count { get; }
        string Name(int index); // Metadata already known locally; never opens a provider.
        Task<MotionBatchInput> ReadAsync(int index,CancellationToken cancellation);
    }
    public sealed class MotionBatchInput : IDisposable
    {
        public string Name { get; }
        public byte[] Bytes { get; private set; }
        public MotionBatchInput(string name,byte[] bytes) { Name=name; Bytes=bytes; }
        public void Dispose() => Bytes=null;
    }
    public enum MotionBatchState { Pending, Reading, Importing, Saved, Failed }
    public sealed class MotionBatchResult
    {
        public int Index { get; internal set; }
        public string Name { get; internal set; }
        public MotionBatchState State { get; internal set; }
        public string Error { get; internal set; }
        public string[] MotionIds { get; internal set; } = Array.Empty<string>();
        public MotionBatchResult Copy() { var copy=(MotionBatchResult)MemberwiseClone(); copy.MotionIds=(string[])MotionIds.Clone(); return copy; }
    }
    /// <summary>Sequential, restartable imports. Use on the Unity thread.</summary>
    public sealed class MotionBatch : IDisposable
    {
        public const int MaximumFiles=128;
        readonly MotionLibrary library;
        readonly IMotionBatchSource source;
        readonly MotionBatchResult[] results;
        CancellationTokenSource cancellation;
        bool disposed;
        string category;
        public bool Running { get; private set; }
        public bool Stopping => cancellation?.IsCancellationRequested == true;
        public int Count => results.Length;
        public int Saved => results.Count(x => x.State == MotionBatchState.Saved);
        public int Failed => results.Count(x => x.State == MotionBatchState.Failed);
        public int Pending => results.Count(x => x.State == MotionBatchState.Pending);
        public string Category => category;
        public string Status { get; private set; }
        public event Action Changed;
        public MotionBatchResult[] Results => results.Select(x => x.Copy()).ToArray();
        public MotionBatch(MotionLibrary library,IMotionBatchSource source)
        {
            this.library=library ?? throw new ArgumentNullException(nameof(library));
            this.source=source ?? throw new ArgumentNullException(nameof(source));
            if (source.Count < 1 || source.Count > MaximumFiles) throw new ModelImportException("Choose between 1 and 128 animation files.");
            results=Enumerable.Range(0,source.Count).Select(i => new MotionBatchResult { Index=i,Name="Selected file "+(i+1) }).ToArray();
            Status="Selected "+Count+" files. Save batch confirms you may use them.";
        }
        public void SetCategory(string value)
        {
            if (Running || disposed) return;
            // Keep the same collection tag across stop/resume/retry. Custom tags
            // can be edited in the book after import.
            if (results.Any(x => x.State != MotionBatchState.Pending)) return;
            category=string.IsNullOrWhiteSpace(value) ? null : new string(value.Trim().Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(32).ToArray());
            Notify();
        }
        public void Stop()
        {
            if (!Running) return;
            cancellation.Cancel(); Status="Stopping after the current save. Completed files stay in your library."; Notify();
        }
        public async Task RunAsync(bool retryFailed=false)
        {
            if (Running || disposed) return;
            int[] targets=results.Where(x => x.State == MotionBatchState.Pending || retryFailed && x.State == MotionBatchState.Failed).Select(x => x.Index).ToArray();
            if (targets.Length == 0) { Status=Failed > 0 ? "Use Retry failed to try the failed files again." : "All selected files are saved. Open Library to browse them."; Notify(); return; }
            Running=true; cancellation=new CancellationTokenSource(); var token=cancellation.Token;
            try
            {
                foreach (int index in targets)
                {
                    if (disposed || token.IsCancellationRequested) break;
                    var item=results[index]; var before=item.State; string priorError=item.Error;
                    item.State=MotionBatchState.Reading; item.Error=null; Status="Reading file "+(index+1)+" of "+Count+"…"; Notify();
                    try
                    {
                        using var input=await source.ReadAsync(index,token);
                        token.ThrowIfCancellationRequested();
                        if (input == null || input.Bytes == null) throw new ModelImportException("The selected file could not be read. Try a local GLB or VRM export.");
                        item.Name=ModelLibrary.SafeName(input.Name);
                        item.State=MotionBatchState.Importing; Status="Saving file "+(index+1)+" of "+Count+": "+item.Name; Notify();
                        // Once a file starts its atomic catalogue update it is
                        // allowed to finish. Cancellation stops before the next.
                        var entries=await library.ImportAsync(input.Name,input.Bytes,category);
                        item.MotionIds=entries.Select(x => x.id).Distinct().ToArray(); item.State=MotionBatchState.Saved;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    { item.State=before; item.Error=priorError; break; }
                    catch (Exception error)
                    {
                        item.State=MotionBatchState.Failed;
                        item.Error=error is ModelImportException ? new string(error.Message.Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(320).ToArray()) : "This file could not be imported. Check its export and try again.";
                    }
                    string name=source.Name(index); if (!string.IsNullOrEmpty(name)) item.Name=ModelLibrary.SafeName(name);
                    Notify();
                }
            }
            finally
            {
                Running=false; cancellation.Dispose(); cancellation=null;
                Status=Saved+" saved, "+Failed+" failed, "+Pending+" waiting. "+(Pending > 0 ? "Resume continues waiting files." : Failed > 0 ? "Retry failed keeps completed files." : "Open Library to browse motions.");
                if(disposed)source.Dispose();
                Notify();
            }
        }
        void Notify() { if (!disposed) Changed?.Invoke(); }
        public void Dispose() { if (disposed) return; disposed=true; cancellation?.Cancel(); if(!Running)source.Dispose(); }
    }
}
