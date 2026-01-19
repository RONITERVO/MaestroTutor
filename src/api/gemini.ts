import { GoogleGenAI } from "@google/genai";
import { debugLogService } from "../features/diagnostics/services/debugLogService";

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

/**
 * Uploads a base64 data URL as a file to the Google GenAI Files API and returns the uploaded file's identifiers.
 *
 * @param dataUrl - A base64-encoded data URL (for example, "data:<mime>;base64,<data>").
 * @param mimeType - The MIME type to assign to the uploaded file.
 * @param displayName - Optional display name / filename for the uploaded file.
 * @returns The uploaded file's `uri` and `mimeType`; each will be an empty string if the API response omits them.
 */
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

  // Log upload attempt
  const log = debugLogService.logRequest('files.upload', 'Files API', { mimeType, displayName, size: blob.size });

  try {
    const uploadResult = await ai.files.upload({
        file,
        config: { displayName, mimeType },
    });
    log.complete(uploadResult);
    return { uri: uploadResult.uri || '', mimeType: uploadResult.mimeType || '' };
  } catch (e) {
    log.error(e);
    throw e;
  }
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
  
  contents.push({ role: 'user', parts: currentParts });

  const config: any = { ...configOverrides };
  if (systemInstruction) config.systemInstruction = systemInstruction;
  
  // Correct tool configuration for Google Search
  if (useGoogleSearch) {
      config.tools = [{ googleSearch: {} }];
  }

  const log = debugLogService.logRequest('generateContent', modelName, { contents, config });

  try {
      const result = await ai.models.generateContent({
        model: modelName,
        contents,
        config
      });
      log.complete({ text: result.text, usage: result.usageMetadata });
      return {
        text: result.text,
        candidates: result.candidates,
        usageMetadata: result.usageMetadata,
      };
  } catch (e: any) {
      console.error("Gemini API Error:", e);
      log.error(e);
      throw new ApiError(e.message || "Gemini API failed", { status: 500, code: e.status });
  }
}

/**
 * Generates an image from a prompt and optional conversation history using the Gemini image model.
 *
 * Builds a content payload from historical messages, recent user input, and an optional maestro avatar,
 * sends it to the Gemini image generation model, and returns the first generated image found as a data URL.
 *
 * @param params - Parameters for image generation
 * @param params.prompt - Primary prompt text to generate the image from. If omitted, `latestMessageText` may be used.
 * @param params.history - Conversation history entries that may include text and image references to provide context.
 * @param params.latestMessageText - Fallback text to use as the current user input when `prompt` is not provided.
 * @param params.latestMessageRole - Role of the latest message (`'user'` or `'assistant'`), used when interpreting history entries.
 * @param params.systemInstruction - Optional system instruction passed to the model as part of the request config.
 * @param params.maestroAvatarUri - Optional URI of an avatar to include as image context for the current user input.
 * @param params.maestroAvatarMimeType - MIME type for `maestroAvatarUri`; used to validate that the avatar is an image.
 * @returns On success, an object with `base64Image` (a data URL like `data:<mime>;base64,<data>`) and `mimeType`. On failure, an object with `error` containing the error message.
 */
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
  
  let processedHistory = history ? history.map(h => ({...h})) : [];

  let imageCount = 0;
  for (let i = processedHistory.length - 1; i >= 0; i--) {
      const h = processedHistory[i];
      const mime = (h.imageMimeType || "").toLowerCase();
      const isImage = mime.startsWith('image/');
      
      if (h.imageFileUri) {
          if (!isImage) {
             h.imageFileUri = undefined;
             const type = mime.split('/')[0] || "File";
             const note = ` [${type} context omitted]`;
             if (h.rawAssistantResponse) h.rawAssistantResponse += note;
             else h.text = (h.text || "") + note;
          } else {
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
      if (!m || m.startsWith('image/')) {
          currentParts.push({ fileData: { fileUri: maestroAvatarUri, mimeType: maestroAvatarMimeType || 'image/png' } });
      }
  }
  
  if (currentParts.length) {
      contents.push({ role: 'user', parts: currentParts });
  }

  const model = 'gemini-2.5-flash-image';
  const config = { responseModalities: ['IMAGE'], systemInstruction };
  const log = debugLogService.logRequest('generateImage', model, { contents, config });

  try {
      const result = await ai.models.generateContent({
        model,
        contents,
        config: config as any
      });
      
      log.complete({ candidates: result.candidates?.length });

      const candidates = result.candidates || [];
      for (const c of candidates) {
          for (const part of c.content?.parts || []) {
              if (part.inlineData && part.inlineData.mimeType?.startsWith('image/')) {
                  return { base64Image: `data:${part.inlineData.mimeType};base64,${part.inlineData.data || ''}`, mimeType: part.inlineData.mimeType };
              }
          }
      }
      throw new Error("No image generated");
  } catch (e: any) {
      log.error(e);
      return { error: e.message };
  }
}

export async function translateText(text: string, from: string, to: string) {
  const ai = getAi();
  const prompt = `Translate the following text from ${from} to ${to}. Return ONLY the translation. Text: "${text}"`;
  const model = 'gemini-3-flash-preview';
  const log = debugLogService.logRequest('translateText', model, { prompt });

  try {
    const result = await ai.models.generateContent({
        model,
        contents: prompt
    });
    log.complete({ text: result.text });
    return { translatedText: result.text || "" };
  } catch (e) {
    log.error(e);
    throw e;
  }
}

/**
 * Generates spoken audio from input text using the Gemini TTS model and returns the audio as a WAV-formatted Base64 string.
 *
 * The function requests an AUDIO response from the model (default voice "Kore"), converts returned PCM16 inline data into a WAV container, and returns the resulting Base64 audio along with its MIME type. If no audio is produced or an error occurs, both return fields are empty strings.
 *
 * @param params - The generation parameters
 * @param params.text - The text to synthesize into speech
 * @param params.voiceName - Optional prebuilt voice name to use (defaults to `"Kore"`)
 * @returns An object with `audioBase64` containing the WAV audio as a Base64 string and `mimeType` set to `"audio/wav"`, or empty strings when no audio is available
 */
export async function generateSpeech(params: { text: string, voiceName?: string }) {
    const ai = getAi();
    const model = 'gemini-2.5-flash-preview-tts';
    const config = {
        responseModalities: ['AUDIO'],
        speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName: params.voiceName || 'Kore' } } }
    };
    const log = debugLogService.logRequest('generateSpeech', model, { text: params.text, config });

    try {
        const result = await ai.models.generateContent({
            model,
            contents: { parts: [{ text: params.text }] },
            config: config as any
        });
        const c = result.candidates?.[0];
        const part = c?.content?.parts?.[0];
        if (part?.inlineData && part.inlineData.data) {
            log.complete({ audioBytes: part.inlineData.data.length });
            // Gemini TTS returns raw PCM in most cases for this model, wrap it in a WAV container to ensure playback
            const wavBase64 = pcm16ToWavBase64(part.inlineData.data);
            return { audioBase64: wavBase64, mimeType: 'audio/wav' }; 
        }
        log.error("No audio data received");
    } catch (e) {
      console.error("Gemini TTS Error:", e);
      log.error(e);
    }
    return { audioBase64: "", mimeType: "" };
}