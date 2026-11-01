// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Book
{
    public enum BrowserPointerPhase { Down, Move, Up, Cancel }

    public interface IBookBrowser
    {
        Texture Surface { get; }
        Vector2Int Resolution { get; }
        bool IsReady { get; }
        void Pointer(int x, int y, BrowserPointerPhase phase);
        void ExecuteBookCommand(string commandJson);
        void SetSuspended(bool suspended);
    }
}
