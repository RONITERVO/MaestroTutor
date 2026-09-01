// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { generateImage } from '../../api/gemini/vision';
import {
  IMAGE_GEN_COPYRIGHT_AVOIDANCE_INSTRUCTION,
  IMAGE_GEN_SYSTEM_INSTRUCTION,
  IMAGE_GEN_USER_PROMPT_TEMPLATE,
} from '../../core/config/prompts';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreRuntime, type CoreRuntime } from '../runtime';

export interface MaestroImageGenerationInput {
  contextText: string;
  history?: unknown[];
  maestroAvatarUri?: string;
  maestroAvatarMimeType?: string;
  maxAttempts?: number;
}

export interface MaestroImageGenerationOptions {
  runtime?: CoreRuntime;
  aiClient?: CoreGeminiClient;
  onAttempt?: (attempt: number, totalAttempts: number) => void;
}

export const runMaestroImageGeneration = async (
  input: MaestroImageGenerationInput,
  options: MaestroImageGenerationOptions = {},
) => {
  const runtime = options.runtime || createCoreRuntime();
  const operationId = runtime.ids.create('image-generation');
  const totalAttempts = Math.max(1, Math.min(7, Math.floor(input.maxAttempts ?? 7)));
  let finalResult: Awaited<ReturnType<typeof generateImage>> = { error: 'No image generated' };
  runtime.events.emit({
    operationId,
    journey: 'media',
    phase: 'image.started',
    data: { historyCount: input.history?.length || 0, contextLength: input.contextText.length, totalAttempts },
  });
  for (let attempt = 0; attempt < totalAttempts; attempt++) {
    options.onAttempt?.(attempt + 1, totalAttempts);
    runtime.events.emit({
      operationId,
      journey: 'media',
      phase: 'image.attempt',
      data: { attempt: attempt + 1, totalAttempts },
    });
    const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace(
      '{TEXT}',
      input.contextText + (attempt > 0 ? IMAGE_GEN_COPYRIGHT_AVOIDANCE_INSTRUCTION : ''),
    );
    finalResult = await generateImage({
      history: input.history,
      latestMessageText: prompt,
      latestMessageRole: 'user',
      systemInstruction: IMAGE_GEN_SYSTEM_INSTRUCTION,
      maestroAvatarUri: input.maestroAvatarUri,
      maestroAvatarMimeType: input.maestroAvatarMimeType,
      aiClient: options.aiClient,
    });
    if ('base64Image' in finalResult) {
      runtime.events.emit({
        operationId,
        journey: 'media',
        phase: 'image.completed',
        data: { attempt: attempt + 1, mimeType: finalResult.mimeType, dataUrlLength: finalResult.base64Image.length },
      });
      return { ...finalResult, operationId, attempts: attempt + 1 };
    }
    if (attempt + 1 < totalAttempts) await runtime.clock.sleep(1_500);
  }
  runtime.events.emit({
    operationId,
    journey: 'media',
    phase: 'image.failed',
    data: { error: 'error' in finalResult ? finalResult.error : 'No image generated', attempts: totalAttempts },
  });
  return { ...finalResult, operationId, attempts: totalAttempts };
};
