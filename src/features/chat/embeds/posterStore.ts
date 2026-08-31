// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Poster bitmaps for frozen embeds.
 *
 * A poster is a heavily downscaled still of the last live frame, so scrolling
 * back past an embed that is no longer running does not look like the content
 * vanished. Posters are deliberately blob URLs, not data URLs: a blob lives
 * outside the JS heap and can be released with revokeObjectURL, whereas a base64
 * data URL is retained string bytes plus a decoded bitmap that we cannot free.
 *
 * The activation manager owns the budget and the revocation; this module only
 * handles capture plumbing.
 */

/** Long edge of a captured poster, in CSS pixels, before devicePixelRatio. */
export const POSTER_MAX_EDGE_PX = 360;
/** JPEG quality — a poster is a hint that content exists, not a reproduction. */
export const POSTER_QUALITY = 0.6;

/** The only types a captured poster is allowed to claim. */
const POSTER_MIME_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp']);

/**
 * Convert a data URL produced inside an embed into a blob URL.
 *
 * Decoded by hand rather than via `fetch(dataUrl)` because the app runs under a
 * restrictive CSP in the Capacitor WebView where `connect-src` does not
 * necessarily admit `data:`.
 */
export const dataUrlToBlobUrl = (dataUrl: string): string | null => {
  const comma = dataUrl.indexOf(',');
  if (comma < 0) return null;

  const header = dataUrl.slice(0, comma);
  if (!header.startsWith('data:')) return null;
  // The data URL is produced inside the artifact, which is model-authored and
  // therefore untrusted: it could name any type. Pin the blob to an image type
  // so a poster can never become, say, a text/html blob URL.
  const declared = header.slice(5).split(';')[0].trim().toLowerCase();
  const mimeType = POSTER_MIME_TYPES.has(declared) ? declared : 'image/jpeg';

  try {
    const binary = atob(dataUrl.slice(comma + 1));
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) bytes[i] = binary.charCodeAt(i);
    return URL.createObjectURL(new Blob([bytes], { type: mimeType }));
  } catch {
    return null;
  }
};
