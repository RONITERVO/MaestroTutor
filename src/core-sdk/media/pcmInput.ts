// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { CoreRuntime } from '../runtime';
import { createCoreRuntime } from '../runtime';

export interface PcmInputFrame {
  pcm: Int16Array;
  sampleRate: number;
  source: 'device' | 'synthetic';
}

export type PcmInputSink = (frame: PcmInputFrame) => void | Promise<void>;

export interface PcmCaptureHandoffStats {
  bufferedPackets: number;
  bufferedSamples: number;
}

/**
 * Lossless, single-use bridge between an early capture owner and its final
 * consumer. Capture may begin before a provider socket exists; transfer drains
 * the queued prefix in order and then routes future packets directly.
 */
export class PcmCaptureHandoff {
  private bufferedPackets: Int16Array[] = [];
  private sink: ((pcm: Int16Array) => void) | null = null;
  private transferred = false;

  push(pcm: Int16Array): void {
    if (!(pcm instanceof Int16Array) || pcm.length === 0) return;
    if (this.sink) {
      this.sink(pcm);
      return;
    }
    this.bufferedPackets.push(pcm.slice());
  }

  transferTo(sink: (pcm: Int16Array) => void): PcmCaptureHandoffStats {
    if (this.transferred) throw new Error('PCM capture has already been transferred.');
    this.transferred = true;
    this.sink = sink;
    const queued = this.bufferedPackets;
    this.bufferedPackets = [];
    for (const packet of queued) sink(packet);
    return {
      bufferedPackets: queued.length,
      bufferedSamples: queued.reduce((total, packet) => total + packet.length, 0),
    };
  }
}

export interface PcmInputSource {
  readonly sampleRate: number;
  readonly kind: 'device' | 'synthetic';
  start(sink: PcmInputSink): Promise<void>;
  stop(): Promise<void>;
}

export class PcmCaptureRouter {
  private readonly sink: PcmInputSink;
  private readonly runtime?: CoreRuntime;
  private readonly operationId: string;
  private source: PcmInputSource | null = null;
  private stopped = false;

  constructor(options: { sink: PcmInputSink; runtime?: CoreRuntime; operationId?: string }) {
    this.sink = options.sink;
    this.runtime = options.runtime;
    this.operationId = options.operationId || this.runtime?.ids.create('pcm-input') || `pcm-input-${Date.now()}`;
  }

  async push(pcm: Int16Array, sampleRate: number, source: PcmInputFrame['source'] = 'device') {
    if (this.stopped || !(pcm instanceof Int16Array) || pcm.length === 0) return;
    const isolated = pcm.slice();
    this.runtime?.events.emit({
      operationId: this.operationId,
      journey: 'speech',
      phase: 'input.frame',
      data: { source, sampleRate, samples: isolated.length },
    });
    await this.sink({ pcm: isolated, sampleRate, source });
  }

  async attach(source: PcmInputSource) {
    if (this.source) throw new Error('A PCM input source is already attached.');
    this.source = source;
    this.stopped = false;
    this.runtime?.events.emit({
      operationId: this.operationId,
      journey: 'speech',
      phase: 'input.started',
      data: { source: source.kind, sampleRate: source.sampleRate },
    });
    await source.start(frame => this.push(frame.pcm, frame.sampleRate, frame.source));
  }

  async stop() {
    if (this.stopped) return;
    this.stopped = true;
    await this.source?.stop();
    this.source = null;
    this.runtime?.events.emit({
      operationId: this.operationId,
      journey: 'speech',
      phase: 'input.stopped',
    });
  }
}

export const createSyntheticPcmSource = (options: {
  pcm: Int16Array;
  sampleRate: number;
  chunkDurationMs?: number;
  pace?: boolean;
  runtime?: CoreRuntime;
}): PcmInputSource => {
  const runtime = options.runtime || createCoreRuntime();
  const samplesPerChunk = Math.max(1, Math.round(options.sampleRate * (options.chunkDurationMs ?? 20) / 1_000));
  let stopped = false;
  return {
    sampleRate: options.sampleRate,
    kind: 'synthetic',
    async start(sink) {
      stopped = false;
      const startedAt = runtime.clock.now();
      let scheduledSamples = 0;
      for (let offset = 0; offset < options.pcm.length && !stopped; offset += samplesPerChunk) {
        const pcm = options.pcm.slice(offset, Math.min(options.pcm.length, offset + samplesPerChunk));
        scheduledSamples += pcm.length;
        if (options.pace !== false) {
          // A microphone is driven by its hardware clock; callback/logging work
          // does not add a fresh frame-duration delay every iteration. Pace to
          // absolute sample deadlines so processing overhead cannot accumulate
          // into a synthetic user who speaks progressively slower than reality.
          const dueAt = startedAt + scheduledSamples / options.sampleRate * 1_000;
          const delayMs = dueAt - runtime.clock.now();
          if (delayMs > 0) await runtime.clock.sleep(delayMs);
        }
        if (stopped) break;
        await sink({ pcm, sampleRate: options.sampleRate, source: 'synthetic' });
      }
    },
    async stop() {
      stopped = true;
    },
  };
};

export const decodePcm16LeBase64 = (value: string): Int16Array => {
  const bytes = Uint8Array.from(globalThis.atob(value), character => character.charCodeAt(0));
  if (bytes.byteLength % 2 !== 0) throw new Error('PCM16 base64 must contain an even number of bytes.');
  const output = new Int16Array(bytes.byteLength / 2);
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  for (let index = 0; index < output.length; index++) output[index] = view.getInt16(index * 2, true);
  return output;
};
