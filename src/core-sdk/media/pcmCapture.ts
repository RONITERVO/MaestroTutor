// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** One prepared microphone owner, independent of WebAudio or a native DSP.
 * Delivers owned mono PCM16 at 16 kHz. Opening prepares capture but does not
 * deliver samples; start installs its only sink. close is terminal, immediately
 * fences delivery, and waits for resource release. flush drains accepted input
 * to that same sink, without closing or changing ownership.
 */
export interface PcmCaptureOwner {
  flush(): Promise<void>;
  close(): Promise<void>;
}

export interface PcmCapture extends PcmCaptureOwner {
  readonly sampleRate: 16000;
  start(sink: (pcm: Int16Array) => void): void;
}

/** The signal owns pending acquisition only. After success, the caller owns
 * close. A cancelled acquisition must release late resources before using them.
 */
export type OpenPcmCapture = (options?: { signal?: AbortSignal }) => Promise<PcmCapture>;
