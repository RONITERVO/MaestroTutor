// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseAssistantResponseForAttachment as parseAttachment } from '../../core-sdk/chat/assistantResponseAttachments';
import { normalizeSuggestionCreatorArtifact as normalizeArtifact } from '../../core-sdk/chat/suggestionAftersteps';
import { buildCoreLiveSystemInstruction as buildLiveInstruction } from '../../core-sdk/chat/liveContext';
import { sanitizeSvgAnimationStructure } from './sanitizeSvgAnimationStructure';

const artifactOptions = { sanitizeSvg: sanitizeSvgAnimationStructure };

export const parseAssistantResponseForAttachment = (responseText?: string | null) =>
  parseAttachment(responseText, artifactOptions);

export const normalizeSuggestionCreatorArtifact = (artifact: unknown) =>
  normalizeArtifact(artifact, artifactOptions);

export const buildCoreLiveSystemInstruction = (input: Omit<Parameters<typeof buildLiveInstruction>[0], 'sanitizeSvg'>) =>
  buildLiveInstruction({ ...input, ...artifactOptions });
