// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import sharp from 'sharp';
import { describe, expect, it } from 'vitest';
import { createSyntheticVisualFrame } from './syntheticVisual';

describe('synthetic Live visual', () => {
  it('creates a deterministic real JPEG frame', async () => {
    const frame = await createSyntheticVisualFrame();
    const metadata = await sharp(Buffer.from(frame.dataBase64, 'base64')).metadata();
    expect(frame).toMatchObject({ mimeType: 'image/jpeg', width: 320, height: 240, semanticLabel: 'RED APPLE' });
    expect(metadata).toMatchObject({ format: 'jpeg', width: 320, height: 240 });
  });
});
