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
      for (let offset = 0; offset < options.pcm.length && !stopped; offset += samplesPerChunk) {
        const pcm = options.pcm.slice(offset, Math.min(options.pcm.length, offset + samplesPerChunk));
        await sink({ pcm, sampleRate: options.sampleRate, source: 'synthetic' });
        if (options.pace !== false && offset + samplesPerChunk < options.pcm.length) {
          await runtime.clock.sleep((pcm.length / options.sampleRate) * 1_000);
        }
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
