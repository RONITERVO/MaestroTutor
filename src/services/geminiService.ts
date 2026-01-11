import { GoogleGenAI } from "@google/genai";

export class ApiError extends Error {
  status?: number;
  code?: string;
  cooldownSuggestSeconds?: number;
  constructor(message: string, opts?: { status?: number; code?: string; cooldownSuggestSeconds?: number }) {
    super(message);
    this.status = opts?.status;
    this.code = opts?.code;
    this.cooldownSuggestSeconds = opts?.cooldownSuggestSeconds;
  }
}

const getAi = () => new GoogleGenAI({ apiKey: process.env.API_KEY });

// Helper to wrap raw PCM16 samples in a WAV container so browsers can play it
function pcm16ToWavBase64(base64Pcm: string, sampleRate = 24000, numChannels = 1): string {
  const binaryString = atob(base64Pcm);
  const len = binaryString.length;
  const buffer = new Uint8Array(len);
  for (let i = 0; i < len; i++) {
    buffer[i] = binaryString.charCodeAt(i);
  }

  const wavHeader = new ArrayBuffer(44);
  const view = new DataView(wavHeader);

  const writeString = (offset: number, str: string) => {
    for (let i = 0; i < str.length; i++) {
      view.setUint8(offset + i, str.charCodeAt(i));
    }
  };

  writeString(0, 'RIFF');
  view.setUint32(4, 36 + len, true);
  writeString(8, 'WAVE');
  writeString(12, 'fmt ');
  view.setUint32(16, 16, true); // PCM chunk size
  view.setUint16(20, 1, true); // Linear PCM
  view.setUint16(22, numChannels, true);
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * numChannels * 2, true); // Byte rate
  view.setUint16(32, numChannels * 2, true); // Block align
  view.setUint16(34, 16, true); // Bits per sample
  writeString(36, 'data');
  view.setUint32(40, len, true);

  const headerBytes = new Uint8Array(wavHeader);
  const wavBytes = new Uint8Array(headerBytes.length + buffer.length);
  wavBytes.set(headerBytes, 0);
  wavBytes.set(buffer, headerBytes.length);

  let binary = '';
  for (let i = 0; i < wavBytes.length; i++) {
    binary += String.fromCharCode(wavBytes[i]);
  }
  return btoa(binary);
}

export async function checkFileStatuses(uris: string[]): Promise<Record<string, { deleted: boolean }>> {
  if (!uris || !uris.length) return {};
  const ai = getAi();
  const out: Record<string, { deleted: boolean }> = {};
  await Promise.all(uris.map(async (uri) => {
    try {
      let name = uri;
      const m = /\/files\/([^?\s]+)/.exec(uri || "");
      if (m) name = `files/${m[1]}`;
      const f = await ai.files.get({ name });
      out[uri] = { deleted: f.state === 'FAILED' }; 
    } catch (e: any) {
      // Only mark as deleted if strictly 404 or specific error, otherwise keep to be safe against transient network errors
      if (e.message?.includes('404') || e.message?.includes('not found') || e.status === 404) {
        out[uri] = { deleted: true };
      } else {
        out[uri] = { deleted: false };
      }
    }
  }));
  return out;
}

export async function sanitizeHistoryWithVerifiedUris(history: any[]) {
  const uris = history.map(h => h.imageFileUri).filter(u => u);
  if (uris.length === 0) return history;
  
  const statuses = await checkFileStatuses(uris);
  return history.map(h => {
    if (h.imageFileUri) {
        const isDeleted = statuses[h.imageFileUri]?.deleted;
        if (isDeleted) {
            const { imageFileUri, imageMimeType, ...rest } = h;
            return rest;
        }
    }
    return h;
  });
}

export async function uploadMediaToFiles(dataUrl: string, mimeType: string, displayName?: string): Promise<{ uri: string; mimeType: string }> {
  const ai = getAi();
  const base64Data = dataUrl.split(',')[1];
  const byteCharacters = atob(base64Data);
  const byteNumbers = new Array(byteCharacters.length);
  for (let i = 0; i < byteCharacters.length; i++) {
    byteNumbers[i] = byteCharacters.charCodeAt(i);
  }
  const byteArray = new Uint8Array(byteNumbers);
  const blob = new Blob([byteArray], { type: mimeType });
  // Create a proper File object for the SDK
  const file = new File([blob], displayName || "upload", { type: mimeType });

  const uploadResult = await ai.files.upload({
    file,
    config: { displayName, mimeType },
  });
  return { uri: uploadResult.uri, mimeType: uploadResult.mimeType };
}

export async function deleteFileByNameOrUri(nameOrUri: string) {
  const ai = getAi();
  let name = nameOrUri;
  const m = /\/files\/([^?\s]+)/.exec(nameOrUri || "");
  if (m) name = `files/${m[1]}`;
  try {
    await ai.files.delete({ name });
    return { ok: true };
  } catch {
    return { ok: false };
  }
}

