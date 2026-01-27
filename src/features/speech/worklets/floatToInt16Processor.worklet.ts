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

/**
 * Processor that converts float32 audio to int16 PCM and sends via MessagePort.
 * 
 * Each audio frame (typically 128 samples at 16kHz = ~8ms) is:
 * 1. Clamped to [-1, 1] range
 * 2. Converted to 16-bit signed integer
 * 3. Transferred to main thread via MessagePort
 */
class FloatToInt16Processor extends AudioWorkletProcessor {
  process(inputs: Float32Array[][]): boolean {
    const input = inputs?.[0];
    const channel = input?.[0];
    
    if (channel && channel.length > 0) {
      const len = channel.length;
      const int16 = new Int16Array(len);
      
      for (let i = 0; i < len; i++) {
        // Clamp to [-1, 1] range
        let sample = channel[i];
        sample = sample < -1 ? -1 : sample > 1 ? 1 : sample;
        
        // Convert to int16: negative samples use 0x8000 (32768), positive use 0x7FFF (32767)
        int16[i] = sample < 0 ? sample * 0x8000 : sample * 0x7FFF;
      }
      
      // Transfer the buffer (zero-copy) to avoid memory overhead
      this.port.postMessage(int16, [int16.buffer]);
    }

    // Return true to keep the processor alive
    return true;
  }
}

// Register the processor with a generic name that can be used by both STT and conversation hooks
registerProcessor('float-to-int16-processor', FloatToInt16Processor);
