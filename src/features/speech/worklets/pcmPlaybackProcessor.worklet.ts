/**
 * AudioWorklet Processor: PCM16 playback queue.
 *
 * Model audio chunks are queued from the main thread and rendered on the audio
 * thread. This avoids per-chunk AudioBufferSourceNode creation on the main
 * thread, which was a frequent source of stutter under UI load.
 */

declare class AudioWorkletProcessor {
  readonly port: MessagePort;
  process(
    inputs: Float32Array[][],
    outputs: Float32Array[][],
    parameters: Record<string, Float32Array>
  ): boolean;
}

declare function registerProcessor(
  name: string,
  processorCtor: new () => AudioWorkletProcessor
): void;

declare const sampleRate: number;

const DEFAULT_INPUT_SAMPLE_RATE = 24000;
const HARD_MAX_QUEUED_MS = 180000;
const STARTUP_BUFFER_MS = 120;
const REFILL_BUFFER_MS = 60;

type PlaybackMessage =
  | { type: 'push'; pcm: Int16Array; inputSampleRate?: number }
  | { type: 'request-drain'; requestId: number }
  | { type: 'reset' };

type PlaybackTelemetryMessage = {
  type: 'telemetry';
  event: 'started' | 'resumed' | 'underrun';
  queuedSamples: number;
  inputSampleRate: number;
  outputSampleRate: number;
};

type PlaybackState = 'startup' | 'playing' | 'refill';

class PcmPlaybackProcessor extends AudioWorkletProcessor {
  private queue: Int16Array[] = [];
  private currentChunk: Int16Array | null = null;
  private currentSampleIndex = 0;
  private currentSubsampleOffset = 0;
  private queuedSamples = 0;
  private playbackState: PlaybackState = 'startup';
  private inputSampleRate = DEFAULT_INPUT_SAMPLE_RATE;
  private pendingDrainRequestIds: number[] = [];

  constructor() {
    super();

    this.port.onmessage = (event: MessageEvent<PlaybackMessage>) => {
      const data = event.data;
      if (!data) return;

      if (data.type === 'reset') {
        this.queue = [];
        this.currentChunk = null;
        this.currentSampleIndex = 0;
        this.currentSubsampleOffset = 0;
        this.queuedSamples = 0;
        this.playbackState = 'startup';
        this.inputSampleRate = DEFAULT_INPUT_SAMPLE_RATE;
        this.pendingDrainRequestIds = [];
        return;
      }

      if (data.type === 'request-drain' && Number.isSafeInteger(data.requestId) && data.requestId > 0) {
        this.pendingDrainRequestIds.push(data.requestId);
        return;
      }

      if (data.type === 'push' && data.pcm instanceof Int16Array && data.pcm.length > 0) {
        this.inputSampleRate = Math.max(1, Math.round(data.inputSampleRate || this.inputSampleRate));

        // Preserve already-buffered speech. If backlog ever becomes truly
        // pathological, refuse only the newest chunk instead of discarding the
        // queue and audibly jumping ahead in the transcript.
        if (this.queuedSamples + data.pcm.length > this.getHardQueueLimitSamples()) {
          return;
        }
        this.queue.push(data.pcm);
        this.queuedSamples += data.pcm.length;
      }
    };
  }

  private getHardQueueLimitSamples(): number {
    return Math.max(1, Math.floor((this.inputSampleRate * HARD_MAX_QUEUED_MS) / 1000));
  }

  private getRequiredBufferedSamples(): number {
    const targetMs = this.playbackState === 'startup'
      ? STARTUP_BUFFER_MS
      : this.playbackState === 'refill'
        ? REFILL_BUFFER_MS
        : 0;
    return Math.max(0, Math.floor((this.inputSampleRate * targetMs) / 1000));
  }

  private emitTelemetry(event: PlaybackTelemetryMessage['event']) {
    const message: PlaybackTelemetryMessage = {
      type: 'telemetry',
      event,
      queuedSamples: this.queuedSamples,
      inputSampleRate: this.inputSampleRate,
      outputSampleRate: sampleRate,
    };
    this.port.postMessage(message);
  }

