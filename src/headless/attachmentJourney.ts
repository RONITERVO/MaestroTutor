// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { HeadlessClient } from './client';
import { runHeadlessChatTurn } from './chatJourney';
import {
  createSyntheticAttachment,
  type SyntheticAttachmentKind,
} from './syntheticAttachments';

export const runHeadlessAttachmentTurn = async (client: HeadlessClient, input: {
  text: string;
  fixture?: SyntheticAttachmentKind;
  dataUrl?: string;
  mimeType?: string;
  displayName?: string;
  languagePairId?: string;
  useGoogleSearch?: boolean;
  requireInvariants?: boolean;
  cleanup?: boolean;
}) => {
  const fixture = input.fixture ? createSyntheticAttachment(input.fixture) : null;
  const dataUrl = input.dataUrl || fixture?.dataUrl;
  const mimeType = input.mimeType || fixture?.mimeType;
  const displayName = input.displayName || fixture?.displayName;
  if (!dataUrl || !mimeType) {
    throw new Error('Provide a synthetic fixture or both dataUrl and mimeType.');
  }

  const operationId = client.runtime.ids.create('attachment-turn');
  client.runtime.events.emit({
    operationId,
    journey: 'media',
    phase: 'attachment.uploadStarted',
    data: { fixture: fixture?.kind, mimeType, displayName },
  });
  const uploaded = await client.backend.uploadMedia({ dataUrl, mimeType, displayName });
  client.runtime.events.emit({
    operationId,
    journey: 'media',
    phase: 'attachment.uploadSucceeded',
    data: { uri: uploaded.uri, mimeType: uploaded.mimeType },
  });

  let turn;
  try {
    turn = await runHeadlessChatTurn(client, {
      text: input.text,
      languagePairId: input.languagePairId,
      useGoogleSearch: input.useGoogleSearch,
      requireInvariants: input.requireInvariants,
      fileParts: [{ fileUri: uploaded.uri, mimeType: uploaded.mimeType }],
    });
  } finally {
    if (input.cleanup) {
      await client.backend.deleteFile({ nameOrUri: uploaded.uri });
      client.runtime.events.emit({
        operationId,
        journey: 'media',
        phase: 'attachment.cleanedUp',
        data: { uri: uploaded.uri },
      });
    }
  }

  return {
    operationId,
    fixture: fixture?.kind || null,
    uploaded: { uri: uploaded.uri, mimeType: uploaded.mimeType, displayName },
    cleanedUp: Boolean(input.cleanup),
    turn,
  };
};
