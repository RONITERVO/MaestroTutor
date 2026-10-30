// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
namespace Maestro.Quest.Persistence
{
    /// <summary>Publish an already staged file. Never retry an effect, restage data or replace via delete/copy.</summary>
    internal static class FilePublication
    {
        internal const int MaximumAttempts=4;
        internal const bool Windows=
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            true;
#else
            false;
#endif
        internal static void Replace(string staged,string destination,string backup)
            =>Replace(staged,destination,backup,Windows,File.Replace,Thread.Sleep);

        // Windows documents these failures as leaving both original filenames in place.
        // In particular 1176/1177 can partially rename files and MUST NOT be retried.
        // https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew
        internal static bool Retryable(IOException error)=>error.HResult is
            unchecked((int)0x80070020) or unchecked((int)0x80070021) or unchecked((int)0x80070497);
        internal static void Replace(string staged,string destination,string backup,bool windows,
            Action<string,string,string> publish,Action<int> wait)
        {
            for(int attempt=0;;attempt++){
                try {publish(staged,destination,backup);if(attempt>0)UnityEngine.Debug.Log($"Maestro file publication recovered after {attempt+1} attempts");return;}
                catch(IOException error) when(windows&&attempt<MaximumAttempts-1&&Retryable(error)&&File.Exists(staged)&&File.Exists(destination)){
                    // At most 20+40+80 ms, only after a known Windows failure.
                    // No catch for permissions, full disks, partial renames or unknown IO errors.
                    wait(20<<attempt);
                }
            }
        }
    }
}
