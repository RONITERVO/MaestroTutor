// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
// @ts-expect-error Development-only source scanner, also usable as a CLI.
import { auditAppCoordinatorBoundaries, auditChatCoordinatorBoundaries, auditCoreBoundaries, auditLiveControllerBoundaries, inspectCoreSource } from '../../scripts/core-boundaries.mjs';

describe('Core architecture boundary', () => {
  it('keeps the whole Core dependency graph independent of browser adapters', () => {
    expect(auditCoreBoundaries()).toEqual([]);
  });
  it('keeps chat coordinators independent of React, store and device diagnostics', () => {
    expect(auditChatCoordinatorBoundaries()).toEqual([]);
  });
  it('keeps Live controllers independent of their browser/native adapters', () => {
    expect(auditLiveControllerBoundaries()).toEqual([]);
  });
  it('keeps App handoff coordinators independent of React and their store adapters', () => {
    expect(auditAppCoordinatorBoundaries()).toEqual([]);
  });
  it('rejects a runtime import from an App coordinator back into its state adapter', () => {
    const root = mkdtempSync(join(tmpdir(), 'maestro-app-boundary-'));
    try {
      mkdirSync(join(root, 'src/app/coordinators'), { recursive: true });
      writeFileSync(join(root, 'src/app/coordinators/sttTurn.ts'), 'import "../speechRoutingState";');
      writeFileSync(join(root, 'src/app/speechRoutingState.ts'), 'export {};');
      expect(auditAppCoordinatorBoundaries(root).map((item: { reason: string }) => item.reason))
        .toEqual(['Runtime boundary reaches adapter: src/app/speechRoutingState.ts']);
    } finally { rmSync(root, { recursive: true, force: true }); }
  });
  it('does not permit an excluded browser owner to be imported back into a Live controller', () => {
    const root = mkdtempSync(join(tmpdir(), 'maestro-live-boundary-'));
    try {
      mkdirSync(join(root, 'src/features/speech/live'), { recursive: true });
      writeFileSync(join(root, 'src/features/speech/live/controller.ts'), 'import "./browserRuntime";');
      writeFileSync(join(root, 'src/features/speech/live/browserRuntime.ts'), 'window.addEventListener("event", () => {});');
      expect(auditLiveControllerBoundaries(root).map((item: { reason: string }) => item.reason))
        .toEqual(['Browser runtime reference: window']);
    } finally { rmSync(root, { recursive: true, force: true }); }
  });
  it('matches Live utility file exceptions exactly instead of allowing similarly named siblings', () => {
    const root = mkdtempSync(join(tmpdir(), 'maestro-live-file-boundary-'));
    try {
      mkdirSync(join(root, 'src/features/speech/live'), { recursive: true });
      mkdirSync(join(root, 'src/features/speech/utils'), { recursive: true });
      writeFileSync(join(root, 'src/features/speech/live/controller.ts'), 'import "../utils/playbackDrain";');
      writeFileSync(join(root, 'src/features/speech/utils/playbackDrain.tsx'), 'export {};');
      expect(auditLiveControllerBoundaries(root).map((item: { reason: string }) => item.reason))
        .toEqual(['Runtime boundary reaches adapter: src/features/speech/utils/playbackDrain.tsx']);
      writeFileSync(join(root, 'src/features/speech/utils/playbackDrain.ts'), 'export {};');
      expect(auditLiveControllerBoundaries(root)).toEqual([]);
    } finally { rmSync(root, { recursive: true, force: true }); }
  });
  it('follows coordinator-to-coordinator imports and rejects an indirect store dependency', () => {
    const root = mkdtempSync(join(tmpdir(), 'maestro-chat-boundary-'));
    try {
      mkdirSync(join(root, 'src/features/chat/coordinators'), { recursive: true });
      mkdirSync(join(root, 'src/shared'), { recursive: true });
      mkdirSync(join(root, 'src/store'), { recursive: true });
      writeFileSync(join(root, 'src/features/chat/coordinators/send.ts'), 'export * from "./capture";');
      writeFileSync(join(root, 'src/features/chat/coordinators/capture.ts'), 'import "../../../shared/log";');
      writeFileSync(join(root, 'src/shared/log.ts'), 'import "../store";');
      writeFileSync(join(root, 'src/store/index.ts'), 'export {};');
      expect(auditChatCoordinatorBoundaries(root).map((item: { reason: string }) => item.reason))
        .toEqual(['Runtime boundary reaches adapter: src/store/index.ts']);
    } finally { rmSync(root, { recursive: true, force: true }); }
  });
  it.each(['window.addEventListener("pagehide", fn)', 'document.visibilityState', 'localStorage.getItem("key")',
    'globalThis.window', 'globalThis["indexedDB"]', 'new AudioContext()', 'const { navigator } = globalThis',
    'new DOMParser()', 'new XMLSerializer()'])
  ('detects browser runtime access: %s', source => {
    expect(inspectCoreSource(source).violations.length).toBeGreaterThan(0);
  });
  it('allows comments, authored examples, data properties and erased types', () => {
    expect(inspectCoreSource(`// window.document
      import type { BrowserType } from '../api/example';
      export type { BrowserType } from '../api/example';
      const example = 'localStorage.getItem("key")';
      const data = { document: 'example' }; const text = data.document;
      interface Port { window: string }; type Context = AudioContext;
    `)).toEqual({ imports: [], violations: [] });
  });
  it('follows re-exports and lazy dependencies, including aliases, to catch hidden browser access', () => {
    const root = mkdtempSync(join(tmpdir(), 'maestro-core-boundary-'));
    try {
      mkdirSync(join(root, 'src/core-sdk'), { recursive: true });
      mkdirSync(join(root, 'src/shared'), { recursive: true });
      mkdirSync(join(root, 'src/api'), { recursive: true });
      writeFileSync(join(root, 'src/core-sdk/index.ts'), 'export * from "../shared/barrel";');
      writeFileSync(join(root, 'src/shared/barrel.ts'), 'export const open = () => import("@/shared/hidden");');
      writeFileSync(join(root, 'src/shared/hidden.ts'), 'import "react"; import "@firebase/app"; import "src/api/client"; localStorage.getItem("key");');
      writeFileSync(join(root, 'src/api/client.ts'), 'export {};');
      const violations = auditCoreBoundaries(root);
      expect(violations.map((item: { reason: string }) => item.reason)).toEqual([
        'Browser runtime reference: localStorage', 'Browser package: react', 'Browser package: @firebase/app', 'Runtime boundary reaches adapter: src/api/client.ts',
      ]);
      expect(violations[0].chain).toEqual(['src/core-sdk/index.ts', 'src/shared/barrel.ts', 'src/shared/hidden.ts']);
    } finally { rmSync(root, { recursive: true, force: true }); }
  });
  it('does not let computed imports silently escape the audit', () => {
    expect(inspectCoreSource('const load = () => import(path);').violations).toHaveLength(1);
  });
});
