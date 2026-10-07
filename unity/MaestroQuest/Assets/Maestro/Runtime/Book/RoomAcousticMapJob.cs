// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Meta.XR.Acoustics;

namespace Maestro.Quest.Book
{
    /// <summary>A bounded acoustic-map computation. Its caller retains every
    /// native geometry/material input until completion or transfers cleanup to
    /// Retire. The worker never accesses a Unity object or changes the live scene.</summary>
    internal sealed class RoomAcousticMapJob
    {
        internal const int MaximumPoints = 8;
        const double MaximumComputeSeconds = 1;
        static readonly ProgressCallback Progress = ReportProgress;
        readonly object sync = new();
        readonly MetaXRAcousticNativeInterface.INativeInterface native;
        readonly Stopwatch timer = new();
        readonly Task task;
        Action firstProgress;
        IntPtr candidate;
        int cancelled;
        bool completed, retired;
        Action cleanup;
        internal string Issue { get; private set; }
        internal bool Completed { get { lock (sync) return completed; } }
        internal bool Cancelled => Volatile.Read(ref cancelled) != 0;

        internal RoomAcousticMapJob(MetaXRAcousticNativeInterface.INativeInterface api, float[] points, Action onFirstProgress = null)
        {
            if (points == null || points.Length == 0 || points.Length % 3 != 0 || points.Length > MaximumPoints * 3)
                throw new ArgumentException("Invalid acoustic map points");
            foreach (float point in points) if (!float.IsFinite(point) || Math.Abs(point) > 10000) throw new ArgumentException("Invalid acoustic map point");
            native = api ?? throw new ArgumentNullException(nameof(api));
            firstProgress = onFirstProgress;
            // Copy before handing data to native code. The caller may reuse its array.
            var ownedPoints = (float[])points.Clone();
            try
            {
                Check(native.CreateAudioSceneIR(out candidate));
                Check(native.AudioSceneIRSetEnabled(candidate, false));
                Check(native.InitializeAudioSceneIRParameters(out var parameters));
                parameters.thisSize = (UIntPtr)Marshal.SizeOf<MapParameters>();
                parameters.threadCount = (UIntPtr)1;
                parameters.reflectionCount = (UIntPtr)8;
                parameters.flags = AcousticMapFlags.NONE;
                task = Task.Run(() => Compute(ownedPoints, parameters));
            }
            catch { DestroyCandidate(); throw; }
        }
        void Compute(float[] points, MapParameters parameters)
        {
            GCHandle callbackOwner = default;
            try
            {
                if (Cancelled) return;
                timer.Start();
                callbackOwner = GCHandle.Alloc(this);
                parameters.callbacks = new SceneIRCallbacks { userData = GCHandle.ToIntPtr(callbackOwner), progress = Progress };
                int result = native.AudioSceneIRComputeCustomPoints(candidate, points, (UIntPtr)(points.Length / 3), ref parameters);
                if (Cancelled) return;
                if (timer.Elapsed.TotalSeconds > MaximumComputeSeconds) { Issue = "Acoustic map exceeded its compute budget"; return; }
                Check(result);
                Check(native.AudioSceneIRGetStatus(candidate, out var status));
                if (status != AcousticMapStatus.READY) throw new InvalidOperationException("Acoustic map is not ready");
            }
            catch (Exception) { Issue = "Acoustic map could not be computed"; }
            finally
            {
                if (callbackOwner.IsAllocated) callbackOwner.Free();
                timer.Stop();
                Action release = null;
                try
                {
                    lock (sync)
                    {
                        completed = true;
                        if (retired) { release = cleanup; cleanup = null; DestroyCandidate(); }
                    }
                }
                finally { release?.Invoke(); }
            }
        }
        [AOT.MonoPInvokeCallback(typeof(ProgressCallback))]
        static bool ReportProgress(IntPtr owner, string description, float progress)
        {
            // AOT-safe static callback. No Unity APIs, logging or captured delegates.
            var job = (RoomAcousticMapJob)GCHandle.FromIntPtr(owner).Target;
            // Tests can hold the actual native callback to exercise concurrent
            // edits/destruction; the shipping path never installs this hook.
            Interlocked.Exchange(ref job.firstProgress, null)?.Invoke();
            return !job.Cancelled && job.timer.Elapsed.TotalSeconds < MaximumComputeSeconds;
        }
        internal void Cancel() => Interlocked.Exchange(ref cancelled, 1);
        internal IntPtr Take()
        {
            lock (sync)
            {
                if (!completed || retired || Cancelled || Issue != null) return IntPtr.Zero;
                var value = candidate; candidate = IntPtr.Zero; return value;
            }
        }
        internal void Retire(Action releaseInputs = null)
        {
            Cancel();
            bool release = false;
            try
            {
                lock (sync)
                {
                    if (retired) throw new InvalidOperationException("Acoustic map job already retired");
                    retired = true;
                    if (!completed) { cleanup = releaseInputs; return; }
                    release = true; DestroyCandidate();
                }
            }
            finally { if (release) releaseInputs?.Invoke(); }
        }
        void DestroyCandidate()
        {
            if (candidate == IntPtr.Zero) return;
            var value = candidate; candidate = IntPtr.Zero; native.DestroyAudioSceneIR(value);
        }
        static void Check(int result) { if (result != 0) throw new InvalidOperationException("Native acoustic operation failed: " + result); }
    }
}
