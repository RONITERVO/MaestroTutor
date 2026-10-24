// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Imports {
    internal sealed class ChatImageTransfer:ChatMediaTransfer {
        internal ChatImageTransfer():base("imageHash",24,SurfaceImage.MaximumBytes,"image.chat.library"){}
    }
}