export async function generateGeminiResponse(
  modelName: string,
  userPrompt: string,
  history: any[],
  systemInstruction?: string,
  imageBase64?: string,
  imageMimeType?: string,
  imageFileUri?: string,
  useGoogleSearch?: boolean,
  configOverrides?: any
) {
  const ai = getAi();
  const contents: any[] = [];
  
  // Transform simplified history to Gemini content format
  history.forEach(h => {
    const parts: any[] = [];
    // Critical: Use rawAssistantResponse if available (contains the full text), otherwise h.text
    const textContent = h.rawAssistantResponse || h.text;
    if (textContent) parts.push({ text: textContent });
    
    if (h.imageFileUri) {
        parts.push({ fileData: { fileUri: h.imageFileUri, mimeType: h.imageMimeType || 'image/jpeg' } });
    } else if (h.imageUrl && h.imageMimeType) {
        // Fallback to inline if no URI (and if mime matches supported inline types)
        const b64 = h.imageUrl.split(',')[1];
        if (b64) parts.push({ inlineData: { data: b64, mimeType: h.imageMimeType } });
    }
    
    if (parts.length > 0) {
        // Gemini API expects 'model' for assistant/AI role
        const role = h.role === 'assistant' ? 'model' : 'user';
        contents.push({ role, parts });
    }
  });

  const currentParts: any[] = [{ text: userPrompt }];
  if (imageFileUri) {
    currentParts.push({ fileData: { fileUri: imageFileUri, mimeType: imageMimeType } });
  } else if (imageBase64 && imageMimeType) {
    const b64 = imageBase64.split(',')[1];
    if (b64) currentParts.push({ inlineData: { data: b64, mimeType: imageMimeType } });
  }
  
  // Merge current user prompt into previous user turn if possible to avoid User-User sequence,
  // or just push as new turn. Gemini generally tolerates User-User but merging is safer.
  // For simplicity and clarity in this implementation, we push a new turn.
  contents.push({ role: 'user', parts: currentParts });

  const config: any = { ...configOverrides };
  if (systemInstruction) config.systemInstruction = systemInstruction;
  
  // Correct tool configuration for Google Search
  if (useGoogleSearch) {
      config.tools = [{ googleSearch: {} }];
  }

  try {
      const result = await ai.models.generateContent({
        model: modelName,
        contents,
        config
      });
      return {
        text: result.text,
        candidates: result.candidates,
        usageMetadata: result.usageMetadata,
      };
  } catch (e: any) {
      console.error("Gemini API Error:", e);
      throw new ApiError(e.message || "Gemini API failed", { status: 500, code: e.status });
  }
}

export async function generateImage(params: {
  prompt?: string;
  history?: any[];
  latestMessageText?: string;
  latestMessageRole?: 'user' | 'assistant';
  systemInstruction?: string;
  maestroAvatarUri?: string;
  maestroAvatarMimeType?: string;
}) {
  const ai = getAi();
  const { prompt, latestMessageText, history, systemInstruction, maestroAvatarUri, maestroAvatarMimeType } = params;
  
  const contents: any[] = [];
  
  if (history && history.length > 0) {
      history.forEach(h => {
        const parts: any[] = [];
        const textContent = h.rawAssistantResponse || h.text;
        if (textContent) parts.push({ text: textContent });
        
        if (h.imageFileUri) {
            parts.push({ fileData: { fileUri: h.imageFileUri, mimeType: h.imageMimeType || 'image/jpeg' } });
        }
        if (parts.length) contents.push({ role: h.role === 'assistant' ? 'model' : 'user', parts });
      });
  }

  const currentParts: any[] = [];
  if (prompt) {
      currentParts.push({ text: prompt });
  } else if (latestMessageText) {
      currentParts.push({ text: latestMessageText });
  }
  
  if (maestroAvatarUri) {
      currentParts.push({ fileData: { fileUri: maestroAvatarUri, mimeType: maestroAvatarMimeType || 'image/png' } });
  }
  
  if (currentParts.length) {
      contents.push({ role: 'user', parts: currentParts });
  }

  try {
      const result = await ai.models.generateContent({
        model: 'gemini-2.5-flash-image',
        contents,
        config: { 
            responseModalities: ['IMAGE'],
            systemInstruction
        }
      });
      
      const candidates = result.candidates || [];
      for (const c of candidates) {
          for (const part of c.content.parts) {
              if (part.inlineData && part.inlineData.mimeType.startsWith('image/')) {
                  return { base64Image: `data:${part.inlineData.mimeType};base64,${part.inlineData.data}`, mimeType: part.inlineData.mimeType };
              }
          }
      }
      throw new Error("No image generated");
  } catch (e: any) {
      return { error: e.message };
  }
}

export async function translateText(text: string, from: string, to: string) {
  const ai = getAi();
  const prompt = `Translate the following text from ${from} to ${to}. Return ONLY the translation. Text: "${text}"`;
  const result = await ai.models.generateContent({
    model: 'gemini-3-flash-preview',
    contents: prompt
  });
  return { translatedText: result.text || "" };
}

export async function generateSpeech(params: { text: string, voiceName?: string }) {
    const ai = getAi();
    try {
        const result = await ai.models.generateContent({
            model: 'gemini-2.5-flash-preview-tts',
            contents: { parts: [{ text: params.text }] },
            config: {
                responseModalities: ['AUDIO'],
                speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName: params.voiceName || 'Kore' } } }
            }
        });
        const c = result.candidates?.[0];
        const part = c?.content?.parts?.[0];
        if (part?.inlineData) {
            // Gemini TTS returns raw PCM in most cases for this model, wrap it in a WAV container to ensure playback
            const wavBase64 = pcm16ToWavBase64(part.inlineData.data);
            return { audioBase64: wavBase64, mimeType: 'audio/wav' }; 
        }
    } catch (e) {
      console.error("Gemini TTS Error:", e);
    }
    return { audioBase64: "", mimeType: "" };
}

export async function sendSessionHeartbeat() {}
export async function markSessionClosed() {}