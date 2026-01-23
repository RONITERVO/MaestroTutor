// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { debugLogService } from '../../features/diagnostics';
import { getAi } from './client';

const writeAscii = (view: DataView, offset: number, value: string) => {
  for (let i = 0; i < value.length; i++) {
    view.setUint8(offset + i, value.charCodeAt(i));
  }
};

const bytesToBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  const len = bytes.byteLength;
  const chunkSize = 0x8000;
  for (let i = 0; i < len; i += chunkSize) {
    const chunk = bytes.subarray(i, Math.min(i + chunkSize, len));
    binary += String.fromCharCode.apply(null, chunk as unknown as number[]);
  }
  return btoa(binary);
};

const pcm16ToWavBase64 = (base64Pcm: string, sampleRate = 24000, numChannels = 1): string => {
  const binaryString = atob(base64Pcm);
  const len = binaryString.length;

  const buffer = new ArrayBuffer(44 + len);
  const view = new DataView(buffer);

  writeAscii(view, 0, 'RIFF');
  view.setUint32(4, 36 + len, true);
  writeAscii(view, 8, 'WAVE');

  writeAscii(view, 12, 'fmt ');
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, numChannels, true);
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * numChannels * 2, true);
  view.setUint16(32, numChannels * 2, true);
  view.setUint16(34, 16, true);

  writeAscii(view, 36, 'data');
  view.setUint32(40, len, true);

  const bytes = new Uint8Array(buffer);
  for (let i = 0; i < len; i++) {
    bytes[44 + i] = binaryString.charCodeAt(i);
  }

  return bytesToBase64(bytes);
};

export const generateSpeech = async (params: { text: string; voiceName?: string }) => {
  const ai = getAi();
  const model = 'gemini-2.5-flash-preview-tts';
  const config = {
    responseModalities: ['AUDIO'],
    speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName: params.voiceName || 'Kore' } } },
  };
  const log = debugLogService.logRequest('generateSpeech', model, { text: params.text, config });

  try {
    const result = await ai.models.generateContent({
      model,
      contents: { parts: [{ text: params.text }] },
      config: config as any,
    });
    const c = result.candidates?.[0];
    const part = c?.content?.parts?.[0];
    if (part?.inlineData && part.inlineData.data) {
      log.complete({ audioBytes: part.inlineData.data.length });
      const wavBase64 = pcm16ToWavBase64(part.inlineData.data);
      return { audioBase64: wavBase64, mimeType: 'audio/wav' };
    }
    log.error('No audio data received');
  } catch (e) {
    console.error('Gemini TTS Error:', e);
    log.error(e);
  }
  return { audioBase64: '', mimeType: '' };
};
