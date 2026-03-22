/**
 * AudioWorklet Processor: Float32 to Int16 PCM Converter
 * 
 * This worklet runs on the audio rendering thread and converts
 * float32 audio samples (-1.0 to 1.0) to int16 PCM format
 * for streaming to speech recognition services.
 * 
 * The conversion happens off the main thread for better performance.
 */

// Note: This file runs in an AudioWorkletGlobalScope, not a regular JS context.
// TypeScript types are declared below for development convenience.

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

const PCM_CHUNK_SIZE = 512;

/**
 * Processor that converts float32 audio to int16 PCM and sends via MessagePort.
 * 
 * Incoming render quanta are typically 128 samples (~8ms at 16kHz). We batch
 * multiple quanta before posting to the main thread to reduce message traffic,
 * base64 work, and GC churn during live streaming.
 *
 * Each flushed PCM chunk is:
 * 1. Clamped to [-1, 1] range
 * 2. Converted to 16-bit signed integer
 * 3. Transferred to main thread via MessagePort
 */
class FloatToInt16Processor extends AudioWorkletProcessor {
  private buffer = new Int16Array(PCM_CHUNK_SIZE);
  private bufferedSamples = 0;

  process(inputs: Float32Array[][]): boolean {
    const input = inputs?.[0];
    const channel = input?.[0];
    
    if (channel && channel.length > 0) {
      let readOffset = 0;
      while (readOffset < channel.length) {
        const writable = PCM_CHUNK_SIZE - this.bufferedSamples;
        const copyCount = Math.min(writable, channel.length - readOffset);

        for (let i = 0; i < copyCount; i++) {
          // Clamp to [-1, 1] range
          let sample = channel[readOffset + i];
          sample = sample < -1 ? -1 : sample > 1 ? 1 : sample;

          // Convert to int16: negative samples use 0x8000 (32768), positive use 0x7FFF (32767)
          this.buffer[this.bufferedSamples + i] = sample < 0 ? sample * 0x8000 : sample * 0x7FFF;
        }

        this.bufferedSamples += copyCount;
        readOffset += copyCount;

        if (this.bufferedSamples === PCM_CHUNK_SIZE) {
          const chunk = this.buffer;
          this.port.postMessage(chunk, [chunk.buffer]);
          this.buffer = new Int16Array(PCM_CHUNK_SIZE);
          this.bufferedSamples = 0;
        }
      }
    }

    // Return true to keep the processor alive
    return true;
  }
}

// Register the processor with a generic name that can be used by both STT and conversation hooks
registerProcessor('float-to-int16-processor', FloatToInt16Processor);

export {};
