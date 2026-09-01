// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { HeadlessClient } from './client';
import type { UploadedAttachmentVariant } from '../core/types';
import {
  selectPrimaryUploadedAttachmentVariant,
  selectUploadedAttachmentParts,
} from '../core-sdk/chat/uploadedAttachmentVariants';
import { buildHeadlessAttachmentUploadPlans } from './attachmentUploadAdapters';
import { runHeadlessChatTurn } from './chatJourney';
import {
  createSyntheticAttachmentFixture,
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
  const fixture = input.fixture ? await createSyntheticAttachmentFixture(input.fixture) : null;
  const dataUrl = input.dataUrl || fixture?.dataUrl;
  const mimeType = input.mimeType || fixture?.mimeType;
  const displayName = input.displayName || fixture?.displayName;
  if (!dataUrl || !mimeType) {
    throw new Error('Provide a synthetic fixture or both dataUrl and mimeType.');
  }

  const operationId = client.runtime.ids.create('attachment-turn');
  const plans = buildHeadlessAttachmentUploadPlans({
    dataUrl,
    mimeType,
    attachmentName: displayName,
  });
  const uploadedVariants: UploadedAttachmentVariant[] = [];
  const uploadedDisplayNames = new Map<string, string | undefined>();
  let cleanedUp = false;
  const cleanupUploads = async () => {
    if (cleanedUp) return;
    cleanedUp = true;
    for (const variant of uploadedVariants) {
      await client.files.delete(variant.uri);
      client.runtime.events.emit({
        operationId,
        journey: 'media',
        phase: 'attachment.cleanedUp',
        data: { id: variant.id, uri: variant.uri },
      });
    }
  };

  let turn;
  try {
    for (const plan of plans) {
      client.runtime.events.emit({
        operationId,
        journey: 'media',
        phase: 'attachment.uploadStarted',
        data: { fixture: fixture?.kind, id: plan.id, source: plan.source },
      });
      const payload = await plan.build();
      const uploaded = await client.files.upload(payload);
      const variant: UploadedAttachmentVariant = {
        id: plan.id,
        uri: uploaded.uri,
        mimeType: uploaded.mimeType,
        targets: plan.targets,
        source: plan.source,
        order: plan.order,
      };
      uploadedVariants.push(variant);
      uploadedDisplayNames.set(variant.id, payload.displayName);
      client.runtime.events.emit({
        operationId,
        journey: 'media',
        phase: 'attachment.uploadSucceeded',
        data: { id: plan.id, source: plan.source, uri: uploaded.uri, mimeType: uploaded.mimeType },
      });
    }
    const fileParts = selectUploadedAttachmentParts({ uploadedFileVariants: uploadedVariants }, 'chat');
    if (fileParts.length === 0) throw new Error('Attachment preparation produced no chat upload variants.');
    turn = await runHeadlessChatTurn(client, {
      text: input.text,
      languagePairId: input.languagePairId,
      useGoogleSearch: input.useGoogleSearch,
      requireInvariants: input.requireInvariants,
      fileParts,
      uploadedFileVariants: uploadedVariants,
    });
  } catch (error) {
    await cleanupUploads();
    throw error;
  } finally {
    if (input.cleanup) await cleanupUploads();
  }

  const primary = selectPrimaryUploadedAttachmentVariant(uploadedVariants);

  return {
    operationId,
    fixture: fixture?.kind || null,
    uploaded: primary ? {
      uri: primary.uri,
      mimeType: primary.mimeType,
      displayName: uploadedDisplayNames.get(primary.id),
    } : null,
    uploadedVariants: uploadedVariants.map(variant => ({
      ...variant,
      displayName: uploadedDisplayNames.get(variant.id),
    })),
    cleanedUp: cleanedUp && Boolean(input.cleanup),
    turn,
  };
};
