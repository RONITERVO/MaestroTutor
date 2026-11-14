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
  | { type: 'push'; generation: number; pcm: Int16Array; inputSampleRate: number }
  | { type: 'request-drain'; generation: number; requestId: number }
  | { type: 'reset'; generation: number };

type PlaybackTelemetryMessage = {
  type: 'telemetry';
  generation: number;
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
  private generation = 0;
  private submittedSamples = 0;
  private renderedSamples = 0;
  private lastProgressSamples = 0;
  private pendingDrains: { requestId: number; fence: number }[] = [];

  constructor() {
    super();

    this.port.onmessage = (event: MessageEvent<PlaybackMessage>) => {
      const data = event.data;
      if (!data) return;

      if (data.type === 'reset' && Number.isSafeInteger(data.generation) && data.generation > this.generation) {
        this.generation = data.generation;
        this.queue = [];
        this.currentChunk = null;
        this.currentSampleIndex = 0;
        this.currentSubsampleOffset = 0;
        this.queuedSamples = 0;
        this.playbackState = 'startup';
        this.inputSampleRate = DEFAULT_INPUT_SAMPLE_RATE;
        this.pendingDrains = [];
        this.submittedSamples = this.renderedSamples = this.lastProgressSamples = 0;
        return;
      }
      if (data.generation !== this.generation) return;

      if (data.type === 'request-drain' && Number.isSafeInteger(data.requestId) && data.requestId > 0) {
        if (this.pendingDrains.length >= 64) { this.port.postMessage({ type: 'error', generation: this.generation }); return; }
        this.pendingDrains.push({ requestId: data.requestId, fence: this.submittedSamples });
        return;
      }

      if (data.type === 'push' && data.pcm instanceof Int16Array && data.pcm.length > 0) {
        // Never silently discard a chunk or jump ahead. Report a refusal to the
        // output owner, which terminates the response with a playback error.
        if (data.inputSampleRate !== DEFAULT_INPUT_SAMPLE_RATE || this.queuedSamples + data.pcm.length > this.getHardQueueLimitSamples()) {
          this.port.postMessage({ type: 'error', generation: this.generation });
          return;
        }
        this.queue.push(data.pcm);
        this.queuedSamples += data.pcm.length;
        this.submittedSamples += data.pcm.length;
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
      generation: this.generation,
      event,
      queuedSamples: this.queuedSamples,
      inputSampleRate: this.inputSampleRate,
      outputSampleRate: sampleRate,
    };
    this.port.postMessage(message);
  }

  private emitCompletedDrains() {
    while (this.pendingDrains[0]?.fence <= this.renderedSamples) {
      const { requestId } = this.pendingDrains.shift()!;
      this.port.postMessage({ type: 'drained', requestId, generation: this.generation, renderedSamples: this.renderedSamples });
    }
  }

  private emitProgress() {
    if (this.renderedSamples === this.lastProgressSamples) return;
    if (this.queuedSamples > 0 && this.renderedSamples - this.lastProgressSamples < DEFAULT_INPUT_SAMPLE_RATE / 20) return;
    this.lastProgressSamples = this.renderedSamples;
    this.port.postMessage({ type: 'progress', generation: this.generation, renderedSamples: this.renderedSamples });
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
      this.renderedSamples += advanceNow;
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

    // A submitted fence must drain even a sub-threshold startup/refill tail;
    // it cannot wait indefinitely for new chunks to fill the anti-stutter buffer.
    const requiredBufferedSamples = this.pendingDrains.length > 0
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

    this.emitProgress();
    this.emitCompletedDrains();

    return true;
  }
}

registerProcessor('pcm-playback-processor', PcmPlaybackProcessor);

export {};
