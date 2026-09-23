// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
// @ts-expect-error Development-only source scanner, also usable as a CLI.
import { auditCoreBoundaries, inspectCoreSource } from '../../scripts/core-boundaries.mjs';

describe('Core architecture boundary', () => {
  it('keeps the whole Core dependency graph independent of browser adapters', () => {
    expect(auditCoreBoundaries()).toEqual([]);
  });
  it.each(['window.addEventListener("pagehide", fn)', 'document.visibilityState', 'localStorage.getItem("key")',
    'globalThis.window', 'globalThis["indexedDB"]', 'new AudioContext()', 'const { navigator } = globalThis'])
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
      writeFileSync(join(root, 'src/shared/hidden.ts'), 'import "react"; import "../api/client"; localStorage.getItem("key");');
      writeFileSync(join(root, 'src/api/client.ts'), 'export {};');
      const violations = auditCoreBoundaries(root);
      expect(violations.map((item: { reason: string }) => item.reason)).toEqual([
        'Browser runtime reference: localStorage', 'Browser package: react', 'Core reaches adapter: src/api/client.ts',
      ]);
      expect(violations[0].chain).toEqual(['src/core-sdk/index.ts', 'src/shared/barrel.ts', 'src/shared/hidden.ts']);
    } finally { rmSync(root, { recursive: true, force: true }); }
  });
  it('does not let computed imports silently escape the audit', () => {
    expect(inspectCoreSource('const load = () => import(path);').violations).toHaveLength(1);
  });
});
