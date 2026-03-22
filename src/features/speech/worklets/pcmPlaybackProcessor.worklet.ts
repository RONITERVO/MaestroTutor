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

const MAX_QUEUED_SAMPLES = 24000 * 8;

type PlaybackMessage =
  | { type: 'push'; pcm: Int16Array }
  | { type: 'reset' };

class PcmPlaybackProcessor extends AudioWorkletProcessor {
  private queue: Int16Array[] = [];
  private currentChunk: Int16Array | null = null;
  private currentOffset = 0;
  private queuedSamples = 0;

  constructor() {
    super();

    this.port.onmessage = (event: MessageEvent<PlaybackMessage>) => {
      const data = event.data;
      if (!data) return;

      if (data.type === 'reset') {
        this.queue = [];
        this.currentChunk = null;
        this.currentOffset = 0;
        this.queuedSamples = 0;
        return;
      }

      if (data.type === 'push' && data.pcm instanceof Int16Array && data.pcm.length > 0) {
        if (this.queuedSamples >= MAX_QUEUED_SAMPLES) {
          this.queue = [];
          this.currentChunk = null;
          this.currentOffset = 0;
          this.queuedSamples = 0;
        }
        this.queue.push(data.pcm);
        this.queuedSamples += data.pcm.length;
      }
    };
  }

  process(_inputs: Float32Array[][], outputs: Float32Array[][]): boolean {
    const output = outputs?.[0]?.[0];
    if (!output) return true;

    let writeIndex = 0;
    while (writeIndex < output.length) {
      if (!this.currentChunk || this.currentOffset >= this.currentChunk.length) {
        this.currentChunk = this.queue.shift() || null;
        this.currentOffset = 0;
        if (!this.currentChunk) {
          break;
        }
      }

      const copyCount = Math.min(
        this.currentChunk.length - this.currentOffset,
        output.length - writeIndex
      );

      for (let i = 0; i < copyCount; i++) {
        output[writeIndex + i] = this.currentChunk[this.currentOffset + i] / 32768;
      }

      this.currentOffset += copyCount;
      writeIndex += copyCount;
      this.queuedSamples = Math.max(0, this.queuedSamples - copyCount);

      if (this.currentOffset >= this.currentChunk.length) {
        this.currentChunk = null;
        this.currentOffset = 0;
      }
    }

    while (writeIndex < output.length) {
      output[writeIndex++] = 0;
    }

    return true;
  }
}

registerProcessor('pcm-playback-processor', PcmPlaybackProcessor);

export {};