  private emitCompletedDrains() {
    if (this.queuedSamples > 0 || this.pendingDrainRequestIds.length === 0) return;
    const completed = this.pendingDrainRequestIds.splice(0);
    for (const requestId of completed) {
      this.port.postMessage({ type: 'drained', requestId });
    }
  }

  private ensureCurrentChunk(): boolean {
    while (!this.currentChunk) {
      this.currentChunk = this.queue.shift() || null;
      this.currentSampleIndex = 0;
      if (!this.currentChunk) {
        return false;
      }
      if (this.currentChunk.length === 0) {
        this.currentChunk = null;
      }
    }
    return true;
  }

  private peekSourceSample(offset: number): number {
    if (!this.ensureCurrentChunk() || !this.currentChunk) return 0;

    let index = this.currentSampleIndex + offset;
    let chunk: Int16Array | null = this.currentChunk;
    if (index < chunk.length) {
      return chunk[index];
    }

    index -= chunk.length;
    for (let i = 0; i < this.queue.length; i++) {
      chunk = this.queue[i];
      if (index < chunk.length) {
        return chunk[index];
      }
      index -= chunk.length;
    }

    return this.currentChunk[this.currentChunk.length - 1] || 0;
  }

  private advanceSourceSamples(count: number) {
    let remainingToAdvance = count;
    while (remainingToAdvance > 0) {
      if (!this.ensureCurrentChunk() || !this.currentChunk) {
        this.currentSampleIndex = 0;
        this.currentSubsampleOffset = 0;
        return;
      }

      const remainingInChunk = this.currentChunk.length - this.currentSampleIndex;
      const advanceNow = Math.min(remainingToAdvance, remainingInChunk);
      this.currentSampleIndex += advanceNow;
      this.queuedSamples = Math.max(0, this.queuedSamples - advanceNow);
      remainingToAdvance -= advanceNow;

      if (this.currentSampleIndex >= this.currentChunk.length) {
        this.currentChunk = null;
        this.currentSampleIndex = 0;
      }
    }
  }

  process(_inputs: Float32Array[][], outputs: Float32Array[][]): boolean {
    const output = outputs?.[0]?.[0];
    if (!output) return true;

    if (this.queuedSamples === 0) {
      output.fill(0);
      this.emitCompletedDrains();
      return true;
    }

    // Once the main thread seals a transport, no more chunks are coming. Drain
    // even a sub-threshold startup/refill tail instead of waiting forever for
    // the normal anti-stutter buffer target.
    const requiredBufferedSamples = this.pendingDrainRequestIds.length > 0
      ? 0
      : this.getRequiredBufferedSamples();

    if (requiredBufferedSamples > 0 && this.queuedSamples < requiredBufferedSamples) {
      output.fill(0);
      return true;
    }

    const previousState = this.playbackState;
    this.playbackState = 'playing';
    if (previousState === 'startup') {
      this.emitTelemetry('started');
    } else if (previousState === 'refill') {
      this.emitTelemetry('resumed');
    }

    let writeIndex = 0;
    const resampleStep = this.inputSampleRate / sampleRate;
    while (writeIndex < output.length) {
      if (!this.ensureCurrentChunk() || !this.currentChunk) {
        this.playbackState = 'refill';
        this.emitTelemetry('underrun');
        break;
      }

      const sampleA = this.peekSourceSample(0);
      const sampleB = this.peekSourceSample(1);
      const interpolated = sampleA + (sampleB - sampleA) * this.currentSubsampleOffset;
      output[writeIndex++] = interpolated / 32768;

      this.currentSubsampleOffset += resampleStep;
      const wholeSourceSamples = Math.floor(this.currentSubsampleOffset);
      if (wholeSourceSamples > 0) {
        this.currentSubsampleOffset -= wholeSourceSamples;
        this.advanceSourceSamples(wholeSourceSamples);
      }
    }

    while (writeIndex < output.length) {
      output[writeIndex++] = 0;
    }

    this.emitCompletedDrains();

    return true;
  }
}

registerProcessor('pcm-playback-processor', PcmPlaybackProcessor);

export {};
