// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment node
import { randomUUID } from 'node:crypto';
import http2 from 'node:http2';
import { createRequire } from 'node:module';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type * as Grpc from '@grpc/grpc-js';

const require = createRequire(import.meta.url);
// Resolve the exact child selected for Firestore, including a nested npm override.
const firestoreRequire = createRequire(require.resolve('@firebase/firestore'));
const grpc: typeof Grpc = firestoreRequire('@grpc/grpc-js');
const protoLoader: typeof import('@grpc/proto-loader') = firestoreRequire('@grpc/proto-loader');
const grpcVersion: string = firestoreRequire('@grpc/grpc-js/package.json').version;
const protoVersion: string = firestoreRequire('@grpc/proto-loader/package.json').version;
const originalConnect = http2.connect;

function bounded<T>(work: Promise<T>, label: string, milliseconds = 3_000): Promise<T> {
  let timer: ReturnType<typeof setTimeout>;
  return Promise.race([
    work,
    new Promise<never>((_, reject) => { timer = setTimeout(() => reject(new Error(label)), milliseconds); }),
  ]).finally(() => clearTimeout(timer));
}

beforeEach(() => {
  vi.spyOn(globalThis, 'fetch').mockImplementation(async () => { throw new Error('External fetch is forbidden in offline compatibility checks'); });
  vi.spyOn(http2, 'connect').mockImplementation((authority, options, listener) => {
    const url = authority instanceof URL ? authority : new URL(authority);
    if (url.hostname !== '127.0.0.1') throw new Error('Only loopback HTTP/2 is permitted');
    return originalConnect(authority, options, listener);
  });
});
afterEach(() => {
  try { expect(globalThis.fetch).not.toHaveBeenCalled(); }
  finally { vi.restoreAllMocks(); }
});

