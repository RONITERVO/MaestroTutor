// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useLayoutEffect, useRef } from 'react';

/**
 * Give an event handler a permanently stable identity.
 *
 * Exists because a single unstable callback silently defeats `React.memo` for
 * an entire subtree, and the cost is invisible until something is profiled.
 * Device profiling found `onQuotaStartLive` — one prop, several layers up a
 * hook chain — re-rendering all 28 chat bubbles and their icon subtrees on
 * every commit.
 *
 * Stabilising at the boundary where the handler is consumed is deliberate: the
 * alternative is auditing and pinning every callback along the chain that
 * produced it, which is both a larger change and one that quietly rots the next
 * time someone adds a dependency.
 *
 * For event handlers only — the returned function must not be called during
 * render, since it deliberately reads the newest implementation rather than the
 * one from the render that created it.
 */
export function useStableCallback<TArgs extends unknown[], TResult>(
  callback: ((...args: TArgs) => TResult) | undefined,
): (...args: TArgs) => TResult | undefined {
  const ref = useRef(callback);
  // Assigned in a layout effect, not during render: a concurrent render that is
  // abandoned must not leave its callback behind for committed handlers to run.
  // Layout effects flush synchronously after DOM mutation and before paint, so
  // no event can fire against a stale implementation in between.
  useLayoutEffect(() => {
    ref.current = callback;
  });

  return useCallback((...args: TArgs) => ref.current?.(...args), []);
}
