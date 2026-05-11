// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useRef, useState } from 'react';

const isScrollableOverflow = (value: string): boolean => /(auto|scroll)/i.test(value);

const getNearestScrollContainer = (element: HTMLElement | null): HTMLElement | null => {
  if (!element || typeof window === 'undefined') return null;

  let parent = element.parentElement;
  while (parent) {
    const style = window.getComputedStyle(parent);
    if (isScrollableOverflow(style.overflowY || '')) {
      return parent;
    }
    parent = parent.parentElement;
  }

  return null;
};

const useChatResettingAttachmentMode = <TElement extends HTMLElement = HTMLDivElement>() => {
  const rootRef = useRef<TElement>(null);
  const [isAttachmentModeEnabled, setIsAttachmentModeEnabled] = useState(false);

  const resetAttachmentMode = useCallback(() => {
    setIsAttachmentModeEnabled(false);
  }, []);

  useEffect(() => {
    const root = rootRef.current;
    if (!root || typeof window === 'undefined') return;

    const scrollContainer = getNearestScrollContainer(root);
    const handleChatSurfaceMove = () => {
      setIsAttachmentModeEnabled(false);
    };

    scrollContainer?.addEventListener('scroll', handleChatSurfaceMove, { passive: true });
    window.addEventListener('scroll', handleChatSurfaceMove, { passive: true });
    window.addEventListener('resize', handleChatSurfaceMove, { passive: true });

    return () => {
      scrollContainer?.removeEventListener('scroll', handleChatSurfaceMove);
      window.removeEventListener('scroll', handleChatSurfaceMove);
      window.removeEventListener('resize', handleChatSurfaceMove);
    };
  }, []);

  return {
    rootRef,
    isAttachmentModeEnabled,
    setIsAttachmentModeEnabled,
    resetAttachmentMode,
  };
};

export default useChatResettingAttachmentMode;
