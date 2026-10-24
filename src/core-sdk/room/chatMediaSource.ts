// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { CHAT_MEDIA_CHUNK, validChatMediaRequest, type ChatMediaOffer } from '../../../shared/chatMedia';
import { bytesToHex } from '@noble/hashes/utils.js';
export type ChatMediaInput = { messageId: string; name: string; origin: string; dataUrl: string };
export class ChatMediaSource<K extends string> {
  constructor(private readonly config: {hashKey: K; minimumBytes: number; maximumBytes: number; pattern: RegExp; fallbackName: string}) {}
  private scope = '';
  private sources: ChatMediaInput[] = [];
  private setId = crypto.randomUUID().replace(/-/g, '');
  private offers: ChatMediaOffer<K>[] = [];
  private encoded = new Map<string, string>();
  private pending: Promise<void> = Promise.resolve();
  update(scope: string, sources: ChatMediaInput[]): Promise<void> {
    // Compare references/strings without serializing another copy of all media bytes.
    const selected = sources.slice(0, 8);
    if (scope === this.scope && selected.length === this.sources.length && selected.every((v, i) => {
      const old = this.sources[i]; return v.messageId === old.messageId && v.name === old.name && v.origin === old.origin && v.dataUrl === old.dataUrl;
    })) return this.pending;
    this.scope = scope; this.sources = selected; this.setId = crypto.randomUUID().replace(/-/g, '');
    this.offers = []; this.encoded.clear();
    const generation = this.setId;
    this.pending = this.prepare(selected, generation); return this.pending;
  }
  private async prepare(selected: ChatMediaInput[], generation: string) {
    const offers: ChatMediaOffer<K>[] = [], encoded = new Map<string, string>();
    for (const source of selected) {
      if (source.dataUrl.length > Math.ceil(this.config.maximumBytes / 3) * 4 + 32) continue;
      const match = this.config.pattern.exec(source.dataUrl);
      if (!match || match[2].length > Math.ceil(this.config.maximumBytes / 3) * 4 || match[2].length % 4 !== 0) continue;
      try {
        const binary = atob(match[2]);
        if (binary.length < this.config.minimumBytes || binary.length > this.config.maximumBytes) continue;
        const bytes = Uint8Array.from(binary, c => c.charCodeAt(0));
        const imageHash = bytesToHex(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)));
        if (this.setId !== generation) return;
        if (encoded.has(imageHash)) continue;
        const name = source.name.replace(/[\x00-\x1f<>]/g, '').slice(0, 80) || this.config.fallbackName;
        const messageId = source.messageId.slice(0, 128), origin = source.origin.replace(/[^a-z-]/g, '').slice(0, 32) || 'unknown';
        offers.push({[this.config.hashKey]: imageHash, name, messageId, origin, bytes: bytes.length} as ChatMediaOffer<K>); encoded.set(imageHash, match[2]);
      } catch { /* Unreadable local content is not offered as a native asset. */ }
    }
    if (this.setId === generation) { this.offers = offers; this.encoded = encoded; this.setId = crypto.randomUUID().replace(/-/g, ''); }
  }
  snapshot(request: unknown) {
    let chunk: (Record<K, string> & {requestId: string; offset: number; data: string}) | undefined;
    if (validChatMediaRequest(request, this.config.hashKey, this.config.minimumBytes, this.config.maximumBytes) && request.offerSet === this.setId) {
      const offer = this.offers.find(v => (v[this.config.hashKey] as string) === (request[this.config.hashKey] as string));
      const data = this.encoded.get(request[this.config.hashKey]);
      if (offer && data && offer.bytes === request.bytes) {
        // A 16 KiB offset is not a base64 group boundary; decode only the
        // overlapping groups and encode the exact requested byte interval.
        const first = Math.floor(request.offset / 3) * 4, prefix = request.offset % 3;
        const size = Math.min(CHAT_MEDIA_CHUNK, request.bytes - request.offset);
        const binary = atob(data.slice(first, first + Math.ceil((prefix + size) / 3) * 4));
        chunk = {[this.config.hashKey]: request[this.config.hashKey], requestId: request.requestId, offset: request.offset, data: btoa(binary.slice(prefix, prefix + size))} as NonNullable<typeof chunk>;
      }
    }
    return {offerSet: this.setId, offers: this.offers, ...(chunk ? {chunk} : {})};
  }
}
