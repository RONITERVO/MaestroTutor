// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Wires the chat's scroll container into the embed system.
 *
 * Two jobs, both of which used to be done N times over (once per embed) and are
 * now done exactly once:
 *
 * 1. Publish `--embed-max-h` — the height cap every reserved box inherits. One
 *    ResizeObserver on the scroll container replaces one per embed, each of
 *    which was also observing this same shared element.
 * 2. Point the activation manager at the scroll root so it can run the single
 *    IntersectionObserver that decides which embed is allowed to be live.
 */

import { useEffect } from 'react';
import { embedActivation } from './embedActivation';
import type { DeviceBudgets } from '../../../store';

/** Room left for the message's own chrome above and below the embed. */
const VIEWPORT_CHROME_PX = 48;
const MIN_EMBED_MAX_HEIGHT_PX = 240;

export const useEmbedViewport = (
  scrollContainerRef: React.RefObject<HTMLElement | null>,
  budgets: DeviceBudgets,
): void => {
  useEffect(() => {
    const container = scrollContainerRef.current;
    if (!container) return;

    embedActivation.setRoot(container);

    const publishMaxHeight = () => {
      // Measured from the visual viewport, not the container. The container's
      // `overflow-y: auto` never engages — the flex chain above it only sets a
      // minimum height — so it is as tall as the whole conversation (9717px
      // against an 800px screen on a real device), and a cap derived from it
      // would never cap anything.
      const viewportHeight = window.visualViewport?.height ?? window.innerHeight;
      const available = Math.min(container.clientHeight, viewportHeight);
      const cap = Math.max(MIN_EMBED_MAX_HEIGHT_PX, Math.floor(available - VIEWPORT_CHROME_PX));
      container.style.setProperty('--embed-max-h', `${cap}px`);
    };

    publishMaxHeight();

    const observer = typeof ResizeObserver !== 'undefined'
      ? new ResizeObserver(publishMaxHeight)
      : null;
    observer?.observe(container);

    // The visual viewport shrinks when the soft keyboard opens without the
    // scroll container's own box changing, so ResizeObserver alone misses it.
    window.visualViewport?.addEventListener('resize', publishMaxHeight, { passive: true });
    window.addEventListener('orientationchange', publishMaxHeight, { passive: true });

    return () => {
      observer?.disconnect();
      window.visualViewport?.removeEventListener('resize', publishMaxHeight);
      window.removeEventListener('orientationchange', publishMaxHeight);
      embedActivation.setRoot(null);
    };
  }, [scrollContainerRef]);

  useEffect(() => {
    embedActivation.setBudgets({
      maxLiveEmbeds: budgets.maxLiveEmbeds,
      posterBudget: budgets.posterBudget,
    });
  }, [budgets]);
};
