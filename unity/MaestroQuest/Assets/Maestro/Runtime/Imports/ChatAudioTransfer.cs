// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
namespace Maestro.Quest.Imports {
    internal sealed class ChatAudioTransfer:ChatMediaTransfer {
        internal ChatAudioTransfer():base("assetHash",44,WaveAudio.MaximumBytes,"audio.chat.library"){}
    }
}
