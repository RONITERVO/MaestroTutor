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

// Helper to write ASCII strings to DataView
function writeAscii(view: DataView, offset: number, string: string) {
  for (let i = 0; i < string.length; i++) {
    view.setUint8(offset + i, string.charCodeAt(i));
  }
}

// Helper to convert Uint8Array to Base64 string efficiently
function bytesToBase64(bytes: Uint8Array): string {
  let binary = '';
  const len = bytes.byteLength;
  const chunkSize = 0x8000; // 32KB chunks
  for (let i = 0; i < len; i += chunkSize) {
    const chunk = bytes.subarray(i, Math.min(i + chunkSize, len));
    binary += String.fromCharCode.apply(null, chunk as unknown as number[]);
  }
  return btoa(binary);
}

// Helper to wrap raw PCM16 samples in a WAV container so browsers can play it
function pcm16ToWavBase64(base64Pcm: string, sampleRate = 24000, numChannels = 1): string {
  const binaryString = atob(base64Pcm);
  const len = binaryString.length;
  
  // WAV Header is 44 bytes
  const buffer = new ArrayBuffer(44 + len);
  const view = new DataView(buffer);

  // RIFF chunk descriptor
  writeAscii(view, 0, 'RIFF');
  view.setUint32(4, 36 + len, true); // ChunkSize
  writeAscii(view, 8, 'WAVE');

  // fmt sub-chunk
  writeAscii(view, 12, 'fmt ');
  view.setUint32(16, 16, true); // Subchunk1Size (16 for PCM)
  view.setUint16(20, 1, true); // AudioFormat (1 for PCM)
  view.setUint16(22, numChannels, true); // NumChannels
  view.setUint32(24, sampleRate, true); // SampleRate
  view.setUint32(28, sampleRate * numChannels * 2, true); // ByteRate
  view.setUint16(32, numChannels * 2, true); // BlockAlign
  view.setUint16(34, 16, true); // BitsPerSample

  // data sub-chunk
  writeAscii(view, 36, 'data');
  view.setUint32(40, len, true);

  // Write PCM data
  const bytes = new Uint8Array(buffer);
  // Skip header (44 bytes) and write PCM data
  for (let i = 0; i < len; i++) {
    bytes[44 + i] = binaryString.charCodeAt(i);
  }

  return bytesToBase64(bytes);
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
  
  // Create a working copy of history to safely modify/filter
  let processedHistory = history ? history.map(h => ({...h})) : [];

  // Filter out non-image media and enforce limits
  let imageCount = 0;
  // Iterate backwards to keep most recent images and drop others
  for (let i = processedHistory.length - 1; i >= 0; i--) {
      const h = processedHistory[i];
      const mime = (h.imageMimeType || "").toLowerCase();
      const isImage = mime.startsWith('image/');
      
      // Note: The history objects coming from deriveHistoryForApi use 'imageFileUri' 
      // for all file types (images, video, audio). We must filter based on mime type.
      if (h.imageFileUri) {
          if (!isImage) {
             // Drop non-image media (audio, video, pdf) as image generation model does not support them
             h.imageFileUri = undefined;
             // Add note to context so model knows media was there
             const type = mime.split('/')[0] || "File";
             const note = ` [${type} context omitted]`;
             if (h.rawAssistantResponse) h.rawAssistantResponse += note;
             else h.text = (h.text || "") + note;
          } else {
             // It is an image, check count limit (max 3 usually for image models)
             if (imageCount >= 3) {
                 h.imageFileUri = undefined;
                 const note = ` [Previous image context omitted]`;
                 if (h.rawAssistantResponse) h.rawAssistantResponse += note;
                 else h.text = (h.text || "") + note;
             } else {
                 imageCount++;
             }
          }
      }
  }

  if (processedHistory.length > 0) {
      processedHistory.forEach(h => {
        const parts: any[] = [];
        const textContent = h.rawAssistantResponse || h.text;
        if (textContent) parts.push({ text: textContent });
        
        if (h.imageFileUri) {
            // Final failsafe: only include image types in the payload for image generation model
            const m = (h.imageMimeType || "").toLowerCase();
            if (m.startsWith('image/')) {
                parts.push({ fileData: { fileUri: h.imageFileUri, mimeType: h.imageMimeType || 'image/jpeg' } });
            }
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
      const m = (maestroAvatarMimeType || "").toLowerCase();
      // Ensure avatar is also an image
      if (!m || m.startsWith('image/')) {
          currentParts.push({ fileData: { fileUri: maestroAvatarUri, mimeType: maestroAvatarMimeType || 'image/png' } });
      }
  }
  
  if (currentParts.length) {
      contents.push({ role: 'user', parts: currentParts });
  }

  // Debug payload for troubleshooting image gen errors
  console.debug("[generateImage] contents:", JSON.stringify(contents, (key, value) => {
      // Avoid spamming logs with huge base64 strings if any exist inline
      if (key === 'data' && typeof value === 'string' && value.length > 100) return value.substring(0, 20) + '...';
      return value;
  }, 2));

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
