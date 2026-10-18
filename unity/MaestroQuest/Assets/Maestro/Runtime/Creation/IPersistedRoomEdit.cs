// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Creation
{
    // Shared completion boundary; object edits and personal navigation have
    // different native acceptance/Undo rules but the same queue and observation.
    internal interface IPersistedRoomEdit:IDisposable
    {
        bool Accepted {get;}
        bool Drained {get;}
        string InterruptionStatus {get;}
        bool Poll(out bool ready,out string error);
        bool Complete(out string error);
    }
}
