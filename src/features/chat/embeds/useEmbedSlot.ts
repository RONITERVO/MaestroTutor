// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/** React binding for the embed activation manager. */

import { useCallback, useEffect, useRef, useState } from 'react';
import { embedActivation } from './embedActivation';
import type { EmbedKind, EmbedPhase } from './embedTypes';

interface UseEmbedSlotOptions {
  /** Stable across re-renders and re-mounts — normally the message id. */
  id: string;
  kind: EmbedKind;
  /** When false the slot never registers and stays a placeholder. */
  enabled?: boolean;
}

export interface EmbedSlot<T extends HTMLElement> {
  /** Attach to the element that represents the reserved box. */
  ref: React.RefObject<T | null>;
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

export function useEmbedSlot<T extends HTMLElement>({
  id,
  kind,
  enabled = true,
}: UseEmbedSlotOptions): EmbedSlot<T> {
  const ref = useRef<T | null>(null);
  const [phase, setPhase] = useState<EmbedPhase>('placeholder');
  const [isFullyVisible, setIsFullyVisible] = useState(false);

  useEffect(() => {
    if (!enabled) {
      setPhase('placeholder');
      setIsFullyVisible(false);
      return;
    }
    return embedActivation.register({
      id,
      kind,
      element: ref.current,
      onPhase: setPhase,
      onFullyVisible: setIsFullyVisible,
    });
  }, [id, kind, enabled]);

  // Releasing the pin on unmount matters: a pinned record that outlives its
  // component would hold the only live slot forever.
  useEffect(() => () => embedActivation.setPinned(id, false), [id]);

  const pin = useCallback(() => embedActivation.setPinned(id, true), [id]);
  const unpin = useCallback(() => embedActivation.setPinned(id, false), [id]);
  const publishPoster = useCallback((url: string) => embedActivation.setPoster(id, url), [id]);

  return {
    ref,
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
