// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/** Image bytes travel on the bounded transport, never as planner arguments. */
export const CHAT_IMAGE_BYTES = 16 * 1024 * 1024;
export const CHAT_IMAGE_CHUNK = 16384;
export interface ChatImageOffer { imageHash: string; name: string; messageId: string; origin: string; bytes: number }
export interface ChatImageRequest { requestId: string; offerSet: string; imageHash: string; offset: number; bytes: number }
const record = (v: unknown): v is Record<string, unknown> => v !== null && typeof v === 'object' && !Array.isArray(v);
export function validChatImageRequest(v: unknown): v is ChatImageRequest {
  return record(v) && Object.keys(v).length === 5 &&
    ['requestId', 'offerSet'].every(k => typeof v[k] === 'string' && /^[a-f0-9]{32}$/.test(v[k] as string)) &&
    typeof v.imageHash === 'string' && /^[a-f0-9]{64}$/.test(v.imageHash) &&
    Number.isInteger(v.bytes) && Number(v.bytes) >= 24 && Number(v.bytes) <= CHAT_IMAGE_BYTES &&
    Number.isInteger(v.offset) && Number(v.offset) >= 0 && Number(v.offset) < Number(v.bytes) && Number(v.offset) % CHAT_IMAGE_CHUNK === 0;
}
