// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface CaptureWorkletFlushCommand {
  type: 'flush';
}

export interface CaptureWorkletFlushComplete {
  type: 'flush-complete';
}

export type CaptureWorkletMessage = Int16Array | CaptureWorkletFlushComplete;

export const isCaptureWorkletFlushComplete = (
  message: unknown
): message is CaptureWorkletFlushComplete => (
  !!message
  && !(message instanceof Int16Array)
  && typeof message === 'object'
  && 'type' in message
  && (message as { type?: unknown }).type === 'flush-complete'
);

export const flushCaptureWorkletNode = (
  node: AudioWorkletNode | null,
  timeoutMs = 100
): Promise<void> => new Promise((resolve) => {
  if (!node) {
    resolve();
    return;
  }

  let settled = false;

  const finish = () => {
    if (settled) return;
    settled = true;
    window.clearTimeout(timeoutId);
    node.port.removeEventListener('message', handleFlushAck as EventListener);
    resolve();
  };

  const handleFlushAck = (event: MessageEvent) => {
    if (isCaptureWorkletFlushComplete(event.data)) {
      finish();
    }
  };

  const timeoutId = window.setTimeout(finish, timeoutMs);

  node.port.addEventListener('message', handleFlushAck as EventListener);
  try {
    node.port.start?.();
  } catch {
    // Ignore browsers that do not require explicit port start.
  }

  try {
    node.port.postMessage({ type: 'flush' } satisfies CaptureWorkletFlushCommand);
  } catch {
    finish();
  }
});
