// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ChatMessage } from '../../core/types';
import { GENERATED_IMAGE_CONTEXT, imageOriginContext } from '../../../shared/prompts/context';

export const generatedImageContext = (message: Pick<ChatMessage, 'imageOrigin' | 'maestroToolKind' | 'uploadedFileVariants'> | undefined): string =>
  message?.imageOrigin === 'generated' || message?.maestroToolKind === 'image' || message?.uploadedFileVariants?.some(part => part.origin === 'generated' && part.mimeType.startsWith('image/')) ? GENERATED_IMAGE_CONTEXT : imageOriginContext(message?.imageOrigin ?? message?.uploadedFileVariants?.find(part => part.mimeType.startsWith('image/') && part.origin)?.origin);

export const withImageOriginContext = (text: string, message: Pick<ChatMessage, 'imageOrigin' | 'maestroToolKind' | 'uploadedFileVariants'> | undefined): string => {
  const label = generatedImageContext(message);
  return label ? `${text}\n${label}`.trim() : text;
};
