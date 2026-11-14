// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** File chooser callbacks lack a frame origin. Only a recent top-document file
 * input click may authorize one native picker; opaque artifact frames cannot set it. */
export function createFileSelectionGate(target: Window, now = () => target.performance.now(), trusted = (event: Event) => event.isTrusted) {
  let interaction = -Infinity;
  let selection = -Infinity;
  const track = (event: Event) => {
    if (trusted(event)) interaction = now();
    const input = event.target;
    if (event.type === 'click' && input instanceof HTMLInputElement && input.ownerDocument === target.document && input.type === 'file' && input.isConnected && now() - interaction < 2000) selection = now();
  };
  for (const type of ['click', 'pointerup', 'keydown']) target.document.addEventListener(type, track, true);
  return {
    take() { const allowed = now() - selection < 2000; interaction = selection = -Infinity; return allowed; },
    clear() { interaction = selection = -Infinity; },
    dispose() { for (const type of ['click', 'pointerup', 'keydown']) target.document.removeEventListener(type, track, true); interaction = selection = -Infinity; },
  };
}
