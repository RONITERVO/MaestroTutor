/**
 * Audio Worklets Module
 * 
 * Provides properly bundled AudioWorklet URLs for use with AudioContext.audioWorklet.addModule().
 * 
 * Vite handles the worklet bundling automatically with the ?worker&url import suffix.
 * This creates a separate bundle for the worklet that runs in the AudioWorkletGlobalScope.
 */

// Import worklet as URL - Vite will bundle this as a separate worker file
// The ?worker&url suffix tells Vite to:
// 1. Bundle the file as a web worker
// 2. Return the URL to the bundled file instead of instantiating it
import floatToInt16ProcessorUrl from './floatToInt16Processor.worklet.ts?worker&url';
import pcmPlaybackProcessorUrl from './pcmPlaybackProcessor.worklet.ts?worker&url';

/**
 * URL to the Float32-to-Int16 audio processor worklet.
 * 
 * Use with: `audioContext.audioWorklet.addModule(FLOAT_TO_INT16_PROCESSOR_URL)`
 * 
 * After loading, create a node with: `new AudioWorkletNode(ctx, 'float-to-int16-processor')`
 */
export const FLOAT_TO_INT16_PROCESSOR_URL: string = floatToInt16ProcessorUrl;

/**
 * The registered name of the float-to-int16 processor.
 * Use this when creating AudioWorkletNode instances.
 */
export const FLOAT_TO_INT16_PROCESSOR_NAME = 'float-to-int16-processor';

/**
 * URL to the PCM16 playback processor worklet.
 *
 * Use with: `audioContext.audioWorklet.addModule(PCM_PLAYBACK_PROCESSOR_URL)`
 */
export const PCM_PLAYBACK_PROCESSOR_URL: string = pcmPlaybackProcessorUrl;

/**
 * The registered name of the PCM playback processor.
 */
export const PCM_PLAYBACK_PROCESSOR_NAME = 'pcm-playback-processor';
