// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { isIP } from 'node:net';
import type { Request } from 'express';

/** A proxy mismatch must not collapse every caller into the proxy's rate bucket. */
export const verifiedQuestClientIp = (request: Request): string | null => {
  const trusted = request.app.get('trust proxy fn') as ((ip: string, hop: number) => boolean) | undefined;
  const peer = request.socket.remoteAddress;
  if (!trusted || !peer || !isIP(peer) || !trusted(peer, 0)) return null;
  // Express walks only configured trusted hops. Require a real untrusted client,
  // not a missing header, malformed address, or a chain made entirely of proxies.
  const client = request.ip;
  if (!client || !isIP(client) || request.ips.length === 0 || trusted(client, 0)) return null;
  return client;
};