describe(`Firestore Node compatibility (grpc ${grpcVersion}, Firestore proto-loader ${protoVersion})`, () => {
  it.each(['require', 'import'] as const)('constructs and terminates the real %s SDK client without network access', async mode => {
    expect(require.resolve('@firebase/firestore')).toMatch(/index\.node\.cjs\.js$/);
    const appSdk: typeof import('firebase/app') = mode === 'require' ? require('firebase/app') : await import('firebase/app');
    const sdk: typeof import('firebase/firestore') = mode === 'require' ? require('firebase/firestore') : await import('firebase/firestore');
    const app = appSdk.initializeApp({ projectId: 'maestro-offline-grpc-compatibility' }, `offline-${randomUUID()}`);
    let db: ReturnType<typeof sdk.initializeFirestore> | undefined;
    try {
      // No API key, auth provider, remote endpoint, database query or emulator process.
      db = sdk.initializeFirestore(app, { host: '127.0.0.1:1', ssl: false });
      await bounded(sdk.disableNetwork(db), 'Firestore did not enter offline mode');
      const reference = sdk.doc(db, 'compatibility', 'offline');
      expect(reference.path).toBe('compatibility/offline');
      expect(reference.firestore).toBe(db);
      expect(http2.connect).not.toHaveBeenCalled();
    } finally {
      try { if (db) await bounded(sdk.terminate(db), 'Firestore termination timed out'); }
      finally { await bounded(appSdk.deleteApp(app), 'Firebase app cleanup timed out'); }
    }
  }, 10_000);

  it("interoperates with Firestore's proto-loader for unary, streaming and cancelled loopback calls", async () => {
    type Message = { text: string };
    type WireClient = Grpc.Client & {
      echo(request: Message, metadata: Grpc.Metadata, options: Grpc.CallOptions, callback: Grpc.requestCallback<Message>): Grpc.ClientUnaryCall;
      chat(metadata: Grpc.Metadata, options: Grpc.CallOptions): Grpc.ClientDuplexStream<Message, Message>;
    };
    const definition = protoLoader.fromJSON({ nested: { compatibility: { nested: {
      Message: { fields: { text: { type: 'string', id: 1 } } },
      Wire: { methods: {
        Echo: { requestType: 'Message', responseType: 'Message' },
        Chat: { requestType: 'Message', responseType: 'Message', requestStream: true, responseStream: true },
      } },
    } } } }, { longs: String, enums: String, defaults: true, oneofs: false });
    const namespace = grpc.loadPackageDefinition(definition).compatibility as Grpc.GrpcObject;
    const Wire = namespace.Wire as Grpc.ServiceClientConstructor;
    const server = new grpc.Server();
    let acknowledgeCancel!: () => void;
    const serverCancelled = new Promise<void>(resolve => { acknowledgeCancel = resolve; });
    const receivedMetadata: string[] = [];
    server.addService(Wire.service, {
      echo(call: Grpc.ServerUnaryCall<Message, Message>, reply: Grpc.sendUnaryData<Message>) {
        receivedMetadata.push(String(call.metadata.get('x-maestro-compatibility')[0]));
        reply(null, { text: `echo:${call.request.text}` });
      },
      chat(call: Grpc.ServerDuplexStream<Message, Message>) {
        if (call.metadata.get('x-maestro-compatibility')[0] === 'cancel') call.on('cancelled', acknowledgeCancel);
        call.on('data', (message: Message) => call.write({ text: `stream:${message.text}` }));
        call.on('end', () => call.end());
        call.on('error', () => { /* Cancellation is asserted on the client and server separately. */ });
      },
    });
    let client: WireClient | undefined;
    try {
      const port = await bounded(new Promise<number>((resolve, reject) => {
        server.bindAsync('127.0.0.1:0', grpc.ServerCredentials.createInsecure(), (error, boundPort) => error ? reject(error) : resolve(boundPort));
      }), 'Loopback gRPC bind timed out');
      // Required by the old baseline; still supported (deprecated) by patched grpc-js.
      server.start();
      expect(grpc.credentials.createSsl()).toBeInstanceOf(grpc.ChannelCredentials);
      client = new Wire(`127.0.0.1:${port}`, grpc.credentials.createInsecure(), {
        'grpc.enable_retries': 0, 'grpc.enable_http_proxy': 0,
      }) as WireClient;
      const metadata = new grpc.Metadata(); metadata.set('x-maestro-compatibility', 'unary');
      const response = await bounded(new Promise<Message>((resolve, reject) => {
        client!.echo({ text: 'hola' }, metadata, { deadline: Date.now() + 2_000 }, (error, value) => error ? reject(error) : resolve(value!));
      }), 'Unary response timed out');
      expect(response).toEqual({ text: 'echo:hola' });
      expect(receivedMetadata).toEqual(['unary']);

      const messages: string[] = [];
      await bounded(new Promise<void>((resolve, reject) => {
        const stream = client!.chat(new grpc.Metadata(), { deadline: Date.now() + 2_000 });
        stream.on('data', (message: Message) => messages.push(message.text));
        stream.on('error', reject); stream.on('end', resolve);
        stream.write({ text: 'uno' }); stream.write({ text: 'dos' }); stream.end();
      }), 'Streaming response timed out');
      expect(messages).toEqual(['stream:uno', 'stream:dos']);

      const cancellation = new grpc.Metadata(); cancellation.set('x-maestro-compatibility', 'cancel');
      const cancelledCode = await bounded(new Promise<number>((resolve, reject) => {
        const stream = client!.chat(cancellation, { deadline: Date.now() + 2_000 });
        stream.once('data', () => stream.cancel());
        stream.once('error', (error: Grpc.ServiceError) => resolve(error.code));
        stream.once('end', () => reject(new Error('Cancelled stream unexpectedly ended successfully')));
        stream.write({ text: 'cancel after delivery' });
      }), 'Cancellation response timed out');
      expect(cancelledCode).toBe(grpc.status.CANCELLED);
      await bounded(serverCancelled, 'Server did not observe cancellation');
      expect(http2.connect).toHaveBeenCalled();
    } finally {
      client?.close();
      server.forceShutdown();
    }
  }, 12_000);
});
