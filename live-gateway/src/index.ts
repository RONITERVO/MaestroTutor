// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createServer } from 'node:http';
import { GoogleGenAI } from '@google/genai';
import WebSocket, { WebSocketServer, type RawData } from 'ws';
import {
  LIVE_GATEWAY_MAX_MESSAGE_BYTES,
} from '../../shared/liveGatewayProtocol';
import {
  checkpointManagedLiveGatewaySession,
  consumeManagedLiveGatewayTicket,
  finalizeManagedLiveGatewaySession,
} from '../../functions/src/managedLiveGateway';
import { pricingEffectiveAt } from '../../functions/src/pricing';
import {
  LiveGatewayConnection,
  type LiveProviderConnector,
} from './session';

const port = Math.max(1, Number(process.env.PORT || 8080));
const providerConnectTimeoutMs = Math.max(
  5_000,
  Math.min(60_000, Number(process.env.LIVE_PROVIDER_CONNECT_TIMEOUT_MS || 20_000)),
);
const apiKey = process.env.GEMINI_API_KEY?.trim() || '';
if (!apiKey) throw new Error('GEMINI_API_KEY is required.');

const ai = new GoogleGenAI({ apiKey, apiVersion: 'v1beta' });
const provider: LiveProviderConnector = {
  connect: async ({ model, config, callbacks }) => (
    ai.live.connect({ model, ...(config ? { config } : {}), callbacks } as any) as any
  ),
};

const server = createServer((request, response) => {
  if (request.method === 'GET' && (request.url === '/' || request.url === '/health')) {
    response.writeHead(200, {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store',
    });
    response.end(JSON.stringify({
      ok: true,
      service: 'managed-live-gateway',
      apiVersion: 'v1beta',
      metering: 'server-observed',
    }));
    return;
  }
  response.writeHead(404, { 'Content-Type': 'application/json; charset=utf-8' });
  response.end(JSON.stringify({ error: 'Not found.' }));
});

const webSockets = new WebSocketServer({
  noServer: true,
  maxPayload: LIVE_GATEWAY_MAX_MESSAGE_BYTES,
  perMessageDeflate: false,
});

server.on('upgrade', (request, socket, head) => {
  let pathname = '';
  try {
    pathname = new URL(request.url || '/', 'http://gateway.invalid').pathname;
  } catch {
    socket.destroy();
    return;
  }
  if (pathname !== '/' && pathname !== '/live') {
    socket.write('HTTP/1.1 404 Not Found\r\nConnection: close\r\n\r\n');
    socket.destroy();
    return;
  }
  webSockets.handleUpgrade(request, socket, head, (webSocket) => {
    webSockets.emit('connection', webSocket, request);
  });
});

webSockets.on('connection', (webSocket) => {
  let alive = true;
  webSocket.on('pong', () => { alive = true; });

  const connection = new LiveGatewayConnection({
    transport: {
      isOpen: () => webSocket.readyState === WebSocket.OPEN,
      send: (text) => {
        if (webSocket.bufferedAmount > 8 * 1024 * 1024) {
          throw new Error('Client is not consuming gateway output.');
        }
        webSocket.send(text);
      },
      close: (code, reason) => webSocket.close(code, reason),
    },
    billing: {
      consumeTicket: (ticket) => consumeManagedLiveGatewayTicket(ticket, pricingEffectiveAt),
      checkpoint: checkpointManagedLiveGatewaySession,
      finalize: finalizeManagedLiveGatewaySession,
    },
    provider,
    providerConnectTimeoutMs,
    log: (level, message, details) => {
      const payload = details === undefined ? [message] : [message, details];
      if (level === 'error') console.error(...payload);
      else if (level === 'warn') console.warn(...payload);
      else console.info(...payload);
    },
  });

  webSocket.on('message', (data: RawData, isBinary: boolean) => {
    if (isBinary) {
      webSocket.close(1003, 'json-text-required');
      return;
    }
    connection.receive(data.toString('utf8'));
  });
  webSocket.on('close', () => connection.disconnect());
  webSocket.on('error', (error) => {
    console.warn('Client WebSocket error.', error instanceof Error ? error.message : String(error));
  });

  const heartbeat = setInterval(() => {
    if (!alive) {
      clearInterval(heartbeat);
      webSocket.terminate();
      return;
    }
    alive = false;
    webSocket.ping();
  }, 30_000);
  heartbeat.unref();
  webSocket.once('close', () => clearInterval(heartbeat));
});

server.listen(port, '0.0.0.0', () => {
  console.info(`Managed Live gateway listening on port ${port}.`);
});

const shutdown = () => {
  for (const client of webSockets.clients) client.close(1012, 'service-restart');
  webSockets.close();
  server.close();
};
process.once('SIGTERM', shutdown);
process.once('SIGINT', shutdown);
