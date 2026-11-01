// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Creation
{
    internal sealed class RoomResourceLimitException:InvalidOperationException
    {
        internal RoomResourceLimitException(string message):base(message){}
    }
}
