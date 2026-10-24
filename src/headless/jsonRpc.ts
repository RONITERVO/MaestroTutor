// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createInterface } from 'node:readline';
import type { CoreEvent } from '../core-sdk/events';
import { createHeadlessClient, type HeadlessClientOptions } from './client';
import { dispatchHeadlessMethod, HeadlessDispatchError } from './dispatcher';

interface JsonRpcRequest {
  jsonrpc: '2.0';
  id?: string | number | null;
  method: string;
  params?: unknown;
}

interface JsonRpcErrorData {
  code: number;
  message: string;
  data?: unknown;
}

const write = (value: unknown) => process.stdout.write(`${JSON.stringify(value)}\n`);

const errorResponse = (id: JsonRpcRequest['id'], error: JsonRpcErrorData) => ({
  jsonrpc: '2.0' as const,
  id: id ?? null,
  error,
});

const validateRequest = (value: unknown): JsonRpcRequest => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Request must be an object.');
  const request = value as Partial<JsonRpcRequest>;
  if (request.jsonrpc !== '2.0') throw new Error('jsonrpc must be "2.0".');
  if (typeof request.method !== 'string' || !request.method) throw new Error('method must be a non-empty string.');
  return request as JsonRpcRequest;
};

/** Keep mutations serial while allowing Stop and task inspection to reach a
 * running agent. A queued Stop behind its own task could never cancel it. */
export function createHeadlessRequestQueue(dispatch: (method: string, params?: unknown) => Promise<unknown>) {
  let serial: Promise<unknown> = Promise.resolve();
  return (method: string, params?: unknown): Promise<unknown> => {
    if (method === 'room.stop' || method === 'room.tasks' || method === 'system.describe') return dispatch(method, params);
    const result = serial.then(() => dispatch(method, params));
    serial = result.catch(() => {});
    return result;
  };
}

export const runJsonRpcServer = async (options: HeadlessClientOptions = {}) => {
  const client = await createHeadlessClient({
    ...options,
    onEvent: (event: CoreEvent) => write({
      jsonrpc: '2.0',
      method: 'maestro.event',
      params: event,
    }),
  });
  const input = createInterface({ input: process.stdin, crlfDelay: Infinity });

  const dispatch = createHeadlessRequestQueue((method, params) => dispatchHeadlessMethod(client, method, params));
  const pending = new Set<Promise<void>>();
  const respond = async (request: JsonRpcRequest) => {
    try {
      const result = await dispatch(request.method, request.params);
      if (request.id !== undefined) write({ jsonrpc: '2.0', id: request.id, result });
    } catch (error) {
      if (request.id !== undefined) write(errorResponse(request.id, {
        code: error instanceof HeadlessDispatchError ? error.rpcCode : -32000,
        message: error instanceof Error ? error.message : String(error),
        data: { name: error instanceof Error ? error.name : 'UnknownError' },
      }));
    }
  };
  try {
    for await (const line of input) {
      if (!line.trim()) continue;
      let parsed: unknown;
      try {
        parsed = JSON.parse(line);
      } catch {
        write(errorResponse(null, { code: -32700, message: 'Parse error' }));
        continue;
      }

      let request: JsonRpcRequest;
      try {
        request = validateRequest(parsed);
      } catch (error) {
        write(errorResponse(null, { code: -32600, message: error instanceof Error ? error.message : 'Invalid Request' }));
        continue;
      }

      const response = respond(request);
      pending.add(response);
      void response.finally(() => pending.delete(response));
    }
    await Promise.all(pending);
  } finally { await client.roomAgent?.disconnect(); }
};
