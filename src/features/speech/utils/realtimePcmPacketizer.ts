// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface RealtimePcmPacketizerOptions {
  sampleRate: number;
  packetDurationMs?: number;
  maxWaitMs?: number;
  onPacket: (packet: Int16Array) => void | Promise<void>;
}

export interface RealtimePcmPacketizerStats {
  totalInputSamples: number;
  totalOutputSamples: number;
  packetsSent: number;
  partialPacketsSent: number;
  timerFlushes: number;
  explicitFlushes: number;
  maxBufferedSamples: number;
}

/**
 * Coalesces incoming PCM chunks into steadier packets before they are encoded
 * and sent to the live API. This reduces message churn, keeps packet ordering
 * deterministic, and flushes the last partial packet so turn-final audio does
 * not get stranded behind the target packet size.
 */
export class RealtimePcmPacketizer {
  private readonly targetPacketSamples: number;
  private readonly maxWaitMs: number;
  private readonly onPacket: (packet: Int16Array) => void | Promise<void>;

  private bufferedChunks: Int16Array[] = [];
  private bufferedSamples = 0;
  private flushTimer: number | null = null;
  private sendQueue: Promise<void> = Promise.resolve();
  private stats: RealtimePcmPacketizerStats = {
    totalInputSamples: 0,
    totalOutputSamples: 0,
    packetsSent: 0,
    partialPacketsSent: 0,
    timerFlushes: 0,
    explicitFlushes: 0,
    maxBufferedSamples: 0,
  };

  constructor(options: RealtimePcmPacketizerOptions) {
    const packetDurationMs = Math.max(20, options.packetDurationMs ?? 100);
    this.targetPacketSamples = Math.max(
      1,
      Math.round((options.sampleRate * packetDurationMs) / 1000)
    );
    this.maxWaitMs = Math.max(packetDurationMs, options.maxWaitMs ?? packetDurationMs + 20);
    this.onPacket = options.onPacket;
  }

  push(chunk: Int16Array) {
    if (!(chunk instanceof Int16Array) || chunk.length === 0) return;

    // Isolate the live-send buffer from any other retention of the same chunk.
    this.bufferedChunks.push(chunk.slice());
    this.bufferedSamples += chunk.length;
    this.stats.totalInputSamples += chunk.length;
    this.stats.maxBufferedSamples = Math.max(this.stats.maxBufferedSamples, this.bufferedSamples);

    this.flushWholePackets();
    if (this.bufferedSamples > 0) {
      this.ensureFlushTimer();
    }
  }

  async flushPending(): Promise<void> {
    this.clearFlushTimer();
    this.flushWholePackets();

    if (this.bufferedSamples > 0) {
      this.enqueuePacket(this.takeSamples(this.bufferedSamples), 'flush');
    }

    await this.sendQueue.catch(() => undefined);
  }

  getStats(): RealtimePcmPacketizerStats {
    return {
      ...this.stats,
      maxBufferedSamples: Math.max(this.stats.maxBufferedSamples, this.bufferedSamples),
    };
  }

  dispose() {
    this.clearFlushTimer();
    this.bufferedChunks = [];
    this.bufferedSamples = 0;
  }

  private ensureFlushTimer() {
    if (this.flushTimer !== null) return;
    this.flushTimer = window.setTimeout(() => {
      this.flushTimer = null;
      if (this.bufferedSamples === 0) return;
      this.enqueuePacket(this.takeSamples(this.bufferedSamples), 'timer');
    }, this.maxWaitMs);
  }

  private clearFlushTimer() {
    if (this.flushTimer === null) return;
    window.clearTimeout(this.flushTimer);
    this.flushTimer = null;
  }

  private flushWholePackets() {
    while (this.bufferedSamples >= this.targetPacketSamples) {
      this.enqueuePacket(this.takeSamples(this.targetPacketSamples), 'full');
    }

    if (this.bufferedSamples === 0) {
      this.clearFlushTimer();
    }
  }

  private enqueuePacket(packet: Int16Array, reason: 'full' | 'timer' | 'flush') {
    if (packet.length === 0) return;
    this.stats.packetsSent += 1;
    this.stats.totalOutputSamples += packet.length;
    if (packet.length < this.targetPacketSamples) {
      this.stats.partialPacketsSent += 1;
    }
    if (reason === 'timer') {
      this.stats.timerFlushes += 1;
    }
    if (reason === 'flush') {
      this.stats.explicitFlushes += 1;
    }
    this.sendQueue = this.sendQueue
      .catch(() => undefined)
      .then(async () => {
        await this.onPacket(packet);
      });
  }

  private takeSamples(sampleCount: number): Int16Array {
    if (sampleCount <= 0 || this.bufferedSamples === 0) {
      return new Int16Array(0);
    }

    const actualCount = Math.min(sampleCount, this.bufferedSamples);
    const packet = new Int16Array(actualCount);
    let writeOffset = 0;

    while (writeOffset < actualCount && this.bufferedChunks.length > 0) {
      const head = this.bufferedChunks[0];
      const copyCount = Math.min(head.length, actualCount - writeOffset);
      packet.set(head.subarray(0, copyCount), writeOffset);
      writeOffset += copyCount;

      if (copyCount === head.length) {
        this.bufferedChunks.shift();
      } else {
        this.bufferedChunks[0] = head.slice(copyCount);
      }
    }

    this.bufferedSamples = Math.max(0, this.bufferedSamples - actualCount);
    return packet;
  }
}

export default RealtimePcmPacketizer;
