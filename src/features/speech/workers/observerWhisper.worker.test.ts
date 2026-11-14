// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import runtimeWasmUrl from 'onnxruntime-web/ort-wasm-simd-threaded.asyncify.wasm?url';
import type { ObserverWhisperRequest, ObserverWhisperResponse } from './observerWhisperProtocol';

const runtime = vi.hoisted(() => ({
  env: {
    allowLocalModels: true,
    backends: { onnx: {
      versions: { web: '1.31.0-dev.20260914-8d85527a0' },
      wasm: { wasmPaths: undefined as string | { mjs?: string; wasm?: string } | undefined, proxy: false },
    } },
  },
  pipeline: vi.fn(),
  transcribe: vi.fn(),
}));
vi.mock('@huggingface/transformers', () => ({ env: runtime.env, pipeline: runtime.pipeline }));

const workerUrl = 'https://appassets.androidplatform.net/assets/observerWhisper.worker-test.js';
const prefix = 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.31.0-dev.20260914-8d85527a0/dist/';
const model = 'onnx-community/whisper-tiny.en';
const defaultPaths = () => ({
  mjs: prefix + 'ort-wasm-simd-threaded.asyncify.mjs',
  wasm: prefix + 'ort-wasm-simd-threaded.asyncify.wasm',
});
let scope: {
  location: { href: string };
  postMessage: ReturnType<typeof vi.fn>;
  onmessage?: (event: MessageEvent<ObserverWhisperRequest>) => void;
};
const deliver = (data: ObserverWhisperRequest) => {
  expect(scope.onmessage).toBeTypeOf('function');
  scope.onmessage!({ data } as MessageEvent<ObserverWhisperRequest>);
};
const initialize = async () => {
  await import('./observerWhisper.worker');
  expect(runtime.pipeline).not.toHaveBeenCalled();
  deliver({ kind: 'init', model, allowFp32Fallback: false });
  await vi.waitFor(() => expect(scope.postMessage).toHaveBeenCalledWith({ kind: 'ready', model, profile: 'q4' }, []));
};

describe('Whisper worker packaged runtime selection', () => {
  beforeEach(() => {
    vi.resetModules();
    runtime.env.allowLocalModels = true;
    runtime.env.backends.onnx.wasm.wasmPaths = defaultPaths();
    runtime.pipeline.mockReset();
    runtime.transcribe.mockReset().mockResolvedValue({ text: 'Please keep our apple.' });
    runtime.pipeline.mockResolvedValue(runtime.transcribe);
    scope = { location: { href: workerUrl }, postMessage: vi.fn() };
    vi.stubGlobal('self', scope);
  });
  afterEach(() => vi.unstubAllGlobals());

  it('selects the packaged asyncify WASM before model construction and preserves real inference requests', async () => {
    runtime.pipeline.mockImplementation(async (task, selectedModel, options) => {
      // Assert at the actual initialization boundary, not after the async load.
      expect(runtime.env.backends.onnx.wasm.wasmPaths).toEqual({ wasm: new URL(runtimeWasmUrl, workerUrl).href });
      expect(new URL((runtime.env.backends.onnx.wasm.wasmPaths as { wasm: string }).wasm).origin).toBe(new URL(workerUrl).origin);
      expect(task).toBe('automatic-speech-recognition');
      expect(selectedModel).toBe(model);
      expect(options.device).toBe('wasm');
      expect(options.dtype).toEqual({ encoder_model: 'q4', decoder_model_merged: 'q4' });
      return runtime.transcribe;
    });
    await initialize();
    expect(runtime.env.allowLocalModels).toBe(false);
    expect(runtime.env.backends.onnx.wasm.proxy).toBe(false);
    const samples = new Float32Array([0, 0.25, -0.5, 0]);
    deliver({ kind: 'transcribe', requestId: 7, audio: samples.buffer });
    await vi.waitFor(() => expect(runtime.transcribe).toHaveBeenCalledWith(samples, { max_new_tokens: 32 }));
    await vi.waitFor(() => expect(scope.postMessage.mock.calls.some(call => {
      const value = call[0] as ObserverWhisperResponse;
      return value.kind === 'result' && value.requestId === 7 && value.text === 'Please keep our apple.';
    })).toBe(true));
  });

  it.each([
    ['existing non-asyncify Safari fallback', { mjs: prefix + 'ort-wasm-simd-threaded.mjs', wasm: prefix + 'ort-wasm-simd-threaded.wasm' }],
    ['custom paired URLs', { mjs: 'https://runtime.example/ort.mjs', wasm: 'https://runtime.example/ort.wasm' }],
    ['custom WASM-only URL', { wasm: 'https://runtime.example/ort.wasm' }],
    ['custom prefix', 'https://runtime.example/'],
    ['mixed custom/default pair', { mjs: prefix + 'ort-wasm-simd-threaded.asyncify.mjs', wasm: 'https://runtime.example/ort.wasm' }],
  ] as const)('preserves %s without replacing its selected factory', async (_name, paths) => {
    runtime.env.backends.onnx.wasm.wasmPaths = paths;
    const selected = structuredClone(paths);
    runtime.pipeline.mockImplementation(async () => {
      expect(runtime.env.backends.onnx.wasm.wasmPaths).toEqual(selected);
      return runtime.transcribe;
    });
    await initialize();
    expect(runtime.pipeline).toHaveBeenCalledTimes(1);
  });
});
