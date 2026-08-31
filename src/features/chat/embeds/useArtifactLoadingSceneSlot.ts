// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Grants the single shared artifact-loading-scene iframe.
 *
 * The scene is a 40 KB SVG whose animation lives in a `<foreignObject>`, so it
 * genuinely needs a browsing context — an `<img>` will not run it. What it does
 * not need is one browsing context *per loading message*: that was a whole
 * document, JS context and compositing layer per in-flight artifact, purely for
 * decoration.
 *
 * First claimant wins and holds the slot until it unmounts, at which point the
 * next waiter is granted it. Everyone else falls back to the plain spinner.
 */

import { useEffect, useState } from 'react';

type Waiter = (granted: boolean) => void;

let holder: Waiter | null = null;
const queue: Waiter[] = [];

const claim = (waiter: Waiter): (() => void) => {
  if (holder === null) {
    holder = waiter;
    waiter(true);
  } else {
    queue.push(waiter);
    waiter(false);
  }

  return () => {
    if (holder === waiter) {
      holder = null;
      const next = queue.shift();
      if (next) {
        holder = next;
        next(true);
      }
      return;
    }
    const index = queue.indexOf(waiter);
    if (index >= 0) queue.splice(index, 1);
  };
};

/** True when this component owns the one shared loading-scene iframe. */
export const useArtifactLoadingSceneSlot = (active: boolean): boolean => {
  const [granted, setGranted] = useState(false);

  useEffect(() => {
    if (!active) {
      setGranted(false);
      return;
    }
    return claim(setGranted);
  }, [active]);

  return granted && active;
};

/** Test-only reset. */
export const __resetArtifactLoadingSceneSlot = (): void => {
  holder = null;
  queue.length = 0;
};
