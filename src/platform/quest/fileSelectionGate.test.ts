// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { afterEach, expect, it } from 'vitest';
import { createFileSelectionGate } from './fileSelectionGate';

afterEach(() => { document.body.replaceChildren(); });
it('authorizes a hidden input opened from a top-level user action once, then expires and clears it', () => {
  let time = 0;
  const gate = createFileSelectionGate(window, () => time, event => event.type === 'pointerup');
  const input = document.createElement('input'); input.type = 'file'; input.hidden = true; document.body.append(input);
  input.click(); expect(gate.take()).toBe(false);
  document.dispatchEvent(new Event('pointerup')); input.click(); expect(gate.take()).toBe(true); expect(gate.take()).toBe(false);
  input.click(); expect(gate.take()).toBe(false);
  document.dispatchEvent(new Event('pointerup')); input.click(); time = 2100; expect(gate.take()).toBe(false);
  document.dispatchEvent(new Event('pointerup')); input.click(); gate.clear(); expect(gate.take()).toBe(false);
  document.dispatchEvent(new Event('pointerup')); input.click(); gate.dispose(); expect(gate.take()).toBe(false);
});
it('does not authorize a picker from an iframe or synthetic background click', () => {
  const gate = createFileSelectionGate(window);
  const input = document.createElement('input'); input.type = 'file'; document.body.append(input);
  document.dispatchEvent(new Event('pointerup')); input.click(); expect(gate.take()).toBe(false);
  gate.dispose();
  const gateWithGesture = createFileSelectionGate(window, () => 0, event => event.type === 'pointerup');
  const frame = document.createElement('iframe'); document.body.append(frame);
  const child = frame.contentDocument!.createElement('input'); child.type = 'file'; frame.contentDocument!.body.append(child);
  document.dispatchEvent(new Event('pointerup')); child.click(); expect(gateWithGesture.take()).toBe(false);
  gateWithGesture.dispose();
});
