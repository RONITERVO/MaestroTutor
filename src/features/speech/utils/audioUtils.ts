// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const arrayBufferToBase64 = (buffer: ArrayBuffer): string => {
  const bytes = new Uint8Array(buffer);
  const chunkSize = 0x8000;
  const chunks: string[] = [];
  for (let i = 0; i < bytes.length; i += chunkSize) {
    const slice = bytes.subarray(i, Math.min(i + chunkSize, bytes.length));
    chunks.push(String.fromCharCode(...slice));
  }
  return btoa(chunks.join(''));
};
