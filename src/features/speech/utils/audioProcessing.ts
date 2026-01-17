
// Utilities for processing raw PCM audio

export function mergeInt16Arrays(arrays: Int16Array[]): Int16Array {
  const totalLength = arrays.reduce((acc, curr) => acc + curr.length, 0);
  const result = new Int16Array(totalLength);
  let offset = 0;
  for (const arr of arrays) {
    result.set(arr, offset);
    offset += arr.length;
  }
  return result;
}

export function pcmToWav(pcm: Int16Array, sampleRate: number = 24000, numChannels: number = 1): string {
  const buffer = new ArrayBuffer(44 + pcm.byteLength);
  const view = new DataView(buffer);

  // RIFF chunk descriptor
  writeString(view, 0, 'RIFF');
  view.setUint32(4, 36 + pcm.byteLength, true);
  writeString(view, 8, 'WAVE');

  // fmt sub-chunk
  writeString(view, 12, 'fmt ');
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, numChannels, true);
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * numChannels * 2, true);
  view.setUint16(32, numChannels * 2, true);
  view.setUint16(34, 16, true);

  // data sub-chunk
  writeString(view, 36, 'data');
  view.setUint32(40, pcm.byteLength, true);

  // Write PCM data
  const dataBytes = new Uint8Array(buffer, 44);
  dataBytes.set(new Uint8Array(pcm.buffer, pcm.byteOffset, pcm.byteLength));

  // Encode the WHOLE buffer (header + data) to Base64
  const wholeBytes = new Uint8Array(buffer);
  let binary = '';
  const len = wholeBytes.byteLength;
  const chunkSize = 0x8000;
  for (let i = 0; i < len; i += chunkSize) {
    binary += String.fromCharCode.apply(null, wholeBytes.subarray(i, Math.min(i + chunkSize, len)) as unknown as number[]);
  }
  return 'data:audio/wav;base64,' + btoa(binary);
}

function writeString(view: DataView, offset: number, string: string) {
  for (let i = 0; i < string.length; i++) {
    view.setUint8(offset + i, string.charCodeAt(i));
  }
}

export function splitPcmBySilence(pcm: Int16Array, sampleRate: number, minSilenceMs: number = 400, threshold: number = 500): Int16Array[] {
  const minSilenceSamples = Math.floor((minSilenceMs / 1000) * sampleRate);
  const chunks: Int16Array[] = [];
  let startIndex = 0;
  let silenceStart = -1;

  for (let i = 0; i < pcm.length; i++) {
    if (Math.abs(pcm[i]) < threshold) {
      if (silenceStart === -1) silenceStart = i;
    } else {
      if (silenceStart !== -1) {
        const silenceDuration = i - silenceStart;
        if (silenceDuration > minSilenceSamples) {
          // Found a split point.
          // Include a bit of the silence (fade out) in the previous chunk
          // and skip most of the silence for the next chunk start
          const splitPoint = silenceStart + Math.floor(minSilenceSamples / 4);
          
          if (splitPoint > startIndex) {
             chunks.push(pcm.slice(startIndex, splitPoint));
          }
          
          startIndex = i - Math.floor(minSilenceSamples / 4); // Start next chunk with a bit of silence (fade in)
        }
        silenceStart = -1;
      }
    }
  }

  if (startIndex < pcm.length) {
    chunks.push(pcm.slice(startIndex));
  }

  // Filter out tiny chunks that might be noise
  return chunks.filter(c => c.length > sampleRate * 0.2); 
}

export function trimSilence(pcm: Int16Array, sampleRate: number = 16000, threshold: number = 500, padMs: number = 150): Int16Array {
  const padSamples = Math.floor((padMs / 1000) * sampleRate);
  
  let firstSpeech = -1;
  for (let i = 0; i < pcm.length; i++) {
      if (Math.abs(pcm[i]) > threshold) {
          firstSpeech = i;
          break;
      }
  }
  
  if (firstSpeech === -1) return new Int16Array(0); // All silence

  let lastSpeech = -1;
  for (let i = pcm.length - 1; i >= firstSpeech; i--) {
      if (Math.abs(pcm[i]) > threshold) {
          lastSpeech = i;
          break;
      }
  }

  const trimmedStart = Math.max(0, firstSpeech - padSamples);
  const trimmedEnd = Math.min(pcm.length, lastSpeech + padSamples);
  
  return pcm.slice(trimmedStart, trimmedEnd);
}
