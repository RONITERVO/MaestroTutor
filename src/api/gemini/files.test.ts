// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, expect, it, vi } from 'vitest';
const provider = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock('./client', () => ({ getDirectAi: async () => ({ files: provider }) }));
vi.mock('../../features/diagnostics', () => ({ debugLogService: {} }));
vi.mock('../../services/access/maestroAccessService', () => ({ maestroAccessService: { resolveAccessMode: async () => 'byok' } }));
vi.mock('../../services/backend/maestroBackendService', () => ({ maestroBackendService: {} }));
import { checkFileStatuses, sanitizeHistoryWithVerifiedUris } from './files';
import { createAttachmentUploads } from '../../features/chat/coordinators/attachmentUploads';
import { PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID } from '../../core-sdk/chat/uploadedAttachmentVariants';
import type { ChatMessage } from '../../core/types';

beforeEach(() => vi.resetAllMocks());

it('keeps cached variants when the provider cannot determine file status', async () => {
  provider.get.mockRejectedValue(Object.assign(new Error('provider temporarily unavailable'), { status: 503 }));
  const message: ChatMessage = { id: 'cached', role: 'user', timestamp: 1, text: 'Describe this',
    imageUrl: 'data:image/png;base64,AQ==', imageMimeType: 'image/png',
    uploadedFileVariants: [{ id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID, uri: 'files/transient', mimeType: 'image/png', targets: ['chat'], source: 'original', order: 0 }] };
  const upload = vi.fn().mockRejectedValue(new Error('replacement unavailable'));
  const update = vi.fn();
  const attachments = createAttachmentUploads({ t: key => key, updateMessage: update,
    computeHistorySubsetForMedia: messages => messages, checkFileStatuses, uploadMediaToFiles: upload,
    buildAttachmentUploadPlans: () => [{ id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID, source: 'original', targets: ['chat'], order: 0,
      build: async () => ({ dataUrl: message.imageUrl!, mimeType: 'image/png' }) }],
  });
  const result = await attachments.ensureUploadedAttachmentVariantsForMessage(message);
  expect(result.chatFileParts).toEqual([{ fileUri: 'files/transient', mimeType: 'image/png' }]);
  expect(upload).not.toHaveBeenCalled();
  expect(update).not.toHaveBeenCalled();
});

it('surfaces an unknown lookup instead of silently sending history without its attachment', async () => {
  provider.get.mockRejectedValue(new Error('network interrupted'));
  await expect(sanitizeHistoryWithVerifiedUris([{ role: 'user', text: 'Describe this',
    fileParts: [{ fileUri: 'files/network', mimeType: 'image/png' }] }])).rejects.toThrow('network interrupted');
});

it('continues distinguishing confirmed active and processing provider files', async () => {
  provider.get.mockResolvedValueOnce({ state: 'ACTIVE' }).mockResolvedValueOnce({ state: 'PROCESSING' });
  expect(await checkFileStatuses(['files/active-fixture', 'files/processing-fixture'])).toEqual({
    'files/active-fixture': { deleted: false, active: true },
    'files/processing-fixture': { deleted: false, active: false },
  });
});
