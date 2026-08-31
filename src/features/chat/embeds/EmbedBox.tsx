// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * The reserved layout box for a rich attachment.
 *
 * Its height comes from a stored aspect ratio and the chat column's width — never
 * from its contents. `contain: layout paint size` makes that structural rather
 * than conventional: content physically cannot resize the box, so mounting,
 * unmounting or freezing an embed moves nothing on the page.
 *
 * The height cap comes from `--embed-max-h`, published once per viewport by
 * ChatInterface, so no embed needs its own ResizeObserver on the scroll container.
 */

import React from 'react';

interface EmbedBoxProps {
  aspectRatio: number;
  /** Fallback cap when --embed-max-h has not been published yet. */
  minHeight?: number;
  className?: string;
  style?: React.CSSProperties;
  boxRef?: React.Ref<HTMLDivElement>;
  children?: React.ReactNode;
}

const EmbedBox: React.FC<EmbedBoxProps> = ({
  aspectRatio,
  minHeight = 220,
  className = '',
  style,
  boxRef,
  children,
}) => (
  <div
    ref={boxRef}
    className={`embed-box ${className}`.trim()}
    style={{
      ['--embed-ar' as string]: `${aspectRatio}`,
      ['--embed-min-h' as string]: `${minHeight}px`,
      ...style,
    }}
  >
    {children}
  </div>
);

export default React.memo(EmbedBox);
