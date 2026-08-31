// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/** React binding for the embed activation manager. */

import { useCallback, useEffect, useState } from 'react';
import { embedActivation } from './embedActivation';
import type { EmbedKind, EmbedPhase } from './embedTypes';

interface UseEmbedSlotOptions {
  /** Stable across re-renders and re-mounts — normally the message id. */
  id: string;
  kind: EmbedKind;
  /** When false the slot never registers and stays a placeholder. */
  enabled?: boolean;
}

export interface EmbedSlot {
  /**
   * Attach to the element that represents the reserved box.
   *
   * A callback ref rather than a RefObject on purpose: a viewer that renders a
   * loading state first only produces its box element on a later render, and a
   * RefObject read once inside an effect would register `null` and never be
   * observed. This re-registers whenever the node actually changes.
   */
  setRef: (element: HTMLElement | null) => void;
  phase: EmbedPhase;
  isLive: boolean;
  /**
   * Essentially all of the box is inside the scroll root. Latched at a single
   * threshold so it changes rarely rather than on every scroll frame.
   */
  isFullyVisible: boolean;
  /** Poster blob URL for the frozen phase, if one was captured. */
  poster: string | undefined;
  /** Mark the user as actively engaged, so this slot outranks visibility. */
  pin: () => void;
  unpin: () => void;
  /** Hand a captured poster to the manager; it takes ownership of the URL. */
  publishPoster: (url: string) => void;
  postersEnabled: boolean;
}

export function useEmbedSlot({ id, kind, enabled = true }: UseEmbedSlotOptions): EmbedSlot {
  const [element, setElement] = useState<HTMLElement | null>(null);
  const [phase, setPhase] = useState<EmbedPhase>('placeholder');
  const [isFullyVisible, setIsFullyVisible] = useState(false);

  const setRef = useCallback((next: HTMLElement | null) => {
    setElement((current) => (current === next ? current : next));
  }, []);

  useEffect(() => {
    if (!enabled || !element) {
      setPhase('placeholder');
      setIsFullyVisible(false);
      return;
    }
    return embedActivation.register({
      id,
      kind,
      element,
      onPhase: setPhase,
      onFullyVisible: setIsFullyVisible,
    });
  }, [id, kind, enabled, element]);

  // Releasing the pin on unmount matters: a pinned record that outlives its
  // component would hold the only live slot forever.
  useEffect(() => () => embedActivation.setPinned(id, false), [id]);

  const pin = useCallback(() => embedActivation.setPinned(id, true), [id]);
  const unpin = useCallback(() => embedActivation.setPinned(id, false), [id]);
  const publishPoster = useCallback((url: string) => embedActivation.setPoster(id, url), [id]);

  return {
    setRef,
    phase,
    isLive: phase === 'live',
    isFullyVisible,
    poster: phase === 'frozen' ? embedActivation.getPoster(id) : undefined,
    pin,
    unpin,
    publishPoster,
    postersEnabled: embedActivation.postersEnabled(),
  };
}
