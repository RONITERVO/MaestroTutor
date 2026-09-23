// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, expect, it, vi } from 'vitest';
import { createAvatarWithOverlay } from './mediaUtils';

afterEach(() => vi.unstubAllGlobals());

it('preserves the identity instruction actually drawn into the model avatar', async () => {
  const fillText = vi.fn();
  const canvas = { width: 0, height: 0, getContext: () => ({ drawImage() {}, fillRect() {}, fillText }),
    toDataURL: () => 'data:image/jpeg;base64,AAAA' };
  vi.stubGlobal('document', { createElement: () => canvas });
  vi.stubGlobal('Image', class {
    naturalWidth = 512;
    naturalHeight = 512;
    onload?: () => void;
    set src(_value: string) { queueMicrotask(() => this.onload?.()); }
  });
  await createAvatarWithOverlay('data:image/png;base64,AAAA');
  expect(fillText.mock.calls).toMatchSnapshot();
});
